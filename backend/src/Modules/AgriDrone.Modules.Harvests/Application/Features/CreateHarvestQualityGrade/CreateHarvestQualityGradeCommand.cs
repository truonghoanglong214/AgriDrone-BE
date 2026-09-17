using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Harvests.Application.Features.CreateHarvestQualityGrade
{
    public sealed record CreateHarvestQualityGradeCommand(
    string Code,
    string Name,
    int DisplayOrder)
    : IRequest<Result<HarvestQualityGradeResponse>>;
}
