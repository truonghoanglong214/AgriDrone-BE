using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.FarmMemberships;

public sealed record RevokeFarmMemberAssignmentRequest(
    [property: JsonRequired] long ExpectedVersion,
    string? Reason);
