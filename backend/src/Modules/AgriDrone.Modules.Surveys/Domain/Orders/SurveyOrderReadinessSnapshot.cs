namespace AgriDrone.Modules.Surveys.Domain;

public sealed record SurveyOrderReadinessSnapshot(
    SurveyOrderStatus OrderStatus,
    SurveyOperationPurpose Purpose,
    bool RequiresBaselineMapping,
    bool HasApprovedFarmBoundary,
    bool IsScopeConfirmed,
    bool HasPublishedFarmBaseMap,
    bool HasConfirmedPoleCount,
    bool HasPriceSnapshot,
    SurveyAppointmentPurpose? AppointmentPurpose,
    SurveyAppointmentStatus? AppointmentStatus,
    SurveyPaymentStatus? PaymentStatus,
    bool HasActiveQualifiedPrimaryManager,
    bool HasPendingPriceAdjustment,
    bool SafetyChecksSatisfied);
