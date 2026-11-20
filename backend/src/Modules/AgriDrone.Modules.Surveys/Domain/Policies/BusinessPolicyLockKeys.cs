namespace AgriDrone.Modules.Surveys.Domain;

public static class BusinessPolicyLockKeys
{
    // Stable PostgreSQL advisory-lock namespace for the V3 commercial-policy
    // activation guard. Approval uses the shared form; every publisher uses
    // the exclusive form in the same transaction.
    public const long ActivationGuard = 0x4147524956330001L;
}
