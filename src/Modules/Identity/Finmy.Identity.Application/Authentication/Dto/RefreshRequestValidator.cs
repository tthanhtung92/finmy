using FluentValidation;

namespace Finmy.Identity.Application.Authentication.Dto;

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty();

        RuleFor(x => x.RefreshToken)
            .MinimumLength(32);
    }
}
