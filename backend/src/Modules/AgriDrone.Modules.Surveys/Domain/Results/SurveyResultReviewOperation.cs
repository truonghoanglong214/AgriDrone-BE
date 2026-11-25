using System.Text.Json;
using System.Text.RegularExpressions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyResultReviewOperation : Entity
{
    public const string ActionRecordComboReview = "RecordComboReview";
    public const string ActionFreezeInternalResult = "FreezeInternalResult";
    public const string ActionFinalizeInvoice = "FinalizeInvoice";
    public const string ActionCorrectPreReleaseInvoice = "CorrectPreReleaseInvoice";
    public const string ActionBeginBalanceCheckout = "BeginBalanceCheckout";
    public const string ActionCompleteBalanceCheckout = "CompleteBalanceCheckout";
    public const string ActionBalanceCheckoutUncertain = "BalanceCheckoutUncertain";
    public const string ApiVersionV3 = "v3";

    private static readonly Regex Hex64Regex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    private SurveyResultReviewOperation() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Action { get; private set; } = null!;
    public string ApiVersion { get; private set; } = null!;
    public Guid OperationId { get; private set; }
    public string RequestFingerprint { get; private set; } = null!;
    public Guid? ComboBatchId { get; private set; }
    public Guid? ReviewRevisionId { get; private set; }
    public Guid? ApprovedResultRevisionId { get; private set; }
    public Guid? InvoiceRevisionId { get; private set; }
    public Guid? CaptureAttemptId { get; private set; }
    public Guid? PoleCountSnapshotId { get; private set; }
    public JsonDocument Outcome { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<SurveyResultReviewOperation> Create(
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        Guid actorId,
        string action,
        string apiVersion,
        Guid operationId,
        string requestFingerprint,
        JsonDocument outcome,
        DateTimeOffset createdAt,
        Guid? comboBatchId = null,
        Guid? reviewRevisionId = null,
        Guid? approvedResultRevisionId = null,
        Guid? captureAttemptId = null,
        Guid? poleCountSnapshotId = null,
        Guid? invoiceRevisionId = null)
    {
        if (tenantId == Guid.Empty) return Result.Failure<SurveyResultReviewOperation>(AppError.Validation("TenantId.Required", "Tenant identifier is required."));
        if (farmId == Guid.Empty) return Result.Failure<SurveyResultReviewOperation>(AppError.Validation("FarmId.Required", "Farm identifier is required."));
        if (surveyOrderId == Guid.Empty) return Result.Failure<SurveyResultReviewOperation>(AppError.Validation("SurveyOrderId.Required", "Survey order identifier is required."));
        if (actorId == Guid.Empty) return Result.Failure<SurveyResultReviewOperation>(AppError.Validation("ActorId.Required", "Actor identifier is required."));
        if (operationId == Guid.Empty) return Result.Failure<SurveyResultReviewOperation>(AppError.Validation("OperationId.Required", "Operation identifier is required."));

        var normalizedAction = action?.Trim() ?? string.Empty;
        if (normalizedAction is not (
            ActionRecordComboReview or
            ActionFreezeInternalResult or
            ActionFinalizeInvoice or
            ActionCorrectPreReleaseInvoice or
            ActionBeginBalanceCheckout or
            ActionCompleteBalanceCheckout or
            ActionBalanceCheckoutUncertain))
            return Result.Failure<SurveyResultReviewOperation>(AppError.Validation("Action.Invalid", $"Unsupported review action '{action}'."));

        var normalizedApiVersion = apiVersion?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedApiVersion) || normalizedApiVersion.Length > 16)
            return Result.Failure<SurveyResultReviewOperation>(AppError.Validation("ApiVersion.Invalid", "API version must be between 1 and 16 characters."));

        var normalizedFingerprint = requestFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Hex64Regex.IsMatch(normalizedFingerprint))
            return Result.Failure<SurveyResultReviewOperation>(AppError.Validation("RequestFingerprint.Invalid", "Request fingerprint must be a 64-character lowercase hex string."));

        if (outcome is null)
            return Result.Failure<SurveyResultReviewOperation>(AppError.Validation("Outcome.Required", "Operation outcome payload is required."));

        if (createdAt == default || createdAt.Offset != TimeSpan.Zero)
            return Result.Failure<SurveyResultReviewOperation>(AppError.Validation("CreatedAt.Invalid", "CreatedAt must be a valid UTC timestamp."));

        return Result.Success(new SurveyResultReviewOperation
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            FarmId = farmId,
            SurveyOrderId = surveyOrderId,
            ActorId = actorId,
            Action = normalizedAction,
            ApiVersion = normalizedApiVersion,
            OperationId = operationId,
            RequestFingerprint = normalizedFingerprint,
            ComboBatchId = comboBatchId,
            ReviewRevisionId = reviewRevisionId,
            ApprovedResultRevisionId = approvedResultRevisionId,
            InvoiceRevisionId = invoiceRevisionId,
            CaptureAttemptId = captureAttemptId,
            PoleCountSnapshotId = poleCountSnapshotId,
            Outcome = JsonDocument.Parse(outcome.RootElement.GetRawText()),
            CreatedAt = SurveyInternalApprovedResultRevision.NormalizeUtcToMicroseconds(createdAt)
        });
    }
}
