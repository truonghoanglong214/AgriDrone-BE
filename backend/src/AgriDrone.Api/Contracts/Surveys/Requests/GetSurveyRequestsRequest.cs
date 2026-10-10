using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.Surveys.Requests;

public sealed record GetSurveyRequestsRequest
{
    public SurveyRequestStatusFilterValue? Status { get; init; }

    public SurveyRequestKindFilterValue? Kind { get; init; }

    public Guid? SurveyServiceId { get; init; }

    public DateTimeOffset? CreatedFrom { get; init; }

    public DateTimeOffset? CreatedTo { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

// These HTTP-bound enum member names deliberately match the stable query-string
// tokens so ASP.NET Core's enum model binder accepts the documented values.
#pragma warning disable CA1707
[JsonConverter(typeof(JsonStringEnumConverter<SurveyRequestStatusFilterValue>))]
public enum SurveyRequestStatusFilterValue
{
    [JsonStringEnumMemberName("SUBMITTED")]
    SUBMITTED,

    [JsonStringEnumMemberName("UNDER_REVIEW")]
    UNDER_REVIEW,

    [JsonStringEnumMemberName("APPROVED")]
    APPROVED,

    [JsonStringEnumMemberName("REJECTED")]
    REJECTED,

    [JsonStringEnumMemberName("WITHDRAWN")]
    WITHDRAWN
}

[JsonConverter(typeof(JsonStringEnumConverter<SurveyRequestKindFilterValue>))]
public enum SurveyRequestKindFilterValue
{
    [JsonStringEnumMemberName("NEW_CUSTOMER")]
    NEW_CUSTOMER,

    [JsonStringEnumMemberName("EXISTING_TENANT_NEW_FARM")]
    EXISTING_TENANT_NEW_FARM,

    [JsonStringEnumMemberName("EXISTING_FARM_SURVEY")]
    EXISTING_FARM_SURVEY
}
#pragma warning restore CA1707
