using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class SurveyAppointmentError
{
    public const string NotFoundCode = "SurveyAppointment.NotFound";
    public const string ForbiddenCode = "SurveyAppointment.Forbidden";
    public const string ActiveAppointmentExistsCode =
        "SurveyAppointment.ActiveAppointmentExists";
    public const string ExpectedCurrentAppointmentRequiredCode =
        "SurveyAppointment.ExpectedCurrentAppointmentRequired";
    public const string UnexpectedCurrentAppointmentCode =
        "SurveyAppointment.UnexpectedCurrentAppointment";
    public const string VersionConflictCode =
        "SurveyAppointment.VersionConflict";
    public const string BaselineMappingNotRequiredCode =
        "SurveyAppointment.BaselineMappingNotRequired";
    public const string InvalidTransitionCode =
        "SurveyAppointment.InvalidTransition";
    public const string ConfirmationWindowExpiredCode =
        "SurveyAppointment.ConfirmationWindowExpired";
    public const string PersistenceConflictCode =
        "SurveyAppointment.PersistenceConflict";
    public const string InvalidWindowCode =
        "SurveyAppointment.InvalidWindow";

    public static AppError NotFound(Guid appointmentId) =>
        AppError.NotFound(
            NotFoundCode,
            $"Survey appointment '{appointmentId}' was not found.");

    public static AppError Forbidden() => AppError.Forbidden(
        ForbiddenCode,
        "The actor cannot perform this survey-appointment action.");

    public static AppError ActiveAppointmentExists() => AppError.Conflict(
        ActiveAppointmentExistsCode,
        "A current appointment already exists for this order and purpose.");

    public static AppError ExpectedCurrentAppointmentRequired() =>
        AppError.Conflict(
            ExpectedCurrentAppointmentRequiredCode,
            "The current reschedule-requested appointment and version are required for reproposal.");

    public static AppError UnexpectedCurrentAppointment() =>
        AppError.Conflict(
            UnexpectedCurrentAppointmentCode,
            "Expected current appointment data must be omitted for an initial proposal.");

    public static AppError VersionConflict() => AppError.Conflict(
        VersionConflictCode,
        "The appointment was changed by another operation.");

    public static AppError BaselineMappingNotRequired() => AppError.Conflict(
        BaselineMappingNotRequiredCode,
        "This order does not require a Baseline Mapping appointment.");

    public static AppError InvalidTransition(string description) =>
        AppError.Conflict(InvalidTransitionCode, description);

    public static AppError ConfirmationWindowExpired() => AppError.Conflict(
        ConfirmationWindowExpiredCode,
        "The proposed appointment window already ended.");

    public static AppError PersistenceConflict() => AppError.Conflict(
        PersistenceConflictCode,
        "Appointment scheduling conflicted with another operation.");

    public static AppError InvalidWindow() => AppError.Validation(
        InvalidWindowCode,
        "The appointment window must be a future UTC interval with end after start.");
}
