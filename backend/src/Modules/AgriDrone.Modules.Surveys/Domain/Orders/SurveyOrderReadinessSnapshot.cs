namespace AgriDrone.Modules.Surveys.Domain;

public sealed record SurveyOrderReadinessSnapshot(
    SurveyOrderStatus OrderStatus,
    bool IsScopeConfirmed,
    SurveyAppointmentStatus? AppointmentStatus,
    SurveyPaymentStatus? PaymentStatus,
    bool HasActiveQualifiedPrimaryManager,
    bool HasPendingPriceAdjustment);
