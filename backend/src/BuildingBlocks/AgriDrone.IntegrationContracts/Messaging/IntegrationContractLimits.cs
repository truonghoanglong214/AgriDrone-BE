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

    public const int MaximumAiInputCount = 1_000;

    public const int MaximumAiOutputCount = 128;

    public const int MaximumAiModelVersionLength = 128;

    public const int MaximumExternalJobIdLength = 128;

    public const int MaximumStorageUriLength = 2_048;

    public const int MaximumCallbackUrlLength = 2_048;

    public const int MaximumMimeTypeLength = 128;

    public const int MaximumFileNameLength = 255;

    public const int MaximumChecksumAlgorithmLength = 32;

    public const int MaximumChecksumLength = 256;

    public const int MaximumOutputTypeLength = 128;

    public const int MaximumErrorCodeLength = 128;

    public const int MaximumErrorMessageLength = 4_096;

    public const int MaximumHealthObservationCount = 10_000;

    public const int MaximumConditionCodeLength = 128;

    public const int MaximumHealthLevelCodeLength = 128;
}