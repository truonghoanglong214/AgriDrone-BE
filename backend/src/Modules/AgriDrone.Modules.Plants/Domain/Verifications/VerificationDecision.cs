namespace AgriDrone.Modules.Plants.Domain.Verifications;

public enum VerificationDecision
{
    Confirmed,
    Corrected,
    Rejected,
    FieldInspectionRequired,

    // Legacy values retained until the Phase 7 contract migration.
    Incorrect,
    NeedFieldInspection,
    Recovered
}
