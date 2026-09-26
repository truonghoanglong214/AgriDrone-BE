using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record UpdateSystemManagerQualificationCommand(
    Guid ProfileId,
    FlightQualificationStatus Status,
    DateTimeOffset? ExpiresAt,
    string Reason,
    long ExpectedVersion) : IRequest<Result<SystemManagerProfileResponse>>;
