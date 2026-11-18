namespace AgriDrone.Modules.Surveys.Domain;

public interface ISurveyComboDiscountPolicyRepository
{
    Task<SurveyComboDiscountPolicy?> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task<SurveyComboDiscountPolicy?> GetCurrentForUpdateAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyComboDiscountPolicy>> GetHistoryAsync(
        CancellationToken cancellationToken = default);

    void Add(SurveyComboDiscountPolicy policy);
}
