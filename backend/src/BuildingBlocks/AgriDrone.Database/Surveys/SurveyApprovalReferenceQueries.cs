using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyApprovalReferenceQueries(
    SurveyApprovalDbContext context)
    : ISurveyApprovalReferenceQueries
{
    public async Task<SurveyApprovalServiceReference?>
        GetServiceForApprovalAsync(
            Guid surveyServiceId,
            CancellationToken cancellationToken = default)
    {
        var exists = await SurveyApprovalRowLock.ExistsAsync(
            context,
            """
            SELECT id
            FROM survey.survey_services
            WHERE id = @service_id
            FOR SHARE
            """,
            cancellationToken,
            SurveyApprovalRowLock.Id("service_id", surveyServiceId));

        if (!exists)
        {
            return null;
        }

        var service = await context.SurveyServices
            .AsNoTracking()
            .SingleAsync(service => service.Id == surveyServiceId, cancellationToken);

        return new SurveyApprovalServiceReference(
            service.Id,
            service.ServiceType,
            service.Status,
            service.Version);
    }

    public async Task<SurveyApprovalTenantOwnerReference?>
        GetTenantOwnerForApprovalAsync(
            Guid tenantId,
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        var exists = await SurveyApprovalRowLock.ExistsAsync(
            context,
            """
            SELECT membership.id
            FROM identity.tenant_memberships AS membership
            JOIN identity.tenants AS tenant
              ON tenant.id = membership.tenant_id
            JOIN identity.users AS app_user
              ON app_user.id = membership.user_id
            WHERE membership.tenant_id = @tenant_id
              AND membership.user_id = @user_id
            FOR SHARE OF membership, tenant, app_user
            """,
            cancellationToken,
            SurveyApprovalRowLock.Id("tenant_id", tenantId),
            SurveyApprovalRowLock.Id("user_id", userId));

        if (!exists)
        {
            return null;
        }

        var membership = await context.TenantMemberships
            .AsNoTracking()
            .SingleAsync(
                item => item.TenantId == tenantId && item.UserId == userId,
                cancellationToken);
        var tenant = await context.Tenants
            .AsNoTracking()
            .SingleAsync(item => item.Id == tenantId, cancellationToken);
        var user = await context.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == userId, cancellationToken);

        return new SurveyApprovalTenantOwnerReference(
            tenantId,
            userId,
            membership.Role == TenantMemberRole.Owner,
            membership.Status == GeneralStatus.Active,
            tenant.Status == GeneralStatus.Active,
            user.Status == UserStatus.Active,
            membership.Version);
    }

    public async Task<SurveyApprovalFarmReference?> GetFarmForApprovalAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        var exists = await SurveyApprovalRowLock.ExistsAsync(
            context,
            """
            SELECT id
            FROM farm.farms
            WHERE id = @farm_id AND tenant_id = @tenant_id
            FOR SHARE
            """,
            cancellationToken,
            SurveyApprovalRowLock.Id("farm_id", farmId),
            SurveyApprovalRowLock.Id("tenant_id", tenantId));

        if (!exists)
        {
            return null;
        }

        var farm = await context.Farms
            .AsNoTracking()
            .SingleAsync(
                item => item.Id == farmId && item.TenantId == tenantId,
                cancellationToken);

        return new SurveyApprovalFarmReference(
            tenantId,
            farmId,
            farm.Status == GeneralStatus.Active && !farm.IsArchived,
            farm.Version);
    }

    public async Task<SurveyApprovalManagerReference?> GetManagerForApprovalAsync(
        Guid systemManagerProfileId,
        CancellationToken cancellationToken = default)
    {
        var exists = await SurveyApprovalRowLock.ExistsAsync(
            context,
            """
            SELECT profile.id
            FROM identity.system_manager_profiles AS profile
            JOIN identity.users AS app_user ON app_user.id = profile.user_id
            WHERE profile.id = @profile_id
            FOR SHARE OF profile, app_user
            """,
            cancellationToken,
            SurveyApprovalRowLock.Id("profile_id", systemManagerProfileId));

        if (!exists)
        {
            return null;
        }

        var profile = await context.SystemManagerProfiles
            .AsNoTracking()
            .SingleAsync(
                item => item.Id == systemManagerProfileId,
                cancellationToken);
        var user = await context.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == profile.UserId, cancellationToken);

        return new SurveyApprovalManagerReference(
            profile.Id,
            profile.UserId,
            user.Status == UserStatus.Active,
            profile.Status == SystemManagerProfileStatus.Active,
            profile.Availability == SystemManagerAvailabilityStatus.Available,
            profile.QualificationStatus == FlightQualificationStatus.Qualified,
            profile.QualificationExpiresAt,
            profile.Version);
    }

    public async Task<SurveyApprovalPrimaryAssignmentReference?>
        GetActivePrimaryAssignmentForApprovalAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default)
    {
        var exists = await SurveyApprovalRowLock.ExistsAsync(
            context,
            """
            SELECT id
            FROM identity.farm_manager_assignments
            WHERE tenant_id = @tenant_id
              AND farm_id = @farm_id
              AND ended_at IS NULL
            FOR SHARE
            """,
            cancellationToken,
            SurveyApprovalRowLock.Id("tenant_id", tenantId),
            SurveyApprovalRowLock.Id("farm_id", farmId));

        if (!exists)
        {
            return null;
        }

        var assignment = await context.FarmManagerAssignments
            .AsNoTracking()
            .SingleAsync(
                item => item.TenantId == tenantId &&
                        item.FarmId == farmId &&
                        item.EndedAt == null,
                cancellationToken);

        return new SurveyApprovalPrimaryAssignmentReference(
            assignment.Id,
            assignment.TenantId,
            assignment.FarmId,
            assignment.SystemManagerProfileId,
            assignment.Version);
    }

    public async Task<SurveyApprovalFarmBaselineReference>
        GetFarmBaselineForApprovalAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default)
    {
        var exists = await SurveyApprovalRowLock.ExistsAsync(
            context,
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
            SurveyApprovalRowLock.Id("tenant_id", tenantId),
            SurveyApprovalRowLock.Id("farm_id", farmId));

        if (!exists)
        {
            return new SurveyApprovalFarmBaselineReference(
                tenantId,
                farmId,
                CurrentPublishedBaseMapVersionId: null,
                ConfirmedActivePoleCount: null);
        }

        var baseMap = await context.FarmBaseMapVersions
            .AsNoTracking()
            .SingleAsync(
                map => map.TenantId == tenantId &&
                       map.FarmId == farmId &&
                       map.Status == FarmBaseMapStatus.Published,
                cancellationToken);
        var sourceOrder = await context.SurveyOrders
            .AsNoTracking()
            .SingleAsync(
                order => order.Id == baseMap.SourceSurveyOrderId,
                cancellationToken);

        return new SurveyApprovalFarmBaselineReference(
            tenantId,
            farmId,
            baseMap.Id,
            sourceOrder.ConfirmedSurveyPoleCount?.Value);
    }

    public async Task<SurveyApprovalPreviousOrderReference?>
        GetPreviousCompatibleOrderForApprovalAsync(
            Guid tenantId,
            Guid farmId,
            Guid surveyServiceId,
            CancellationToken cancellationToken = default)
    {
        var exists = await SurveyApprovalRowLock.ExistsAsync(
            context,
            """
            SELECT survey_order.id
            FROM survey.survey_orders AS survey_order
            JOIN survey.survey_results AS survey_result
              ON survey_result.survey_order_id = survey_order.id
             AND survey_result.tenant_id = survey_order.tenant_id
             AND survey_result.farm_id = survey_order.farm_id
            WHERE survey_order.tenant_id = @tenant_id
              AND survey_order.farm_id = @farm_id
              AND survey_order.survey_service_id = @service_id
              AND survey_order.status = 'COMPLETED'::system.survey_order_status
              AND survey_result.status = 'PUBLISHED'::system.survey_result_status
            ORDER BY survey_result.published_at DESC, survey_order.id DESC
            LIMIT 1
            FOR SHARE OF survey_order, survey_result
            """,
            cancellationToken,
            SurveyApprovalRowLock.Id("tenant_id", tenantId),
            SurveyApprovalRowLock.Id("farm_id", farmId),
            SurveyApprovalRowLock.Id("service_id", surveyServiceId));

        if (!exists)
        {
            return null;
        }

        var previous = await (
                from order in context.SurveyOrders.AsNoTracking()
                join result in context.SurveyResults.AsNoTracking()
                    on new { order.Id, order.TenantId, order.FarmId }
                    equals new
                    {
                        Id = result.SurveyOrderId,
                        result.TenantId,
                        result.FarmId
                    }
                where order.TenantId == tenantId &&
                      order.FarmId == farmId &&
                      order.SurveyServiceId == surveyServiceId &&
                      order.Status == SurveyOrderStatus.Completed &&
                      result.Status == SurveyResultStatus.Published
                orderby result.PublishedAt descending, order.Id descending
                select new { OrderId = order.Id, result.PublishedAt })
            .FirstAsync(cancellationToken);

        return new SurveyApprovalPreviousOrderReference(
            previous.OrderId,
            previous.PublishedAt!.Value);
    }
}
