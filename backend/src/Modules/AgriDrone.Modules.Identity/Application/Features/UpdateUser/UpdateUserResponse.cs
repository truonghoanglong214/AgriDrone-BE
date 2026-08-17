using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateUser
{
    public sealed record UpdateUserResponse(
        string fullName,
        string? phone,
        DateTimeOffset updateAt);
}
