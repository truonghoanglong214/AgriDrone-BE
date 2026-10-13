using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.ConfirmSurveyOrderPoleCount;

internal sealed class ConfirmSurveyOrderPoleCountCommandValidator
    : AbstractValidator<ConfirmSurveyOrderPoleCountCommand>
{
    public ConfirmSurveyOrderPoleCountCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.ExpectedFarmBaseMapVersionId).NotEmpty();
        RuleFor(command => command.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(command => command.EvidenceReference)
            .NotEmpty()
            .MaximumLength(1000);
    }
}
