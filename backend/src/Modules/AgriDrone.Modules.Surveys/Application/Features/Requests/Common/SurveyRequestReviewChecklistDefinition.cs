namespace AgriDrone.Modules.Surveys.Application.Features.Requests.Common;

public static class SurveyRequestReviewChecklistDefinition
{
    public const string Version = "survey-request-review.v1";

    public static IReadOnlyList<SurveyRequestReviewChecklistItem> Items { get; } =
    [
        new(
            "contact-valid",
            "Contact and applicant information is valid."),
        new(
            "dragon-fruit-farm",
            "The proposed or existing Farm is a dragon-fruit farm."),
        new(
            "service-area-supported",
            "The Farm is within a supported service area."),
        new(
            "location-sufficient",
            "The submitted location is sufficient for preliminary review and is not an approved Farm boundary."),
        new(
            "legal-flight-feasible",
            "Preliminary legal and flight feasibility has been reviewed."),
        new(
            "eligible-manager-available",
            "An active, available and flight-qualified SystemManager exists.")
    ];
}

public sealed record SurveyRequestReviewChecklistItem(
    string Code,
    string Description);

public sealed record SurveyRequestReviewChecklistInput(
    string Version,
    bool ContactValid,
    bool IsDragonFruitFarm,
    bool IsServiceAreaSupported,
    bool IsLocationSufficient,
    bool IsPreliminaryLegalAndFlightFeasible,
    bool IsEligibleSystemManagerAvailable,
    string? Notes);
