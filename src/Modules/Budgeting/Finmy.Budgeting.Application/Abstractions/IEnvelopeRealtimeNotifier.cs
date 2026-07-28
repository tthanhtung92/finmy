using Finmy.Budgeting.Application.Abstractions.Dtos;

namespace Finmy.Budgeting.Application.Abstractions;

public interface IEnvelopeRealtimeNotifier
{
    Task EnvelopeUpdatedAsync(EnvelopeBalanceSnapshot snapshot, CancellationToken cancellationToken);
    Task EnvelopeAlertAsync(EnvelopeAlert alert, CancellationToken cancellationToken);
    Task EnvelopeDeletedAsync(Guid id, CancellationToken cancellationToken);
}
