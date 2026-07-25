using Finmy.Budgeting.Domain.Envelopes;

using Shouldly;

namespace Finmy.UnitTests.Budgeting;

public class EnvelopeUpdateTests
{
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly DateTimeOffset PeriodStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    private static Envelope CreateValid() =>
        Envelope.Create("Groceries", "Monthly food budget", CategoryId, 1_500m, PeriodStart, PeriodEnd).Value;

    [Fact]
    public void Update_WithNullName_KeepOldValue_ReturnsFailureWithoutThrowing()
    {
        var envelope = CreateValid();

        var result = envelope.Update(
            null!,
            "Buy clothes",
            CategoryId,
            1_500m,
            PeriodStart,
            PeriodEnd);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.NameEmpty);
        envelope.Name.ShouldBe("Groceries");
        envelope.Description.ShouldBe("Monthly food budget");
        envelope.Allocated.ShouldBe(1_500m);
    }
}
