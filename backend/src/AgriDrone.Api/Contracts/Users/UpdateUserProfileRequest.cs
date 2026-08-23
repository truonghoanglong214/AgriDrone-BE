 namespace AgriDrone.Api.Contracts.Users
{
    public sealed record UpdateUserProfileRequest
    {
        public string Name { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
    }
}
