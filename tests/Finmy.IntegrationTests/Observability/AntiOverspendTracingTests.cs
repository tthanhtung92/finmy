using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;

using Finmy.Budgeting.Domain.Envelopes;
using Finmy.Budgeting.Infrastructure.Persistence;
using Finmy.SharedKernel.Observability;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Wolverine.Tracking;

namespace Finmy.IntegrationTests.Observability;

/// <summary>
/// Asserts the deliverable of the anti-overspend <c>ActivitySource</c>: one transaction produces
/// one trace across both modules, not two separate ones stitched together after the fact. Uses
/// an <see cref="ActivityListener"/>/<see cref="MeterListener"/> rather than a real OTLP
/// collector, so this needs nothing extra in CI.
/// </summary>
[Collection(ApiCollection.Name)]
public class AntiOverspendTracingTests(FinmyApiFactory factory)
{
    private static readonly Guid SeededCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Overspending_produces_one_trace_spanning_both_modules()
    {
        var envelopeId = await SeedEnvelopeAsync(allocated: 100m);
        var spaceId = Guid.CreateVersion7();

        var capturedActivities = new List<Activity>();
        long overspentMeasurements = 0;

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FinmyTelemetry.AntiOverspendSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                lock (capturedActivities)
                {
                    capturedActivities.Add(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == FinmyTelemetry.MeterName && instrument.Name == "finmy.envelopes.overspent")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>(
            (_, measurement, _, _) => Interlocked.Add(ref overspentMeasurements, measurement));
        meterListener.Start();

        using var client = await factory.CreateAuthenticatedClientAsync(TestContext.Current.CancellationToken);

        await factory.Services.ExecuteAndWaitAsync(
            async () =>
            {
                var response = await client.PostAsJsonAsync("/api/v1/transactions", new
                {
                    spaceId,
                    envelopeId,
                    amount = 500m,
                    // No JsonStringEnumConverter is registered; 0 is Expense.
                    direction = 0,
                    occurredOn = DateTimeOffset.UtcNow,
                    description = "Too much"
                }, TestContext.Current.CancellationToken);

                response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            },
            timeoutInMilliseconds: 30_000);

        var spanNames = capturedActivities.Select(a => a.OperationName).ToList();
        spanNames.ShouldContain("ledger.record_transaction");
        spanNames.ShouldContain("budgeting.apply_transaction");
        spanNames.ShouldContain("ledger.reverse_transaction");

        capturedActivities.Select(a => a.TraceId).Distinct().Count().ShouldBe(1,
            "the whole anti-overspend loop must be one trace across both modules, not one per handler");

        var budgetingSpan = capturedActivities.Single(a => a.OperationName == "budgeting.apply_transaction");
        budgetingSpan.GetTagItem("outcome").ShouldBe("overspent");

        overspentMeasurements.ShouldBeGreaterThanOrEqualTo(1);
    }

    private async Task<Guid> SeedEnvelopeAsync(decimal allocated)
    {
        var periodStart = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var envelope = Envelope.Create(
            $"Envelope {Guid.CreateVersion7()}",
            description: null,
            SeededCategoryId,
            allocated,
            periodStart,
            periodStart.AddMonths(1));

        envelope.IsSuccess.ShouldBeTrue();

        await using var scope = factory.Services.CreateAsyncScope();
        var budgeting = scope.ServiceProvider.GetRequiredService<BudgetingDbContext>();

        budgeting.Envelopes.Add(envelope.Value);
        await budgeting.SaveChangesAsync(TestContext.Current.CancellationToken);

        return envelope.Value.Id;
    }
}
