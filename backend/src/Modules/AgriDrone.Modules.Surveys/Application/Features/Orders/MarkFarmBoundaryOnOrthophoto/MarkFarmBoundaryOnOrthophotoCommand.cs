using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.MarkFarmBoundaryOnOrthophoto;

public sealed record MarkFarmBoundaryOnOrthophotoCommand(
    Guid SurveyOrderId,
    IReadOnlyList<CornerPointDto> Corners,
    uint ExpectedOrderVersion,
    string? Reason = null,
    string? EvidenceReference = null,
    Guid? CaptureAttemptId = null,
    uint? ExpectedMissionVersion = null,
    int? ExpectedBoundaryVersionNumber = null)
    : IRequest<Result<MarkFarmBoundaryOnOrthophotoResponse>>;
