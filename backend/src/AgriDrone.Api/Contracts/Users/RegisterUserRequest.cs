namespace AgriDrone.Api.Contracts.Users
{
    public sealed class RegisterUserRequest
    {
        public string Email { get; init; } = null!;  
        public string Password { get; init; } = null!;
        public string FullName { get; init; } = null!;
        public string? Phone { get; init; }
        public string TenantCode { get; init; } = null!;
        public string TenantName { get; init; } = null!;
    }
}
