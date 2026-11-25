using AgriDrone.Modules.Identity.Application.Contracts.Authentication;

namespace AgriDrone.Modules.Identity.Application.Abstractions.Services;

public sealed record TenantSelectionValidationResult(
    Guid UserId,
    int AuthenticationVersion,
    DateTimeOffset ExpiresAt);

public interface ITenantSelectionTokenService
{
    TenantSelectionTokenResult Generate(Guid userId, int authenticationVersion);

    TenantSelectionValidationResult? Validate(string token);
}
