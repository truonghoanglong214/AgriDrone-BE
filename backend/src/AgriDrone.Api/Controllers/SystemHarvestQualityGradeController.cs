using AgriDrone.Api.Contracts.HarvestQualityGrades;
using AgriDrone.Modules.Harvests.Application.Features.CreateHarvestQualityGrade;
using AgriDrone.Modules.Harvests.Application.Features.RetireHarvestQualityGrade;
using AgriDrone.Modules.Harvests.Application.Features.VersionHarvestQualityGrade;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.Api.Legacy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers
{
    [Route("api/system/harvest-quality-grades")]
    [ApiController]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
    public class SystemHarvestQualityGradeController(
        ISender sender) : ControllerBase
    {
        [HttpPost]
        [LegacyEndpoint(
            "harvest-quality-grades.create",
            "Harvest quality grading is outside scope; configure Harvest Readiness criteria in the future Survey workflow.")]
        public async Task<IResult> Create(
            [FromBody] CreateHarvestQualityGradeRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CreateHarvestQualityGradeCommand(
                request.Code,
                request.Name,
                request.DisplayOrder);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                response => Results.Json(
                    response,
                    statusCode: StatusCodes.Status201Created));
        }

        /// <summary>Tạo version tiếp theo từ một quality grade đang active.</summary>
        [HttpPost("{gradeId:guid}/versions")]
        [LegacyEndpoint(
            "harvest-quality-grades.version",
            "Harvest quality grading is outside scope; existing records remain read-only history.")]
        public async Task<IResult> CreateVersion(
            [FromRoute] Guid gradeId,
            [FromBody] VersionHarvestQualityGradeRequest request,
            CancellationToken cancellationToken)
        {
            var command = new VersionHarvestQualityGradeCommand(
                gradeId,
                request.Name,
                request.DisplayOrder,
                request.ExpectedVersion);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                response => Results.Json(
                    response,
                    statusCode: StatusCodes.Status201Created));
        }

        /// <summary>Ngừng sử dụng một quality grade đang active.</summary>
        [HttpPut("{gradeId:guid}/retire")]
        [LegacyEndpoint(
            "harvest-quality-grades.retire",
            "Harvest quality grading is outside scope; existing records remain read-only history.")]
        public async Task<IResult> Retire(
            [FromRoute] Guid gradeId,
            [FromBody] RetireHarvestQualityGradeRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new RetireHarvestQualityGradeCommand(
                    gradeId,
                    request.ExpectedVersion),
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                () => Results.NoContent());
        }
    }
}
