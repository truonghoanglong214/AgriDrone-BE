using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.SystemManagers;

public sealed record CreateSystemManagerProfileRequest(Guid UserId);

public sealed class GetSystemManagersRequest
{
    public SystemManagerProfileStatusValue? Status { get; init; }

    public SystemManagerAvailabilityValue? Availability { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public sealed record SystemManagerStateChangeRequest(
    string Reason,
    long ExpectedVersion);

public sealed record UpdateSystemManagerAvailabilityRequest(
    SystemManagerAvailabilityValue Availability,
    string Reason,
    long ExpectedVersion);

public sealed record AssignPrimaryFarmManagerRequest(
    Guid SystemManagerProfileId,
    string Reason,
    long? ExpectedCurrentAssignmentVersion);

public sealed record EndPrimaryFarmManagerAssignmentRequest(
    string Reason,
    long ExpectedVersion);

#pragma warning disable CA1707
[JsonConverter(typeof(JsonStringEnumConverter<SystemManagerProfileStatusValue>))]
public enum SystemManagerProfileStatusValue
{
    [JsonStringEnumMemberName("ACTIVE")]
    ACTIVE,

    [JsonStringEnumMemberName("SUSPENDED")]
    SUSPENDED
}

[JsonConverter(typeof(JsonStringEnumConverter<SystemManagerAvailabilityValue>))]
public enum SystemManagerAvailabilityValue
{
    [JsonStringEnumMemberName("AVAILABLE")]
    AVAILABLE,

    [JsonStringEnumMemberName("UNAVAILABLE")]
    UNAVAILABLE
}
#pragma warning restore CA1707
