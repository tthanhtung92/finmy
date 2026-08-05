using Finmy.Ledger.Application.Transactions;
using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Infrastructure.Persistence;

/// <summary>
/// The Postgres-backed row behind ITransactionRequestStatusStore (TECH-DEBT #2). Error is
/// flattened into three nullable columns rather than stored as a nested owned type, matching
/// how IdempotencyRecord keeps its shape a plain, flat POCO.
/// </summary>
public sealed class TransactionRequestRecord
{
    public required Guid TransactionId { get; set; }
    public required TransactionRequestStatus Status { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; set; }
    public required DateTimeOffset LastUpdatedAtUtc { get; set; }

    /// <summary>
    /// TECH-DEBT #3's retention window: when the pruning background service is allowed to
    /// delete this row, and what the status endpoint reports as its Expires header.
    /// </summary>
    public required DateTimeOffset ExpiresAtUtc { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorDescription { get; set; }
    public ErrorType? ErrorType { get; set; }
}
