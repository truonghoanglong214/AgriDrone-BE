using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FullName,
    string? Phone,
    string TenantCode,
    string TenantName) : IRequest<Result<RegisterUserResponse>>;
