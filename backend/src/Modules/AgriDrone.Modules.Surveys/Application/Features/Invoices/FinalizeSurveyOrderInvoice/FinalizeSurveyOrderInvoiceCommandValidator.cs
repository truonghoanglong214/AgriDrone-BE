using System.Text.RegularExpressions;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.FinalizeSurveyOrderInvoice;

internal sealed partial class FinalizeSurveyOrderInvoiceCommandValidator
    : AbstractValidator<FinalizeSurveyOrderInvoiceCommand>
{
    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Hex64Pattern();

    public FinalizeSurveyOrderInvoiceCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.OperationId).NotEmpty();
        RuleFor(c => c.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(c => c.ExpectedApprovedResultRevisionId).NotEmpty();
        RuleFor(c => c.ExpectedApprovedResultFingerprint)
            .NotEmpty()
            .Must(v => !string.IsNullOrWhiteSpace(v) && Hex64Pattern().IsMatch(v.Trim()))
            .WithMessage("ExpectedApprovedResultFingerprint must be a 64-character hexadecimal SHA-256 string.");
        RuleFor(c => c.ExpectedCountSnapshotId).NotEmpty();
        RuleFor(c => c.ExpectedCountFingerprint)
            .NotEmpty()
            .Must(v => !string.IsNullOrWhiteSpace(v) && Hex64Pattern().IsMatch(v.Trim()))
            .WithMessage("ExpectedCountFingerprint must be a 64-character hexadecimal SHA-256 string.");

        RuleFor(c => c.FinalizationReason)
            .NotEmpty()
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .MaximumLength(500);

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

    private static bool HasConsistentZeroPoleSet(FinalizeSurveyOrderInvoiceCommand command)
    {
        var anySet = command.ZeroPoleReasonCode is not null ||
                     command.ZeroPoleEvidenceReference is not null;

        var allSet = !string.IsNullOrWhiteSpace(command.ZeroPoleReasonCode) &&
                     !string.IsNullOrWhiteSpace(command.ZeroPoleEvidenceReference);

        return !anySet || allSet;
    }
}
