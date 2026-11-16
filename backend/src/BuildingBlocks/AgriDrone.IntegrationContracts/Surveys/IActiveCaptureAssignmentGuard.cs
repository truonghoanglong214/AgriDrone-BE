namespace AgriDrone.IntegrationContracts.Surveys;

public interface IActiveCaptureAssignmentGuard
{
    Task<bool> HasInFlightCaptureAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);
}
