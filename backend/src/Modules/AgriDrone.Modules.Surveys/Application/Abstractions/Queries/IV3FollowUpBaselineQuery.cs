namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

public sealed record V3FollowUpBaselineEligibilityResult(
    bool IsEligible,
    Guid? BaseMapVersionId,
    Guid? SourceOrderId,
    int? ConfirmedActivePoleCount,
    string? RejectionReason = null)
{
    public static V3FollowUpBaselineEligibilityResult Eligible(
        Guid baseMapVersionId,
        Guid sourceOrderId,
        int confirmedActivePoleCount) =>
        new(true, baseMapVersionId, sourceOrderId, confirmedActivePoleCount);

    public static V3FollowUpBaselineEligibilityResult Ineligible(string reason) =>
        new(false, null, null, null, reason);
}

public interface IV3FollowUpBaselineQuery
{
    /// <summary>
    /// Checks current durable release/withdrawal evidence for intake. Missing
    /// evidence fails closed. Approval must repeat the same eligibility checks
    /// using its own transaction and locks; intake is not an approval receipt.
    /// </summary>
    Task<V3FollowUpBaselineEligibilityResult> GetEligibleAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);
}
