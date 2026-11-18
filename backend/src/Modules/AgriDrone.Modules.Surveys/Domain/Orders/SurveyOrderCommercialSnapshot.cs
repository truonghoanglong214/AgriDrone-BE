using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyOrderCommercialSnapshot
{
    private SurveyOrderCommercialSnapshot() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid BusinessPolicyVersionId { get; private set; }
    public string PolicyFingerprint { get; private set; } = null!;
    public Guid SurveyServiceRevisionId { get; private set; }
    public int SurveyServiceRevisionVersion { get; private set; }
    public string ServiceCode { get; private set; } = null!;
    public bool MapsPoles { get; private set; }
    public bool ScreensDisease { get; private set; }
    public Guid PriceVersionId { get; private set; }
    public int PriceVersionNumber { get; private set; }
    public decimal PricePerPole { get; private set; }
    public string Currency { get; private set; } = null!;
    public Guid DepositPolicyId { get; private set; }
    public int DepositVersionNumber { get; private set; }
    public decimal DepositAmount { get; private set; }
    public Guid ClaimPolicyId { get; private set; }
    public int ClaimPolicyVersionNumber { get; private set; }
    public int ClaimWindowDays { get; private set; }
    public string ClaimTimeZoneId { get; private set; } = null!;
    public bool ExcludeNoticeDate { get; private set; }
    public Guid CancellationPolicyId { get; private set; }
    public int CancellationPolicyVersionNumber { get; private set; }
    public bool RefundableBeforeCapture { get; private set; }
    public bool RetainDepositAfterCapture { get; private set; }
    public int AllowedFreeNoShowReschedules { get; private set; }
    public DateTimeOffset EvaluatedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SurveyOrderCommercialSnapshot Create(
        Guid surveyOrderId,
        ResolvedSurveyCommercialSnapshot snapshot,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        ArgumentNullException.ThrowIfNull(snapshot);
        DomainGuard.NotEmpty(snapshot.BusinessPolicyVersionId, nameof(snapshot.BusinessPolicyVersionId));
        DomainGuard.NotEmpty(snapshot.SurveyServiceRevisionId, nameof(snapshot.SurveyServiceRevisionId));
        DomainGuard.NotEmpty(snapshot.PriceVersionId, nameof(snapshot.PriceVersionId));
        DomainGuard.NotEmpty(snapshot.DepositPolicyId, nameof(snapshot.DepositPolicyId));
        DomainGuard.NotEmpty(snapshot.ClaimPolicyId, nameof(snapshot.ClaimPolicyId));
        DomainGuard.NotEmpty(snapshot.CancellationPolicyId, nameof(snapshot.CancellationPolicyId));
        DomainGuard.Utc(snapshot.EvaluatedAt, nameof(snapshot.EvaluatedAt));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var validatedPrice = PolicyValidationGuard.ValidatePositiveWholeVnd(snapshot.PricePerPole, nameof(snapshot.PricePerPole));
        var validatedDeposit = PolicyValidationGuard.ValidatePositiveWholeVnd(snapshot.DepositAmount, nameof(snapshot.DepositAmount));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(CurrencyCode.Create(snapshot.Currency), nameof(snapshot.Currency));
        var normalizedFingerprint = PolicyValidationGuard.NormalizeRequired(snapshot.PolicyFingerprint, 64, nameof(snapshot.PolicyFingerprint));
        var normalizedCode = PolicyValidationGuard.NormalizeRequired(snapshot.ServiceCode, 32, nameof(snapshot.ServiceCode));
        var normalizedTz = PolicyValidationGuard.NormalizeRequired(snapshot.ClaimTimeZoneId, 64, nameof(snapshot.ClaimTimeZoneId));

        if (snapshot.ClaimWindowDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Claim window days must be at least 1.");
        }

        if (snapshot.AllowedFreeNoShowReschedules < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Allowed free reschedules cannot be negative.");
        }

        return new SurveyOrderCommercialSnapshot
        {
            SurveyOrderId = surveyOrderId,
            BusinessPolicyVersionId = snapshot.BusinessPolicyVersionId,
            PolicyFingerprint = normalizedFingerprint,
            SurveyServiceRevisionId = snapshot.SurveyServiceRevisionId,
            SurveyServiceRevisionVersion = snapshot.SurveyServiceRevisionVersion,
            ServiceCode = normalizedCode,
            MapsPoles = snapshot.MapsPoles,
            ScreensDisease = snapshot.ScreensDisease,
            PriceVersionId = snapshot.PriceVersionId,
            PriceVersionNumber = snapshot.PriceVersionNumber,
            PricePerPole = validatedPrice,
            Currency = validatedCurrency,
            DepositPolicyId = snapshot.DepositPolicyId,
            DepositVersionNumber = snapshot.DepositVersionNumber,
            DepositAmount = validatedDeposit,
            ClaimPolicyId = snapshot.ClaimPolicyId,
            ClaimPolicyVersionNumber = snapshot.ClaimPolicyVersionNumber,
            ClaimWindowDays = snapshot.ClaimWindowDays,
            ClaimTimeZoneId = normalizedTz,
            ExcludeNoticeDate = snapshot.ExcludeNoticeDate,
            CancellationPolicyId = snapshot.CancellationPolicyId,
            CancellationPolicyVersionNumber = snapshot.CancellationPolicyVersionNumber,
            RefundableBeforeCapture = snapshot.RefundableBeforeCapture,
            RetainDepositAfterCapture = snapshot.RetainDepositAfterCapture,
            AllowedFreeNoShowReschedules = snapshot.AllowedFreeNoShowReschedules,
            EvaluatedAt = snapshot.EvaluatedAt,
            CreatedAt = createdAt
        };
    }
}
