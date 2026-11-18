namespace AgriDrone.Modules.Surveys.Domain;

public enum CancellationDecisionKind
{
    CustomerBeforeCapture,
    CustomerAfterAcceptableCapture,
    OperationalPostponement,
    ServiceFailure
}
