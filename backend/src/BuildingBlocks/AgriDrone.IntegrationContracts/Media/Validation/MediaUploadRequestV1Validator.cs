using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.IntegrationContracts.Media.Validation;

public static class MediaUploadRequestV1Validator
{
    public static IReadOnlyList<string> Validate(
        MediaUploadRequestV1? request)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Request is required.");
            return errors;
        }

        if (request.OperationId == Guid.Empty)
        {
            errors.Add("OperationId is required.");
        }

        if (request.TenantId == Guid.Empty)
        {
            errors.Add("TenantId is required.");
        }

        if (request.MissionId == Guid.Empty)
        {
            errors.Add("MissionId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            errors.Add("FileName is required.");
        }
        else if (request.FileName.Length >
                 IntegrationContractLimits.MaximumFileNameLength)
        {
            errors.Add(
                $"FileName cannot exceed {IntegrationContractLimits.MaximumFileNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(request.MimeType))
        {
            errors.Add("MimeType is required.");
        }
        else if (request.MimeType.Length >
                 IntegrationContractLimits.MaximumMimeTypeLength)
        {
            errors.Add(
                $"MimeType cannot exceed {IntegrationContractLimits.MaximumMimeTypeLength} characters.");
        }

        if (request.FileSizeBytes <= 0)
        {
            errors.Add("FileSizeBytes must be greater than zero.");
        }

        ValidateChecksum(request.Checksum, errors);

        return errors;
    }

    private static void ValidateChecksum(
        MediaChecksumV1? checksum,
        List<string> errors)
    {
        if (checksum is null)
        {
            errors.Add("Checksum is required.");
            return;
        }

        if (!ChecksumAlgorithms.IsSupported(checksum.Algorithm))
        {
            errors.Add("Checksum.Algorithm must be MD5 or SHA256.");
        }

        if (string.IsNullOrWhiteSpace(checksum.Value))
        {
            errors.Add("Checksum.Value is required.");
        }
        else if (checksum.Value.Length >
                 IntegrationContractLimits.MaximumChecksumLength)
        {
            errors.Add(
                $"Checksum.Value cannot exceed {IntegrationContractLimits.MaximumChecksumLength} characters.");
        }
    }
}