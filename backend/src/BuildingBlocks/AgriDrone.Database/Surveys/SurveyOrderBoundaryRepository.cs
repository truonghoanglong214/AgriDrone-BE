using System.Data.Common;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyOrderBoundaryRepository(
    SurveyBoundaryVerificationDbContext context)
    : ISurveyOrderBoundaryRepository
{
    public async Task<Result<SurveyOrderBoundaryVerificationState>>
        GetForVerificationAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset verifiedAt,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();

        var orderExists = await ExistsAsync(
            """
            SELECT survey_order.id
            FROM survey.survey_orders AS survey_order
            WHERE survey_order.id = @order_id
            FOR UPDATE OF survey_order
            """,
            cancellationToken,
            Id("order_id", surveyOrderId));
        if (!orderExists)
        {
            return Result.Failure<SurveyOrderBoundaryVerificationState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        var access = await ReadManagerAccessAsync(
            surveyOrderId,
            actorUserId,
            verifiedAt,
            cancellationToken);
        if (access is null)
        {
            return Result.Failure<SurveyOrderBoundaryVerificationState>(
                SurveyOrderError.Forbidden());
        }

        var order = await context.SurveyOrders
            .Include(item => item.ZoneScopes)
            .SingleAsync(item => item.Id == surveyOrderId, cancellationToken);
        var activeZoneSnapshots = await context.FarmZones
            .AsNoTracking()
            .Where(zone => zone.FarmId == order.FarmId &&
                           zone.Status == AgriDrone.SharedKernel.Domain.GeneralStatus.Active &&
                           zone.DeletedAt == null &&
                           zone.Boundary != null)
            .Select(zone => new SurveyZoneSelectionSnapshot(
                zone.Id, zone.Version, zone.Boundary!))
            .ToListAsync(cancellationToken);

        var baseMapLocked = await ExistsAsync(
            """
            SELECT base_map.id
            FROM farm.farm_base_map_versions AS base_map
            JOIN survey.survey_orders AS source_order
              ON source_order.id = base_map.source_survey_order_id
             AND source_order.tenant_id = base_map.tenant_id
             AND source_order.farm_id = base_map.farm_id
            WHERE base_map.tenant_id = @tenant_id
              AND base_map.farm_id = @farm_id
              AND base_map.status = 'PUBLISHED'::system.farm_base_map_status
            FOR SHARE OF base_map, source_order
            """,
            cancellationToken,
            Id("tenant_id", order.TenantId),
            Id("farm_id", order.FarmId));

        if (!baseMapLocked)
        {
            return Result.Success(new SurveyOrderBoundaryVerificationState(
                order,
                access.Value,
                CurrentPublishedBaseMapVersionId: null,
                CurrentConfirmedPoleCount: null,
                activeZoneSnapshots));
        }

        var baseMap = await context.FarmBaseMapVersions
            .AsNoTracking()
            .SingleAsync(
                map => map.TenantId == order.TenantId &&
                       map.FarmId == order.FarmId &&
                       map.Status == FarmBaseMapStatus.Published,
                cancellationToken);
        var sourceOrder = await context.SurveyOrders
            .AsNoTracking()
            .SingleAsync(
                item => item.Id == baseMap.SourceSurveyOrderId,
                cancellationToken);

        if (sourceOrder.ConfirmedSurveyPoleCount is null)
        {
            return Result.Failure<SurveyOrderBoundaryVerificationState>(
                SurveyBoundaryVerificationError.FarmInventoryIncomplete());
        }

        return Result.Success(new SurveyOrderBoundaryVerificationState(
            order,
            access.Value,
            baseMap.Id,
            sourceOrder.ConfirmedSurveyPoleCount.Value.Value,
            activeZoneSnapshots));
    }

    public async Task<Guid?> FindCaptureOrthophotoAsync(
        SurveyOrder order, Guid attemptId, uint missionVersion, CancellationToken cancellationToken)
    {
        context.EnsureTransaction();
        await using var command = CreateCommand("""
            SELECT asset.id
            FROM survey.capture_attempts AS attempt
            JOIN mission.drone_missions AS mission ON mission.capture_attempt_id = attempt.id
                AND mission.survey_order_id = attempt.order_id
                AND mission.tenant_id = attempt.tenant_id AND mission.farm_id = attempt.farm_id
            JOIN mission.mission_media AS link ON link.mission_id = mission.id
            JOIN media.media_assets AS asset ON asset.id = link.media_id
                AND asset.tenant_id = mission.tenant_id AND asset.farm_id = mission.farm_id
            WHERE attempt.id = @attempt_id AND attempt.order_id = @order_id
              AND attempt.tenant_id = @tenant_id AND attempt.farm_id = @farm_id
              AND attempt.status = 'PROCESSING' AND mission.mission_purpose = 'CAPTURE'
              AND mission.xmin::text::bigint = @mission_version
              AND link.media_role = 'PROCESSED_IMAGE' AND asset.mime_type = 'image/jpeg'
              AND asset.storage_status = 'ACTIVE' AND asset.deleted_at IS NULL
              AND jsonb_typeof(asset.metadata->'corners') = 'object'
              AND NOT EXISTS (
                  SELECT 1 FROM farm.mapping_candidate_batches AS batch
                  WHERE batch.capture_attempt_id = attempt.id)
            ORDER BY asset.created_at DESC, asset.id DESC
            LIMIT 1
            FOR UPDATE OF attempt, mission
            FOR SHARE OF link, asset
            """,
            Id("attempt_id", attemptId), Id("order_id", order.Id),
            Id("tenant_id", order.TenantId), Id("farm_id", order.FarmId),
            new NpgsqlParameter("mission_version", (long)missionVersion));
        return await command.ExecuteScalarAsync(cancellationToken) is Guid mediaId ? mediaId : null;
    }

    public async Task<uint?> BindCaptureBoundaryAsync(
        SurveyOrder order, Guid attemptId, uint missionVersion, Guid boundaryId,
        DateTimeOffset markedAt, CancellationToken cancellationToken)
    {
        context.EnsureTransaction();
        await using var command = CreateCommand("""
            UPDATE mission.drone_missions
            SET farm_boundary_version_id = @boundary_id, updated_at = @marked_at
            WHERE capture_attempt_id = @attempt_id AND survey_order_id = @order_id
              AND tenant_id = @tenant_id AND farm_id = @farm_id
              AND xmin::text::bigint = @mission_version
            RETURNING xmin::text::bigint
            """,
            Id("boundary_id", boundaryId), Id("attempt_id", attemptId), Id("order_id", order.Id),
            Id("tenant_id", order.TenantId), Id("farm_id", order.FarmId),
            new NpgsqlParameter("mission_version", (long)missionVersion),
            new NpgsqlParameter("marked_at", markedAt));
        return await command.ExecuteScalarAsync(cancellationToken) is long version ? (uint)version : null;
    }

    public async Task<Result<SurveyBoundaryApprovalSnapshot>>
        StageApprovedBoundaryAsync(
            SurveyOrder order,
            Polygon geometry,
            int? expectedCurrentBoundaryVersionNumber,
            Guid actorUserId,
            string reason,
            string? evidenceReference,
            DateTimeOffset verifiedAt,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(geometry);
        context.EnsureTransaction();

        var currentBoundaries = await context.FarmBoundaries
            .FromSqlInterpolated($$"""
                SELECT boundary.*, boundary.xmin
                FROM farm.farm_boundaries AS boundary
                WHERE boundary.tenant_id = {{order.TenantId}}
                  AND boundary.farm_id = {{order.FarmId}}
                  AND boundary.status =
                      'APPROVED'::system.farm_boundary_status
                FOR UPDATE OF boundary
                """)
            .ToListAsync(cancellationToken);
        var currentApproved = currentBoundaries.SingleOrDefault();

        if (currentApproved is null &&
            expectedCurrentBoundaryVersionNumber.HasValue)
        {
            return Result.Failure<SurveyBoundaryApprovalSnapshot>(
                SurveyBoundaryVerificationError.UnexpectedBoundaryVersion());
        }

        if (currentApproved is not null &&
            !expectedCurrentBoundaryVersionNumber.HasValue)
        {
            return Result.Failure<SurveyBoundaryApprovalSnapshot>(
                SurveyBoundaryVerificationError.BoundaryVersionRequired());
        }

        if (currentApproved is not null &&
            currentApproved.VersionNumber !=
                expectedCurrentBoundaryVersionNumber)
        {
            return Result.Failure<SurveyBoundaryApprovalSnapshot>(
                SurveyBoundaryVerificationError.BoundaryVersionConflict());
        }

        var activeZones = await context.FarmZones
            .Where(zone => zone.FarmId == order.FarmId &&
                           zone.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var latestVersionNumber = await context.FarmBoundaries
            .Where(boundary => boundary.FarmId == order.FarmId)
            .Select(boundary => (int?)boundary.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        FarmBoundary draft;
        try
        {
            draft = FarmBoundary.CreateDraft(
                order.TenantId,
                order.FarmId,
                checked(latestVersionNumber + 1),
                geometry,
                FarmBoundarySource.SystemManager,
                order.SurveyRequestId,
                actorUserId,
                verifiedAt,
                evidenceReference);
            FarmBoundaryApprovalService.Approve(
                draft,
                currentApproved,
                activeZones,
                actorUserId,
                reason,
                verifiedAt);
        }
        catch (FarmBoundaryDomainException exception)
        {
            return Result.Failure<SurveyBoundaryApprovalSnapshot>(
                MapBoundaryError(exception));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<SurveyBoundaryApprovalSnapshot>(
                SurveyBoundaryVerificationError.InvalidGeometry(
                    exception.Message));
        }

        context.FarmBoundaries.Add(draft);
        return Result.Success(new SurveyBoundaryApprovalSnapshot(
            draft.Id,
            draft.VersionNumber));
    }

    private async Task<Guid?> ReadManagerAccessAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        DateTimeOffset verifiedAt,
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
            JOIN identity.users AS app_user
              ON app_user.id = profile.user_id
            WHERE survey_order.id = @order_id
              AND farm.deleted_at IS NULL
              AND farm.status = 'ACTIVE'::system.general_status
              AND app_user.id = @actor_id
              AND app_user.status = 'ACTIVE'::system.user_status
              AND profile.status = 'ACTIVE'::system.system_manager_profile_status
            FOR SHARE OF farm, assignment, profile, app_user
            """,
            Id("order_id", surveyOrderId),
            Id("actor_id", actorUserId));

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid profileId ? profileId : null;
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

    private static AppError MapBoundaryError(
        FarmBoundaryDomainException exception) =>
        exception.Code switch
        {
            FarmBoundaryDomainErrorCodes.VersionConflict =>
                SurveyBoundaryVerificationError.BoundaryVersionConflict(),
            FarmBoundaryDomainErrorCodes.ZoneBoundaryRequired =>
                SurveyBoundaryVerificationError.ZoneBoundaryRequired(),
            FarmBoundaryDomainErrorCodes.ZoneOutsideBoundary =>
                SurveyBoundaryVerificationError.ZoneOutsideBoundary(),
            FarmBoundaryDomainErrorCodes.ZonesOverlap =>
                SurveyBoundaryVerificationError.ZonesOverlap(),
            _ => SurveyBoundaryVerificationError.InvalidGeometry(
                exception.Message)
        };
}
