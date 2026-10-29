using AgriDrone.SharedKernel.Application;
using AgriDrone.Modules.Surveys.Domain;
using MediatR;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.VerifySurveyOrderBoundaryAndScope;

public sealed record VerifySurveyOrderBoundaryAndScopeCommand(
    Guid SurveyOrderId,
    Polygon Boundary,
    string Reason,
    string? EvidenceReference,
    uint ExpectedOrderVersion,
    int? ExpectedCurrentBoundaryVersionNumber,
    SurveyScopeType ScopeType = SurveyScopeType.FarmWide,
    IReadOnlyList<Guid>? ZoneIds = null)
    : IRequest<Result<VerifySurveyOrderBoundaryAndScopeResponse>>;
