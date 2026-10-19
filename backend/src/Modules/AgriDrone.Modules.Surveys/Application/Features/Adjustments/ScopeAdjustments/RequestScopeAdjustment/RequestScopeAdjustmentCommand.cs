using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.RequestScopeAdjustment;

public sealed record RequestScopeAdjustmentCommand(
    Guid SurveyOrderId,
    ScopeAdjustmentReason Reason,
    Guid? BeforeBoundaryVersionId,
    Guid? AfterBoundaryVersionId,
    int BeforePoleCount,
    int AfterPoleCount,
    decimal FinancialImpact,
    string Currency,
    string EvidenceReference,
    uint ExpectedOrderVersion)
    : IRequest<Result<ScopeAdjustmentResponse>>;

public sealed record ScopeAdjustmentResponse(
    Guid Id,
    Guid SurveyOrderId,
    ScopeAdjustmentReason Reason,
    Guid? BeforeBoundaryVersionId,
    Guid? AfterBoundaryVersionId,
    int BeforePoleCount,
    int AfterPoleCount,
    decimal FinancialImpact,
    string Currency,
    string EvidenceReference,
    ScopeAdjustmentStatus Status,
    Guid RequestedBy,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewReason,
    uint Version,
    DateTimeOffset CreatedAt);
