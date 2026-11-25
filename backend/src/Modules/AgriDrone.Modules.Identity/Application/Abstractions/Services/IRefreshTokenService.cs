namespace AgriDrone.Modules.Identity.Application.Abstractions.Services;

public sealed record GeneratedRefreshToken(string RawToken, string TokenHash);

public interface IRefreshTokenService
{
    GeneratedRefreshToken Generate();

    bool TryHash(string? rawToken, out string tokenHash);
}
