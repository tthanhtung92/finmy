namespace Finmy.Budgeting.Infrastructure.Persistence;

public sealed class ProcessedTransaction
{
    public Guid TransactionId { get; set; }
    public Guid EnvelopeId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset AppliedAtUtc { get; set; }
}