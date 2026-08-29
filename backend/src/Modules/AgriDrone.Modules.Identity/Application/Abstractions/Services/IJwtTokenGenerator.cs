using AgriDrone.Modules.Identity.Application.Contracts.Authentication;

namespace AgriDrone.Modules.Identity.Application.Abstractions.Services
{
    public interface IJwtTokenGenerator
    {
        AccessTokenResult GenerateAccessToken(
            AccessTokenRequest request);
    }
}
