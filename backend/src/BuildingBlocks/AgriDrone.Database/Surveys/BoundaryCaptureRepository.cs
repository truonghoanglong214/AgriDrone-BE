using System.Data.Common;
using AgriDrone.Modules.Surveys.Application.Abstractions.BoundaryCapture;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class BoundaryCaptureRepository(
    SurveyBoundaryVerificationDbContext context)
    : IBoundaryCaptureRepository
{
    public async Task<Result<BoundaryCaptureStartState>> GetForStartAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        Guid droneId,
        Guid startOperationId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<BoundaryCaptureStartState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        var managerProfileId = await ReadManagerAccessAsync(
            surveyOrderId,
            actorUserId,
            evaluatedAt,
            cancellationToken);
        if (!managerProfileId.HasValue)
        {
            return Result.Failure<BoundaryCaptureStartState>(
                BoundaryCaptureError.Forbidden());
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == surveyOrderId,
            cancellationToken);
        if (order.Status != SurveyOrderStatus.AwaitingBoundaryCapture ||
            !order.RequiresBaselineMapping)
        {
            return Result.Failure<BoundaryCaptureStartState>(
                BoundaryCaptureError.InvalidState(
                    "The order is not awaiting boundary capture."));
        }

        var appointment = await context.SurveyAppointments
            .Where(item => item.SurveyOrderId == surveyOrderId &&
                           item.Purpose ==
                               SurveyAppointmentPurpose.BaselineMapping &&
                           item.Status == SurveyAppointmentStatus.Confirmed)
            .OrderByDescending(item => item.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (appointment is null)
        {
            return Result.Failure<BoundaryCaptureStartState>(
                BoundaryCaptureError.AppointmentNotConfirmed());
        }

        if (evaluatedAt < appointment.ProposedStartAt ||
            evaluatedAt > appointment.ProposedEndAt)
        {
            return Result.Failure<BoundaryCaptureStartState>(
                BoundaryCaptureError.AppointmentWindowClosed());
        }

        if (!await HasBothValidAuthorizationsAsync(
                surveyOrderId,
                evaluatedAt,
                cancellationToken))
        {
            return Result.Failure<BoundaryCaptureStartState>(
                BoundaryCaptureError.AuthorizationNotValid());
        }

        var existingSession = await context.BoundaryCaptureSessions
            .Include(item => item.Samples)
            .SingleOrDefaultAsync(
                item => item.SurveyOrderId == surveyOrderId &&
                        item.StartOperationId == startOperationId,
                cancellationToken);
        if (existingSession is null &&
            !await DroneIsAvailableAsync(droneId, cancellationToken))
        {
            return Result.Failure<BoundaryCaptureStartState>(
                BoundaryCaptureError.DroneNotAvailable());
        }

        return Result.Success(new BoundaryCaptureStartState(
            order,
            appointment,
            managerProfileId.Value,
            existingSession));
    }

    public async Task<Result<BoundaryCaptureMutationState>> GetForMutationAsync(
        Guid surveyOrderId,
        Guid sessionId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<BoundaryCaptureMutationState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        var managerProfileId = await ReadManagerAccessAsync(
            surveyOrderId,
            actorUserId,
            evaluatedAt,
            cancellationToken);
        if (!managerProfileId.HasValue)
        {
            return Result.Failure<BoundaryCaptureMutationState>(
                BoundaryCaptureError.Forbidden());
        }

        var sessions = await context.BoundaryCaptureSessions
            .FromSqlInterpolated($$"""
                SELECT capture.*, capture.xmin
                FROM survey.boundary_capture_sessions AS capture
                WHERE capture.id = {{sessionId}}
                  AND capture.survey_order_id = {{surveyOrderId}}
                FOR UPDATE OF capture
                """)
            .Include(item => item.Samples)
            .ToListAsync(cancellationToken);
        var session = sessions.SingleOrDefault();
        if (session is null)
        {
            return Result.Failure<BoundaryCaptureMutationState>(
                BoundaryCaptureError.NotFound(sessionId));
        }

        var currentBoundaryVersion = await context.FarmBoundaries
            .Where(item => item.FarmId == session.FarmId &&
                           item.Status ==
                               Modules.Farms.Domain.Boundaries.FarmBoundaryStatus.Approved)
            .Select(item => (int?)item.VersionNumber)
            .SingleOrDefaultAsync(cancellationToken);
        var isAppointmentCurrent = await context.SurveyAppointments
            .AsNoTracking()
            .AnyAsync(
                item => item.Id == session.AppointmentId &&
                        item.SurveyOrderId == surveyOrderId &&
                        item.Version == session.AppointmentVersion &&
                        item.Status == SurveyAppointmentStatus.Confirmed &&
                        item.ProposedStartAt <= evaluatedAt &&
                        evaluatedAt <= item.ProposedEndAt,
                cancellationToken);
        var hasValidAuthorizations = await HasBothValidAuthorizationsAsync(
            surveyOrderId,
            evaluatedAt,
            cancellationToken);

        return Result.Success(new BoundaryCaptureMutationState(
            await context.SurveyOrders.SingleAsync(
                item => item.Id == surveyOrderId,
                cancellationToken),
            session,
            managerProfileId.Value,
            currentBoundaryVersion,
            isAppointmentCurrent,
            hasValidAuthorizations));
    }

    public Task<BoundaryCaptureSession?> GetAsync(
        Guid surveyOrderId,
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        context.BoundaryCaptureSessions
            .AsNoTracking()
            .Include(item => item.Samples)
            .SingleOrDefaultAsync(
                item => item.Id == sessionId &&
                        item.SurveyOrderId == surveyOrderId,
                cancellationToken);

    public void Add(BoundaryCaptureSession session)
    {
        context.EnsureTransaction();
        context.BoundaryCaptureSessions.Add(session);
    }

    public void Add(BaselineRoutePlanReference routePlanReference)
    {
        context.EnsureTransaction();
        context.BaselineRoutePlanReferences.Add(routePlanReference);
    }

    private Task<bool> LockOrderAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken) => ExistsAsync(
            """
            SELECT survey_order.id
            FROM survey.survey_orders AS survey_order
            WHERE survey_order.id = @order_id
            FOR UPDATE OF survey_order
            """,
            cancellationToken,
            Id("order_id", surveyOrderId));

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
            JOIN identity.farm_manager_assignments AS assignment
              ON assignment.farm_id = survey_order.farm_id
             AND assignment.tenant_id = survey_order.tenant_id
             AND assignment.ended_at IS NULL
            JOIN identity.system_manager_profiles AS profile
              ON profile.id = assignment.system_manager_profile_id
            JOIN identity.users AS app_user ON app_user.id = profile.user_id
            WHERE survey_order.id = @order_id
              AND app_user.id = @actor_id
              AND app_user.status = 'ACTIVE'::system.user_status
              AND profile.status = 'ACTIVE'::system.system_manager_profile_status
              AND profile.availability = 'AVAILABLE'::system.system_manager_availability_status
              AND profile.qualification_status = 'QUALIFIED'::system.flight_qualification_status
              AND profile.qualification_expires_at > @evaluated_at
            FOR SHARE OF assignment, profile, app_user
            """,
            Id("order_id", surveyOrderId),
            Id("actor_id", actorUserId),
            new NpgsqlParameter("evaluated_at", evaluatedAt));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private async Task<bool> HasBothValidAuthorizationsAsync(
        Guid surveyOrderId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            """
SELECT count(DISTINCT auth.authorization_type)
FROM survey.survey_capture_authorizations AS auth
WHERE auth.survey_order_id = @order_id
  AND auth.authorization_type IN (
    'WRITTEN_DATA_USE_CONSENT'::system.survey_capture_authorization_type,
    'FLIGHT_PERMISSION'::system.survey_capture_authorization_type)
  AND auth.status = 'VERIFIED'::system.survey_capture_authorization_status
  AND auth.verified_at <= @evaluated_at
  AND (auth.expires_at IS NULL OR @evaluated_at < auth.expires_at)
  AND auth.revoked_at IS NULL
""",
            Id("order_id", surveyOrderId),
            new NpgsqlParameter("evaluated_at", evaluatedAt));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == 2;
    }

    private Task<bool> DroneIsAvailableAsync(
        Guid droneId,
        CancellationToken cancellationToken) => ExistsAsync(
            """
            SELECT drone.id
            FROM mission.drones AS drone
            WHERE drone.id = @drone_id
              AND drone.status = 'AVAILABLE'::system.drone_status
              AND drone.deleted_at IS NULL
              AND NOT EXISTS (
                SELECT 1
                FROM survey.boundary_capture_sessions AS active_capture
                WHERE active_capture.drone_id = drone.id
                  AND active_capture.status = 'InProgress')
            FOR SHARE OF drone
            """,
            cancellationToken,
            Id("drone_id", droneId));

    private async Task<bool> ExistsAsync(
        string sql,
        CancellationToken cancellationToken,
        params NpgsqlParameter[] parameters)
    {
        await using var command = CreateCommand(sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    private DbCommand CreateCommand(
        string sql,
        params NpgsqlParameter[] parameters)
    {
        context.EnsureTransaction();
        var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static NpgsqlParameter Id(string name, Guid value) => new(name, value);
}
