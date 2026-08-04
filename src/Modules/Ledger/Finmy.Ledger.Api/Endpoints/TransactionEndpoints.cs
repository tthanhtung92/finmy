using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Application.Transactions;
using Finmy.Ledger.Application.Transactions.Dtos;
using Finmy.Ledger.Domain.Transactions;
using Finmy.Modularity.Extensions;
using Finmy.Modularity.Filters;
using Finmy.SharedKernel.Results;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Wolverine;

namespace Finmy.Ledger.Api.Endpoints;

public sealed class TransactionEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/transactions");

        group.MapPost("/", RecordTransactionAsync).AddEndpointFilter<ValidationFilter<RecordTransactionRequest>>();
        group.MapGet("/{id:guid}", GetTransactionStatusAsync);
    }

    private static async Task<IResult> RecordTransactionAsync(
        RecordTransactionRequest request,
        IMessageBus bus,
        ITransactionRequestStatusStore transactionRequestStatusStore,
        HttpResponse httpResponse,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var newTransactionId = Guid.CreateVersion7();

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            if (idempotencyKey.Length > 255)
                return Result.Failure(TransactionErrors.IdempotencyKeyTooLong).ToProblemDetails();

            var requestHash = RequestFingerprint.Compute(request);

            var idempotencyRecord = await idempotencyStore.FindAsync(idempotencyKey, request.SpaceId, cancellationToken);
            if (idempotencyRecord is not null)
            {
                var replaySnapshot = await transactionRequestStatusStore.FindAsync(idempotencyRecord.TransactionId, cancellationToken);

                return string.Equals(requestHash, idempotencyRecord.RequestHash, StringComparison.Ordinal)
                    ? Results.Accepted($"/transactions/{idempotencyRecord.TransactionId}", new { transactionId = idempotencyRecord.TransactionId, status = replaySnapshot?.Status.ToString() })
                    : Result.Failure(TransactionErrors.IdempotencyKeyReused).ToProblemDetails();
            }

            // Claim the idempotency key so two concurrent requests with the same key cannot both proceed
            var reserveSuccess = await idempotencyStore.TryReserveAsync(idempotencyKey, request.SpaceId, requestHash, newTransactionId, cancellationToken);
            if (!reserveSuccess)
            {
                httpResponse.Headers.RetryAfter = "5";
                return Result.Failure(TransactionErrors.RequestInProgress).ToProblemDetails();
            }
        }

        await transactionRequestStatusStore.MarkPendingAsync(newTransactionId, cancellationToken);
        var command = new RecordTransactionCommand(newTransactionId, request.SpaceId, request.EnvelopeId, request.Amount, request.Direction, request.OccurredOn, request.Description);
        await bus.SendAsync(command);

        httpResponse.Headers.RetryAfter = "5";
        return Results.Accepted($"/transactions/{newTransactionId}", new { transactionId = newTransactionId, status = TransactionRequestStatus.Pending.ToString() });
    }

    private static async Task<IResult> GetTransactionStatusAsync(
        Guid id,
        ITransactionRequestStatusStore statusStore,
        CancellationToken cancellationToken)
    {
        var snapshot = await statusStore.FindAsync(id, cancellationToken);
        if (snapshot is null)
            return Result.Failure(TransactionErrors.NotFound(id)).ToProblemDetails();

        if (snapshot.Status == TransactionRequestStatus.Failed)
        {
            return snapshot.Error is null
                ? Result.Failure(TransactionErrors.ErrorInvalid).ToProblemDetails()
                : Result.Failure(snapshot.Error).ToProblemDetails();
        }

        var response = new TransactionStatusResponse(id, snapshot.Status.ToString(), snapshot.CreatedAt, snapshot.LastUpdatedAt);
        return Results.Ok(response);
    }
}
