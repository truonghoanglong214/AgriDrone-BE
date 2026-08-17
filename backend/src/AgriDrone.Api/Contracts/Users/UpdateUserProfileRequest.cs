namespace AgriDrone.Api.Contracts.Users
{
    public sealed class UpdateUserProfileRequest
    {
        public string Name { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
    }
}
