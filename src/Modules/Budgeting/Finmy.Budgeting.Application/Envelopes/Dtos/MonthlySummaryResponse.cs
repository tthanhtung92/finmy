using System.ComponentModel;

namespace Finmy.Budgeting.Application.Envelopes.Dtos;

[ImmutableObject(true)]
public sealed record MonthlySummaryResponse(
    int Year,
    int Month,
    IReadOnlyList<MonthlyCategorySummary> Categories,
    decimal GrandTotalAllocated,
    decimal GrandTotalSpent,
    decimal GrandTotalRemaining);
