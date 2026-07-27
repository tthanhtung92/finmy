using Finmy.Budgeting.Domain.Envelopes;

using Microsoft.EntityFrameworkCore;

using Shouldly;

namespace Finmy.IntegrationTests.Budgeting;

public class EnvelopeConcurrencyTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Spend_WithoutContention_PersistsSpentAndVersion()
    {
        var ct = TestContext.Current.CancellationToken;

        var categoryId = await CreateCategoryAsync();
        var envelope = CreateEnvelope(categoryId, allocated: 100_000m);

        await using (var writeContext = fixture.CreateBudgetingDbContext())
        {
            writeContext.Envelopes.Add(envelope);
            await writeContext.SaveChangesAsync(ct);
        }

        await using (var context = fixture.CreateBudgetingDbContext())
        {
            var loaded = await context.Envelopes.SingleAsync(e => e.Id == envelope.Id, ct);
            var spendResult = loaded.Spend(1_000m);
            spendResult.IsSuccess.ShouldBeTrue();
            await context.SaveChangesAsync(ct);
        }

        await using var verifyContext = fixture.CreateBudgetingDbContext();
        var reloaded = await verifyContext.Envelopes.SingleAsync(e => e.Id == envelope.Id, ct);
        reloaded.Spent.ShouldBe(1_000m);
        reloaded.Version.ShouldBe(1);
    }

    [Fact]
    public async Task TwoConcurrentSpends_OnlyOneSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;

        var categoryId = await CreateCategoryAsync();
        var envelope = CreateEnvelope(categoryId, allocated: 100_000m);

        await using (var writeContext = fixture.CreateBudgetingDbContext())
        {
            writeContext.Envelopes.Add(envelope);
            await writeContext.SaveChangesAsync(ct);
        }

        await using var contextA = fixture.CreateBudgetingDbContext();
        await using var contextB = fixture.CreateBudgetingDbContext();

        var envelopeA = await contextA.Envelopes.SingleAsync(e => e.Id == envelope.Id, ct);
        var envelopeB = await contextB.Envelopes.SingleAsync(e => e.Id == envelope.Id, ct);

        var spendA = envelopeA.Spend(100_000m);
        var spendB = envelopeB.Spend(100_000m);

        spendA.IsSuccess.ShouldBeTrue();
        spendB.IsSuccess.ShouldBeTrue();

        await contextA.SaveChangesAsync(ct);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());

        await using var verifyContext = fixture.CreateBudgetingDbContext();
        var reloaded = await verifyContext.Envelopes.SingleAsync(e => e.Id == envelope.Id, ct);
        reloaded.Spent.ShouldBe(100_000m);
    }

    [Fact]
    public async Task SecondSpendAfterReload_ReturnsInsufficientFunds()
    {
        var ct = TestContext.Current.CancellationToken;

        var categoryId = await CreateCategoryAsync();
        var envelope = CreateEnvelope(categoryId, allocated: 100_000m);

        await using (var writeContext = fixture.CreateBudgetingDbContext())
        {
            writeContext.Envelopes.Add(envelope);
            await writeContext.SaveChangesAsync(ct);
        }

        await using (var contextA = fixture.CreateBudgetingDbContext())
        await using (var contextB = fixture.CreateBudgetingDbContext())
        {
            var envelopeA = await contextA.Envelopes.SingleAsync(e => e.Id == envelope.Id, ct);
            var envelopeB = await contextB.Envelopes.SingleAsync(e => e.Id == envelope.Id, ct);

            envelopeA.Spend(100_000m).IsSuccess.ShouldBeTrue();
            envelopeB.Spend(100_000m).IsSuccess.ShouldBeTrue();

            await contextA.SaveChangesAsync(ct);

            await Should.ThrowAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());
        }

        await using var retryContext = fixture.CreateBudgetingDbContext();
        var reloaded = await retryContext.Envelopes.SingleAsync(e => e.Id == envelope.Id, ct);

        var retrySpend = reloaded.Spend(100_000m);

        retrySpend.IsFailure.ShouldBeTrue();
        retrySpend.Error.ShouldBe(EnvelopeErrors.InsufficientFunds);
    }

    private static Envelope CreateEnvelope(Guid categoryId, decimal allocated)
    {
        var result = Envelope.Create(
            name: $"Envelope {Guid.NewGuid():N}",
            description: null,
            categoryId: categoryId,
            allocated: allocated,
            periodStart: DateTimeOffset.UtcNow,
            periodEnd: DateTimeOffset.UtcNow.AddMonths(1));

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private async Task<Guid> CreateCategoryAsync()
    {
        var categoryId = Guid.CreateVersion7();

        await using var context = fixture.CreateBudgetingDbContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO budgeting."Categories" ("Id", "Name") VALUES ({categoryId}, {"Test Category"})""");

        return categoryId;
    }
}
