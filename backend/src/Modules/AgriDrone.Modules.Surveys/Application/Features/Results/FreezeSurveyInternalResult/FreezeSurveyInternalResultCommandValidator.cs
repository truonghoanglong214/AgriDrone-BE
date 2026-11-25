using System.Text.RegularExpressions;
using AgriDrone.Modules.Surveys.Domain;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.FreezeSurveyInternalResult;

internal sealed partial class FreezeSurveyInternalResultCommandValidator
    : AbstractValidator<FreezeSurveyInternalResultCommand>
{
    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Hex64Pattern();

    public FreezeSurveyInternalResultCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.OperationId).NotEmpty();
        RuleFor(c => c.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(c => c.ExpectedAttemptId).NotEmpty();
        RuleFor(c => c.ExpectedCountSnapshotId).NotEmpty();
        RuleFor(c => c.ExpectedCountFingerprint)
            .NotEmpty()
            .Must(v => !string.IsNullOrWhiteSpace(v) && Hex64Pattern().IsMatch(v.Trim()))
            .WithMessage("ExpectedCountFingerprint must be a 64-character hexadecimal SHA-256 string.");

        RuleFor(c => c.ReviewDecision)
            .NotEmpty()
            .Must(d => SurveyInternalApprovedResultRevision.CanonicalizeReviewDecision(d) is
                SurveyInternalApprovedResultRevision.DecisionApproved or
                SurveyInternalApprovedResultRevision.DecisionApprovedWithObservations or
                SurveyInternalApprovedResultRevision.DecisionInternalApproved)
            .WithMessage("ReviewDecision must be Approved, ApprovedWithObservations, or InternalApproved.");

        RuleFor(c => c.FreezeReason)
            .NotEmpty()
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .MaximumLength(500);

        RuleFor(c => c.ExpectedComboBatchId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("ExpectedComboBatchId cannot be empty when supplied.");
        RuleFor(c => c.ExpectedComboSourceRevision)
            .Must(rev => !rev.HasValue || rev.Value >= 1)
            .WithMessage("ExpectedComboSourceRevision must be >= 1 when supplied.");
        RuleFor(c => c.ExpectedComboBatchFingerprint)
            .Must(v => v is null || (!string.IsNullOrWhiteSpace(v) && Hex64Pattern().IsMatch(v.Trim())))
            .WithMessage("ExpectedComboBatchFingerprint must be a 64-character hexadecimal SHA-256 string.");
        RuleFor(c => c.ExpectedComboReviewRevisionId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("ExpectedComboReviewRevisionId cannot be empty when supplied.");
        RuleFor(c => c.ExpectedComboReviewFingerprint)
            .Must(v => v is null || (!string.IsNullOrWhiteSpace(v) && Hex64Pattern().IsMatch(v.Trim())))
            .WithMessage("ExpectedComboReviewFingerprint must be a 64-character hexadecimal SHA-256 string.");

        RuleFor(c => c)
            .Must(HasConsistentComboExpectationSet)
            .WithMessage("COMBO expected batch and review tokens must be supplied together or all omitted for MAP.");
    }

    private static bool HasConsistentComboExpectationSet(FreezeSurveyInternalResultCommand command)
    {
        var anySet = command.ExpectedComboBatchId.HasValue ||
                     command.ExpectedComboSourceRevision.HasValue ||
                     command.ExpectedComboBatchFingerprint is not null ||
                     command.ExpectedComboReviewRevisionId.HasValue ||
                     command.ExpectedComboReviewFingerprint is not null;

        var allSet = command.ExpectedComboBatchId.HasValue &&
                     command.ExpectedComboSourceRevision.HasValue &&
                     command.ExpectedComboBatchFingerprint is not null &&
                     command.ExpectedComboReviewRevisionId.HasValue &&
                     command.ExpectedComboReviewFingerprint is not null;

        return !anySet || allSet;
    }
}
