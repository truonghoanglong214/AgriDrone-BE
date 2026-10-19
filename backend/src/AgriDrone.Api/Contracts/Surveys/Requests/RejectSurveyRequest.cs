using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.Surveys.Requests;

public sealed record RejectSurveyRequest(
    string Reason,
    SurveyRequestReviewChecklistRequest Checklist,
    uint ExpectedVersion);

public sealed record SurveyRequestReviewChecklistRequest(
    [param: Required]
    [property: JsonRequired]
    [property: JsonPropertyName("contact-valid")]
    bool ContactValid,
    [param: Required]
    [property: JsonRequired]
    [property: JsonPropertyName("dragon-fruit-farm-confirmed")]
    bool DragonFruitFarmConfirmed,
    [param: Required]
    [property: JsonRequired]
    [property: JsonPropertyName("service-area-supported")]
    bool ServiceAreaSupported,
    [param: Required]
    [property: JsonRequired]
    [property: JsonPropertyName("candidate-location-sufficient")]
    bool CandidateLocationSufficient,
    [param: Required]
    [property: JsonRequired]
    [property: JsonPropertyName("consent-feasibility")]
    bool ConsentFeasibility,
    [param: Required]
    [property: JsonRequired]
    [property: JsonPropertyName("flight-permission-feasibility")]
    bool FlightPermissionFeasibility,
    [param: Required]
    [property: JsonRequired]
    [property: JsonPropertyName("eligible-manager-available")]
    bool EligibleManagerAvailable,
    string? Notes);
