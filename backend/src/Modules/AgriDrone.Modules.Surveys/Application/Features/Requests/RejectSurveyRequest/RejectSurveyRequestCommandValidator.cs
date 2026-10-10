using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.RejectSurveyRequest;

internal sealed class RejectSurveyRequestCommandValidator
    : AbstractValidator<RejectSurveyRequestCommand>
{
    public RejectSurveyRequestCommandValidator()
    {
        RuleFor(command => command.SurveyRequestId).NotEmpty();
        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(4_000);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0u);
        RuleFor(command => command.Checklist)
            .NotNull()
            .SetValidator(new SurveyRequestReviewChecklistInputValidator());
    }

    private sealed class SurveyRequestReviewChecklistInputValidator
        : AbstractValidator<SurveyRequestReviewChecklistInput>
    {
        public SurveyRequestReviewChecklistInputValidator()
        {
            RuleFor(checklist => checklist.Version)
            .Equal(SurveyRequestReviewChecklistDefinition.Version)
            .WithMessage(
                $"Checklist version must be '{SurveyRequestReviewChecklistDefinition.Version}'.");
            RuleFor(checklist => checklist.Notes)
                .MaximumLength(4_000)
                .When(checklist => checklist.Notes is not null);
        }
    }
}
