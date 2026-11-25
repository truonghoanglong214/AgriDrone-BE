using System.Text.RegularExpressions;
using AgriDrone.Modules.Surveys.Domain;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.RecordSurveyComboReview;

internal sealed partial class RecordSurveyComboReviewCommandValidator : AbstractValidator<RecordSurveyComboReviewCommand>
{
    private const int MaxItems = 10_000;

    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Hex64Pattern();

    public RecordSurveyComboReviewCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.OperationId).NotEmpty();
        RuleFor(c => c.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(c => c.ExpectedBatchId).NotEmpty();
        RuleFor(c => c.ExpectedSourceRevision).GreaterThanOrEqualTo(1);
        RuleFor(c => c.ExpectedBatchFingerprint)
            .NotEmpty()
            .Must(v => !string.IsNullOrWhiteSpace(v) && Hex64Pattern().IsMatch(v.Trim()))
            .WithMessage("ExpectedBatchFingerprint must be a 64-character hexadecimal SHA-256 string.");
        RuleFor(c => c.ExpectedPreviousReviewId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("ExpectedPreviousReviewId cannot be an empty GUID when provided.");
        RuleFor(c => c.Outcome)
            .NotEmpty()
            .Must(o => SurveyComboReviewRevision.CanonicalizeOutcome(o) is SurveyComboReviewRevision.OutcomeApproved or SurveyComboReviewRevision.OutcomeRejected)
            .WithMessage("Outcome must be Approved or Rejected.");
        RuleFor(c => c.ReviewReason)
            .NotEmpty()
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .MaximumLength(500);
        RuleFor(c => c.NegativeResultConfirmationEvidence)
            .MaximumLength(500)
            .When(c => c.NegativeResultConfirmationEvidence is not null);

        RuleFor(c => c.FindingDecisions)
            .NotNull()
            .Must(list => list.Count <= MaxItems)
            .WithMessage($"FindingDecisions cannot exceed {MaxItems} items.")
            .Must(list => list.All(i => i is not null) && list.Select(i => i.FindingId).Distinct().Count() == list.Count)
            .WithMessage("FindingDecisions must contain unique non-null finding items.");

        RuleForEach(c => c.FindingDecisions).ChildRules(item =>
        {
            item.RuleFor(f => f.FindingId).NotEmpty();
            item.RuleFor(f => f.Decision)
                .NotEmpty()
                .Must(d => SurveyComboReviewRevision.CanonicalizeFindingDecision(d) is SurveyComboReviewRevision.FindingDecisionAccept or SurveyComboReviewRevision.FindingDecisionExclude)
                .WithMessage("Finding decision must be Accept or Exclude.");
            item.RuleFor(f => f.Reason)
                .NotEmpty()
                .Must(r => !string.IsNullOrWhiteSpace(r))
                .MaximumLength(2000)
                .When(f => SurveyComboReviewRevision.CanonicalizeFindingDecision(f.Decision) == SurveyComboReviewRevision.FindingDecisionExclude);
            item.RuleFor(f => f.EvidenceMediaIds)
                .NotNull()
                .Must(HasValidEvidenceList)
                .WithMessage("Excluding a finding requires unique non-empty evidence media IDs.")
                .When(f => SurveyComboReviewRevision.CanonicalizeFindingDecision(f.Decision) == SurveyComboReviewRevision.FindingDecisionExclude);
            item.RuleFor(f => f.EvidenceMediaIds)
                .Must(HasValidOptionalEvidenceList)
                .WithMessage("EvidenceMediaIds must contain unique non-empty IDs.");
        });

        RuleFor(c => c.ZoneDecisions)
            .NotNull()
            .Must(list => list.Count <= MaxItems)
            .WithMessage($"ZoneDecisions cannot exceed {MaxItems} items.")
            .Must(list => list.All(i => i is not null) &&
                          list.Select(i => i.SourceProposalId?.Trim() ?? string.Empty).Distinct(StringComparer.Ordinal).Count() == list.Count)
            .WithMessage("ZoneDecisions must contain unique non-null zone proposal items.");

        RuleForEach(c => c.ZoneDecisions).ChildRules(item =>
        {
            item.RuleFor(z => z.SourceProposalId).NotEmpty().MaximumLength(200);
            item.RuleFor(z => z.Decision)
                .NotEmpty()
                .Must(d => SurveyComboReviewRevision.CanonicalizeZoneDecision(d) is
                    SurveyComboReviewRevision.ZoneDecisionApprove or
                    SurveyComboReviewRevision.ZoneDecisionReject or
                    SurveyComboReviewRevision.ZoneDecisionAdjust)
                .WithMessage("Zone decision must be Approve, Reject, or Adjust.");
            item.RuleFor(z => z.Reason)
                .NotEmpty()
                .Must(r => !string.IsNullOrWhiteSpace(r))
                .MaximumLength(2000)
                .When(z => SurveyComboReviewRevision.CanonicalizeZoneDecision(z.Decision) is
                    SurveyComboReviewRevision.ZoneDecisionReject or SurveyComboReviewRevision.ZoneDecisionAdjust);
            item.RuleFor(z => z.EvidenceMediaIds)
                .NotNull()
                .Must(HasValidEvidenceList)
                .WithMessage("Rejecting or adjusting a zone requires unique non-empty evidence media IDs.")
                .When(z => SurveyComboReviewRevision.CanonicalizeZoneDecision(z.Decision) is
                    SurveyComboReviewRevision.ZoneDecisionReject or SurveyComboReviewRevision.ZoneDecisionAdjust);
            item.RuleFor(z => z.EvidenceMediaIds)
                .Must(HasValidOptionalEvidenceList)
                .WithMessage("EvidenceMediaIds must contain unique non-empty IDs.");
            item.RuleFor(z => z.SelectedGuidanceId)
                .Must(id => !id.HasValue || id.Value != Guid.Empty)
                .WithMessage("SelectedGuidanceId cannot be empty when supplied.");
        });

        RuleFor(c => c.BlockerResolutions)
            .NotNull()
            .Must(list => list.Count <= MaxItems)
            .WithMessage($"BlockerResolutions cannot exceed {MaxItems} items.")
            .Must(list => list.All(i => i is not null) &&
                          list.Select(i => (
                                  i.SourceType?.Trim() ?? string.Empty,
                                  i.SourceId?.Trim() ?? string.Empty,
                                  i.ReasonCode?.Trim() ?? string.Empty))
                              .Distinct()
                              .Count() == list.Count)
            .WithMessage("BlockerResolutions must contain unique (SourceType, SourceId, ReasonCode) items.");

        RuleForEach(c => c.BlockerResolutions).ChildRules(item =>
        {
            item.RuleFor(b => b.SourceType).NotEmpty().MaximumLength(64);
            item.RuleFor(b => b.SourceId).NotEmpty().MaximumLength(200);
            item.RuleFor(b => b.ReasonCode).NotEmpty().MaximumLength(128);
            item.RuleFor(b => b.Resolution)
                .NotEmpty()
                .Must(r => SurveyComboReviewRevision.CanonicalizeBlockerResolution(r) is
                    SurveyComboReviewRevision.BlockerResolutionResolved or
                    SurveyComboReviewRevision.BlockerResolutionAcceptedWithJustification or
                    SurveyComboReviewRevision.BlockerResolutionExcluded or
                    SurveyComboReviewRevision.BlockerResolutionCorrected or
                    SurveyComboReviewRevision.BlockerResolutionUnresolved)
                .WithMessage("Unsupported blocker resolution value.");
            item.RuleFor(b => b.Reason)
                .NotEmpty()
                .Must(r => !string.IsNullOrWhiteSpace(r))
                .MaximumLength(2000)
                .When(b => SurveyComboReviewRevision.IsResolvedBlockerStatus(
                    SurveyComboReviewRevision.CanonicalizeBlockerResolution(b.Resolution)));
            item.RuleFor(b => b.EvidenceMediaIds)
                .NotNull()
                .Must(HasValidEvidenceList)
                .WithMessage("Resolving a blocker requires unique non-empty evidence media IDs.")
                .When(b => SurveyComboReviewRevision.IsResolvedBlockerStatus(
                    SurveyComboReviewRevision.CanonicalizeBlockerResolution(b.Resolution)));
            item.RuleFor(b => b.EvidenceMediaIds)
                .Must(HasValidOptionalEvidenceList)
                .WithMessage("EvidenceMediaIds must contain unique non-empty IDs.");
        });
    }

    private static bool HasValidEvidenceList(IReadOnlyList<Guid>? mediaIds) =>
        mediaIds is { Count: > 0 and <= 128 } &&
        !mediaIds.Contains(Guid.Empty) &&
        mediaIds.Distinct().Count() == mediaIds.Count;

    private static bool HasValidOptionalEvidenceList(IReadOnlyList<Guid>? mediaIds) =>
        mediaIds is null ||
        (mediaIds.Count <= 128 &&
         !mediaIds.Contains(Guid.Empty) &&
         mediaIds.Distinct().Count() == mediaIds.Count);
}
