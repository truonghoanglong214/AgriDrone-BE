namespace AgriDrone.Modules.Surveys.Application.Features.Requests.Common;

public static class SurveyRequestReviewChecklistDefinition
{
    public const string Version = "survey-request-review.v3";

    public static IReadOnlyList<SurveyRequestReviewChecklistItem> Items { get; } =
    [
        new(
            "contact-valid",
            "Contact and applicant information is valid."),
        new(
            "service-area-supported",
            "The Farm is within a supported service area."),
        new(
            "legal-flight-feasible",
            "Preliminary legal and flight feasibility has been reviewed.")
    ];
}

public sealed record SurveyRequestReviewChecklistItem(
    string Code,
    string Description);

public sealed record SurveyRequestReviewChecklistInput(
    string Version,
    bool ContactValid,
    bool IsServiceAreaSupported,
    bool IsPreliminaryLegalAndFlightFeasible,
    string? Notes);
