using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Abstractions
{
    public interface IJwtTokenGenerator
    {
        string GenerateAccessToken(
            Guid userId,
            string email,
            IEnumerable<string> roles);
    }
}
