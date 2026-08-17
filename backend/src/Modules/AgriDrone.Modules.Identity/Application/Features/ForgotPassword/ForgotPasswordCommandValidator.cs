using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.ForgotPassword;

internal sealed class ForgotPasswordCommandValidator
    : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(320).WithMessage("Email must not exceed 320 characters.");
    }
}
