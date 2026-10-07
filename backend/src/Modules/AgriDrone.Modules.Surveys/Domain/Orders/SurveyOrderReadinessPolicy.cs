namespace AgriDrone.Modules.Surveys.Domain;

public static class SurveyOrderReadinessPolicy
{
    public static SurveyOrderReadinessDecision Evaluate(
        SurveyOrderReadinessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var failures = new List<SurveyOrderReadinessFailure>();

        var expectedOrderStatus = snapshot.Purpose switch
        {
            SurveyOperationPurpose.BaselineMapping => SurveyOrderStatus.BaselineReady,
            SurveyOperationPurpose.PlantHealth or
            SurveyOperationPurpose.HarvestReadiness =>
                SurveyOrderStatus.ReadyForPaidService,
            _ => throw new ArgumentOutOfRangeException(
                nameof(snapshot),
                snapshot.Purpose,
                "Unsupported survey operation purpose.")
        };

        if (snapshot.OrderStatus != expectedOrderStatus)
        {
            failures.Add(SurveyOrderReadinessFailure.OrderNotEligible);
        }

        if (snapshot.Purpose == SurveyOperationPurpose.BaselineMapping &&
            !snapshot.RequiresBaselineMapping)
        {
            failures.Add(SurveyOrderReadinessFailure.BaselineMappingNotRequired);
        }

        if (!snapshot.HasApprovedFarmBoundary)
        {
            failures.Add(SurveyOrderReadinessFailure.BoundaryNotApproved);
        }

        if (!snapshot.IsScopeConfirmed)
        {
            failures.Add(SurveyOrderReadinessFailure.ScopeNotConfirmed);
        }

        var expectedAppointmentPurpose = snapshot.Purpose switch
        {
            SurveyOperationPurpose.BaselineMapping =>
                SurveyAppointmentPurpose.BaselineMapping,
            SurveyOperationPurpose.PlantHealth or
            SurveyOperationPurpose.HarvestReadiness =>
                SurveyAppointmentPurpose.PaidService,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot))
        };

        if (snapshot.AppointmentPurpose != expectedAppointmentPurpose)
        {
            failures.Add(SurveyOrderReadinessFailure.AppointmentPurposeMismatch);
        }

        if (snapshot.AppointmentStatus != SurveyAppointmentStatus.Confirmed)
        {
            failures.Add(SurveyOrderReadinessFailure.AppointmentNotConfirmed);
        }

        if (!snapshot.HasActiveQualifiedPrimaryManager)
        {
            failures.Add(SurveyOrderReadinessFailure.PrimaryManagerNotReady);
        }

        if (!snapshot.SafetyChecksSatisfied)
        {
            failures.Add(SurveyOrderReadinessFailure.SafetyChecksNotSatisfied);
        }

        if (snapshot.Purpose is SurveyOperationPurpose.PlantHealth or
            SurveyOperationPurpose.HarvestReadiness)
        {
            EvaluatePaidService(snapshot, failures);
        }

        return new SurveyOrderReadinessDecision(failures.Count == 0, failures);
    }

    private static void EvaluatePaidService(
        SurveyOrderReadinessSnapshot snapshot,
        List<SurveyOrderReadinessFailure> failures)
    {
        if (!snapshot.HasPublishedFarmBaseMap)
        {
            failures.Add(SurveyOrderReadinessFailure.FarmBaseMapNotPublished);
        }

        if (!snapshot.HasConfirmedPoleCount)
        {
            failures.Add(SurveyOrderReadinessFailure.PoleCountNotConfirmed);
        }

        if (!snapshot.HasPriceSnapshot)
        {
            failures.Add(SurveyOrderReadinessFailure.PriceNotConfirmed);
        }

        if (snapshot.PaymentStatus != SurveyPaymentStatus.Confirmed)
        {
            failures.Add(SurveyOrderReadinessFailure.PaymentNotConfirmed);
        }

        if (snapshot.HasPendingPriceAdjustment)
        {
            failures.Add(SurveyOrderReadinessFailure.PriceAdjustmentPending);
        }
    }
}
