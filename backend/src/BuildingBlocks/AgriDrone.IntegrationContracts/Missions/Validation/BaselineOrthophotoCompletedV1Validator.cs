namespace AgriDrone.IntegrationContracts.Missions.Validation;

public static class BaselineOrthophotoCompletedV1Validator
{
    public static IReadOnlyList<string> Validate(BaselineOrthophotoCompletedV1? payload)
    {
        var errors = new List<string>();
        if (payload is null)
        {
            errors.Add("Payload is required.");
            return errors;
        }

        if (payload.SurveyOrderId == Guid.Empty)
        {
            errors.Add("SurveyOrderId is required.");
        }

        if (payload.MissionId == Guid.Empty)
        {
            errors.Add("MissionId is required.");
        }

        if (payload.TenantId == Guid.Empty)
        {
            errors.Add("TenantId is required.");
        }

        if (payload.FarmId == Guid.Empty)
        {
            errors.Add("FarmId is required.");
        }

        if (payload.CompletedAt == default)
        {
            errors.Add("CompletedAt is required.");
        }
        else if (payload.CompletedAt.Offset != TimeSpan.Zero)
        {
            errors.Add("CompletedAt must use the UTC offset.");
        }

        return errors;
    }
}
