namespace Finmy.Ledger.Infrastructure.Persistence;

public sealed class TransactionStatusOptions
{
    public const string SectionName = "TransactionStatus";

    /// <summary>
    /// How long a transaction request status row is kept after it was last written, before the
    /// pruning background service (see TransactionRequestPruningService) deletes it.
    /// </summary>
    public int RetentionDays { get; set; } = 7;
}
