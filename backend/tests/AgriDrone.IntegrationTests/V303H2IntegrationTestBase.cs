using System.Data;
using System.Text.Json;
using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public abstract class V303H2IntegrationTestBase : IAsyncLifetime
{
    protected const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";

    protected static readonly string[] StartConflictCodes = ["CAPTURE_IN_FLIGHT", "CANCELLATION_VERSION_CONFLICT", "CAPTURE_START_CONFLICT", "CAPTURE_VERSION_CONFLICT", "ORDER_INVALID_STATE"];
    protected static readonly string[] CancelConflictCodes = ["CAPTURE_VERSION_CONFLICT", "CAPTURE_GATE_CLOSED", "CAPTURE_IN_FLIGHT", "CANCELLATION_CONFLICT"];

    protected string DatabaseName { get; }

    protected V303H2IntegrationTestBase(string suitePrefix = "v303h2")
    {
        DatabaseName = $"agridrone_{suitePrefix}_{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        await RecreateDatabaseAsync(DatabaseName);
        await using var context = CreateContext(DatabaseName);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await DropDatabaseAsync(DatabaseName);
    }

    protected static AgriDroneSchemaDbContext CreateContext(string databaseName)
    {
        var builder = new NpgsqlDataSourceBuilder(BuildConnectionString(databaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(builder);
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        AgriDroneSchemaDbContextOptions.Configure(options, builder.Build());
        return new AgriDroneSchemaDbContext(options.Options);
    }

    protected static ISurveyCaptureExecutionCoordinator CreateCoordinator(
        AgriDroneSchemaDbContext context,
        ISurveyCaptureExecutionProbe? probe = null)
    {
        var clock = TimeProvider.System;
        return new SurveyCaptureExecutionCoordinator(
            context,
            clock,
            new OutboxMessageFactory(
                new SystemTextJsonIntegrationMessageSerializer(),
                clock),
            probe);
    }

    public sealed class TestExecutionContext(Guid actorId, Guid? tenantId = null) : AgriDrone.SharedKernel.Application.Abstractions.Execution.IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => tenantId;
        public Guid? ActorId => actorId;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public AgriDrone.SharedKernel.Application.Abstractions.Execution.ExecutionContextSource Source =>
            AgriDrone.SharedKernel.Application.Abstractions.Execution.ExecutionContextSource.Http;
    }

    public sealed class NoopAuditLogSink : AgriDrone.SharedInfrastructure.Auditing.IAuditLogSink
    {
        public void AddAuditLog(AgriDrone.SharedInfrastructure.Auditing.AuditLog auditLog)
        {
        }
    }

    protected static async Task<uint> GetOrderVersionAsync(string databaseName, Guid orderId)
    {
        await using var ctx = CreateContext(databaseName);
        return await ctx.Set<SurveyOrder>()
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => o.Version)
            .SingleAsync();
    }

    protected static string BuildConnectionString(string databaseName) =>
        $"Host=127.0.0.1;Port=55432;Database={databaseName};Username=agridrone_test;Password=agridrone_test";

    public sealed record V3OrderTestGraph(
        Guid TenantId,
        Guid FarmId,
        Guid DroneId,
        Guid OrderId,
        Guid RequestId,
        Guid ManagerUserId,
        Guid OwnerUserId,
        Guid AdminUserId,
        Guid ObligationId,
        Guid PolicyVersionId);

    public sealed class ThrowAtCheckpointProbe(SurveyCaptureCheckpoint target) : ISurveyCaptureExecutionProbe
    {
        public ValueTask HitAsync(SurveyCaptureCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            if (checkpoint == target)
            {
                throw new InvalidOperationException($"Injected failure at {checkpoint}.");
            }

            return ValueTask.CompletedTask;
        }
    }

    public sealed class BarrierCaptureProbe(int participants) : ISurveyCaptureExecutionProbe, IDisposable
    {
        private readonly Barrier _barrier = new(participants);

        public ValueTask HitAsync(SurveyCaptureCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            if (checkpoint == SurveyCaptureCheckpoint.BeforeOrderLock)
            {
                if (!_barrier.SignalAndWait(TimeSpan.FromSeconds(15), cancellationToken))
                {
                    throw new TimeoutException("Race barrier timed out at BeforeOrderLock.");
                }
            }

            return ValueTask.CompletedTask;
        }

        public void Dispose() => _barrier.Dispose();
    }

    protected static async Task<V3OrderTestGraph> SeedV3OrderGraphAsync(
        string databaseName,
        SurveyOrderStatus initialStatus = SurveyOrderStatus.ReadyForCapture,
        bool isAssignedManager = true,
        bool isSatisfiedDeposit = true,
        bool hasConfirmedAppointment = true,
        string serviceCode = "COMBO")
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var obligationId = Guid.NewGuid();
        var adminRoleId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var depositPolicyId = Guid.NewGuid();
        var claimPolicyId = Guid.NewGuid();
        var cancellationPolicyId = Guid.NewGuid();
        var policyVersionId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var priceVersionId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        const string policyFingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();

        // 1. Get seeded service id
        using var getService = connection.CreateCommand();
        getService.CommandText = $"SELECT id FROM survey.survey_services WHERE code = '{serviceCode}' LIMIT 1;";
        var serviceId = (Guid)(await getService.ExecuteScalarAsync())!;

        var hasExistingPolicies = false;
        using var getExistingPolicy = connection.CreateCommand();
        getExistingPolicy.CommandText = "SELECT id, survey_deposit_policy_id, claim_policy_id, cancellation_policy_id FROM survey.business_policy_versions WHERE stream_key = 'DEFAULT' AND version_number = 1 LIMIT 1;";
        await using (var reader = await getExistingPolicy.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                hasExistingPolicies = true;
                policyVersionId = reader.GetGuid(0);
                depositPolicyId = reader.GetGuid(1);
                claimPolicyId = reader.GetGuid(2);
                cancellationPolicyId = reader.GetGuid(3);
            }
        }

        if (hasExistingPolicies)
        {
            using var getRev = connection.CreateCommand();
            getRev.CommandText = $"SELECT id FROM survey.survey_service_revisions WHERE survey_service_id = '{serviceId}' AND version_number = 1 LIMIT 1;";
            revisionId = (Guid)(await getRev.ExecuteScalarAsync())!;

            using var getPrice = connection.CreateCommand();
            getPrice.CommandText = $"SELECT id FROM survey.survey_service_prices WHERE survey_service_id = '{serviceId}' AND version_number = 1 LIMIT 1;";
            priceVersionId = (Guid)(await getPrice.ExecuteScalarAsync())!;
        }

        var now = DateTimeOffset.UtcNow;
        var appStart = now.AddHours(-1);
        var appEnd = now.AddHours(3);

        var assignedEndedAtSql = isAssignedManager ? "NULL" : "NOW()";
        var depositPaymentStatusSql = isSatisfiedDeposit ? "PAID" : "PENDING";
        var depositObligationStatusSql = isSatisfiedDeposit ? "SATISFIED" : "PENDING";
        var depositAllocatedAmountSql = isSatisfiedDeposit ? "100000" : "0";
        var depositSatisfiedAtSql = isSatisfiedDeposit ? "NOW()" : "NULL";
        var appointmentStatusSql = hasConfirmedAppointment ? "CONFIRMED" : "PROPOSED";
        var confirmedByOwnerSql = hasConfirmedAppointment ? $"'{ownerUserId}'" : "NULL";
        var confirmedAtSql = hasConfirmedAppointment ? "NOW()" : "NULL";
        var tenantCode = $"TEN-{tenantId:N}"[..15];
        var farmCode = $"FARM-{farmId:N}"[..15];
        var droneCode = $"DRN-{droneId:N}"[..15];
        var registrationNumber = $"VN-{droneId:N}"[..8].ToUpperInvariant();
        var requestNumber = $"REQ-{requestId:N}"[..15];
        var idempotencyKey = $"IDEM-{requestId:N}"[..15];
        var orderNumber = $"ORD-{orderId:N}"[..15];
        var orderStatusSql = ToDbEnumString(initialStatus);

        await ExecuteAsync(connection, $$"""
            -- Users
            INSERT INTO identity.users (id, email, password_hash, full_name, status)
            VALUES 
              ('{{managerUserId}}', 'mgr-{{managerUserId:N}}@test.local', 'hash', 'Manager User', 'ACTIVE'::system.user_status),
              ('{{ownerUserId}}', 'owner-{{ownerUserId:N}}@test.local', 'hash', 'Owner User', 'ACTIVE'::system.user_status),
              ('{{adminUserId}}', 'admin-{{adminUserId:N}}@test.local', 'hash', 'Admin User', 'ACTIVE'::system.user_status);

            -- Role & UserRole for admin
            INSERT INTO identity.roles (id, code, name)
            VALUES ('{{adminRoleId}}', 'SYSTEM_ADMIN', 'System Administrator')
            ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name;

            INSERT INTO identity.user_roles (user_id, role_id)
            SELECT '{{adminUserId}}', id FROM identity.roles WHERE code = 'SYSTEM_ADMIN'
            ON CONFLICT DO NOTHING;

            -- Tenant & Owner Membership
            INSERT INTO identity.tenants (id, code, name)
            VALUES ('{{tenantId}}', '{{tenantCode}}', 'Test Tenant');

            INSERT INTO identity.tenant_memberships (id, tenant_id, user_id, role, status)
            VALUES ('{{membershipId}}', '{{tenantId}}', '{{ownerUserId}}', 'OWNER'::system.tenant_member_role, 'ACTIVE'::system.general_status);

            -- Farm
            INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
            VALUES ('{{farmId}}', '{{tenantId}}', '{{farmCode}}', 'Test Farm', '{{ownerUserId}}');

            -- System Manager Profile & Farm Manager Assignment
            INSERT INTO identity.system_manager_profiles (id, user_id, availability, status, created_at, updated_at)
            VALUES ('{{profileId}}', '{{managerUserId}}', 'AVAILABLE'::system.system_manager_availability_status, 'ACTIVE'::system.system_manager_profile_status, NOW(), NOW());

            -- Farm Manager Assignment
            INSERT INTO identity.farm_manager_assignments (id, tenant_id, farm_id, system_manager_profile_id, assigned_by, assignment_reason, assigned_at, ended_at)
            VALUES ('{{assignmentId}}', '{{tenantId}}', '{{farmId}}', '{{profileId}}', '{{adminUserId}}', 'Assignment', NOW(), {{assignedEndedAtSql}});

            -- Drone
            INSERT INTO mission.drones (id, code, name, status, specifications, registration_number, registration_date, registration_expiry_date)
            VALUES ('{{droneId}}', '{{droneCode}}', 'Test Drone', 'AVAILABLE'::system.drone_status, '{"capabilities":["BASELINE_MAPPING","PLANT_HEALTH"]}'::jsonb, '{{registrationNumber}}', '2020-01-01', '2030-01-01');
            """);

        if (!hasExistingPolicies)
        {
            await ExecuteAsync(connection, $$"""
                -- Policies
                INSERT INTO survey.v3_survey_deposit_policies (id, version_number, amount, currency, effective_from, created_by, created_at, reason_code)
                VALUES ('{{depositPolicyId}}', 1, 100000, 'VND', NOW(), '{{adminUserId}}', NOW(), 'INIT');

                -- Claim Policies
                INSERT INTO survey.claim_policies (id, version_number, calendar_window_days, time_zone_id, exclude_notice_date, effective_from, created_by, created_at, reason_code)
                VALUES ('{{claimPolicyId}}', 1, 7, 'Asia/Ho_Chi_Minh', TRUE, NOW(), '{{adminUserId}}', NOW(), 'INIT');

                -- Cancellation Policies
                INSERT INTO survey.cancellation_policies (id, version_number, refundable_before_capture, retain_deposit_after_capture, allowed_free_no_show_reschedules, operational_postponement_refundable, service_failure_refundable, effective_from, created_by, created_at, reason_code)
                VALUES ('{{cancellationPolicyId}}', 1, TRUE, TRUE, 1, TRUE, TRUE, NOW(), '{{adminUserId}}', NOW(), 'INIT');

                -- Service Revision & Price
                INSERT INTO survey.survey_service_revisions (id, survey_service_id, version_number, name, description, maps_poles, screens_disease, effective_from, created_by, created_at, reason_code)
                VALUES ('{{revisionId}}', '{{serviceId}}', 1, 'Combo Survey Service', 'Combo mapping and disease survey', TRUE, TRUE, NOW(), '{{adminUserId}}', NOW(), 'INIT');

                INSERT INTO survey.survey_service_prices (id, survey_service_id, version_number, price_per_pole, currency, effective_from, created_by, created_at, reason_code)
                VALUES ('{{priceVersionId}}', '{{serviceId}}', 1, 5000, 'VND', NOW(), '{{adminUserId}}', NOW(), 'INIT');

                -- Business Policy Versions
                INSERT INTO survey.business_policy_versions (id, stream_key, version_number, survey_deposit_policy_id, claim_policy_id, cancellation_policy_id, service_revision_ids, pricing_currency, policy_fingerprint, effective_from, created_by, created_at, reason_code)
                VALUES ('{{policyVersionId}}', 'DEFAULT', 1, '{{depositPolicyId}}', '{{claimPolicyId}}', '{{cancellationPolicyId}}', ARRAY['{{revisionId}}'::uuid], 'VND', '{{policyFingerprint}}', NOW(), '{{adminUserId}}', NOW(), 'INIT');
                """);
        }

        await ExecuteAsync(connection, $$"""
            -- Survey Request
            INSERT INTO survey.survey_requests
              (id, request_number, kind, tenant_id, farm_id, requested_by_user_id,
               survey_service_id, caller_scope, idempotency_key, applicant_name,
               applicant_email, applicant_phone, farm_name, farm_address,
               approximate_area_ha, map_location, status)
            VALUES
              ('{{requestId}}', '{{requestNumber}}', 'EXISTING_FARM_SURVEY', '{{tenantId}}', '{{farmId}}', '{{ownerUserId}}',
               '{{serviceId}}', 'tenant:{{tenantId}}', '{{idempotencyKey}}', 'Applicant',
               'owner@test.local', '0900000000', 'Test Farm', 'Address',
               1.0000, ST_SetSRID(ST_MakePoint(106.5, 10.5), 4326), 'APPROVED');

            -- Survey Order
            INSERT INTO survey.survey_orders
              (id, order_number, tenant_id, farm_id, survey_request_id,
               survey_service_id, scope, requires_baseline_mapping, status,
               deposit_amount, deposit_payment_status, deposit_obligation_id,
               business_policy_version_id, created_at, updated_at)
            VALUES
              ('{{orderId}}', '{{orderNumber}}', '{{tenantId}}', '{{farmId}}', '{{requestId}}',
               '{{serviceId}}', 'V3'::system.contract_scope, FALSE, '{{orderStatusSql}}'::system.survey_order_status,
               100000, '{{depositPaymentStatusSql}}'::system.survey_payment_status, '{{obligationId}}',
               '{{policyVersionId}}', NOW(), NOW());

            -- Commercial Snapshot
            INSERT INTO survey.order_commercial_snapshots
              (survey_order_id, business_policy_version_id, policy_fingerprint,
               survey_service_revision_id, survey_service_revision_version, service_code,
               maps_poles, screens_disease, price_version_id, price_version_number,
               price_per_pole, currency, deposit_policy_id, deposit_version_number,
               deposit_amount, claim_policy_id, claim_policy_version_number,
               claim_window_days, claim_time_zone_id, exclude_notice_date,
               cancellation_policy_id, cancellation_policy_version_number,
               refundable_before_capture, retain_deposit_after_capture,
               allowed_free_no_show_reschedules, evaluated_at, created_at)
            VALUES
              ('{{orderId}}', '{{policyVersionId}}', '{{policyFingerprint}}',
               '{{revisionId}}', 1, '{{serviceCode}}',
               TRUE, TRUE, '{{priceVersionId}}', 1,
               5000, 'VND', '{{depositPolicyId}}', 1,
               100000, '{{claimPolicyId}}', 1,
               7, 'Asia/Ho_Chi_Minh', TRUE,
               '{{cancellationPolicyId}}', 1,
               TRUE, TRUE,
               1, NOW(), NOW());

            -- Deposit Obligation
            INSERT INTO survey.survey_deposit_obligations
              (id, survey_order_id, tenant_id, farm_id, business_policy_version_id,
               deposit_policy_id, amount, currency, allocated_amount, status,
               created_at, satisfied_at, cancelled_at)
            VALUES
              ('{{obligationId}}', '{{orderId}}', '{{tenantId}}', '{{farmId}}',
               '{{policyVersionId}}', '{{depositPolicyId}}', 100000, 'VND', {{depositAllocatedAmountSql}},
               '{{depositObligationStatusSql}}'::system.survey_deposit_obligation_status,
               NOW(), {{depositSatisfiedAtSql}}, NULL);

            -- Survey Appointment
            INSERT INTO survey.survey_appointments
              (id, survey_order_id, purpose, proposed_start_at, proposed_end_at,
               revision_number, previous_appointment_id, proposed_by_system_manager_id,
               status, confirmed_by_tenant_owner_id, confirmed_at, reschedule_reason,
               created_at, updated_at)
            VALUES
              ('{{appointmentId}}', '{{orderId}}', 'CAPTURE'::system.survey_appointment_purpose,
               '{{appStart:O}}', '{{appEnd:O}}', 1, NULL, '{{managerUserId}}',
               '{{appointmentStatusSql}}'::system.survey_appointment_status,
               {{confirmedByOwnerSql}}, {{confirmedAtSql}}, NULL, NOW(), NOW());
            """);

        if (isSatisfiedDeposit)
        {
            var txId = $"TX-{receiptId:N}"[..15];
            await ExecuteAsync(connection, $$"""
                -- Funding Receipt & Allocation
                INSERT INTO survey.survey_funding_receipts
                  (id, tenant_id, survey_order_id, deposit_obligation_id, provider,
                   provider_transaction_id, amount, currency, payment_reference,
                   payload_fingerprint, received_at, created_at)
                VALUES
                  ('{{receiptId}}', '{{tenantId}}', '{{orderId}}', '{{obligationId}}', 'SEPAY',
                   '{{txId}}', 100000, 'VND', 'REF-{{receiptId:N}}', 'FINGERPRINT-{{receiptId:N}}',
                   NOW(), NOW());

                INSERT INTO survey.survey_funding_allocations
                  (id, funding_receipt_id, survey_order_id, deposit_obligation_id,
                   allocated_amount, currency, allocated_at)
                VALUES
                  (gen_random_uuid(), '{{receiptId}}', '{{orderId}}', '{{obligationId}}',
                   100000, 'VND', NOW());
                """);
        }

        return new V3OrderTestGraph(
            tenantId,
            farmId,
            droneId,
            orderId,
            requestId,
            managerUserId,
            ownerUserId,
            adminUserId,
            obligationId,
            policyVersionId);
    }

    protected static async Task RecreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();

        await ExecuteAsync(connection, $"""
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = '{databaseName}' AND pid <> pg_backend_pid();
            """);

        await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS \"{databaseName}\";");
        await ExecuteAsync(connection, $"CREATE DATABASE \"{databaseName}\";");
    }

    protected static async Task DropDatabaseAsync(string databaseName)
    {
        try
        {
            await using var connection = new NpgsqlConnection(AdminConnection);
            await connection.OpenAsync();

            await ExecuteAsync(connection, $"""
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{databaseName}' AND pid <> pg_backend_pid();
                """);

            await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS \"{databaseName}\";");
        }
        catch
        {
            // Best effort cleanup in tests
        }
    }

    protected static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static string ToDbEnumString(SurveyOrderStatus status) =>
        UpperSnakeCaseNameTranslator.Instance.TranslateMemberName(status.ToString());
}
