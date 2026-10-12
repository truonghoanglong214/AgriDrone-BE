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

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;

internal sealed class ProposeSurveyAppointmentCommandHandler(
    ISurveyAppointmentSchedulingUnitOfWork unitOfWork,
    ISurveyAppointmentSchedulingRepository repository,
    ISurveyAppointmentSchedulingOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ProposeSurveyAppointmentCommand,
        Result<ProposeSurveyAppointmentResponse>>
{
    public async Task<Result<ProposeSurveyAppointmentResponse>> Handle(
        ProposeSurveyAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                SurveyAppointmentError.Forbidden());
        }

        var proposedAt = timeProvider.GetUtcNow();
        if (command.ProposedStartAt <= proposedAt ||
            command.ProposedEndAt <= command.ProposedStartAt)
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                SurveyAppointmentError.InvalidWindow());
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                token => ProposeWithinTransactionAsync(
                    command,
                    actorId,
                    proposedAt,
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

    private async Task<Result<ProposeSurveyAppointmentResponse>>
        ProposeWithinTransactionAsync(
            ProposeSurveyAppointmentCommand command,
            Guid actorId,
            DateTimeOffset proposedAt,
            CancellationToken cancellationToken)
    {
        var stateResult = await repository.GetForManagerProposalAsync(
            command.SurveyOrderId,
            command.Purpose,
            actorId,
            proposedAt,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                stateResult.Error);
        }

        var state = stateResult.Value;
        var order = state.Order;
        try
        {
            order.EnsureVersion(command.ExpectedOrderVersion);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                SurveyAppointmentCommandSupport.MapDomainError(exception));
        }

        if (command.Purpose == SurveyAppointmentPurpose.BaselineMapping &&
            !order.RequiresBaselineMapping)
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                SurveyAppointmentError.BaselineMappingNotRequired());
        }

        if (command.Purpose == SurveyAppointmentPurpose.BaselineMapping &&
            order.Status != SurveyOrderStatus.AwaitingBaselineAppointment)
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                SurveyOrderError.InvalidTransition(
                    "A Baseline Mapping appointment can only be proposed while the order awaits that appointment."));
        }

        if (command.Purpose == SurveyAppointmentPurpose.PaidService &&
            (order.ConfirmedSurveyPoleCount is null ||
             order.PricePerPoleSnapshot is null ||
             order.FinalPrice is null ||
             order.Currency is null))
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                SurveyAppointmentError.PaidServiceNotReady());
        }

        if (command.Purpose == SurveyAppointmentPurpose.PaidService &&
            order.Status != SurveyOrderStatus.AwaitingPaidAppointment)
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                SurveyOrderError.InvalidTransition(
                    "A paid-service appointment can only be proposed after pole-count and pricing confirmation."));
        }

        var current = state.CurrentAppointment;
        var latest = state.LatestAppointment;
        if (current is null &&
            (command.ExpectedCurrentAppointmentId.HasValue ||
             command.ExpectedCurrentAppointmentVersion.HasValue))
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                SurveyAppointmentError.UnexpectedCurrentAppointment());
        }

        if (current is not null &&
            current.Status != SurveyAppointmentStatus.RescheduleRequested)
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                SurveyAppointmentError.ActiveAppointmentExists());
        }

        if (current is not null &&
            (!command.ExpectedCurrentAppointmentId.HasValue ||
             !command.ExpectedCurrentAppointmentVersion.HasValue))
        {
            return Result.Failure<ProposeSurveyAppointmentResponse>(
                SurveyAppointmentError.ExpectedCurrentAppointmentRequired());
        }

        if (current is not null)
        {
            if (current.Id != command.ExpectedCurrentAppointmentId)
            {
                return Result.Failure<ProposeSurveyAppointmentResponse>(
                    SurveyAppointmentError.VersionConflict());
            }

            try
            {
                current.EnsureVersion(
                    command.ExpectedCurrentAppointmentVersion!.Value);
                current.Supersede(proposedAt);
            }
            catch (SurveyDomainException exception)
            {
                return Result.Failure<ProposeSurveyAppointmentResponse>(
                    SurveyAppointmentCommandSupport.MapDomainError(exception));
            }
        }

        var appointment = SurveyAppointment.Create(
            order.Id,
            command.Purpose,
            latest is null ? 1 : checked(latest.RevisionNumber + 1),
            latest?.Id,
            actorId,
            command.ProposedStartAt,
            command.ProposedEndAt,
            proposedAt);
        repository.Add(appointment);

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            orderStatus = order.Status.ToString(),
            previousAppointmentId = current?.Id,
            previousRevisionNumber = current?.RevisionNumber,
            previousStatus = current?.Status.ToString()
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            appointment.Id,
            appointment.RevisionNumber,
            purpose = SurveyAppointmentCommandSupport.MapPurpose(
                appointment.Purpose),
            appointment.ProposedStartAt,
            appointment.ProposedEndAt,
            systemManagerProfileId = state.SystemManagerProfileId
        });
        auditWriter.AddUserAction(
            unitOfWork,
            order.TenantId,
            order.FarmId,
            actorId,
            executionContext.CorrelationId,
            nameof(SurveyAppointment),
            appointment.Id,
            command.Purpose == SurveyAppointmentPurpose.BaselineMapping
                ? "PROPOSE_BASELINE_APPOINTMENT"
                : "PROPOSE_PAID_SERVICE_APPOINTMENT",
            oldData,
            newData,
            proposedAt);

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyAppointmentChangedV1,
            Guid.CreateVersion7(),
            executionContext.CorrelationId,
            order.TenantId,
            actorId,
            proposedAt,
            SurveyAppointmentCommandSupport.CreateEvent(
                order,
                appointment,
                proposedAt));
        outbox.Add(envelope, order.Id.ToString("D"));
        await unitOfWork.FlushAsync(cancellationToken);

        return Result.Success(new ProposeSurveyAppointmentResponse(
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
            proposedAt));
    }

    private static bool IsConflict(string? sqlState) => sqlState is
        PostgresErrorCodes.UniqueViolation or
        PostgresErrorCodes.SerializationFailure or
        PostgresErrorCodes.DeadlockDetected;

    private static Result<ProposeSurveyAppointmentResponse> Conflict() =>
        Result.Failure<ProposeSurveyAppointmentResponse>(
            SurveyAppointmentError.PersistenceConflict());
}
