using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

public sealed record DownloadMissionTreesCsvResult(byte[] Bytes, string FileName);

public sealed record DownloadMissionTreesCsvQuery(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId) : IRequest<Result<DownloadMissionTreesCsvResult>>;
