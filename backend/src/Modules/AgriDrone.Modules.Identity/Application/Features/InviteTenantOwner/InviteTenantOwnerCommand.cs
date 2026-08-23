using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantOwner
{
    public sealed record InviteTenantOwnerCommand(
        string email) : IRequest<Result<InviteTenantOwnerResponse>>;
}
