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

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;

internal sealed class ConfirmSurveyAppointmentCommandHandler(
    ISurveyAppointmentSchedulingUnitOfWork unitOfWork,
    ISurveyAppointmentSchedulingRepository repository,
    ISurveyAppointmentSchedulingOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ConfirmSurveyAppointmentCommand,
        Result<ConfirmSurveyAppointmentResponse>>
{
    public async Task<Result<ConfirmSurveyAppointmentResponse>> Handle(
        ConfirmSurveyAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<ConfirmSurveyAppointmentResponse>(
                SurveyAppointmentError.Forbidden());
        }

        var confirmedAt = timeProvider.GetUtcNow();
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                token => ConfirmWithinTransactionAsync(
                    command,
                    actorId,
                    confirmedAt,
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

    private async Task<Result<ConfirmSurveyAppointmentResponse>>
        ConfirmWithinTransactionAsync(
            ConfirmSurveyAppointmentCommand command,
            Guid actorId,
            DateTimeOffset confirmedAt,
            CancellationToken cancellationToken)
    {
        var stateResult = await repository.GetForOwnerMutationAsync(
            command.SurveyOrderId,
            command.AppointmentId,
            actorId,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<ConfirmSurveyAppointmentResponse>(
                stateResult.Error);
        }

        var order = stateResult.Value.Order;
        var appointment = stateResult.Value.Appointment;
        if (appointment.Purpose == SurveyAppointmentPurpose.BaselineMapping &&
            !order.RequiresBaselineMapping)
        {
            return Result.Failure<ConfirmSurveyAppointmentResponse>(
                SurveyAppointmentError.BaselineMappingNotRequired());
        }

        var previousAppointmentStatus = appointment.Status;
        var previousOrderStatus = order.Status;
        try
        {
            order.EnsureVersion(command.ExpectedOrderVersion);
            appointment.EnsureVersion(command.ExpectedAppointmentVersion);
            appointment.Confirm(actorId, confirmedAt);
            if (appointment.Purpose == SurveyAppointmentPurpose.BaselineMapping)
            {
                order.MarkBaselineAppointmentConfirmed(confirmedAt);
            }
            else
            {
                order.MarkPaidAppointmentConfirmed(confirmedAt);
            }
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<ConfirmSurveyAppointmentResponse>(
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
            appointment.ConfirmedAt
        });
        auditWriter.AddUserAction(
            unitOfWork,
            order.TenantId,
            order.FarmId,
            actorId,
            executionContext.CorrelationId,
            nameof(SurveyAppointment),
            appointment.Id,
            appointment.Purpose == SurveyAppointmentPurpose.BaselineMapping
                ? "CONFIRM_BASELINE_APPOINTMENT"
                : "CONFIRM_PAID_SERVICE_APPOINTMENT",
            oldData,
            newData,
            confirmedAt);

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyAppointmentChangedV1,
            Guid.CreateVersion7(),
            executionContext.CorrelationId,
            order.TenantId,
            actorId,
            confirmedAt,
            SurveyAppointmentCommandSupport.CreateEvent(
                order,
                appointment,
                confirmedAt));
        outbox.Add(envelope, order.Id.ToString("D"));
        await unitOfWork.FlushAsync(cancellationToken);

        return Result.Success(new ConfirmSurveyAppointmentResponse(
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
            actorId,
            confirmedAt));
    }

    private static bool IsConflict(string? sqlState) => sqlState is
        PostgresErrorCodes.UniqueViolation or
        PostgresErrorCodes.SerializationFailure or
        PostgresErrorCodes.DeadlockDetected;

    private static Result<ConfirmSurveyAppointmentResponse> Conflict() =>
        Result.Failure<ConfirmSurveyAppointmentResponse>(
            SurveyAppointmentError.PersistenceConflict());
}
