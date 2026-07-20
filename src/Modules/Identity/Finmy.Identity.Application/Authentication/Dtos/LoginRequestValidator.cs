using FluentValidation;

namespace Finmy.Identity.Application.Authentication.Dtos;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]+").Matches(@"[a-z]+").Matches(@"[0-9]+");
    }
}