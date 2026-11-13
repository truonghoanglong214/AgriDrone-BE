namespace AgriDrone.Modules.Surveys.Domain;

public interface ISurveyDepositPolicyRepository
{
    Task<LegacySurveyDepositPolicy?> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task<LegacySurveyDepositPolicy?> GetCurrentForUpdateAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegacySurveyDepositPolicy>> GetHistoryAsync(
        CancellationToken cancellationToken = default);

    void Add(LegacySurveyDepositPolicy policy);
}
