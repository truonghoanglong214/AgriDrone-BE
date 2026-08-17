namespace AgriDrone.Api.Contracts.Users
{
    public sealed class UpdateUserPasswordRequest
    {
        public string OldPassword { get; init; } = string.Empty;
        public string NewPassword { get; init; } = string.Empty;
    }
}
