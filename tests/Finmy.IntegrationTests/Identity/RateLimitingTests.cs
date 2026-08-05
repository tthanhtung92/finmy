using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Finmy.IntegrationTests.Identity;

/// <summary>
/// Login/register/refresh sit behind a tighter "auth" rate-limit policy than the rest of the
/// API. The limiter's state lives for the process lifetime of the host, not per test, so
/// deliberately bursting past it here would exhaust the whole window's quota for every other
/// test sharing <see cref="Finmy.IntegrationTests.ApiCollection"/>'s fixture, and the first test
/// elsewhere in the collection to need a fresh login in that window would get 429'd instead of
/// the 200/202 it expects. This class gets its own <see cref="FinmyApiFactory"/> instead, so the
/// burst can only ever poison its own limiter state.
///
/// <see cref="FinmyApiFactory"/> also raises the auth limit to a practically unlimited default
/// for every other test, since even a handful of legitimate register/login calls across several
/// test classes could otherwise trip the production ceiling on a slow CI runner. This test opts
/// back into that real production ceiling on its own isolated host via
/// <see cref="WebApplicationFactory{TEntryPoint}.WithWebHostBuilder"/>.
/// </summary>
public class RateLimitingTests(FinmyApiFactory factory) : IClassFixture<FinmyApiFactory>
{
    [Fact]
    public async Task Login_bursts_past_the_limit_are_rejected_with_429()
    {
        using var scopedFactory = factory.WithWebHostBuilder(
            builder => builder.UseSetting("RateLimiting:AuthPermitLimit", "10"));
        using var client = scopedFactory.CreateClient();
        var statusCodes = new List<HttpStatusCode>();

        for (var i = 0; i < 15; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/identity/login",
                new { email = "rate-limit-probe@finmy.local", password = "Wrong-Password-1" },
                TestContext.Current.CancellationToken);

            statusCodes.Add(response.StatusCode);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                response.Headers.RetryAfter.ShouldNotBeNull();
                return;
            }
        }

        Assert.Fail($"Expected a 429 within 15 requests; got: {string.Join(", ", statusCodes)}");
    }
}
