using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Envelopes;
using Finmy.Budgeting.Application.Envelopes.Dtos;
using Finmy.Budgeting.Domain.Envelopes;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Finmy.UnitTests.Budgeting;

public class EnvelopeServiceTests
{
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly DateTimeOffset PeriodStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    private static CreateEnvelopeRequest CreateValidCreateRequest()
        => new("Groceries", "Monthly food budget", CategoryId, 1_500m, PeriodStart, PeriodEnd);

    [Fact]
    public static async Task Create_WithCategoryNotFound()
    {
        var envelopeRepo = Substitute.For<IEnvelopeRepository>();
        var categoryRepo = Substitute.For<ICategoryRepository>();
        var cache = Substitute.For<HybridCache>();
        var logger = Substitute.For<ILogger<EnvelopeService>>();
        var outputCache = Substitute.For<IOutputCacheInvalidator>();
        var realtime = Substitute.For<IEnvelopeRealtimeNotifier>();
        var service = new EnvelopeService(envelopeRepo, categoryRepo, cache, logger, outputCache, realtime);

        categoryRepo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var request = CreateValidCreateRequest();

        var result = await service.CreateAsync(request, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.CategoryNotFound(request.CategoryId));

        envelopeRepo.DidNotReceive().Add(Arg.Any<Envelope>());

        await envelopeRepo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
