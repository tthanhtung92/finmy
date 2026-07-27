namespace Finmy.Budgeting.Application.Envelopes;

public sealed class EnvelopeNotFoundException(Guid envelopeId) : Exception($"Envelope '{envelopeId}' does not exist.")
{
    public Guid EnvelopeId { get; } = envelopeId;
}
