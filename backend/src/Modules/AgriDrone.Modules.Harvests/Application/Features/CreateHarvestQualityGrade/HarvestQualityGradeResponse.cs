using AgriDrone.Modules.Harvests.Domain.Quality;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Harvests.Application.Features.CreateHarvestQualityGrade
{
    public sealed record HarvestQualityGradeResponse(
        Guid Id,
        string Code,
        string Name,
        int DisplayOrder,
        int RevisionNumber,
        Guid? SupersedesId,
        bool IsActive,
        DateTimeOffset CreatedAt,
        long Version)
    {
        public static HarvestQualityGradeResponse From(
            HarvestQualityGrade grade) =>
            new(
                grade.Id,
                grade.Code,
                grade.Name,
                grade.DisplayOrder,
                grade.RevisionNumber,
                grade.SupersedesId,
                grade.IsActive,
                grade.CreatedAt,
                grade.Version);
    }
}
