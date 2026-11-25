using System.Text.RegularExpressions;
using AgriDrone.Modules.Surveys.Domain;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.CorrectSurveyOrderPreReleaseInvoice;

internal sealed partial class CorrectSurveyOrderPreReleaseInvoiceCommandValidator
    : AbstractValidator<CorrectSurveyOrderPreReleaseInvoiceCommand>
{
    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Hex64Pattern();

    public CorrectSurveyOrderPreReleaseInvoiceCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.OperationId).NotEmpty();
        RuleFor(c => c.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(c => c.ExpectedCurrentApprovedResultRevisionId).NotEmpty();
        RuleFor(c => c.ExpectedCurrentApprovedResultFingerprint)
            .NotEmpty()
            .Must(v => !string.IsNullOrWhiteSpace(v) && Hex64Pattern().IsMatch(v.Trim()))
            .WithMessage("ExpectedCurrentApprovedResultFingerprint must be a 64-character hexadecimal SHA-256 string.");
        RuleFor(c => c.ExpectedCurrentInvoiceRevisionId).NotEmpty();
        RuleFor(c => c.ExpectedCurrentInvoiceFingerprint)
            .NotEmpty()
            .Must(v => !string.IsNullOrWhiteSpace(v) && Hex64Pattern().IsMatch(v.Trim()))
            .WithMessage("ExpectedCurrentInvoiceFingerprint must be a 64-character hexadecimal SHA-256 string.");
        RuleFor(c => c.ExpectedCurrentCountSnapshotId).NotEmpty();
        RuleFor(c => c.ExpectedCurrentCountFingerprint)
            .NotEmpty()
            .Must(v => !string.IsNullOrWhiteSpace(v) && Hex64Pattern().IsMatch(v.Trim()))
            .WithMessage("ExpectedCurrentCountFingerprint must be a 64-character hexadecimal SHA-256 string.");

        RuleFor(c => c.ReviewDecision)
            .NotEmpty()
            .Must(d => SurveyInternalApprovedResultRevision.CanonicalizeReviewDecision(d) is
                SurveyInternalApprovedResultRevision.DecisionApproved or
                SurveyInternalApprovedResultRevision.DecisionApprovedWithObservations or
                SurveyInternalApprovedResultRevision.DecisionInternalApproved)
            .WithMessage("ReviewDecision must be Approved, ApprovedWithObservations, or InternalApproved.");

        RuleFor(c => c.CorrectionReason)
            .NotEmpty()
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .MaximumLength(500);

        RuleFor(c => c.ItemCorrections)
            .NotNull()
            .Must(items => items is { Count: > 0 and <= 10_000 })
            .WithMessage("ItemCorrections must contain between 1 and 10,000 items.")
            .Must(items => items is null || items.Select(i => i.MappingCandidateId).Distinct().Count() == items.Count)
            .WithMessage("ItemCorrections cannot contain duplicate MappingCandidateId values.");

        RuleForEach(c => c.ItemCorrections).ChildRules(item =>
        {
            item.RuleFor(i => i.MappingCandidateId).NotEmpty();
            item.RuleFor(i => i.ExpectedPlantProfileId)
                .Must(id => !id.HasValue || id.Value != Guid.Empty)
                .WithMessage("ExpectedPlantProfileId cannot be empty when supplied.");
            item.RuleFor(i => i)
                .Must(i => i.IsAccepted ? string.IsNullOrWhiteSpace(i.ExclusionReason) : !string.IsNullOrWhiteSpace(i.ExclusionReason))
                .WithMessage("Excluded items require ExclusionReason; accepted items must omit ExclusionReason.");
            item.RuleFor(i => i.ExclusionReason)
                .MaximumLength(500);
        });

        RuleFor(c => c.ZeroPoleReasonCode)
            .MaximumLength(64)
            .Must(v => v is null || !string.IsNullOrWhiteSpace(v))
            .WithMessage("ZeroPoleReasonCode cannot be whitespace when supplied.");
        RuleFor(c => c.ZeroPoleEvidenceReference)
            .MaximumLength(500)
            .Must(v => v is null || !string.IsNullOrWhiteSpace(v))
            .WithMessage("ZeroPoleEvidenceReference cannot be whitespace when supplied.");

        RuleFor(c => c)
            .Must(HasConsistentZeroPoleSet)
            .WithMessage("ZeroPoleReasonCode and ZeroPoleEvidenceReference must either both be supplied together or both omitted.");
    }

    private static bool HasConsistentZeroPoleSet(CorrectSurveyOrderPreReleaseInvoiceCommand command)
    {
        var anySet = command.ZeroPoleReasonCode is not null ||
                     command.ZeroPoleEvidenceReference is not null;

        var allSet = !string.IsNullOrWhiteSpace(command.ZeroPoleReasonCode) &&
                     !string.IsNullOrWhiteSpace(command.ZeroPoleEvidenceReference);

        return !anySet || allSet;
    }
}
