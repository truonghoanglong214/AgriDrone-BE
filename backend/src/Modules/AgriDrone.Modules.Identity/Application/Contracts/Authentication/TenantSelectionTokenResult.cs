using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Contracts.Authentication
{
    public sealed record TenantSelectionTokenResult(
    string Token,
    DateTimeOffset ExpiresAt);
}
