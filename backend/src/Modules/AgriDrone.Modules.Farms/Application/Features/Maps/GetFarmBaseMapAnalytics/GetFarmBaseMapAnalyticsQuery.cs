using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapAnalytics;

public sealed record GetFarmBaseMapAnalyticsQuery(
    Guid FarmId)
    : IRequest<Result<FarmBaseMapAnalyticsResponse>>;

