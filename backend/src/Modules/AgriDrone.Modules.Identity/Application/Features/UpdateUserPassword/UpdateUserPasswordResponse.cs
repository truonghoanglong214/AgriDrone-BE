using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateUserPassword
{
    public sealed record UpdateUserPasswordResponse(string email,string message);
}
