namespace Finmy.Ledger.Infrastructure.Persistence;

public sealed class IdempotencyRecord
{
    public required string Key { get; set; }
    public Guid SpaceId { get; set; }
    public required string RequestHash { get; set; }
    public Guid TransactionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
