using AgriDrone.Modules.Identity.Application.Contracts.Authentication;

namespace AgriDrone.Modules.Identity.Application.Abstractions.Services;

public interface ITenantSelectionTokenService
{
    TenantSelectionTokenResult Generate(Guid userId);

    Guid? Validate(string token);
}
