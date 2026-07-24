using Microsoft.AspNetCore.SignalR;

namespace Finmy.Budgeting.Api.Realtime;

public class EnvelopeHub : Hub<IEnvelopeClient>
{
    public async Task WatchEnvelope(Guid envelopeId)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId, 
            EnvelopeGroups.ForEnvelope(envelopeId)
        );
    }

    public async Task UnwatchEnvelope(Guid envelopeId)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId, 
            EnvelopeGroups.ForEnvelope(envelopeId)
        );
    }
}
