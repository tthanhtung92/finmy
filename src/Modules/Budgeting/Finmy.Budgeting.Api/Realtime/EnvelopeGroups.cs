namespace Finmy.Budgeting.Api.Realtime;

public static class EnvelopeGroups
{
    /// seam: đổi sang group theo Space khi có Space aggregate
    public static string ForEnvelope(Guid envelopeId) => $"envelope-{envelopeId}";
}
