namespace AgriDrone.Modules.Surveys.Domain;

public static class PolicyDomainErrorCodes
{
    public const string WindowAlreadyClosed =
        "POLICY_WINDOW_ALREADY_CLOSED";
    public const string InvalidEffectiveWindow =
        "POLICY_INVALID_EFFECTIVE_WINDOW";
    public const string InactivePolicy =
        "POLICY_INACTIVE";
    public const string ActivationDisabled =
        "POLICY_ACTIVATION_DISABLED";
}
