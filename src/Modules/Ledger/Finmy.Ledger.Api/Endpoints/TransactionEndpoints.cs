using System.Globalization;

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

public static class TransactionEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/transactions");

        group.MapPost("/", RecordTransactionAsync).AddEndpointFilter<ValidationFilter<RecordTransactionRequest>>();
        group.MapGet("/{id:guid}", GetTransactionAsync);
        group.MapGet("/{id:guid}/status", GetTransactionStatusAsync);
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

                if (!string.Equals(requestHash, idempotencyRecord.RequestHash, StringComparison.Ordinal))
                    return Result.Failure(TransactionErrors.IdempotencyKeyReused).ToProblemDetails();

                httpResponse.Headers.RetryAfter = "5";
                return Results.Accepted(
                    $"/api/v1/transactions/{idempotencyRecord.TransactionId}/status",
                    new { transactionId = idempotencyRecord.TransactionId, status = replaySnapshot?.Status.ToString() });
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
        return Results.Accepted(
            $"/api/v1/transactions/{newTransactionId}/status",
            new { transactionId = newTransactionId, status = TransactionRequestStatus.Pending.ToString() });
    }

    /// <summary>
    /// TECH-DEBT #8: the transaction itself, what the status endpoint's 303 See Other points at
    /// once the request resolves to Succeeded.
    /// </summary>
    private static async Task<IResult> GetTransactionAsync(
        Guid id,
        ITransactionRepository repository,
        CancellationToken cancellationToken)
    {
        var transaction = await repository.GetByIdAsync(id, cancellationToken);
        if (transaction is null)
            return Result.Failure(TransactionErrors.NotFound(id)).ToProblemDetails();

        var response = new TransactionResponse(
            transaction.Id,
            transaction.SpaceId,
            transaction.EnvelopeId,
            transaction.Amount,
            (int)transaction.Direction,
            (int)transaction.State,
            transaction.OccurredOnUtc,
            transaction.Description,
            transaction.ConfirmedAtUtc,
            transaction.ReversedAtUtc);

        return Results.Ok(response);
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

        // TECH-DEBT #8: once Budgeting has applied the deduction, the status resource redirects
        // to the transaction itself rather than reporting the same "Succeeded" envelope forever.
        if (snapshot.Status == TransactionRequestStatus.Succeeded)
            return new SeeOtherResult($"/api/v1/transactions/{id}");

        // Pending: TECH-DEBT #3's Expires header, so a client knows how long the status
        // resource is worth polling before the retention pruning sweeps it.
        return new TransactionStatusResult(
            new TransactionStatusResponse(id, snapshot.Status.ToString(), snapshot.CreatedAt, snapshot.LastUpdatedAt),
            snapshot.ExpiresAt);
    }

    /// <summary>
    /// Results.Redirect has no 303 option (only 301/302/307/308), and 303 is the correct code
    /// for "the result of this POST lives at a different URI, fetch it with GET" (RFC 9110
    /// 15.4.4), so this is a minimal, purpose-built IResult rather than reaching for a redirect
    /// helper that would send the wrong status code.
    /// </summary>
    private sealed class SeeOtherResult(string location) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
            httpContext.Response.Headers.Location = location;

            return Task.CompletedTask;
        }
    }

    private sealed class TransactionStatusResult(TransactionStatusResponse response, DateTimeOffset expiresAt) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.Expires = expiresAt.UtcDateTime.ToString("R", CultureInfo.InvariantCulture);

            return Results.Ok(response).ExecuteAsync(httpContext);
        }
    }
}
