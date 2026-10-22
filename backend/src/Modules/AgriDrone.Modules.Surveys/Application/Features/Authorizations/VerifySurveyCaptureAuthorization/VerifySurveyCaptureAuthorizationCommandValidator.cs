using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Authorizations.VerifySurveyCaptureAuthorization;

internal sealed class VerifySurveyCaptureAuthorizationCommandValidator
    : AbstractValidator<VerifySurveyCaptureAuthorizationCommand>
{
    public VerifySurveyCaptureAuthorizationCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId)
            .NotEmpty();

        RuleFor(command => command.Type)
            .IsInEnum();

        RuleFor(command => command.EvidenceReference)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(command => command.IssuedAt)
            .NotEmpty();

        RuleFor(command => command.ExpiresAt)
            .GreaterThan(command => command.IssuedAt)
            .When(command => command.ExpiresAt.HasValue)
            .WithMessage("Thời hạn hết hiệu lực (ExpiresAt) phải sau ngày cấp (IssuedAt).");
    }
}
