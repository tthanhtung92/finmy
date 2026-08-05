using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Finmy.SharedKernel.Observability;

/// <summary>
/// The single <see cref="ActivitySource"/> and <see cref="Meter"/> for the anti-overspend path,
/// so one transaction can be traced across the Ledger and Budgeting modules without either
/// module referencing the other. Lives in SharedKernel, which every module's Application layer
/// already depends on.
/// </summary>
public static class FinmyTelemetry
{
    public const string AntiOverspendSourceName = "Finmy.AntiOverspend";
    public const string MeterName = "Finmy";

    public static readonly ActivitySource AntiOverspend = new(AntiOverspendSourceName);

    /// <summary>
    /// Exposed (not private) so the host can register observable instruments on it too, such as
    /// the outbox backlog gauge, without a second <see cref="Meter"/> under a different name.
    /// </summary>
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> TransactionsRecorded =
        Meter.CreateCounter<long>("finmy.transactions.recorded", description: "Transactions accepted or rejected by RecordTransactionHandler.");

    public static readonly Counter<long> EnvelopesOverspent =
        Meter.CreateCounter<long>("finmy.envelopes.overspent", description: "Transactions that failed with insufficient envelope funds.");

    public static readonly Counter<long> ConcurrencyConflicts =
        Meter.CreateCounter<long>("finmy.envelope.concurrency_conflicts", description: "Optimistic concurrency conflicts on the envelope balance.");
}
