using AgriDrone.Modules.Identity.Application.Features.LoginUser;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SelectTenant;

public sealed record SelectTenantCommand(
    string SelectionToken,
    Guid TenantId) : IRequest<Result<LoginUserResponse>>;
