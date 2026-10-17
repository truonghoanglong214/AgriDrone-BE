namespace AgriDrone.Modules.Surveys.Domain;

/// <summary>
/// Locks the MVP commercial surface to Plant Health. Harvest Readiness data is
/// retained only for historical compatibility until a separate Stretch gate is
/// approved.
/// </summary>
public static class SurveyServiceMvpPolicy
{
    public static bool IsSellable(SurveyServiceType serviceType) =>
        serviceType == SurveyServiceType.PlantHealth;
}
