using System.Data.Common;
using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyPricingConfirmationRepository(
    SurveyPricingConfirmationDbContext context)
    : ISurveyPricingConfirmationRepository
{
    public async Task<Result<SurveyPricingConfirmationState>>
        GetForConfirmationAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await ExistsAsync(
                """
                SELECT survey_order.id
                FROM survey.survey_orders AS survey_order
                WHERE survey_order.id = @order_id
                FOR UPDATE OF survey_order
                """,
                cancellationToken,
                Id("order_id", surveyOrderId)))
        {
            return Result.Failure<SurveyPricingConfirmationState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        var managerProfileId = await ReadManagerAccessAsync(
            surveyOrderId,
            actorUserId,
            evaluatedAt,
            cancellationToken);
        if (!managerProfileId.HasValue)
        {
            return Result.Failure<SurveyPricingConfirmationState>(
                SurveyPricingError.Forbidden());
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == surveyOrderId,
            cancellationToken);
        if (!order.FarmBoundaryVersionId.HasValue ||
            !await HasApprovedScopeAsync(order, cancellationToken))
        {
            return Result.Failure<SurveyPricingConfirmationState>(
                SurveyPricingError.ApprovedScopeNotFound());
        }

        var map = await ReadCurrentMapAsync(
            order,
            cancellationToken);
        if (map is null)
        {
            return Result.Failure<SurveyPricingConfirmationState>(
                SurveyPricingError.CurrentBaseMapNotFound());
        }

        var inventory = await ReadInventoryAsync(
            order,
            map.Value.MapVersionId,
            cancellationToken);
        if (inventory.TotalActiveCount == 0)
        {
            return Result.Failure<SurveyPricingConfirmationState>(
                SurveyPricingError.InventoryEmpty());
        }

        if (inventory.TotalActiveCount != inventory.EligibleActiveCount)
        {
            return Result.Failure<SurveyPricingConfirmationState>(
                SurveyPricingError.InventoryOutsideApprovedScope());
        }

        var prices = await ReadEffectivePricesAsync(
            order.SurveyServiceId,
            evaluatedAt,
            cancellationToken);
        if (prices.Count == 0)
        {
            return Result.Failure<SurveyPricingConfirmationState>(
                SurveyPricingError.EffectivePriceNotFound());
        }

        if (prices.Count > 1)
        {
            return Result.Failure<SurveyPricingConfirmationState>(
                SurveyPricingError.EffectivePriceAmbiguous());
        }

        var price = prices[0];
        return Result.Success(new SurveyPricingConfirmationState(
            order,
            managerProfileId.Value,
            map.Value.MapVersionId,
            map.Value.SourceSurveyOrderId,
            inventory.EligibleActiveCount,
            price.PriceVersionId,
            price.PricePerPole,
            price.Currency));
    }

    private async Task<Guid?> ReadManagerAccessAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            """
            SELECT profile.id
            FROM survey.survey_orders AS survey_order
            JOIN farm.farms AS farm
              ON farm.id = survey_order.farm_id
             AND farm.tenant_id = survey_order.tenant_id
            JOIN identity.farm_manager_assignments AS assignment
              ON assignment.farm_id = survey_order.farm_id
             AND assignment.tenant_id = survey_order.tenant_id
             AND assignment.ended_at IS NULL
            JOIN identity.system_manager_profiles AS profile
              ON profile.id = assignment.system_manager_profile_id
            JOIN identity.users AS app_user ON app_user.id = profile.user_id
            WHERE survey_order.id = @order_id
              AND farm.deleted_at IS NULL
              AND farm.status = 'ACTIVE'::system.general_status
              AND app_user.id = @actor_id
              AND app_user.status = 'ACTIVE'::system.user_status
              AND profile.status = 'ACTIVE'::system.system_manager_profile_status
              AND profile.qualification_status = 'QUALIFIED'::system.flight_qualification_status
              AND profile.qualification_expires_at > @evaluated_at
            FOR SHARE OF farm, assignment, profile, app_user
            """,
            Id("order_id", surveyOrderId),
            Id("actor_id", actorUserId),
            new NpgsqlParameter("evaluated_at", evaluatedAt));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private async Task<MapSnapshot?> ReadCurrentMapAsync(
        SurveyOrder order,
        CancellationToken cancellationToken)
    {
        if (!order.FarmBoundaryVersionId.HasValue)
        {
            return null;
        }

        await using var command = CreateCommand(
            """
            SELECT base_map.id, base_map.source_survey_order_id
            FROM farm.farm_base_map_versions AS base_map
            JOIN farm.farm_boundaries AS boundary
              ON boundary.id = @boundary_id
             AND boundary.tenant_id = base_map.tenant_id
             AND boundary.farm_id = base_map.farm_id
             AND boundary.status = 'APPROVED'::system.farm_boundary_status
            WHERE base_map.tenant_id = @tenant_id
              AND base_map.farm_id = @farm_id
              AND base_map.status = 'PUBLISHED'::system.farm_base_map_status
            FOR SHARE OF base_map, boundary
            """,
            Id("boundary_id", order.FarmBoundaryVersionId.Value),
            Id("tenant_id", order.TenantId),
            Id("farm_id", order.FarmId));
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new MapSnapshot(reader.GetGuid(0), reader.GetGuid(1))
            : null;
    }

    private Task<bool> HasApprovedScopeAsync(
        SurveyOrder order,
        CancellationToken cancellationToken) => ExistsAsync(
            """
            SELECT boundary.id
            FROM farm.farm_boundaries AS boundary
            WHERE boundary.id = @boundary_id
              AND boundary.tenant_id = @tenant_id
              AND boundary.farm_id = @farm_id
              AND boundary.status = 'APPROVED'::system.farm_boundary_status
            FOR SHARE OF boundary
            """,
            cancellationToken,
            Id("boundary_id", order.FarmBoundaryVersionId!.Value),
            Id("tenant_id", order.TenantId),
            Id("farm_id", order.FarmId));

    private async Task<InventorySnapshot> ReadInventoryAsync(
        SurveyOrder order,
        Guid mapVersionId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            """
            SELECT
                count(*)::integer AS total_active_count,
                count(*) FILTER (
                    WHERE plant.location IS NOT NULL
                      AND zone.id IS NOT NULL
                      AND zone.boundary IS NOT NULL
                      AND ST_Covers(boundary.geometry, plant.location)
                      AND ST_Covers(zone.boundary, plant.location)
                )::integer AS eligible_active_count
            FROM plant.plants AS plant
            JOIN farm.farm_boundaries AS boundary
              ON boundary.id = @boundary_id
             AND boundary.tenant_id = @tenant_id
             AND boundary.farm_id = plant.farm_id
             AND boundary.status = 'APPROVED'::system.farm_boundary_status
            LEFT JOIN farm.farm_zones AS zone
              ON zone.id = plant.zone_id
             AND zone.farm_id = plant.farm_id
             AND zone.status = 'ACTIVE'::system.general_status
             AND zone.deleted_at IS NULL
            WHERE plant.farm_id = @farm_id
              AND plant.current_map_version_id = @map_version_id
              AND plant.lifecycle_status = 'ACTIVE'::system.plant_lifecycle_status
            """,
            Id("boundary_id", order.FarmBoundaryVersionId!.Value),
            Id("tenant_id", order.TenantId),
            Id("farm_id", order.FarmId),
            Id("map_version_id", mapVersionId));
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new InventorySnapshot(0, 0);
        }

        return new InventorySnapshot(reader.GetInt32(0), reader.GetInt32(1));
    }

    private async Task<List<PriceSnapshot>> ReadEffectivePricesAsync(
        Guid surveyServiceId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            """
            SELECT price.id, price.price_per_pole, btrim(price.currency)
            FROM survey.survey_service_prices AS price
            JOIN survey.survey_services AS service
              ON service.id = price.survey_service_id
             AND service.status <> 'RETIRED'::system.survey_service_status
            WHERE price.survey_service_id = @service_id
              AND price.price_per_pole IS NOT NULL
              AND price.price_per_ha IS NULL
              AND btrim(price.currency) = 'VND'
              AND price.effective_from <= @evaluated_at
              AND (price.effective_to IS NULL OR @evaluated_at < price.effective_to)
            ORDER BY price.effective_from DESC, price.id DESC
            FOR SHARE OF price, service
            """,
            Id("service_id", surveyServiceId),
            new NpgsqlParameter("evaluated_at", evaluatedAt));
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        var prices = new List<PriceSnapshot>();
        while (await reader.ReadAsync(cancellationToken))
        {
            prices.Add(new PriceSnapshot(
                reader.GetGuid(0),
                reader.GetDecimal(1),
                reader.GetString(2)));
        }

        return prices;
    }

    private async Task<bool> ExistsAsync(
        string sql,
        CancellationToken cancellationToken,
        params NpgsqlParameter[] parameters)
    {
        await using var command = CreateCommand(sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    private DbCommand CreateCommand(
        string sql,
        params NpgsqlParameter[] parameters)
    {
        context.EnsureTransaction();
        var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = context.Database.CurrentTransaction!
            .GetDbTransaction();
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static NpgsqlParameter Id(string name, Guid value) =>
        new(name, value);

    private readonly record struct MapSnapshot(
        Guid MapVersionId,
        Guid SourceSurveyOrderId);

    private readonly record struct InventorySnapshot(
        int TotalActiveCount,
        int EligibleActiveCount);

    private readonly record struct PriceSnapshot(
        Guid PriceVersionId,
        decimal PricePerPole,
        string Currency);
}
