using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.BootstrapSystemAdmin;

internal sealed record BootstrapSystemAdminCommand(
    string Email,
    string FullName)
    : IRequest<Result<BootstrapSystemAdminResponse>>;
