using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers.GetSystemManagerProfile;

public sealed record GetSystemManagerProfileQuery(Guid ProfileId)
    : IRequest<Result<SystemManagerProfileResponse>>;
