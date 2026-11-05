namespace AgriDrone.Modules.Surveys.Domain;

public static class SurveyOrderReadinessPolicy
{
    public static SurveyOrderReadinessDecision Evaluate(
        SurveyOrderReadinessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var failures = new List<SurveyOrderReadinessFailure>();

        EvaluateCommercialRequirements(snapshot, failures);

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

        if (!snapshot.SafetyChecksSatisfied)
        {
            failures.Add(SurveyOrderReadinessFailure.SafetyChecksNotSatisfied);
        }

        return new SurveyOrderReadinessDecision(failures.Count == 0, failures);
    }

    public static SurveyOrderReadinessDecision EvaluateCommercialReadiness(
        SurveyOrderReadinessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var failures = new List<SurveyOrderReadinessFailure>();

        EvaluateCommercialRequirements(snapshot, failures);

        return new SurveyOrderReadinessDecision(failures.Count == 0, failures);
    }

    public static SurveyOrderReadinessDecision EvaluateOperationalReadiness(
        SurveyOrderReadinessSnapshot commercialSnapshot,
        bool isMissionPrepared,
        bool isMissionScheduled,
        bool isDroneReady,
        bool isPreflightCompleted,
        bool safetyChecksSatisfied)
    {
        ArgumentNullException.ThrowIfNull(commercialSnapshot);
        var combinedSnapshot = commercialSnapshot with
        {
            IsMissionPreparedForPurpose = isMissionPrepared,
            IsMissionScheduledWithinAppointment = isMissionScheduled,
            IsDroneReady = isDroneReady,
            IsPreflightCompleted = isPreflightCompleted,
            SafetyChecksSatisfied = safetyChecksSatisfied
        };

        return Evaluate(combinedSnapshot);
    }

    private static void EvaluateCommercialRequirements(
        SurveyOrderReadinessSnapshot snapshot,
        List<SurveyOrderReadinessFailure> failures)
    {
        if (snapshot.Purpose == SurveyOperationPurpose.HarvestReadiness)
        {
            failures.Add(SurveyOrderReadinessFailure.ServiceDisabled);
            return;
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
            if (snapshot.DepositPaymentStatus != SurveyPaymentStatus.Paid)
            {
                failures.Add(SurveyOrderReadinessFailure.PaymentNotConfirmed);
            }
        }

        if (snapshot.Purpose == SurveyOperationPurpose.PlantHealth)
        {
            if (!snapshot.HasApprovedFarmBoundary)
            {
                failures.Add(SurveyOrderReadinessFailure.BoundaryNotApproved);
            }

            if (!snapshot.IsScopeConfirmed)
            {
                failures.Add(SurveyOrderReadinessFailure.ScopeNotConfirmed);
            }
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

        if (!snapshot.HasActiveQualifiedPrimaryManager)
        {
            failures.Add(SurveyOrderReadinessFailure.PrimaryManagerNotReady);
        }

        if (!snapshot.IsPrimaryManagerAvailable)
        {
            failures.Add(SurveyOrderReadinessFailure.PrimaryManagerUnavailable);
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
        else if (snapshot.PaymentStatus != SurveyPaymentStatus.Paid)
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
