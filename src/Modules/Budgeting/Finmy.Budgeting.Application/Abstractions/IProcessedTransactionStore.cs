namespace Finmy.Budgeting.Application.Abstractions;

public interface IProcessedTransactionStore
{
    Task<bool> WasAppliedAsync(Guid transactionId, CancellationToken cancellationToken = default);
    void MarkApplied(Guid transactionId, Guid envelopeId, decimal amount, DateTimeOffset appliedAtUtc);
}
