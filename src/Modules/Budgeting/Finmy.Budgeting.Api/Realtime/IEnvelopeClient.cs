namespace Finmy.Budgeting.Api.Realtime;

public interface IEnvelopeClient
{
    Task EnvelopeUpdated(EnvelopeRealtimeState state);
    Task EnvelopeDeleted(Guid id);
}
