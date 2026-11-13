using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyDeposit;

internal sealed class CreateSurveyDepositPolicyCommandValidator
    : AbstractValidator<CreateSurveyDepositPolicyCommand>
{
    public CreateSurveyDepositPolicyCommandValidator()
    {
        RuleFor(command => command.AmountVnd)
            .GreaterThanOrEqualTo(0m)
            .Must(amount => amount == decimal.Truncate(amount));
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}
