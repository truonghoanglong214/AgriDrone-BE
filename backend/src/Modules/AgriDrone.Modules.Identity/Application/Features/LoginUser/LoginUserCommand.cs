using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.LoginUser
{
    public sealed record LoginUserCommand(string email, string password) : IRequest<Result<LoginUserResponse>>;
}
