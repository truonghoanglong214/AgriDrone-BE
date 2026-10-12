using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.VerifySurveyOrderBoundaryAndScope;

internal sealed class VerifySurveyOrderBoundaryAndScopeCommandValidator
    : AbstractValidator<VerifySurveyOrderBoundaryAndScopeCommand>
{
    public VerifySurveyOrderBoundaryAndScopeCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.Boundary)
            .NotNull()
            .Must(boundary =>
                boundary.SRID == 4326 &&
                !boundary.IsEmpty &&
                boundary.IsValid)
            .WithMessage(
                "Boundary must be a non-empty valid Polygon using SRID 4326.");
        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(2000);
        RuleFor(command => command.EvidenceReference)
            .MaximumLength(1000)
            .When(command => command.EvidenceReference is not null);
        RuleFor(command => command.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(command => command.ExpectedCurrentBoundaryVersionNumber)
            .GreaterThan(0)
            .When(command =>
                command.ExpectedCurrentBoundaryVersionNumber.HasValue);
    }
}
