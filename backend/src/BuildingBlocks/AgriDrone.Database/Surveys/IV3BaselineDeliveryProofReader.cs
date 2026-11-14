using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

/// <summary>
/// Reads the current baseline's durable V3 release evidence. A V3-06 adapter
/// must use the supplied context's connection and transaction, and lock release
/// and withdrawal facts when it is a SurveyApprovalDbContext. Catalogue flags,
/// Completed orders and Published legacy results are not release evidence.
/// Null means the release evidence source is unavailable or has no proof.
/// </summary>
internal interface IV3BaselineDeliveryProofReader
{
    Task<V3BaselineDeliveryProof?> ReadCurrentAsync(
        DbContext context,
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);
}

internal sealed record V3BaselineDeliveryProof(
    Guid TenantId,
    Guid FarmId,
    Guid BaseMapVersionId,
    Guid SourceOrderId,
    Guid ReleaseId,
    bool IsReleased,
    bool FinanciallySettledAtRelease,
    bool IsWithdrawn)
{
    internal bool AllowsFollowUp(Guid tenantId, Guid farmId) =>
        tenantId != Guid.Empty && farmId != Guid.Empty &&
        TenantId == tenantId && FarmId == farmId &&
        BaseMapVersionId != Guid.Empty && SourceOrderId != Guid.Empty &&
        ReleaseId != Guid.Empty && IsReleased &&
        FinanciallySettledAtRelease && !IsWithdrawn;
}

/// <summary>
/// V3-02 has no durable V3 release/withdrawal store. Fail closed until V3-06
/// supplies a transactional adapter; never manufacture delivery from statuses.
/// </summary>
internal sealed class UnavailableV3BaselineDeliveryProofReader : IV3BaselineDeliveryProofReader
{
    public Task<V3BaselineDeliveryProof?> ReadCurrentAsync(
        DbContext context,
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<V3BaselineDeliveryProof?>(null);
    }
}
