using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Caching;
using Finmy.Budgeting.Application.Envelopes;
using Finmy.Budgeting.Application.Envelopes.Dtos;
using Finmy.Budgeting.Application.Abstractions.Dtos;
using Finmy.Budgeting.Domain.Envelopes;
using Finmy.SharedKernel.Results;

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
    public async Task Create_WithCategoryNotFound()
    {
        var envelopeRepo = Substitute.For<IEnvelopeRepository>();
        var categoryRepo = Substitute.For<ICategoryRepository>();
        var cache = Substitute.For<HybridCache>();
        var logger = Substitute.For<ILogger<EnvelopeService>>();
        var realtime = Substitute.For<IEnvelopeRealtimeNotifier>();
        var envelopeCacheInvalidator = Substitute.For<IEnvelopeCacheInvalidator>();

        var service = new EnvelopeService(
            envelopeRepo, 
            categoryRepo, 
            cache, 
            logger,
            realtime,
            envelopeCacheInvalidator);

        categoryRepo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var request = CreateValidCreateRequest();

        var result = await service.CreateAsync(request, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.CategoryNotFound(request.CategoryId));

        envelopeRepo.DidNotReceive().Add(Arg.Any<Envelope>());

        await envelopeRepo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenTheSaveConflicts_ReturnsConflict()
    {
        var envelopeRepo = Substitute.For<IEnvelopeRepository>();
        var categoryRepo = Substitute.For<ICategoryRepository>();
        var cacheInvalidator = Substitute.For<IEnvelopeCacheInvalidator>();
        var realtime = Substitute.For<IEnvelopeRealtimeNotifier>();

        var service = new EnvelopeService(
            envelopeRepo,
            categoryRepo,
            Substitute.For<HybridCache>(),
            Substitute.For<ILogger<EnvelopeService>>(),
            realtime,
            cacheInvalidator);

        var envelope = Envelope.Create("Groceries", null, CategoryId, 1_500m, PeriodStart, PeriodEnd).Value;

        envelopeRepo.GetByIdAsync(envelope.Id, Arg.Any<CancellationToken>()).Returns(envelope);
        categoryRepo.ExistsAsync(CategoryId, Arg.Any<CancellationToken>()).Returns(true);

        // What EnvelopeRepository returns when EF Core reports a concurrency conflict.
        envelopeRepo.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(EnvelopeErrors.ConcurrencyConflict));

        var request = new UpdateEnvelopeRequest("Groceries", null, CategoryId, 1_500m, PeriodStart, PeriodEnd);

        var result = await service.UpdateAsync(envelope.Id, request, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.ConcurrencyConflict);
        result.Error.Type.ShouldBe(ErrorType.Conflict, "ResultExtensions maps Conflict to 409, which is the whole point");

        // A write that did not land must not evict the cache or push a stale balance to clients.
        await cacheInvalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        await realtime.DidNotReceive().EnvelopeUpdatedAsync(
            Arg.Any<EnvelopeBalanceSnapshot>(), Arg.Any<CancellationToken>());
    }
}
