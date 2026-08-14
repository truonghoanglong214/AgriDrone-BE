using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.RegisterUser
{
    public sealed record RegisterUserResponse(
        Guid Id,
        string Email,
        string FullName,
        string? Phone,
        string TenantCode,
        string TenantName,
        DateTimeOffset CreatedAt);
}
