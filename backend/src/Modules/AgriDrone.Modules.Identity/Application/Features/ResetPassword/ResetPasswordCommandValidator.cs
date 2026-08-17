using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.ResetPassword;

internal sealed class ResetPasswordCommandValidator
    : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Token)
            .NotEmpty().WithMessage("Password reset token is required.")
            .MaximumLength(512).WithMessage("Password reset token is invalid.");

        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("New password must be at least 8 characters long.")
            .MaximumLength(128).WithMessage("New password must not exceed 128 characters.");

        RuleFor(command => command.ConfirmPassword)
            .NotEmpty().WithMessage("Password confirmation is required.")
            .Equal(command => command.NewPassword)
            .WithMessage("Password confirmation does not match the new password.");
    }
}
