using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.GetMissionDetails;

internal sealed class GetMissionDetailsQueryHandler(
    IMissionQueries missionQueries)
    : IRequestHandler<
        GetMissionDetailsQuery,
        Result<MissionResponse>>
{
    public async Task<Result<MissionResponse>> Handle(
        GetMissionDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var mission = await missionQueries.GetByIdAsync(
            request.TenantId,
            request.FarmId,
            request.MissionId,
            cancellationToken);

        return mission is null
            ? Result.Failure<MissionResponse>(
                MissionError.NotFound(request.MissionId))
            : Result.Success(mission);
    }
}
