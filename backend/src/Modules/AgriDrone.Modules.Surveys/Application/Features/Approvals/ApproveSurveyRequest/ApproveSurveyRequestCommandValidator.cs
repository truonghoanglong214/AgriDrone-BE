using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

internal sealed class ApproveSurveyRequestCommandValidator
    : AbstractValidator<ApproveSurveyRequestCommand>
{
    public ApproveSurveyRequestCommandValidator()
    {
        RuleFor(command => command.SurveyRequestId).NotEmpty();
        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(4_000);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0u);
        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .Must(key => !string.IsNullOrWhiteSpace(key))
            .WithMessage("Idempotency key cannot contain only whitespace.")
            .MaximumLength(
                SurveyApprovalIdempotencyRecord.MaximumKeyLength);
        RuleFor(command => command.Checklist)
            .NotNull()
            .SetValidator(new ApprovalChecklistValidator());
    }

    private sealed class ApprovalChecklistValidator
        : AbstractValidator<SurveyRequestReviewChecklistInput>
    {
        public ApprovalChecklistValidator()
        {
            RuleFor(checklist => checklist.Version)
                .Equal(SurveyRequestReviewChecklistDefinition.Version)
                .WithMessage(
                    $"Checklist version must be '{SurveyRequestReviewChecklistDefinition.Version}'.");
            RuleFor(checklist => checklist.Answers)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(SurveyRequestReviewChecklistDefinition.HasExactAnswers)
                .WithMessage(
                    "Checklist answers must contain exactly the current checklist item codes.")
                .Must(SurveyRequestReviewChecklistDefinition.AreAllAnswersConfirmed)
                .WithMessage(
                    "Every approval checklist answer must be confirmed.");
            RuleFor(checklist => checklist.Notes)
                .MaximumLength(4_000)
                .When(checklist => checklist.Notes is not null);
        }
    }
}
