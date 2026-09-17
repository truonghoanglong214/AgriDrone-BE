using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Harvests.Application.Features.RetireHarvestQualityGrade;

public sealed record RetireHarvestQualityGradeCommand(
    Guid GradeId,
    long ExpectedVersion) : IRequest<Result>;
