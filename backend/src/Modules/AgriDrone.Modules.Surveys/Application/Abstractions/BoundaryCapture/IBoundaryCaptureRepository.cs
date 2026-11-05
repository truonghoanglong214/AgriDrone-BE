using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.BoundaryCapture;

public interface IBoundaryCaptureRepository
{
    Task<Result<BoundaryCaptureStartState>> GetForStartAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        Guid droneId,
        Guid startOperationId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<Result<BoundaryCaptureMutationState>> GetForMutationAsync(
        Guid surveyOrderId,
        Guid sessionId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<BoundaryCaptureSession?> GetAsync(
        Guid surveyOrderId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    void Add(BoundaryCaptureSession session);

    void Add(BaselineRoutePlanReference routePlanReference);
}

public sealed record BoundaryCaptureStartState(
    SurveyOrder Order,
    SurveyAppointment Appointment,
    Guid SystemManagerProfileId,
    BoundaryCaptureSession? ExistingSession);

public sealed record BoundaryCaptureMutationState(
    SurveyOrder Order,
    BoundaryCaptureSession Session,
    Guid SystemManagerProfileId,
    int? CurrentBoundaryVersionNumber,
    bool IsAppointmentCurrent);
