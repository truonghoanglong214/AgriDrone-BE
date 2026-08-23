namespace AgriDrone.Api.Contracts.Users
{
    public sealed record GetUserRequest
    {
        public int PageNumber { get; init; } = 1;

        public int PageSize { get; init; } = 20;
    }
}
