namespace AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;

internal static class SurveyRequestPersistenceConstraints
{
    public const string RequestNumber = "uq_survey_requests_number";

    public const string CallerIdempotency =
        "uq_survey_requests_caller_idempotency";

    public const string ActiveTenantCode =
        "ux_survey_requests_active_tenant_code";
}
