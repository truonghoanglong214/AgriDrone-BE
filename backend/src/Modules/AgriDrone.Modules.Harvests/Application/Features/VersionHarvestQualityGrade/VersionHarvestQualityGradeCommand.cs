using AgriDrone.Modules.Harvests.Application.Features.CreateHarvestQualityGrade;
using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Harvests.Application.Features.VersionHarvestQualityGrade
{
    public sealed record VersionHarvestQualityGradeCommand(
        Guid GradeId,
        string Name,
        int DisplayOrder,
        long ExpectedVersion) : IRequest<Result<HarvestQualityGradeResponse>>;
}
