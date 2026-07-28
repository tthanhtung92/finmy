using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Abstractions.Dtos;

using Microsoft.AspNetCore.SignalR;

namespace Finmy.Budgeting.Api.Realtime;

public class EnvelopeRealtimeNotifier(IHubContext<EnvelopeHub, IEnvelopeClient> hubContext) : IEnvelopeRealtimeNotifier
{
    public async Task EnvelopeUpdatedAsync(EnvelopeBalanceSnapshot snapshot, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(EnvelopeGroups.ForEnvelope(snapshot.Id))
            .EnvelopeUpdated(new EnvelopeRealtimeState(
                snapshot.Id,
                snapshot.Name,
                snapshot.Allocated,
                snapshot.Spent,
                snapshot.Remaining)); 
    }

    public async Task EnvelopeAlertAsync(EnvelopeAlert alert, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(EnvelopeGroups.ForEnvelope(alert.Id))
            .EnvelopeAlert(new EnvelopeAlertState(
                alert.Id,
                alert.Level,
                alert.Allocated,
                alert.Spent,
                alert.Remaining,
                alert.AttemptedAmount));
    }

    public async Task EnvelopeDeletedAsync(Guid id, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(EnvelopeGroups.ForEnvelope(id))
            .EnvelopeDeleted(id);
    }
}
