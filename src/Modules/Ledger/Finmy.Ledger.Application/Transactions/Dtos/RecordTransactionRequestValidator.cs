using FluentValidation;

namespace Finmy.Ledger.Application.Transactions.Dtos;

public sealed class RecordTransactionRequestValidator : AbstractValidator<RecordTransactionRequest>
{
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(5);

    public RecordTransactionRequestValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.EnvelopeId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.OccurredOn).Must(occurredOn => occurredOn <= DateTimeOffset.UtcNow.Add(ClockSkewTolerance));
    }
}
