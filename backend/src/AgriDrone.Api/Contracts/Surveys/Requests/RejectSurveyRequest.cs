namespace AgriDrone.Api.Contracts.Surveys.Requests;

public sealed record RejectSurveyRequest(
    string Reason,
    SurveyRequestReviewChecklistRequest Checklist,
    uint ExpectedVersion);

public sealed record SurveyRequestReviewChecklistRequest(
    string Version,
    bool ContactValid,
    bool IsServiceAreaSupported,
    bool IsPreliminaryLegalAndFlightFeasible,
    string? Notes);
