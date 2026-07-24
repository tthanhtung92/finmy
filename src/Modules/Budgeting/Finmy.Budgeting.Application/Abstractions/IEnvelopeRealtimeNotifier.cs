namespace Finmy.Budgeting.Application.Abstractions;

public interface IEnvelopeRealtimeNotifier
{
    Task EnvelopeUpdatedAsync(Guid id, string name, decimal allocated, CancellationToken cancellationToken);
    Task EnvelopeDeletedAsync(Guid id, CancellationToken cancellationToken);
}
