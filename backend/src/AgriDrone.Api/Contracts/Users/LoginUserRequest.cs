namespace AgriDrone.Api.Contracts.Users
{
    public sealed class LoginUserRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }
}
