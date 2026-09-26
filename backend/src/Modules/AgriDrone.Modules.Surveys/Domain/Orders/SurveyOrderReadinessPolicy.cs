namespace AgriDrone.Modules.Surveys.Domain;

public static class SurveyOrderReadinessPolicy
{
    public static SurveyOrderReadinessDecision Evaluate(
        SurveyOrderReadinessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var failures = new List<SurveyOrderReadinessFailure>();

        if (snapshot.OrderStatus is SurveyOrderStatus.Cancelled or
            SurveyOrderStatus.Completed)
        {
            failures.Add(SurveyOrderReadinessFailure.OrderNotEligible);
        }

        if (!snapshot.IsScopeConfirmed)
        {
            failures.Add(SurveyOrderReadinessFailure.ScopeNotConfirmed);
        }

        if (snapshot.AppointmentStatus != SurveyAppointmentStatus.Confirmed)
        {
            failures.Add(SurveyOrderReadinessFailure.AppointmentNotConfirmed);
        }

        if (snapshot.PaymentStatus != SurveyPaymentStatus.Confirmed)
        {
            failures.Add(SurveyOrderReadinessFailure.PaymentNotConfirmed);
        }

        if (!snapshot.HasActiveQualifiedPrimaryManager)
        {
            failures.Add(SurveyOrderReadinessFailure.PrimaryManagerNotReady);
        }

        if (snapshot.HasPendingPriceAdjustment)
        {
            failures.Add(SurveyOrderReadinessFailure.PriceAdjustmentPending);
        }

        return new SurveyOrderReadinessDecision(failures.Count == 0, failures);
    }
}
