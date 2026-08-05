using System.Net;
using System.Net.Http.Json;

using Finmy.Budgeting.Domain.Envelopes;
using Finmy.Budgeting.Infrastructure.Persistence;
using Finmy.Contracts.Budgeting;
using Finmy.Ledger.Application.Transactions;
using Finmy.Ledger.Domain.Transactions;
using Finmy.Ledger.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Wolverine.Tracking;

namespace Finmy.IntegrationTests.Ledger;

/// <summary>
/// Drives POST /api/v1/transactions over HTTP and follows the anti-overspend loop to its end.
///
/// Waiting uses Wolverine's tracked session rather than Task.Delay, so a test fails because
/// the message was not handled, not because a sleep was too short.
/// </summary>
[Collection(ApiCollection.Name)]
public class RecordTransactionEndpointTests(FinmyApiFactory factory)
{
    private static readonly Guid SeededCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Posting_a_transaction_deducts_from_the_envelope_and_confirms()
    {
        var envelopeId = await SeedEnvelopeAsync(allocated: 1_000m);
        var spaceId = Guid.CreateVersion7();

        using var client = await factory.CreateAuthenticatedClientAsync(TestContext.Current.CancellationToken);

        var session = await factory.Services.ExecuteAndWaitAsync(
            async () =>
            {
                var response = await client.PostAsJsonAsync("/api/v1/transactions", new
                {
                    spaceId,
                    envelopeId,
                    amount = 250m,
                    // No JsonStringEnumConverter is registered, so the enum crosses the wire
                    // as a number. 0 is Expense.
                    direction = 0,
                    occurredOn = DateTimeOffset.UtcNow,
                    description = "Groceries"
                }, TestContext.Current.CancellationToken);

                response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            },
            timeoutInMilliseconds: 30_000);

        session.Executed.MessagesOf<EnvelopeBalanceChangedEvent>().ShouldNotBeEmpty(
            "the balance change is what proves Budgeting applied the deduction");

        var envelope = await LoadEnvelopeAsync(envelopeId);
        envelope.Spent.ShouldBe(250m);
        envelope.Remaining.ShouldBe(750m);

        var transaction = await SingleTransactionForAsync(spaceId);
        transaction.State.ShouldBe(TransactionState.Confirmed);

        // TECH-DEBT #2: the status now lives in Postgres, not a ConcurrentDictionary, so it
        // survives a restart and is visible to every replica.
        var statusRow = await LoadTransactionRequestAsync(transaction.Id);
        statusRow.Status.ShouldBe(TransactionRequestStatus.Succeeded);
        statusRow.ExpiresAtUtc.ShouldBeGreaterThan(statusRow.CreatedAtUtc);
    }

    [Fact]
    public async Task Overspending_reverses_the_transaction_and_leaves_the_balance_alone()
    {
        var envelopeId = await SeedEnvelopeAsync(allocated: 100m);
        var spaceId = Guid.CreateVersion7();

        using var client = await factory.CreateAuthenticatedClientAsync(TestContext.Current.CancellationToken);

        var session = await factory.Services.ExecuteAndWaitAsync(
            async () =>
            {
                var response = await client.PostAsJsonAsync("/api/v1/transactions", new
                {
                    spaceId,
                    envelopeId,
                    amount = 500m,
                    direction = 0,
                    occurredOn = DateTimeOffset.UtcNow,
                    description = "Too much"
                }, TestContext.Current.CancellationToken);

                response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            },
            timeoutInMilliseconds: 30_000);

        session.Executed.MessagesOf<EnvelopeOverspentEvent>().ShouldNotBeEmpty(
            "insufficient funds is a business conclusion and has to travel as an event");

        var envelope = await LoadEnvelopeAsync(envelopeId);
        envelope.Spent.ShouldBe(0m, "a refused spend must not move the balance");

        var transaction = await SingleTransactionForAsync(spaceId);
        transaction.State.ShouldBe(TransactionState.Reversed);
    }

    [Fact]
    public async Task The_same_idempotency_key_records_one_transaction()
    {
        var envelopeId = await SeedEnvelopeAsync(allocated: 1_000m);
        var spaceId = Guid.CreateVersion7();
        var idempotencyKey = $"test-{Guid.CreateVersion7()}";

        using var client = await factory.CreateAuthenticatedClientAsync(TestContext.Current.CancellationToken);

        var payload = new
        {
            spaceId,
            envelopeId,
            amount = 40m,
            direction = 0,
            occurredOn = DateTimeOffset.UtcNow,
            description = "Coffee"
        };

        await factory.Services.ExecuteAndWaitAsync(
            async () =>
            {
                using var request = BuildRequest(payload, idempotencyKey);
                var first = await client.SendAsync(request, TestContext.Current.CancellationToken);
                first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            },
            timeoutInMilliseconds: 30_000);

        using var replayRequest = BuildRequest(payload, idempotencyKey);
        var replay = await client.SendAsync(replayRequest, TestContext.Current.CancellationToken);
        replay.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await using var scope = factory.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        var count = await ledger.Transactions
            .Where(x => x.SpaceId == spaceId)
            .CountAsync(TestContext.Current.CancellationToken);

        count.ShouldBe(1, "replaying an Idempotency-Key must not record a second transaction");

        var envelope = await LoadEnvelopeAsync(envelopeId);
        envelope.Spent.ShouldBe(40m, "and it must not deduct twice either");
    }

    [Fact]
    public async Task Posting_without_a_token_is_rejected()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            spaceId = Guid.CreateVersion7(),
            envelopeId = Guid.CreateVersion7(),
            amount = 10m,
            direction = 0,
            occurredOn = DateTimeOffset.UtcNow,
            description = "No token"
        }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static HttpRequestMessage BuildRequest(object payload, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions")
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return request;
    }

    private async Task<Guid> SeedEnvelopeAsync(decimal allocated)
    {
        var periodStart = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var envelope = Envelope.Create(
            $"Envelope {Guid.CreateVersion7()}",
            description: null,
            SeededCategoryId,
            allocated,
            periodStart,
            periodStart.AddMonths(1));

        envelope.IsSuccess.ShouldBeTrue();

        await using var scope = factory.Services.CreateAsyncScope();
        var budgeting = scope.ServiceProvider.GetRequiredService<BudgetingDbContext>();

        budgeting.Envelopes.Add(envelope.Value);
        await budgeting.SaveChangesAsync(TestContext.Current.CancellationToken);

        return envelope.Value.Id;
    }

    private async Task<Envelope> LoadEnvelopeAsync(Guid envelopeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var budgeting = scope.ServiceProvider.GetRequiredService<BudgetingDbContext>();

        return await budgeting.Envelopes.AsNoTracking()
            .SingleAsync(x => x.Id == envelopeId, TestContext.Current.CancellationToken);
    }

    private async Task<Transaction> SingleTransactionForAsync(Guid spaceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        return await ledger.Transactions.AsNoTracking()
            .SingleAsync(x => x.SpaceId == spaceId, TestContext.Current.CancellationToken);
    }

    private async Task<TransactionRequestRecord> LoadTransactionRequestAsync(Guid transactionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        return await ledger.TransactionRequests.AsNoTracking()
            .SingleAsync(x => x.TransactionId == transactionId, TestContext.Current.CancellationToken);
    }
}
