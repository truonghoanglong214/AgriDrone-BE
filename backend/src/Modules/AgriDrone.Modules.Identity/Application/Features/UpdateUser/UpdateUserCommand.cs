using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateUser
{
    public sealed record UpdateUserCommand(
        string fullName,
        string phone) : IRequest<Result<UpdateUserResponse>>; 
}
