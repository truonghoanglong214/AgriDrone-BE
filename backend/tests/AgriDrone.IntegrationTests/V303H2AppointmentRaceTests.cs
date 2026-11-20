using System.Text.Json;
using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class V303H2AppointmentRaceTests : V303H2IntegrationTestBase
{
    public V303H2AppointmentRaceTests() : base("v303h2_appt")
    {
    }

    [Fact]
    public async Task Scenario03T03ConcurrentAppointmentConfirmAndRescheduleSerializesWithOptimisticLock()
    {
        // 1. Seed Order with a proposed appointment
        var seeded = await SeedV3OrderGraphAsync(
            DatabaseName,
            initialStatus: SurveyOrderStatus.ReadyForCapture,
            isSatisfiedDeposit: true,
            hasConfirmedAppointment: false);

        await using var verifyCtx = CreateContext(DatabaseName);
        var appointment = await verifyCtx.Set<SurveyAppointment>()
            .AsNoTracking()
            .SingleAsync(a => a.SurveyOrderId == seeded.OrderId);

        Assert.Equal(SurveyAppointmentStatus.Proposed, appointment.Status);

        var orderVersion = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var appointmentVersion = appointment.Version;

        // 2. Prepare 2 concurrent commands:
        //    Task A: Confirm appointment by Owner
        //    Task B: Request reschedule by Owner with the same initial version
        var confirmCmd = new ConfirmSurveyAppointmentCommand(
            seeded.OrderId,
            appointment.Id,
            orderVersion,
            appointmentVersion,
            ContractScope.V3);

        var rescheduleCmd = new RequestSurveyAppointmentRescheduleCommand(
            seeded.OrderId,
            appointment.Id,
            "Owner prefers afternoon slot",
            orderVersion,
            appointmentVersion,
            ContractScope.V3);

        var confirmTask = Task.Run(async () =>
        {
            var services = CreateSchedulingServices(DatabaseName, seeded.OwnerUserId);
            return await services.ConfirmHandler.Handle(confirmCmd, CancellationToken.None);
        });

        var rescheduleTask = Task.Run(async () =>
        {
            var services = CreateSchedulingServices(DatabaseName, seeded.OwnerUserId);
            return await services.RescheduleHandler.Handle(rescheduleCmd, CancellationToken.None);
        });

        await Task.WhenAll(confirmTask, rescheduleTask);

        var confirmResult = await confirmTask;
        var rescheduleResult = await rescheduleTask;

        // Exactly one operation can win under row lock and version serialization
        Assert.False(confirmResult.IsSuccess && rescheduleResult.IsSuccess, "Both Confirm and Reschedule succeeded concurrently!");
        Assert.True(confirmResult.IsSuccess || rescheduleResult.IsSuccess, "Both operations failed!");

        if (confirmResult.IsSuccess)
        {
            Assert.True(rescheduleResult.IsFailure);
            Assert.True(rescheduleResult.Error.Code.Contains("Conflict") ||
                        rescheduleResult.Error.Code.Contains("InvalidTransition") ||
                        rescheduleResult.Error.Code.Contains("InvalidStatus"));
        }
        else
        {
            Assert.True(rescheduleResult.IsSuccess);
            Assert.True(confirmResult.IsFailure);
            Assert.True(confirmResult.Error.Code.Contains("Conflict") ||
                        confirmResult.Error.Code.Contains("InvalidTransition") ||
                        confirmResult.Error.Code.Contains("InvalidStatus"));
        }

        // Verify final appointment state in fresh context
        await using var checkCtx = CreateContext(DatabaseName);
        var finalAppt = await checkCtx.Set<SurveyAppointment>()
            .AsNoTracking()
            .SingleAsync(a => a.Id == appointment.Id);

        Assert.True(
            finalAppt.Status == SurveyAppointmentStatus.Confirmed ||
            finalAppt.Status == SurveyAppointmentStatus.RescheduleRequested);
    }

    [Fact]
    public async Task Scenario03T03ForeignUserCannotConfirmAppointment()
    {
        var seeded = await SeedV3OrderGraphAsync(
            DatabaseName,
            initialStatus: SurveyOrderStatus.ReadyForCapture,
            isSatisfiedDeposit: true,
            hasConfirmedAppointment: false);

        await using var verifyCtx = CreateContext(DatabaseName);
        var appointment = await verifyCtx.Set<SurveyAppointment>()
            .AsNoTracking()
            .SingleAsync(a => a.SurveyOrderId == seeded.OrderId);

        var orderVersion = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var foreignUserId = Guid.NewGuid();

        var confirmCmd = new ConfirmSurveyAppointmentCommand(
            seeded.OrderId,
            appointment.Id,
            orderVersion,
            appointment.Version,
            ContractScope.V3);

        var services = CreateSchedulingServices(DatabaseName, foreignUserId);
        var result = await services.ConfirmHandler.Handle(confirmCmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("SurveyAppointment.Forbidden", result.Error.Code);
    }

    private static SchedulingServices CreateSchedulingServices(string databaseName, Guid actorId)
    {
        var builder = new NpgsqlDataSourceBuilder(BuildConnectionString(databaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(builder);
        var dataSource = builder.Build();

        var options = new DbContextOptionsBuilder<SurveyAppointmentSchedulingDbContext>()
            .UseNpgsql(dataSource, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                PostgreSqlEnumMappings.ConfigureOptions(npgsql);
            })
            .Options;

        var dbContext = new SurveyAppointmentSchedulingDbContext(options);
        var repository = new SurveyAppointmentSchedulingRepository(dbContext);
        var clock = TimeProvider.System;
        var outbox = new SurveyAppointmentSchedulingOutbox(
            dbContext,
            new OutboxMessageFactory(new SystemTextJsonIntegrationMessageSerializer(), clock));
        var auditLogSink = new NoopAuditLogSink();
        var auditWriter = new AuditWriter();
        var execContext = new TestExecutionContext(actorId);

        var confirmHandler = new ConfirmSurveyAppointmentCommandHandler(
            dbContext,
            repository,
            outbox,
            auditWriter,
            execContext,
            clock);

        var rescheduleHandler = new RequestSurveyAppointmentRescheduleCommandHandler(
            dbContext,
            repository,
            outbox,
            auditWriter,
            execContext,
            clock);

        return new SchedulingServices(dbContext, confirmHandler, rescheduleHandler);
    }

    private sealed record SchedulingServices(
        SurveyAppointmentSchedulingDbContext DbContext,
        ConfirmSurveyAppointmentCommandHandler ConfirmHandler,
        RequestSurveyAppointmentRescheduleCommandHandler RescheduleHandler);
}
