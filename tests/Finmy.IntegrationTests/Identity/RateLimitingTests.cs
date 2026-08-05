using System.Net;
using System.Net.Http.Json;

using Shouldly;

namespace Finmy.IntegrationTests.Identity;

/// <summary>
/// Login/register/refresh sit behind a tighter "auth" rate-limit policy than the rest of the
/// API. The fixture is shared across the whole collection and other tests in it also call
/// login once (FinmyApiFactory.CreateAuthenticatedClientAsync, cached after the first call), so
/// this asserts "a burst well past the limit produces at least one 429" rather than pinning an
/// exact request number -- an exact count would be coupled to how much of the shared quota
/// other tests already spent.
/// </summary>
[Collection(ApiCollection.Name)]
public class RateLimitingTests(FinmyApiFactory factory)
{
    [Fact]
    public async Task Login_bursts_past_the_limit_are_rejected_with_429()
    {
        using var client = factory.CreateClient();
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
