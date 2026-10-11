using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;

internal sealed class RequestSurveyAppointmentRescheduleCommandHandler(
    ISurveyAppointmentSchedulingUnitOfWork unitOfWork,
    ISurveyAppointmentSchedulingRepository repository,
    ISurveyAppointmentSchedulingOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<RequestSurveyAppointmentRescheduleCommand,
        Result<RequestSurveyAppointmentRescheduleResponse>>
{
    public async Task<Result<RequestSurveyAppointmentRescheduleResponse>> Handle(
        RequestSurveyAppointmentRescheduleCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<RequestSurveyAppointmentRescheduleResponse>(
                SurveyAppointmentError.Forbidden());
        }

        var requestedAt = timeProvider.GetUtcNow();
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                token => RequestWithinTransactionAsync(
                    command,
                    actorId,
                    requestedAt,
                    token),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        catch (PostgresException exception) when (IsConflict(exception.SqlState))
        {
            return Conflict();
        }
        catch (DbUpdateException exception) when (
            exception.HasPostgresSqlState(PostgresErrorCodes.UniqueViolation) ||
            exception.HasPostgresSqlState(PostgresErrorCodes.SerializationFailure) ||
            exception.HasPostgresSqlState(PostgresErrorCodes.DeadlockDetected))
        {
            return Conflict();
        }
    }

    private async Task<Result<RequestSurveyAppointmentRescheduleResponse>>
        RequestWithinTransactionAsync(
            RequestSurveyAppointmentRescheduleCommand command,
            Guid actorId,
            DateTimeOffset requestedAt,
            CancellationToken cancellationToken)
    {
        var stateResult = await repository.GetForOwnerMutationAsync(
            command.SurveyOrderId,
            command.AppointmentId,
            actorId,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<RequestSurveyAppointmentRescheduleResponse>(
                stateResult.Error);
        }

        var order = stateResult.Value.Order;
        var appointment = stateResult.Value.Appointment;
        if (!order.RequiresBaselineMapping ||
            appointment.Purpose != SurveyAppointmentPurpose.BaselineMapping)
        {
            return Result.Failure<RequestSurveyAppointmentRescheduleResponse>(
                SurveyAppointmentError.BaselineMappingNotRequired());
        }

        var previousAppointmentStatus = appointment.Status;
        var previousOrderStatus = order.Status;
        try
        {
            order.EnsureVersion(command.ExpectedOrderVersion);
            appointment.EnsureVersion(command.ExpectedAppointmentVersion);
            appointment.RequestReschedule(command.Reason, requestedAt);
            if (previousOrderStatus == SurveyOrderStatus.BaselineReady)
            {
                order.ReopenBaselineAppointment(requestedAt);
            }
            else if (previousOrderStatus !=
                     SurveyOrderStatus.AwaitingBaselineAppointment)
            {
                return Result.Failure<RequestSurveyAppointmentRescheduleResponse>(
                    SurveyOrderError.InvalidTransition(
                        "A Baseline Mapping appointment cannot be rescheduled after Baseline work started."));
            }
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<RequestSurveyAppointmentRescheduleResponse>(
                SurveyAppointmentCommandSupport.MapDomainError(exception));
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            appointmentStatus = previousAppointmentStatus.ToString(),
            orderStatus = previousOrderStatus.ToString()
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            appointmentStatus = appointment.Status.ToString(),
            orderStatus = order.Status.ToString(),
            reason = appointment.RescheduleReason
        });
        auditWriter.AddUserAction(
            unitOfWork,
            order.TenantId,
            order.FarmId,
            actorId,
            executionContext.CorrelationId,
            nameof(SurveyAppointment),
            appointment.Id,
            "REQUEST_BASELINE_APPOINTMENT_RESCHEDULE",
            oldData,
            newData,
            requestedAt);

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyAppointmentChangedV1,
            Guid.CreateVersion7(),
            executionContext.CorrelationId,
            order.TenantId,
            actorId,
            requestedAt,
            SurveyAppointmentCommandSupport.CreateEvent(
                order,
                appointment,
                requestedAt));
        outbox.Add(envelope, order.Id.ToString("D"));
        await unitOfWork.FlushAsync(cancellationToken);

        return Result.Success(new RequestSurveyAppointmentRescheduleResponse(
            order.Id,
            appointment.Id,
            appointment.Purpose,
            appointment.RevisionNumber,
            appointment.PreviousAppointmentId,
            appointment.ProposedStartAt,
            appointment.ProposedEndAt,
            appointment.Status,
            appointment.Version,
            order.Status,
            order.Version,
            appointment.RescheduleReason!,
            requestedAt));
    }

    private static bool IsConflict(string? sqlState) => sqlState is
        PostgresErrorCodes.UniqueViolation or
        PostgresErrorCodes.SerializationFailure or
        PostgresErrorCodes.DeadlockDetected;

    private static Result<RequestSurveyAppointmentRescheduleResponse>
        Conflict() =>
        Result.Failure<RequestSurveyAppointmentRescheduleResponse>(
            SurveyAppointmentError.PersistenceConflict());
}
