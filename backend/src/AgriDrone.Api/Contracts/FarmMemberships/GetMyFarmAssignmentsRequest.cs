namespace AgriDrone.Api.Contracts.FarmMemberships;

public sealed record GetMyFarmAssignmentsRequest
{
    public FarmMemberRoleValue? Role { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
