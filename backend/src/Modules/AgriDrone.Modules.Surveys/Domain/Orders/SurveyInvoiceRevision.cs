using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyInvoiceRevision : Entity
{
    public const string StatusCurrent = "CURRENT";
    public const string StatusSuperseded = "SUPERSEDED";
    public const decimal MaxPersistableAmount = 9_999_999_999_999_999m;

    private static readonly Regex Hex64Regex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    public static DateTimeOffset NormalizeUtcToMicroseconds(DateTimeOffset dt)
    {
        var utc = dt.ToUniversalTime();
        var microsecondTicks = (utc.Ticks / 10L) * 10L;
        return new DateTimeOffset(microsecondTicks, TimeSpan.Zero);
    }

    private SurveyInvoiceRevision() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public int RevisionNumber { get; private set; }
    public Guid? PredecessorRevisionId { get; private set; }
    public Guid ApprovedResultRevisionId { get; private set; }
    public Guid PoleCountSnapshotId { get; private set; }
    public int VerifiedBillablePoleCount { get; private set; }
    public Guid SurveyServicePriceId { get; private set; }
    public decimal PricePerPole { get; private set; }
    public int? PlantHealthPoleCount { get; private set; }
    public decimal? MappingPricePerPole { get; private set; }
    public decimal? PlantHealthPricePerPole { get; private set; }
    public Polygon? PlantHealthSurveyArea { get; private set; }
    public string Currency { get; private set; } = "VND";
    public Guid CommercialPolicyVersionId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal ValidAllocatedDepositNet { get; private set; }
    public decimal DueAmount { get; private set; }
    public decimal OverpaymentAmount { get; private set; }
    public string EvidenceFingerprint { get; private set; } = null!;
    public Guid FinalizedBy { get; private set; }
    public DateTimeOffset FinalizedAt { get; private set; }
    public string FinalizationReason { get; private set; } = null!;
    public string Status { get; private set; } = StatusCurrent;
    public Guid? ZeroPoleApprovedBy { get; private set; }
    public string? ZeroPoleReasonCode { get; private set; }
    public string? ZeroPoleEvidenceReference { get; private set; }
    public Guid? SupersededBy { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public string? SupersededReason { get; private set; }
    public Guid? SuccessorRevisionId { get; private set; }

    public static string ComputeEvidenceFingerprint(
        Guid tenantId,
        Guid surveyOrderId,
        Guid farmId,
        int revisionNumber,
        Guid approvedResultRevisionId,
        Guid poleCountSnapshotId,
        int verifiedBillablePoleCount,
        Guid surveyServicePriceId,
        decimal pricePerPole,
        string currency,
        Guid commercialPolicyVersionId,
        decimal totalAmount,
        decimal validAllocatedDepositNet,
        decimal dueAmount,
        decimal overpaymentAmount,
        Guid finalizedBy,
        DateTimeOffset finalizedAt,
        string finalizationReason,
        Guid? predecessorRevisionId,
        Guid? zeroPoleApprovedBy,
        string? zeroPoleReasonCode,
        string? zeroPoleEvidenceReference,
        ComboInvoicePricing? comboPricing = null)
    {
        var normalizedFinalizedAt = NormalizeUtcToMicroseconds(finalizedAt);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("tenant_id", tenantId.ToString("D"));
            writer.WriteString("survey_order_id", surveyOrderId.ToString("D"));
            writer.WriteString("farm_id", farmId.ToString("D"));
            writer.WriteNumber("revision_number", revisionNumber);
            if (predecessorRevisionId.HasValue)
                writer.WriteString("predecessor_revision_id", predecessorRevisionId.Value.ToString("D"));
            else
                writer.WriteNull("predecessor_revision_id");
            writer.WriteString("approved_result_revision_id", approvedResultRevisionId.ToString("D"));
            writer.WriteString("pole_count_snapshot_id", poleCountSnapshotId.ToString("D"));
            writer.WriteNumber("verified_billable_pole_count", verifiedBillablePoleCount);
            writer.WriteString("survey_service_price_id", surveyServicePriceId.ToString("D"));
            writer.WriteNumber("price_per_pole", (long)decimal.Truncate(pricePerPole));
            if (comboPricing is not null)
            {
                writer.WriteNumber("plant_health_pole_count", comboPricing.PlantHealthPoleCount);
                writer.WriteNumber("mapping_price_per_pole", (long)comboPricing.MappingPricePerPole);
                writer.WriteNumber("plant_health_price_per_pole", (long)comboPricing.PlantHealthPricePerPole);
                if (comboPricing.PlantHealthSurveyArea is { } area)
                    writer.WriteBase64String("plant_health_survey_area", new WKBWriter().Write(area));
            }
            writer.WriteString("currency", "VND");
            writer.WriteString("commercial_policy_version_id", commercialPolicyVersionId.ToString("D"));
            writer.WriteNumber("total_amount", (long)decimal.Truncate(totalAmount));
            writer.WriteNumber("valid_allocated_deposit_net", (long)decimal.Truncate(validAllocatedDepositNet));
            writer.WriteNumber("due_amount", (long)decimal.Truncate(dueAmount));
            writer.WriteNumber("overpayment_amount", (long)decimal.Truncate(overpaymentAmount));
            writer.WriteString("finalized_by", finalizedBy.ToString("D"));
            writer.WriteString("finalized_at", normalizedFinalizedAt.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture));
            writer.WriteString("finalization_reason", finalizationReason?.Trim() ?? string.Empty);
            if (zeroPoleApprovedBy.HasValue)
                writer.WriteString("zero_pole_approved_by", zeroPoleApprovedBy.Value.ToString("D"));
            else
                writer.WriteNull("zero_pole_approved_by");
            if (!string.IsNullOrWhiteSpace(zeroPoleReasonCode))
                writer.WriteString("zero_pole_reason_code", zeroPoleReasonCode.Trim());
            else
                writer.WriteNull("zero_pole_reason_code");
            if (!string.IsNullOrWhiteSpace(zeroPoleEvidenceReference))
                writer.WriteString("zero_pole_evidence_reference", zeroPoleEvidenceReference.Trim());
            else
                writer.WriteNull("zero_pole_evidence_reference");
            writer.WriteEndObject();
        }

        var hash = SHA256.HashData(stream.ToArray());
        return Convert.ToHexStringLower(hash);
    }

    public static Result<SurveyInvoiceRevision> Create(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        int revisionNumber,
        Guid approvedResultRevisionId,
        Guid poleCountSnapshotId,
        int verifiedBillablePoleCount,
        Guid surveyServicePriceId,
        decimal pricePerPole,
        string currency,
        Guid commercialPolicyVersionId,
        decimal validAllocatedDepositNet,
        Guid finalizedBy,
        DateTimeOffset finalizedAt,
        string finalizationReason,
        string evidenceFingerprint,
        Guid? predecessorRevisionId = null,
        Guid? zeroPoleApprovedBy = null,
        string? zeroPoleReasonCode = null,
        string? zeroPoleEvidenceReference = null,
        ComboInvoicePricing? comboPricing = null)
    {
        if (id == Guid.Empty) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("Id.Required", "Identifier is required."));
        if (tenantId == Guid.Empty) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("TenantId.Required", "Tenant identifier is required."));
        if (farmId == Guid.Empty) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("FarmId.Required", "Farm identifier is required."));
        if (surveyOrderId == Guid.Empty) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("SurveyOrderId.Required", "Survey order identifier is required."));
        if (revisionNumber < 1) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("RevisionNumber.Invalid", "Revision number must be >= 1."));

        if (revisionNumber == 1 && predecessorRevisionId.HasValue)
            return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("PredecessorRevisionId.Invalid", "Initial revision cannot have a predecessor."));
        if (revisionNumber > 1 && (!predecessorRevisionId.HasValue || predecessorRevisionId.Value == Guid.Empty || predecessorRevisionId.Value == id))
            return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("PredecessorRevisionId.Required", "Subsequent revision must reference a valid distinct predecessor."));

        if (approvedResultRevisionId == Guid.Empty) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("ApprovedResultRevisionId.Required", "Approved result identifier is required."));
        if (poleCountSnapshotId == Guid.Empty) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("PoleCountSnapshotId.Required", "Pole count snapshot identifier is required."));
        if (verifiedBillablePoleCount < 0) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("VerifiedBillablePoleCount.Invalid", "Verified billable pole count cannot be negative."));

        if (surveyServicePriceId == Guid.Empty) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("SurveyServicePriceId.Required", "Survey service price identifier is required."));
        if (commercialPolicyVersionId == Guid.Empty) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("CommercialPolicyVersionId.Required", "Commercial policy version identifier is required for V3 invoices."));

        // Price per pole must be strictly positive and whole integer VND
        if (pricePerPole <= 0m || pricePerPole != decimal.Truncate(pricePerPole))
            return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("PricePerPole.Invalid", "Price per pole must be a positive whole VND amount."));

        if (pricePerPole > MaxPersistableAmount)
            return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("PricePerPole.Overflow", "Price per pole exceeds maximum persistable currency limit."));

        if (!string.Equals(currency?.Trim(), "VND", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("Currency.Invalid", "Currency must be VND."));

        if (validAllocatedDepositNet < 0m || validAllocatedDepositNet != decimal.Truncate(validAllocatedDepositNet))
            return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("ValidAllocatedDepositNet.Invalid", "Valid allocated deposit net must be a non-negative whole VND amount."));

        if (validAllocatedDepositNet > MaxPersistableAmount)
            return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("ValidAllocatedDepositNet.Overflow", "Valid allocated deposit net exceeds maximum persistable currency limit."));

        var total = CalculateTotal(verifiedBillablePoleCount, pricePerPole, comboPricing);
        if (total.IsFailure)
            return Result.Failure<SurveyInvoiceRevision>(total.Error);

        if (finalizedBy == Guid.Empty) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("FinalizedBy.Required", "Finalizer actor identifier is required."));
        if (finalizedAt == default || finalizedAt.Offset != TimeSpan.Zero) return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("FinalizedAt.Invalid", "FinalizedAt must be a valid UTC timestamp."));

        var normalizedFinalizedAt = NormalizeUtcToMicroseconds(finalizedAt);
        var normalizedFinalizationReason = finalizationReason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedFinalizationReason) || normalizedFinalizationReason.Length > 500)
            return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("FinalizationReason.Invalid", "Finalization reason must be between 1 and 500 characters."));

        var normalizedZeroPoleReason = string.IsNullOrWhiteSpace(zeroPoleReasonCode) ? null : zeroPoleReasonCode.Trim();
        var normalizedZeroPoleEvidence = string.IsNullOrWhiteSpace(zeroPoleEvidenceReference) ? null : zeroPoleEvidenceReference.Trim();

        // Zero-pole exception validation (BR-PRC-03 & ADR 0020)
        if (verifiedBillablePoleCount == 0)
        {
            if (!zeroPoleApprovedBy.HasValue || zeroPoleApprovedBy.Value == Guid.Empty)
                return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("ZeroPoleApprovedBy.Required", "Zero-pole invoice requires authorized approver actor ID."));
            if (string.IsNullOrWhiteSpace(normalizedZeroPoleReason) || normalizedZeroPoleReason.Length > 64)
                return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("ZeroPoleReasonCode.Required", "Zero-pole invoice requires a reason code (1-64 chars)."));
            if (string.IsNullOrWhiteSpace(normalizedZeroPoleEvidence) || normalizedZeroPoleEvidence.Length > 500)
                return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("ZeroPoleEvidenceReference.Required", "Zero-pole invoice requires a technical evidence reference (1-500 chars)."));
        }
        else
        {
            if (zeroPoleApprovedBy.HasValue || normalizedZeroPoleReason is not null || normalizedZeroPoleEvidence is not null)
                return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("ZeroPoleException.Invalid", "Zero-pole exception fields cannot be set when pole count > 0."));
        }

        var totalAmount = total.Value;
        var dueAmount = Math.Max(0m, totalAmount - validAllocatedDepositNet);
        var overpaymentAmount = Math.Max(0m, validAllocatedDepositNet - totalAmount);

        if (string.IsNullOrWhiteSpace(evidenceFingerprint) || !Hex64Regex.IsMatch(evidenceFingerprint))
            return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("EvidenceFingerprint.Invalid", "Evidence fingerprint must be a 64-character lowercase hex string."));

        var expectedFingerprint = ComputeEvidenceFingerprint(
            tenantId, surveyOrderId, farmId, revisionNumber,
            approvedResultRevisionId, poleCountSnapshotId, verifiedBillablePoleCount,
            surveyServicePriceId, pricePerPole, "VND", commercialPolicyVersionId,
            totalAmount, validAllocatedDepositNet, dueAmount, overpaymentAmount,
            finalizedBy, normalizedFinalizedAt, normalizedFinalizationReason, predecessorRevisionId,
            zeroPoleApprovedBy, normalizedZeroPoleReason, normalizedZeroPoleEvidence, comboPricing);

        if (!string.Equals(evidenceFingerprint, expectedFingerprint, StringComparison.Ordinal))
            return Result.Failure<SurveyInvoiceRevision>(AppError.Validation("EvidenceFingerprint.Mismatch", "Evidence fingerprint does not match computed immutable token."));

        return Result.Success(new SurveyInvoiceRevision
        {
            Id = id,
            TenantId = tenantId,
            FarmId = farmId,
            SurveyOrderId = surveyOrderId,
            RevisionNumber = revisionNumber,
            PredecessorRevisionId = predecessorRevisionId,
            ApprovedResultRevisionId = approvedResultRevisionId,
            PoleCountSnapshotId = poleCountSnapshotId,
            VerifiedBillablePoleCount = verifiedBillablePoleCount,
            SurveyServicePriceId = surveyServicePriceId,
            PricePerPole = pricePerPole,
            PlantHealthPoleCount = comboPricing?.PlantHealthPoleCount,
            MappingPricePerPole = comboPricing?.MappingPricePerPole,
            PlantHealthPricePerPole = comboPricing?.PlantHealthPricePerPole,
            PlantHealthSurveyArea = (Polygon?)comboPricing?.PlantHealthSurveyArea?.Copy(),
            Currency = "VND",
            CommercialPolicyVersionId = commercialPolicyVersionId,
            TotalAmount = totalAmount,
            ValidAllocatedDepositNet = validAllocatedDepositNet,
            DueAmount = dueAmount,
            OverpaymentAmount = overpaymentAmount,
            EvidenceFingerprint = evidenceFingerprint,
            FinalizedBy = finalizedBy,
            FinalizedAt = normalizedFinalizedAt,
            FinalizationReason = normalizedFinalizationReason,
            Status = StatusCurrent,
            ZeroPoleApprovedBy = zeroPoleApprovedBy,
            ZeroPoleReasonCode = normalizedZeroPoleReason,
            ZeroPoleEvidenceReference = normalizedZeroPoleEvidence
        });
    }

    public static Result<decimal> CalculateTotal(int mappingPoleCount, decimal pricePerPole, ComboInvoicePricing? comboPricing = null)
    {
        if (mappingPoleCount < 0 || pricePerPole <= 0m || pricePerPole > MaxPersistableAmount ||
            pricePerPole != decimal.Truncate(pricePerPole))
        {
            return Result.Failure<decimal>(AppError.Validation("InvoicePricing.Invalid", "A valid count and whole-VND rate are required."));
        }

        if (comboPricing is { } combo &&
            (combo.PlantHealthPoleCount < 0 || combo.PlantHealthPoleCount > mappingPoleCount ||
             combo.MappingPricePerPole <= 0m || combo.PlantHealthPricePerPole <= 0m ||
             combo.MappingPricePerPole > pricePerPole || combo.PlantHealthPricePerPole > pricePerPole ||
             combo.MappingPricePerPole != decimal.Truncate(combo.MappingPricePerPole) ||
             combo.PlantHealthPricePerPole != decimal.Truncate(combo.PlantHealthPricePerPole) ||
             combo.MappingPricePerPole + combo.PlantHealthPricePerPole != pricePerPole ||
             (combo.PlantHealthSurveyArea is { } area && (area.SRID != 4326 || area.IsEmpty || !area.IsValid))))
        {
            return Result.Failure<decimal>(AppError.Validation("ComboPricing.Invalid", "The survey count and the two pinned rates must match the invoice scope and price."));
        }

        var total = comboPricing is null
            ? mappingPoleCount * pricePerPole
            : mappingPoleCount * comboPricing.MappingPricePerPole + comboPricing.PlantHealthPoleCount * comboPricing.PlantHealthPricePerPole;
        return total <= MaxPersistableAmount
            ? Result.Success(total)
            : Result.Failure<decimal>(AppError.Validation("TotalAmount.Overflow", "Computed total amount exceeds maximum persistable currency limit."));
    }

    public Result Supersede(Guid actorId, DateTimeOffset supersededAt, string reason, Guid successorRevisionId)
    {
        if (actorId == Guid.Empty)
            return Result.Failure(AppError.Validation("ActorId.Required", "Actor identifier is required to supersede invoice revision."));
        if (supersededAt == default || supersededAt.Offset != TimeSpan.Zero)
            return Result.Failure(AppError.Validation("SupersededAt.Invalid", "SupersededAt must be a valid UTC timestamp."));

        var normalizedSupersededAt = NormalizeUtcToMicroseconds(supersededAt);
        if (normalizedSupersededAt < FinalizedAt)
            return Result.Failure(AppError.Validation("SupersededAt.BeforeFinalizedAt", "SupersededAt cannot be earlier than FinalizedAt."));

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 500)
            return Result.Failure(AppError.Validation("Reason.Invalid", "Superseded reason must be between 1 and 500 characters."));

        if (successorRevisionId == Guid.Empty || successorRevisionId == Id)
            return Result.Failure(AppError.Validation("SuccessorRevisionId.Invalid", "Successor revision ID must be a valid distinct identifier."));

        if (Status == StatusSuperseded)
        {
            // Idempotent replay check
            if (SupersededBy == actorId &&
                SupersededAt == normalizedSupersededAt &&
                string.Equals(SupersededReason, normalizedReason, StringComparison.Ordinal) &&
                SuccessorRevisionId == successorRevisionId)
            {
                return Result.Success();
            }

            return Result.Failure(AppError.Conflict(
                "InvoiceRevision.AlreadySuperseded",
                "Invoice revision has already been superseded with different audit metadata."));
        }

        Status = StatusSuperseded;
        SupersededBy = actorId;
        SupersededAt = normalizedSupersededAt;
        SupersededReason = normalizedReason;
        SuccessorRevisionId = successorRevisionId;
        return Result.Success();
    }
}
