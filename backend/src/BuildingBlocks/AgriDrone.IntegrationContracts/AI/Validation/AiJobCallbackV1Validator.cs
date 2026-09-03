using AgriDrone.IntegrationContracts.Media;
using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.IntegrationContracts.AI.Validation;

public static class AiJobCallbackV1Validator
{
    public static IReadOnlyList<string> Validate(
        AiJobCallbackV1? callback)
    {
        var errors = new List<string>();

        if (callback is null)
        {
            errors.Add("Callback is required.");
            return errors;
        }

        if (callback.JobId == Guid.Empty)
        {
            errors.Add("JobId is required.");
        }

        if (callback.TenantId == Guid.Empty)
        {
            errors.Add("TenantId is required.");
        }

        if (callback.CorrelationId == Guid.Empty)
        {
            errors.Add("CorrelationId is required.");
        }

        if (string.IsNullOrWhiteSpace(callback.ExternalJobId))
        {
            errors.Add("ExternalJobId is required.");
        }
        else if (callback.ExternalJobId.Length >
                 IntegrationContractLimits.MaximumExternalJobIdLength)
        {
            errors.Add("ExternalJobId is too long.");
        }

        if (!AiJobStatuses.IsSupported(callback.Status))
        {
            errors.Add("Status is not supported.");
        }

        if (callback.AttemptNumber <= 0)
        {
            errors.Add("AttemptNumber must be greater than zero.");
        }

        if (callback.SequenceNumber <= 0)
        {
            errors.Add("SequenceNumber must be greater than zero.");
        }

        if (callback.ProgressPercent < 0 ||
            callback.ProgressPercent > 100)
        {
            errors.Add("ProgressPercent must be in the range [0, 100].");
        }

        if (callback.OccurredAt == default)
        {
            errors.Add("OccurredAt is required.");
        }
        else if (callback.OccurredAt.Offset != TimeSpan.Zero)
        {
            errors.Add("OccurredAt must use the UTC offset.");
        }

        ValidateStatusSpecificFields(callback, errors);
        ValidateOutputs(callback.Outputs, errors);

        return errors;
    }

    private static void ValidateStatusSpecificFields(
        AiJobCallbackV1 callback,
        List<string> errors)
    {
        if (callback.Status == AiJobStatuses.Completed)
        {
            if (callback.ProgressPercent != 100)
            {
                errors.Add(
                    "ProgressPercent must be 100 for a completed job.");
            }

            if (callback.Outputs is null || callback.Outputs.Count == 0)
            {
                errors.Add(
                    "Outputs must contain at least one item for a completed job.");
            }
        }

        if (callback.Status == AiJobStatuses.Failed)
        {
            if (string.IsNullOrWhiteSpace(callback.ErrorCode))
            {
                errors.Add("ErrorCode is required for a failed job.");
            }

            if (string.IsNullOrWhiteSpace(callback.ErrorMessage))
            {
                errors.Add("ErrorMessage is required for a failed job.");
            }
        }

        if (callback.ErrorCode?.Length >
            IntegrationContractLimits.MaximumErrorCodeLength)
        {
            errors.Add("ErrorCode is too long.");
        }

        if (callback.ErrorMessage?.Length >
            IntegrationContractLimits.MaximumErrorMessageLength)
        {
            errors.Add("ErrorMessage is too long.");
        }
    }

    private static void ValidateOutputs(
        IReadOnlyList<AiJobOutputV1>? outputs,
        List<string> errors)
    {
        if (outputs is null)
        {
            errors.Add("Outputs is required.");
            return;
        }

        if (outputs.Count > IntegrationContractLimits.MaximumAiOutputCount)
        {
            errors.Add(
                $"Outputs cannot contain more than {IntegrationContractLimits.MaximumAiOutputCount} items.");
            return;
        }

        for (var index = 0; index < outputs.Count; index++)
        {
            var output = outputs[index];
            var path = $"Outputs[{index}]";

            if (output is null)
            {
                errors.Add($"{path} is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(output.OutputType))
            {
                errors.Add($"{path}.OutputType is required.");
            }
            else if (output.OutputType.Length >
                     IntegrationContractLimits.MaximumOutputTypeLength)
            {
                errors.Add($"{path}.OutputType is too long.");
            }

            if (string.IsNullOrWhiteSpace(output.StorageUri) ||
                !Uri.TryCreate(
                    output.StorageUri,
                    UriKind.Absolute,
                    out _))
            {
                errors.Add(
                    $"{path}.StorageUri must be an absolute URI.");
            }

            if (output.FileSizeBytes is <= 0)
            {
                errors.Add(
                    $"{path}.FileSizeBytes must be greater than zero when provided.");
            }

            var hasAlgorithm =
                !string.IsNullOrWhiteSpace(output.ChecksumAlgorithm);
            var hasChecksum =
                !string.IsNullOrWhiteSpace(output.Checksum);

            if (hasAlgorithm != hasChecksum)
            {
                errors.Add(
                    $"{path}.ChecksumAlgorithm and Checksum must be provided together.");
            }

            if (hasAlgorithm &&
                !ChecksumAlgorithms.IsSupported(
                    output.ChecksumAlgorithm))
            {
                errors.Add(
                    $"{path}.ChecksumAlgorithm must be MD5 or SHA256.");
            }
        }
    }
}