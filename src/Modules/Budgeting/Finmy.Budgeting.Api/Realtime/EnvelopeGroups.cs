namespace Finmy.Budgeting.Api.Realtime;

public static class EnvelopeGroups
{
    /// Seam: switch to per-Space groups once the Space aggregate exists
    public static string ForEnvelope(Guid envelopeId) => $"envelope-{envelopeId}";
}
