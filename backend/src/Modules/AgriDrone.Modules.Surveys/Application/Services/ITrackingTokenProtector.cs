namespace AgriDrone.Modules.Surveys.Application.Services;

public interface ITrackingTokenProtector
{
    string GenerateToken();
    string ComputeHash(string plaintextToken);
    string Protect(string plaintextToken);
    string Unprotect(string encryptedToken);
}
