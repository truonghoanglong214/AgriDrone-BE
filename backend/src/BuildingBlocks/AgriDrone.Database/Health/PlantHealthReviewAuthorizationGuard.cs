using AgriDrone.Modules.Surveys.Application.Abstractions.HealthReview;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Health;

internal sealed class PlantHealthReviewAuthorizationGuard(
    SurveyResultPublicationDbContext db, ISystemManagerAccessService access) : IPlantHealthReviewAuthorizationGuard
{
    private sealed record AuthorizationRow(Guid AssignmentId);

    public async Task<bool> AuthorizeAsync(Guid tenantId, Guid farmId, Guid actorId, CancellationToken token)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Authorization requires a transaction.");
        var permission = await access.ResolveFarmAccessAsync(farmId, token);
        if (!permission.IsAllowed || permission.TenantId != tenantId || permission.FarmId != farmId) return false;
        // SHARE conflicts with non-key UPDATE too: revocation, suspension and availability changes
        // cannot pass the commit point while this review is authorized.
        var row = await db.Database.SqlQuery<AuthorizationRow>($"""
            SELECT a.id AS "AssignmentId"
            FROM identity.users u
            JOIN identity.system_manager_profiles p ON p.user_id = u.id
            JOIN identity.farm_manager_assignments a ON a.system_manager_profile_id = p.id
            JOIN farm.farms f ON f.id = a.farm_id AND f.tenant_id = a.tenant_id
            WHERE u.id = {actorId} AND u.status = 'ACTIVE'
              AND p.status = 'ACTIVE' AND p.availability = 'AVAILABLE'
              AND a.farm_id = {farmId} AND a.tenant_id = {tenantId} AND a.ended_at IS NULL
              AND f.status = 'ACTIVE' AND f.deleted_at IS NULL
            FOR SHARE OF u, p, a, f
            """).SingleOrDefaultAsync(token);
        return row is not null;
    }
}
