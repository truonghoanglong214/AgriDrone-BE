namespace AgriDrone.IntegrationContracts.Messaging;

public static class IntegrationContractLimits
{
    public const int MaximumMessageBodyBytes = 4 * 1024 * 1024;

    public const int MaximumEventTypeLength = 128;

    public const int MaximumAlgorithmVersionLength = 128;

    public const int MaximumParameterCount = 64;

    public const int MaximumParameterKeyLength = 128;

    public const int MaximumParameterValueLength = 1024;

    public const int MaximumMappingCandidateCount = 10_000;

    public const int MaximumPlantMappingCount = 10_000;
}
