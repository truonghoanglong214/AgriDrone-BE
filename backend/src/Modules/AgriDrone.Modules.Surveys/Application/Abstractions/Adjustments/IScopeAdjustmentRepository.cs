using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;

public interface IScopeAdjustmentRepository
{
    Task<ScopeAdjustment?> GetAsync(
        Guid surveyOrderId,
        Guid adjustmentId,
        CancellationToken cancellationToken = default);

    Task<ScopeAdjustment?> GetOpenAdjustmentAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default);

    void Add(ScopeAdjustment adjustment);
}
