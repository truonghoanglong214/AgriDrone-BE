using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.V3;

public sealed record TrackSurveyRequestV3ApiRequest(
    [param: Required, StringLength(128, MinimumLength = 16)]
    string TrackingToken);
