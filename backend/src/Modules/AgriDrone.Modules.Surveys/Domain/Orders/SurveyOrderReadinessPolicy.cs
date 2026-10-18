namespace AgriDrone.Modules.Surveys.Domain;

public static class SurveyOrderReadinessPolicy
{
    public static SurveyOrderReadinessDecision Evaluate(
        SurveyOrderReadinessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var failures = new List<SurveyOrderReadinessFailure>();

        if (snapshot.Purpose == SurveyOperationPurpose.HarvestReadiness)
        {
            return new SurveyOrderReadinessDecision(
                false,
                [SurveyOrderReadinessFailure.ServiceDisabled]);
        }

        var expectedOrderStatus = snapshot.Purpose switch
        {
            SurveyOperationPurpose.BaselineMapping => SurveyOrderStatus.BaselineReady,
            SurveyOperationPurpose.PlantHealth =>
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

        if (snapshot.Purpose == SurveyOperationPurpose.BaselineMapping)
        {
            if (!snapshot.HasFinalizedBoundaryCapture)
            {
                failures.Add(
                    SurveyOrderReadinessFailure.BoundaryCaptureNotFinalized);
            }

            if (!snapshot.HasApprovedBaselineRoute)
            {
                failures.Add(
                    SurveyOrderReadinessFailure.BaselineRouteNotApproved);
            }

            if (!snapshot.IsBaselineRouteCurrent)
            {
                failures.Add(SurveyOrderReadinessFailure.BaselineRouteStale);
            }

            if (!snapshot.IsBaselineRouteWithinSafetyInset)
            {
                failures.Add(
                    SurveyOrderReadinessFailure.BaselineRouteOutsideSafetyInset);
            }
        }

        if (!snapshot.HasApprovedFarmBoundary)
        {
            failures.Add(SurveyOrderReadinessFailure.BoundaryNotApproved);
        }

        if (!snapshot.IsScopeConfirmed)
        {
            failures.Add(SurveyOrderReadinessFailure.ScopeNotConfirmed);
        }

        if (!snapshot.HasValidWrittenDataUseConsent)
        {
            failures.Add(
                SurveyOrderReadinessFailure.WrittenConsentNotValid);
        }

        if (!snapshot.HasValidFlightPermission)
        {
            failures.Add(
                SurveyOrderReadinessFailure.FlightPermissionNotValid);
        }

        var expectedAppointmentPurpose = snapshot.Purpose switch
        {
            SurveyOperationPurpose.BaselineMapping =>
                SurveyAppointmentPurpose.BaselineMapping,
            SurveyOperationPurpose.PlantHealth =>
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

        if (!snapshot.IsAppointmentCurrent)
        {
            failures.Add(SurveyOrderReadinessFailure.AppointmentNotCurrent);
        }

        if (!snapshot.IsMissionPreparedForPurpose)
        {
            failures.Add(
                SurveyOrderReadinessFailure.MissionNotPreparedForPurpose);
        }

        if (!snapshot.IsMissionScheduledWithinAppointment)
        {
            failures.Add(SurveyOrderReadinessFailure.MissionScheduleMismatch);
        }

        if (!snapshot.IsDroneReady)
        {
            failures.Add(SurveyOrderReadinessFailure.DroneNotReady);
        }

        if (!snapshot.IsPreflightCompleted)
        {
            failures.Add(SurveyOrderReadinessFailure.PreflightNotCompleted);
        }

        if (!snapshot.HasActiveQualifiedPrimaryManager)
        {
            failures.Add(SurveyOrderReadinessFailure.PrimaryManagerNotReady);
        }

        if (!snapshot.IsPrimaryManagerAvailable)
        {
            failures.Add(SurveyOrderReadinessFailure.PrimaryManagerUnavailable);
        }

        if (!snapshot.SafetyChecksSatisfied)
        {
            failures.Add(SurveyOrderReadinessFailure.SafetyChecksNotSatisfied);
        }

        if (snapshot.Purpose == SurveyOperationPurpose.PlantHealth)
        {
            EvaluatePaidService(snapshot, failures);
        }

        if (snapshot.HasPendingFinancialSettlement)
        {
            failures.Add(
                SurveyOrderReadinessFailure.FinancialSettlementPending);
        }

        if (snapshot.HasUnapprovedExtraFlight)
        {
            failures.Add(SurveyOrderReadinessFailure.ExtraFlightNotApproved);
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

        if (snapshot.PaymentStatus == SurveyPaymentStatus.Refunded)
        {
            failures.Add(SurveyOrderReadinessFailure.PaymentRevoked);
        }
        else if (snapshot.PaymentStatus != SurveyPaymentStatus.Confirmed)
        {
            failures.Add(SurveyOrderReadinessFailure.PaymentNotConfirmed);
        }

        if (!snapshot.IsPaymentAmountAndCurrencyMatched)
        {
            failures.Add(
                SurveyOrderReadinessFailure.PaymentAmountOrCurrencyMismatch);
        }

        if (snapshot.HasPendingPriceAdjustment)
        {
            failures.Add(SurveyOrderReadinessFailure.PriceAdjustmentPending);
        }
    }
}
