using System.ComponentModel.DataAnnotations;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record RequestScopeAdjustmentApiRequest(
    [Required] ScopeAdjustmentReason Reason,
    Guid? BeforeBoundaryVersionId,
    Guid? AfterBoundaryVersionId,
    [Required] int BeforePoleCount,
    [Required] int AfterPoleCount,
    [Required] decimal FinancialImpact,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    [Required, StringLength(1000)] string EvidenceReference,
    [Required] uint? ExpectedOrderVersion);

public sealed record ReviewScopeAdjustmentApiRequest(
    [Required] bool Approved,
    [Required, StringLength(2000)] string ReviewReason,
    [Required] uint? ExpectedAdjustmentVersion);

public sealed record ApplyScopeAdjustmentApiRequest(
    [Required] uint? ExpectedAdjustmentVersion);

public sealed record ScopeAdjustmentApiResponse(
    Guid Id,
    Guid SurveyOrderId,
    string Reason,
    Guid? BeforeBoundaryVersionId,
    Guid? AfterBoundaryVersionId,
    int BeforePoleCount,
    int AfterPoleCount,
    decimal FinancialImpact,
    string Currency,
    string EvidenceReference,
    string Status,
    Guid RequestedBy,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewReason,
    uint Version,
    DateTimeOffset CreatedAt);

public sealed record CreateAdjustmentSettlementApiRequest(
    [Required] Guid PriceAdjustmentId,
    [Required] AdjustmentSettlementType Type,
    [Required] decimal Amount,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    [Required, StringLength(200)] string IdempotencyKey);

public sealed record ConfirmAdjustmentSettlementApiRequest(
    string? ProviderReference,
    [Required] uint? ExpectedVersion);

public sealed record FailAdjustmentSettlementApiRequest(
    [Required, StringLength(2000)] string FailureReason,
    [Required] uint? ExpectedVersion);

public sealed record AdjustmentSettlementApiResponse(
    Guid Id,
    Guid PriceAdjustmentId,
    string Type,
    string Status,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string? ProviderReference,
    string? FailureReason,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RequestExtraFlightApiRequest(
    [Required] Guid FarmId,
    Guid? SourceMissionId,
    [Required] ExtraFlightReason Reason,
    [Required, StringLength(1000)] string EvidenceReference,
    [Required] decimal CostImpact,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    [Required] uint? ExpectedOrderVersion);

public sealed record ReviewExtraFlightApiRequest(
    [Required] bool Approved,
    [Required] uint? ExpectedVersion);

public sealed record ConsumeExtraFlightApiRequest(
    [Required] Guid ExtraMissionId,
    [Required] uint? ExpectedVersion);

public sealed record ExtraFlightApiResponse(
    Guid Id,
    Guid SurveyOrderId,
    Guid FarmId,
    Guid? SourceMissionId,
    Guid? ExtraMissionId,
    string Reason,
    string EvidenceReference,
    decimal CostImpact,
    string Currency,
    string Status,
    Guid RequestedBy,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    uint Version,
    DateTimeOffset CreatedAt);
