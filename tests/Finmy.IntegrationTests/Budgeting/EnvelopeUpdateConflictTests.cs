using Finmy.Budgeting.Domain.Envelopes;
using Finmy.Budgeting.Infrastructure.Persistence;
using Finmy.SharedKernel.Results;

using Microsoft.EntityFrameworkCore;

using Shouldly;

namespace Finmy.IntegrationTests.Budgeting;

/// <summary>
/// TECH-DEBT #5: EnvelopeService called SaveChangesAsync bare, so a concurrency conflict on
/// PUT /envelopes/{id} reached GlobalExceptionHandler and came back as 500. The bus path had
/// the retry policy from Program.cs; the HTTP path had nothing.
///
/// EnvelopeRepository now translates DbUpdateConcurrencyException into
/// EnvelopeErrors.ConcurrencyConflict, an ErrorType.Conflict that ResultExtensions maps to
/// 409. The translation lives in Infrastructure because Finmy.Budgeting.Application is not
/// allowed to reference EF Core, which the architecture tests enforce.
///
/// These tests use two contexts over one row rather than racing HTTP requests, so the losing
/// write is deterministic instead of timing-dependent.
/// </summary>
public class EnvelopeUpdateConflictTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly Guid SeededCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task A_losing_update_returns_a_conflict_error_rather_than_throwing()
    {
        var envelopeId = await SeedEnvelopeAsync();

        await using var winner = fixture.CreateBudgetingDbContext();
        await using var loser = fixture.CreateBudgetingDbContext();

        // Both read the same version.
        var winnerEnvelope = await winner.Envelopes.SingleAsync(x => x.Id == envelopeId, TestContext.Current.CancellationToken);
        var loserEnvelope = await loser.Envelopes.SingleAsync(x => x.Id == envelopeId, TestContext.Current.CancellationToken);

        winnerEnvelope.Fund(100m).IsSuccess.ShouldBeTrue();
        await winner.SaveChangesAsync(TestContext.Current.CancellationToken);

        loserEnvelope.Update("Renamed by the loser", null, SeededCategoryId, 1_000m, loserEnvelope.PeriodStartUtc, loserEnvelope.PeriodEndUtc)
            .IsSuccess.ShouldBeTrue();

        var repository = new EnvelopeRepository(loser);

        var result = await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue("the row moved on, so this save cannot land");
        result.Error.ShouldBe(EnvelopeErrors.ConcurrencyConflict);
        result.Error.Type.ShouldBe(ErrorType.Conflict, "ResultExtensions turns Conflict into 409");
    }

    [Fact]
    public async Task An_uncontested_update_still_succeeds()
    {
        var envelopeId = await SeedEnvelopeAsync();

        await using var context = fixture.CreateBudgetingDbContext();

        var envelope = await context.Envelopes.SingleAsync(x => x.Id == envelopeId, TestContext.Current.CancellationToken);
        envelope.Update("Renamed", null, SeededCategoryId, 1_000m, envelope.PeriodStartUtc, envelope.PeriodEndUtc)
            .IsSuccess.ShouldBeTrue();

        var repository = new EnvelopeRepository(context);

        var result = await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    private async Task<Guid> SeedEnvelopeAsync()
    {
        var periodStart = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var envelope = Envelope.Create(
            $"Conflict {Guid.CreateVersion7()}",
            description: null,
            SeededCategoryId,
            1_000m,
            periodStart,
            periodStart.AddMonths(1));

        envelope.IsSuccess.ShouldBeTrue();

        await using var context = fixture.CreateBudgetingDbContext();

        context.Envelopes.Add(envelope.Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return envelope.Value.Id;
    }
}
