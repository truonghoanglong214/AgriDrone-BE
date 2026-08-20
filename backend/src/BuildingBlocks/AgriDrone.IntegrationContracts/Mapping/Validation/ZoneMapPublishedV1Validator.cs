using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.IntegrationContracts.Mapping.Validation;

public static class ZoneMapPublishedV1Validator
{
    public static IReadOnlyList<string> Validate(ZoneMapPublishedV1? payload)
    {
        var errors = new List<string>();

        if (payload is null)
        {
            errors.Add("Payload is required.");
            return errors;
        }

        if (payload.SourceMessageId == Guid.Empty)
        {
            errors.Add("SourceMessageId is required.");
        }

        if (payload.ApprovalId == Guid.Empty)
        {
            errors.Add("ApprovalId is required.");
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

        if (payload.MapVersionId == Guid.Empty)
        {
            errors.Add("MapVersionId is required.");
        }

        if (payload.VersionNumber <= 0)
        {
            errors.Add("VersionNumber must be greater than zero.");
        }

        if (payload.PublishedAt == default)
        {
            errors.Add("PublishedAt is required.");
        }
        else if (payload.PublishedAt.Offset != TimeSpan.Zero)
        {
            errors.Add("PublishedAt must use the UTC offset.");
        }

        ValidatePlantMappings(payload.PlantMappings, errors);

        return errors;
    }

    private static void ValidatePlantMappings(
        IReadOnlyList<PlantMappingV1>? plantMappings,
        List<string> errors)
    {
        if (plantMappings is null || plantMappings.Count == 0)
        {
            errors.Add("PlantMappings must contain at least one item.");
            return;
        }

        if (plantMappings.Count >
            IntegrationContractLimits.MaximumPlantMappingCount)
        {
            errors.Add(
                $"PlantMappings cannot contain more than {IntegrationContractLimits.MaximumPlantMappingCount} items.");
            return;
        }

        var observationIds = new HashSet<Guid>();
        var plantIds = new HashSet<Guid>();

        for (var index = 0; index < plantMappings.Count; index++)
        {
            var mapping = plantMappings[index];
            var path = $"PlantMappings[{index}]";

            if (mapping is null)
            {
                errors.Add($"{path} is required.");
                continue;
            }

            if (mapping.ObservationId == Guid.Empty)
            {
                errors.Add($"{path}.ObservationId is required.");
            }
            else if (!observationIds.Add(mapping.ObservationId))
            {
                errors.Add($"{path}.ObservationId is duplicated.");
            }

            if (mapping.PlantId == Guid.Empty)
            {
                errors.Add($"{path}.PlantId is required.");
            }
            else if (!plantIds.Add(mapping.PlantId))
            {
                errors.Add($"{path}.PlantId is used by more than one observation.");
            }
        }
    }
}
