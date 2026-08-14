namespace AgriDrone.Api.Contracts.Users
{
    public sealed class RegisterUserRequest
    {
        public string Email { get; private set; } = null!;

        public string Password { get; private set; } = null!;

        public string FullName { get; private set; } = null!;

        public string? Phone { get; private set; }
        public string TenantCode { get; private set; } = null!;

        public string TenantName { get; private set; } = null!;
    }
}
