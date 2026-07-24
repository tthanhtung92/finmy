using Finmy.Budgeting.Application.Abstractions;

using Microsoft.AspNetCore.SignalR;

namespace Finmy.Budgeting.Api.Realtime;

public class EnvelopeRealtimeNotifier(IHubContext<EnvelopeHub, IEnvelopeClient> hubContext) : IEnvelopeRealtimeNotifier
{
    public async Task EnvelopeUpdatedAsync(Guid id, string name, decimal allocated, CancellationToken cancellationToken)
    {
        await hubContext.Clients.Group(EnvelopeGroups.ForEnvelope(id)).EnvelopeUpdated(new EnvelopeRealtimeState(id, name, allocated));
    }

    public async Task EnvelopeDeletedAsync(Guid id, CancellationToken cancellationToken)
    {
        await hubContext.Clients.Group(EnvelopeGroups.ForEnvelope(id)).EnvelopeDeleted(id);
    }
}
