using System;
using System.Collections.Generic;
using System.Text;
using AgriDrone.Modules.Identity.Application.Contracts.Authentication;
using AgriDrone.Modules.Identity.Domain.Tenants;

namespace AgriDrone.Modules.Identity.Application.Abstractions
{
    public interface IJwtTokenGenerator
    {
        AccessTokenResult GenerateAccessToken(
            AccessTokenRequest request);
    }
}
