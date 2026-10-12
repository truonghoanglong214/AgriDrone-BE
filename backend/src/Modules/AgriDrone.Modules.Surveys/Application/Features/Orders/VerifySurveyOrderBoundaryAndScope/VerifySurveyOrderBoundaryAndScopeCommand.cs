using AgriDrone.SharedKernel.Application;
using MediatR;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.VerifySurveyOrderBoundaryAndScope;

public sealed record VerifySurveyOrderBoundaryAndScopeCommand(
    Guid SurveyOrderId,
    Polygon Boundary,
    string Reason,
    string? EvidenceReference,
    uint ExpectedOrderVersion,
    int? ExpectedCurrentBoundaryVersionNumber)
    : IRequest<Result<VerifySurveyOrderBoundaryAndScopeResponse>>;
