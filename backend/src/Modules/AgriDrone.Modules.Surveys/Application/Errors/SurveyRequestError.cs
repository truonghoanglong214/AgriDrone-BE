using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class SurveyRequestError
{
    public const string NotFoundCode = "SurveyRequest.NotFound";
    public const string InvalidTransitionCode = SurveyRequestDomainErrorCodes.InvalidTransition;
    public const string VersionConflictCode = SurveyRequestDomainErrorCodes.VersionConflict;
    public const string DuplicateCode = "SurveyRequest.Duplicate";
    public const string InvalidInputCode = "SurveyRequest.InvalidInput";
    public const string IdempotencyPayloadMismatchCode =
        "SurveyRequest.IdempotencyPayloadMismatch";
    public const string RequestNumberConflictCode =
        "SurveyRequest.RequestNumberConflict";
    public const string NotReviewableCode =
        "SurveyRequest.NotReviewable";
    public const string ForbiddenCode = "SurveyRequest.Forbidden";
    public const string NotVisibleCode = "SurveyRequest.NotVisible";
    public const string ServiceUnavailableCode =
        "SurveyRequest.ServiceUnavailable";
    public const string RequestContextRequiredCode =
        "SurveyRequest.RequestContextRequired";
    public const string CurrentTenantOwnerRequiredCode =
        "SurveyRequest.CurrentTenantOwnerRequired";
    public const string ApplicantProfileIncompleteCode =
        "SurveyRequest.ApplicantProfileIncomplete";

    public static AppError NotFound(Guid id) =>
        AppError.NotFound(NotFoundCode, $"Survey request '{id}' was not found.");

    public static AppError InvalidTransition(string description) =>
        AppError.Conflict(InvalidTransitionCode, description);

    public static AppError VersionConflict() =>
        AppError.Conflict(
            VersionConflictCode,
            "The survey request was changed by another operation.");

    public static AppError InvalidInput(string description) =>
        AppError.Validation(InvalidInputCode, description);

    public static AppError Duplicate() =>
        AppError.Conflict(
            DuplicateCode,
            "A concurrent request used the same idempotency key. Retry the operation.");

    public static AppError IdempotencyPayloadMismatch() =>
        AppError.Conflict(
            IdempotencyPayloadMismatchCode,
            "The idempotency key was already used with a different survey request payload.");

    public static AppError RequestNumberConflict() =>
        AppError.Conflict(
            RequestNumberConflictCode,
            "A unique survey request number could not be allocated. Retry the operation.");

    public static AppError NotReviewable(Guid id) =>
        AppError.Conflict(
            NotReviewableCode,
            $"Survey request '{id}' is not in a reviewable state.");

    public static AppError Forbidden() =>
        AppError.Forbidden(
            ForbiddenCode,
            "The current actor cannot perform this survey request operation.");

    public static AppError NotVisible(Guid id) =>
        AppError.NotFound(
            NotVisibleCode,
            $"Survey request '{id}' was not found.");

    public static AppError ServiceUnavailable() =>
        AppError.NotFound(
            ServiceUnavailableCode,
            "The selected survey service is not currently accepting requests.");

    public static AppError RequestContextRequired() =>
        AppError.Failure(
            RequestContextRequiredCode,
            "A valid server request context is required.");

    public static AppError CurrentTenantOwnerRequired() =>
        AppError.Unauthorized(
            CurrentTenantOwnerRequiredCode,
            "An authenticated tenant owner with an active tenant context is required.");

    public static AppError ApplicantProfileIncomplete() =>
        AppError.Validation(
            ApplicantProfileIncompleteCode,
            "The tenant owner profile must contain a valid name, email and phone number before submitting a survey request.");
}
