using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.RegisterUser
{
    public sealed record RegisterUserCommand(
        string email, 
        string password, 
        string fullName, 
        string? phone) : IRequest<Result<RegisterUserResponse>>;
}
