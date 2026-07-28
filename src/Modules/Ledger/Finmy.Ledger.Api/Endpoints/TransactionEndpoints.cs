using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Application.Transactions;
using Finmy.Ledger.Application.Transactions.Dtos;
using Finmy.Ledger.Domain.Transactions;
using Finmy.Modularity.Extensions;
using Finmy.Modularity.Filters;
using Finmy.SharedKernel.Results;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        ITransactionRequestStatusStore statusStore,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var transactionId = Guid.CreateVersion7();

        await statusStore.MarkPendingAsync(transactionId, cancellationToken);

        var command = new RecordTransactionCommand(transactionId, request.SpaceId, request.EnvelopeId, request.Amount, request.Direction, request.OccurredOn, request.Description);

        await bus.SendAsync(command);

        response.Headers.RetryAfter = "5";

        return Results.Accepted($"/transactions/{transactionId}", new { transactionId, status = TransactionRequestStatus.Pending.ToString() });
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
            if (snapshot.Error is null)
                return Result.Failure(TransactionErrors.ErrorInvalid).ToProblemDetails();

            return Result.Failure(snapshot.Error).ToProblemDetails();
        }

        var response = new TransactionStatusResponse(id, snapshot.Status.ToString(), snapshot.CreatedAt, snapshot.LastUpdatedAt);

        return Results.Ok(response);
    }
}
