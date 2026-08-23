namespace AgriDrone.Api.Contracts.Users
{
    public sealed record LoginUserRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }
}
