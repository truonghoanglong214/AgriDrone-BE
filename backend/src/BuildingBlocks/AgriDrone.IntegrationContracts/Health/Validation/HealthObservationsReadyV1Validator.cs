using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.IntegrationContracts.Health.Validation;

public static class HealthObservationsReadyV1Validator
{
    public static IReadOnlyList<string> Validate(
        HealthObservationsReadyV1? payload)
    {
        var errors = new List<string>();

        if (payload is null)
        {
            errors.Add("Payload is required.");
            return errors;
        }

        if (payload.HandoffId == Guid.Empty)
        {
            errors.Add("HandoffId is required.");
        }

        if (payload.MissionId == Guid.Empty)
        {
            errors.Add("MissionId is required.");
        }

        if (payload.FarmId == Guid.Empty)
        {
            errors.Add("FarmId is required.");
        }

        if (payload.ZoneId == Guid.Empty)
        {
            errors.Add("ZoneId is required.");
        }

        if (payload.JobId == Guid.Empty)
        {
            errors.Add("JobId is required.");
        }

        if (payload.ModelVersionId == Guid.Empty)
        {
            errors.Add("ModelVersionId is required.");
        }

        if (string.IsNullOrWhiteSpace(payload.ModelVersion))
        {
            errors.Add("ModelVersion is required.");
        }

        var hasThresholdId = payload.ThresholdProfileId.HasValue;
        var hasThresholdVersion =
            !string.IsNullOrWhiteSpace(payload.ThresholdProfileVersion);

        if (hasThresholdId != hasThresholdVersion)
        {
            errors.Add(
                "ThresholdProfileId and ThresholdProfileVersion must be provided together.");
        }

        ValidateObservations(payload.Observations, errors);

        return errors;
    }

    private static void ValidateObservations(
        IReadOnlyList<HealthObservationV1>? observations,
        List<string> errors)
    {
        if (observations is null || observations.Count == 0)
        {
            errors.Add(
                "Observations must contain at least one item.");
            return;
        }

        if (observations.Count >
            IntegrationContractLimits.MaximumHealthObservationCount)
        {
            errors.Add(
                $"Observations cannot contain more than {IntegrationContractLimits.MaximumHealthObservationCount} items.");
            return;
        }

        var observationVersions = new HashSet<(Guid, int)>();

        for (var index = 0; index < observations.Count; index++)
        {
            var item = observations[index];
            var path = $"Observations[{index}]";

            if (item is null)
            {
                errors.Add($"{path} is required.");
                continue;
            }

            if (item.ObservationId == Guid.Empty)
            {
                errors.Add($"{path}.ObservationId is required.");
            }

            if (item.ObservationVersion <= 0)
            {
                errors.Add(
                    $"{path}.ObservationVersion must be greater than zero.");
            }

            if (item.ObservationId != Guid.Empty &&
                item.ObservationVersion > 0 &&
                !observationVersions.Add(
                    (item.ObservationId, item.ObservationVersion)))
            {
                errors.Add(
                    $"{path} contains a duplicated observation version.");
            }

            if (item.PlantId == Guid.Empty)
            {
                errors.Add($"{path}.PlantId is required.");
            }

            if (item.MediaAssetId == Guid.Empty)
            {
                errors.Add($"{path}.MediaAssetId is required.");
            }

            if (string.IsNullOrWhiteSpace(item.EvidenceStorageUri) ||
                !Uri.TryCreate(
                    item.EvidenceStorageUri,
                    UriKind.Absolute,
                    out _))
            {
                errors.Add(
                    $"{path}.EvidenceStorageUri must be an absolute URI.");
            }

            if (item.ObservedAt == default ||
                item.ObservedAt.Offset != TimeSpan.Zero)
            {
                errors.Add(
                    $"{path}.ObservedAt must be a non-default UTC timestamp.");
            }

            if (string.IsNullOrWhiteSpace(item.ConditionCode))
            {
                errors.Add($"{path}.ConditionCode is required.");
            }

            if (string.IsNullOrWhiteSpace(item.HealthLevelCode))
            {
                errors.Add($"{path}.HealthLevelCode is required.");
            }

            if (item.SeverityPercent < 0 ||
                item.SeverityPercent > 100)
            {
                errors.Add(
                    $"{path}.SeverityPercent must be in the range [0, 100].");
            }

            if (item.Confidence < 0 || item.Confidence > 1)
            {
                errors.Add(
                    $"{path}.Confidence must be in the range [0, 1].");
            }
        }
    }
}