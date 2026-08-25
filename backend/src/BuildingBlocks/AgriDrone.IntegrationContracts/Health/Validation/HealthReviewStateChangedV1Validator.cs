namespace AgriDrone.IntegrationContracts.Health.Validation;

public static class HealthReviewStateChangedV1Validator
{
    public static IReadOnlyList<string> Validate(
        HealthReviewStateChangedV1? payload)
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

        if (payload.ReviewVersion <= 0)
        {
            errors.Add("ReviewVersion must be greater than zero.");
        }

        if (!HealthReviewStates.IsSupported(payload.State))
        {
            errors.Add("State is not supported.");
        }

        if (payload.TotalObservations < 0 ||
            payload.PendingReviews < 0 ||
            payload.AwaitingFieldVerification < 0 ||
            payload.ResolvedReviews < 0)
        {
            errors.Add("Review counters cannot be negative.");
        }

        var counterTotal =
            payload.PendingReviews +
            payload.AwaitingFieldVerification +
            payload.ResolvedReviews;

        if (counterTotal != payload.TotalObservations)
        {
            errors.Add(
                "Review counters must equal TotalObservations.");
        }

        if (payload.State == HealthReviewStates.Resolved &&
            (payload.PendingReviews != 0 ||
             payload.AwaitingFieldVerification != 0))
        {
            errors.Add(
                "A resolved review cannot contain pending or field-verification items.");
        }

        if (payload.ChangedAt == default ||
            payload.ChangedAt.Offset != TimeSpan.Zero)
        {
            errors.Add(
                "ChangedAt must be a non-default UTC timestamp.");
        }

        return errors;
    }
}