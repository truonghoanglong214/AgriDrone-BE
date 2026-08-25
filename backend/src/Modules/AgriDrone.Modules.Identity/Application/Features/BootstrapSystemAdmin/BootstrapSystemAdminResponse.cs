namespace AgriDrone.Modules.Identity.Application.Features.BootstrapSystemAdmin;

internal sealed record BootstrapSystemAdminResponse(
    bool Created,
    Guid? UserId,
    string Email);
