using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.VerifyOrderBillablePoles;

internal sealed class VerifyOrderBillablePolesCommandHandler(
    SurveysDbContext dbContext,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<VerifyOrderBillablePolesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        VerifyOrderBillablePolesCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<int>(
                AppError.Unauthorized("Authentication.ActorRequired", "Current user is required."));
        }

        var order = await dbContext.SurveyOrders
            .SingleOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<int>(
                AppError.NotFound("SurveyOrder.NotFound", "Survey order not found."));
        }

        if (order.FarmBoundaryVersionId is null)
        {
            return Result.Failure<int>(
                AppError.Conflict("SurveyOrder.MissingBoundary", "Cannot verify billable poles without an approved farm boundary."));
        }

        var now = timeProvider.GetUtcNow();

        // Query verified poles inside the approved farm boundary using PostGIS ST_Within
        // strictly excluding poles located on or outside the boundary.
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*)::int
            FROM plant.plant_profiles p
            INNER JOIN farm.farm_boundaries b ON b.id = @boundaryId
            WHERE p.farm_id = @farmId
              AND p.status = 'Active'
              AND ST_Within(p.current_location, b.boundary_geometry);";

        var paramBoundary = cmd.CreateParameter();
        paramBoundary.ParameterName = "@boundaryId";
        paramBoundary.Value = order.FarmBoundaryVersionId.Value;
        cmd.Parameters.Add(paramBoundary);

        var paramFarm = cmd.CreateParameter();
        paramFarm.ParameterName = "@farmId";
        paramFarm.Value = order.FarmId;
        cmd.Parameters.Add(paramFarm);

        var billableCountObj = await cmd.ExecuteScalarAsync(cancellationToken);
        var billableCount = Convert.ToInt32(billableCountObj, System.Globalization.CultureInfo.InvariantCulture);

        order.RecordVerifiedBillablePoleCount(billableCount, actorId, now);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(billableCount);
    }
}
