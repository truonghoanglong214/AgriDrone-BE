namespace AgriDrone.Modules.Identity.Application.Abstractions;

public sealed record TenantSelectionTokenResult(
    string Token,
    DateTimeOffset ExpiresAt);

public interface ITenantSelectionTokenService
{
    TenantSelectionTokenResult Generate(Guid userId);

    Guid? Validate(string token);
}
