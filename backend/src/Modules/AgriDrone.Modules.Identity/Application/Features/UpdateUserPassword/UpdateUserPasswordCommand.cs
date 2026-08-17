using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateUserPassword
{
    public sealed record UpdateUserPasswordCommand(
        string newPassword, 
        string oldPassword) : IRequest<Result<UpdateUserPasswordResponse>>;
}
