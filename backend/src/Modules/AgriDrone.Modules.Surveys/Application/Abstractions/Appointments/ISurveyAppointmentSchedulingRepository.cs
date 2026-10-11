using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;

public interface ISurveyAppointmentSchedulingRepository
{
    Task<Result<SurveyAppointmentManagerState>> GetForManagerProposalAsync(
        Guid surveyOrderId,
        SurveyAppointmentPurpose purpose,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<Result<SurveyAppointmentOwnerState>> GetForOwnerMutationAsync(
        Guid surveyOrderId,
        Guid appointmentId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    void Add(SurveyAppointment appointment);
}

public sealed record SurveyAppointmentManagerState(
    SurveyOrder Order,
    SurveyAppointment? CurrentAppointment,
    SurveyAppointment? LatestAppointment,
    Guid SystemManagerProfileId);

public sealed record SurveyAppointmentOwnerState(
    SurveyOrder Order,
    SurveyAppointment Appointment);
