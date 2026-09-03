using AgriDrone.IntegrationContracts.Media;
using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.IntegrationContracts.AI.Validation;

public static class AiJobRequestV1Validator
{
    public static IReadOnlyList<string> Validate(AiJobRequestV1? request)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Request is required.");
            return errors;
        }

        ValidateHeader(request, errors);
        ValidateParameters(request.Parameters, errors);
        ValidateInputs(request.Inputs, errors);
        ValidateCallbackUrl(request.CallbackUrl, errors);

        return errors;
    }

    private static void ValidateHeader(
        AiJobRequestV1 request,
        List<string> errors)
    {
        if (request.JobId == Guid.Empty)
        {
            errors.Add("JobId is required.");
        }

        if (request.MissionId == Guid.Empty)
        {
            errors.Add("MissionId is required.");
        }

        if (request.TenantId == Guid.Empty)
        {
            errors.Add("TenantId is required.");
        }

        if (request.CorrelationId == Guid.Empty)
        {
            errors.Add("CorrelationId is required.");
        }

        if (!AiJobTypes.IsSupported(request.JobType))
        {
            errors.Add("JobType is not supported.");
        }

        if (request.AttemptNumber <= 0)
        {
            errors.Add("AttemptNumber must be greater than zero.");
        }

        if (request.ModelVersionId == Guid.Empty)
        {
            errors.Add("ModelVersionId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ModelVersion))
        {
            errors.Add("ModelVersion is required.");
        }
        else if (request.ModelVersion.Length >
                 IntegrationContractLimits.MaximumAiModelVersionLength)
        {
            errors.Add(
                $"ModelVersion cannot exceed {IntegrationContractLimits.MaximumAiModelVersionLength} characters.");
        }

        if (request.ThresholdProfileId == Guid.Empty)
        {
            errors.Add(
                "ThresholdProfileId cannot be an empty GUID when provided.");
        }

        var hasThresholdId = request.ThresholdProfileId.HasValue;
        var hasThresholdVersion =
            !string.IsNullOrWhiteSpace(request.ThresholdProfileVersion);

        if (hasThresholdId != hasThresholdVersion)
        {
            errors.Add(
                "ThresholdProfileId and ThresholdProfileVersion must be provided together.");
        }

        if (request.ThresholdProfileVersion?.Length >
            IntegrationContractLimits.MaximumAiModelVersionLength)
        {
            errors.Add(
                $"ThresholdProfileVersion cannot exceed {IntegrationContractLimits.MaximumAiModelVersionLength} characters.");
        }

        if (request.RequestedAt == default)
        {
            errors.Add("RequestedAt is required.");
        }
        else if (request.RequestedAt.Offset != TimeSpan.Zero)
        {
            errors.Add("RequestedAt must use the UTC offset.");
        }
    }

    private static void ValidateParameters(
        IReadOnlyDictionary<string, string>? parameters,
        List<string> errors)
    {
        if (parameters is null)
        {
            errors.Add("Parameters is required.");
            return;
        }

        if (parameters.Count > IntegrationContractLimits.MaximumParameterCount)
        {
            errors.Add(
                $"Parameters cannot contain more than {IntegrationContractLimits.MaximumParameterCount} items.");
        }

        foreach (var parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Key))
            {
                errors.Add("Parameters cannot contain an empty key.");
            }
            else if (parameter.Key.Length >
                     IntegrationContractLimits.MaximumParameterKeyLength)
            {
                errors.Add(
                    $"Parameter key '{parameter.Key}' is too long.");
            }

            if (parameter.Value is null)
            {
                errors.Add($"Parameters['{parameter.Key}'] cannot be null.");
            }
            else if (parameter.Value.Length >
                     IntegrationContractLimits.MaximumParameterValueLength)
            {
                errors.Add(
                    $"Parameters['{parameter.Key}'] is too long.");
            }
        }
    }

    private static void ValidateInputs(
        IReadOnlyList<AiJobInputV1>? inputs,
        List<string> errors)
    {
        if (inputs is null || inputs.Count == 0)
        {
            errors.Add("Inputs must contain at least one item.");
            return;
        }

        if (inputs.Count > IntegrationContractLimits.MaximumAiInputCount)
        {
            errors.Add(
                $"Inputs cannot contain more than {IntegrationContractLimits.MaximumAiInputCount} items.");
            return;
        }

        var mediaAssetIds = new HashSet<Guid>();

        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var path = $"Inputs[{index}]";

            if (input is null)
            {
                errors.Add($"{path} is required.");
                continue;
            }

            if (input.MediaAssetId == Guid.Empty)
            {
                errors.Add($"{path}.MediaAssetId is required.");
            }
            else if (!mediaAssetIds.Add(input.MediaAssetId))
            {
                errors.Add($"{path}.MediaAssetId is duplicated.");
            }

            if (!AiJobInputRoles.IsSupported(input.Role))
            {
                errors.Add($"{path}.Role is not supported.");
            }

            ValidateStorageUri(input.StorageUri, path, errors);

            if (string.IsNullOrWhiteSpace(input.MimeType))
            {
                errors.Add($"{path}.MimeType is required.");
            }
            else if (input.MimeType.Length >
                     IntegrationContractLimits.MaximumMimeTypeLength)
            {
                errors.Add($"{path}.MimeType is too long.");
            }

            if (input.FileSizeBytes <= 0)
            {
                errors.Add(
                    $"{path}.FileSizeBytes must be greater than zero.");
            }

            if (!ChecksumAlgorithms.IsSupported(
                    input.ChecksumAlgorithm))
            {
                errors.Add(
                    $"{path}.ChecksumAlgorithm must be MD5 or SHA256.");
            }

            if (string.IsNullOrWhiteSpace(input.Checksum))
            {
                errors.Add($"{path}.Checksum is required.");
            }
            else if (input.Checksum.Length >
                     IntegrationContractLimits.MaximumChecksumLength)
            {
                errors.Add($"{path}.Checksum is too long.");
            }
        }
    }

    private static void ValidateCallbackUrl(
        string? callbackUrl,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            errors.Add("CallbackUrl is required.");
            return;
        }

        if (callbackUrl.Length >
            IntegrationContractLimits.MaximumCallbackUrlLength)
        {
            errors.Add("CallbackUrl is too long.");
            return;
        }

        if (!Uri.TryCreate(
                callbackUrl,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add(
                "CallbackUrl must be an absolute HTTP or HTTPS URL.");
        }
    }

    private static void ValidateStorageUri(
        string? storageUri,
        string path,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(storageUri))
        {
            errors.Add($"{path}.StorageUri is required.");
            return;
        }

        if (storageUri.Length >
            IntegrationContractLimits.MaximumStorageUriLength)
        {
            errors.Add($"{path}.StorageUri is too long.");
            return;
        }

        if (!Uri.TryCreate(storageUri, UriKind.Absolute, out _))
        {
            errors.Add($"{path}.StorageUri must be an absolute URI.");
        }
    }
}