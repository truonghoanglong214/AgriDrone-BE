using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record InviteSystemManagerCommand(
    string Email)
    : IRequest<Result<InviteSystemManagerResponse>>;