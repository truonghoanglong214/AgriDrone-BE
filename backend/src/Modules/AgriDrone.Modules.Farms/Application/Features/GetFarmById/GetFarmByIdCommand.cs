using AgriDrone.SharedKernel.Application;
using MediatR;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.GetFarmById
{
    public sealed record GetFarmByIdCommand(
        Guid FarmId) : IRequest<Result<GetFarmByIdResponse>>;
}
