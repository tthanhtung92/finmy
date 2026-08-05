using System.Security.Claims;

using Finmy.Budgeting.Api.Caching;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;

using Shouldly;

namespace Finmy.UnitTests.Budgeting;

public class VaryByUserPolicyTests
{
    [Fact]
    public async Task CacheRequestAsync_varies_by_the_sub_claim()
    {
        var policy = new VaryByUserPolicy();
        var contextA = BuildContext("user-a");
        var contextB = BuildContext("user-b");

        await policy.CacheRequestAsync(contextA, TestContext.Current.CancellationToken);
        await policy.CacheRequestAsync(contextB, TestContext.Current.CancellationToken);

        contextA.CacheVaryByRules.VaryByValues["sub"].ShouldBe("user-a");
        contextB.CacheVaryByRules.VaryByValues["sub"].ShouldBe("user-b");
        contextA.AllowCacheLookup.ShouldBeTrue();
        contextA.AllowCacheStorage.ShouldBeTrue();
    }

    private static OutputCacheContext BuildContext(string sub)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", sub)], "TestAuth"))
        };

        return new OutputCacheContext { HttpContext = httpContext };
    }
}
