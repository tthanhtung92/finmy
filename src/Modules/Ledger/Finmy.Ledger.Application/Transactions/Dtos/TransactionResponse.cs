namespace Finmy.Ledger.Application.Transactions.Dtos;

/// <summary>
/// The transaction itself, at /transactions/{id} -- what the status endpoint's 303 See Other
/// points at once the request resolves to Succeeded. Direction and State cross the wire as
/// numbers: no JsonStringEnumConverter is registered anywhere in the host.
/// </summary>
public sealed record TransactionResponse(
    Guid Id,
    Guid SpaceId,
    Guid EnvelopeId,
    decimal Amount,
    int Direction,
    int State,
    DateTimeOffset OccurredOnUtc,
    string? Description,
    DateTimeOffset? ConfirmedAtUtc,
    DateTimeOffset? ReversedAtUtc);
