using AgriDrone.Modules.Identity.Domain.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetUsers
{
    public sealed record UserListItemResponse(Guid Id,
    string Email,
    string FullName,
    string? Phone,
    UserStatus Status,
    DateTimeOffset CreatedAt);
}
