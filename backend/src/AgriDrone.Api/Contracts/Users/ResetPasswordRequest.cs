namespace AgriDrone.Api.Contracts.Users;

public sealed record ResetPasswordRequest(
    string Token,
    string NewPassword,
    string ConfirmPassword);
