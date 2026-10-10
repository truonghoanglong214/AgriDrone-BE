using System.Data;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

/// <summary>
/// The only persistence boundary allowed to mutate cross-module state while a
/// survey request is approved. It intentionally maps just the approval graph.
/// </summary>
public sealed class SurveyApprovalDbContext(
    DbContextOptions<SurveyApprovalDbContext> options,
    ISurveyApprovalFailureInjector failureInjector)
    : DbContext(options)
{
    private bool _approvalTransactionActive;
    private bool _outboxWasStaged;

    public DbSet<User> Users => Set<User>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();
    public DbSet<SystemManagerProfile> SystemManagerProfiles =>
        Set<SystemManagerProfile>();
    public DbSet<FarmManagerAssignment> FarmManagerAssignments =>
        Set<FarmManagerAssignment>();
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<FarmBaseMapVersion> FarmBaseMapVersions =>
        Set<FarmBaseMapVersion>();
    public DbSet<SurveyService> SurveyServices => Set<SurveyService>();
    public DbSet<SurveyRequest> SurveyRequests => Set<SurveyRequest>();
    public DbSet<SurveyRequestReview> SurveyRequestReviews =>
        Set<SurveyRequestReview>();
    public DbSet<SurveyOrder> SurveyOrders => Set<SurveyOrder>();
    public DbSet<SurveyResult> SurveyResults => Set<SurveyResult>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public void AddAuditLog(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        EnsureApprovalTransaction();
        AuditLogs.Add(auditLog);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_approvalTransactionActive || Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "A survey approval transaction is already active on this context.");
        }

        await using var transaction = await Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        _approvalTransactionActive = true;
        _outboxWasStaged = false;

        try
        {
            var result = await operation(cancellationToken);

            if (result is Result { IsFailure: true })
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return result;
            }

            await base.SaveChangesAsync(cancellationToken);

            if (_outboxWasStaged)
            {
                await failureInjector.AfterFlushAsync(
                    SurveyApprovalPersistenceCheckpoint.Outbox,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            _approvalTransactionActive = false;
            _outboxWasStaged = false;
        }
    }

    internal async Task FlushCheckpointAsync(
        SurveyApprovalPersistenceCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        EnsureApprovalTransaction();
        await base.SaveChangesAsync(cancellationToken);
        await failureInjector.AfterFlushAsync(checkpoint, cancellationToken);
    }

    internal void AddOutboxMessage(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureApprovalTransaction();
        OutboxMessages.Add(message);
        _outboxWasStaged = true;
    }

    internal void EnsureApprovalTransaction()
    {
        if (!_approvalTransactionActive || Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Survey approval persistence requires the active specialized transaction.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new TenantMembershipConfiguration());
        modelBuilder.ApplyConfiguration(new TenantInvitationConfiguration());
        modelBuilder.ApplyConfiguration(new SystemManagerProfileConfiguration());
        modelBuilder.ApplyConfiguration(new FarmManagerAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new FarmConfiguration());
        modelBuilder.ApplyConfiguration(new FarmBaseMapVersionConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyServiceConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyRequestConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyRequestReviewConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyOrderConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyResultConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        IgnoreUnrelatedGraph(modelBuilder);
        ConfigureCrossModuleRelationships(modelBuilder);
    }

    private static void IgnoreUnrelatedGraph(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .Ignore(user => user.FarmMemberships)
            .Ignore(user => user.UserRoles)
            .Ignore(user => user.AssignedZoneAssignments);
        modelBuilder.Entity<TenantMembership>()
            .Ignore(membership => membership.FarmMemberships);
        modelBuilder.Entity<Farm>()
            .Ignore(farm => farm.Zones);
        modelBuilder.Entity<FarmBaseMapVersion>()
            .Ignore(map => map.ZoneMapVersions);
        modelBuilder.Entity<SurveyService>()
            .Ignore(service => service.Prices);
        modelBuilder.Entity<SurveyOrder>()
            .Ignore(order => order.SurveyServicePrice)
            .Ignore(order => order.Appointments)
            .Ignore(order => order.Payments);
        modelBuilder.Entity<SurveyResult>()
            .Ignore(result => result.HarvestReadinessAssessments);

        modelBuilder.Ignore<SurveyServicePrice>();
    }

    private static void ConfigureCrossModuleRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Farm>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(farm => farm.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farms_tenants_tenant_id");
        modelBuilder.Entity<Farm>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(farm => farm.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farms_users_created_by");

        modelBuilder.Entity<FarmManagerAssignment>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(assignment => new
            {
                assignment.FarmId,
                assignment.TenantId
            })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_manager_assignments_farms_same_tenant");
        modelBuilder.Entity<FarmManagerAssignment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_manager_assignments_users_assigned_by");
        modelBuilder.Entity<FarmManagerAssignment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.EndedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_manager_assignments_users_ended_by");

        modelBuilder.Entity<SurveyRequest>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(request => request.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_requests_tenants_tenant_id");
        modelBuilder.Entity<SurveyRequest>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(request => new { request.FarmId, request.TenantId })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_requests_farms_same_tenant");
        modelBuilder.Entity<SurveyRequest>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(request => request.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_requests_users_requested_by");
        modelBuilder.Entity<SurveyRequestReview>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(review => review.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_request_reviews_users_reviewed_by");

        modelBuilder.Entity<SurveyOrder>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(order => order.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_orders_tenants_tenant_id");
        modelBuilder.Entity<SurveyOrder>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(order => new { order.FarmId, order.TenantId })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_orders_farms_same_tenant");
        ConfigureOrderUserRelationship(
            modelBuilder,
            order => order.ScopeConfirmedBy,
            "fk_survey_orders_users_scope_confirmed_by");
        ConfigureOrderUserRelationship(
            modelBuilder,
            order => order.PoleCountConfirmedBy,
            "fk_survey_orders_users_pole_count_confirmed_by");
        ConfigureOrderUserRelationship(
            modelBuilder,
            order => order.PricingConfirmedBy,
            "fk_survey_orders_users_pricing_confirmed_by");
        modelBuilder.Entity<SurveyOrder>()
            .HasOne<FarmBaseMapVersion>()
            .WithMany()
            .HasForeignKey(order => new
            {
                order.FarmBaseMapVersionId,
                order.FarmId
            })
            .HasPrincipalKey(map => new { map.Id, map.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_orders_farm_base_map_same_farm");

        modelBuilder.Entity<FarmBaseMapVersion>()
            .HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(map => new
            {
                map.SourceSurveyOrderId,
                map.TenantId,
                map.FarmId
            })
            .HasPrincipalKey(order => new
            {
                order.Id,
                order.TenantId,
                order.FarmId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_farm_base_map_versions_orders_same_tenant_farm");
        modelBuilder.Entity<FarmBaseMapVersion>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(map => map.PublishedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_base_map_versions_users_published_by");

        modelBuilder.Entity<SurveyResult>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(result => result.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_results_users_reviewed_by");
        modelBuilder.Entity<SurveyResult>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(result => result.PublishedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_results_users_published_by");

        modelBuilder.Entity<AuditLog>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(audit => audit.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_audit_logs_users_user_id");
        modelBuilder.Entity<AuditLog>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(audit => audit.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_logs_tenants_tenant_id");
        modelBuilder.Entity<AuditLog>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(audit => new { audit.FarmId, audit.TenantId })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_logs_farms_same_tenant");
    }

    private static void ConfigureOrderUserRelationship(
        ModelBuilder modelBuilder,
        System.Linq.Expressions.Expression<Func<SurveyOrder, object?>> foreignKey,
        string constraintName) =>
        modelBuilder.Entity<SurveyOrder>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(foreignKey)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(constraintName);
}
