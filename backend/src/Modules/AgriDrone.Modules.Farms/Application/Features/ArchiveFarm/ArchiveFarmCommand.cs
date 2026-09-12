using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.ArchiveFarm;

public sealed record ArchiveFarmCommand(
    Guid FarmId,
    long ExpectedVersion) : IRequest<Result>;
