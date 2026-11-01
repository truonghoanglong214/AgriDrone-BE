namespace AgriDrone.Modules.Surveys.Domain;

[Obsolete("Removed in Be-Plan-v2. Kept only for historical EF Core migration designer compilation.")]
public enum SurveyCaptureAuthorizationType
{
    WrittenDataUseConsent = 1,
    FlightPermission = 2
}

[Obsolete("Removed in Be-Plan-v2. Kept only for historical EF Core migration designer compilation.")]
public enum SurveyCaptureAuthorizationStatus
{
    PendingVerification = 1,
    Verified = 2,
    Revoked = 3
}
