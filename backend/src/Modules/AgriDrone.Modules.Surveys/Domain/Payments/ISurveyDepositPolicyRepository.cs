namespace AgriDrone.Modules.Surveys.Domain;

public interface ISurveyDepositPolicyRepository
{
    Task<SurveyDepositPolicy?> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task<SurveyDepositPolicy?> GetCurrentForUpdateAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyDepositPolicy>> GetHistoryAsync(
        CancellationToken cancellationToken = default);

    void Add(SurveyDepositPolicy policy);
}
