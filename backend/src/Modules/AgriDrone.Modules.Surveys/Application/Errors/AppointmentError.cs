using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class AppointmentError
{
    public const string NotFoundCode = "Appointment.NotFound";
    public const string InvalidTransitionCode = AppointmentDomainErrorCodes.InvalidTransition;
    public const string VersionConflictCode = AppointmentDomainErrorCodes.VersionConflict;
    public const string InvalidWindowCode = "Appointment.InvalidWindow";
}
