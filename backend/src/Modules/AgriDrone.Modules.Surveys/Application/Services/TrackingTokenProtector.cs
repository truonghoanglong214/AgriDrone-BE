using Microsoft.AspNetCore.DataProtection;

namespace AgriDrone.Modules.Surveys.Application.Services;

public sealed class TrackingTokenProtector : ITrackingTokenProtector
{
    private const string Purpose = "AgriDrone.Surveys.CommercialTrackingToken.v1";
    private readonly IDataProtector _protector;

    public TrackingTokenProtector(IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string GenerateToken() => TrackingTokenService.GenerateToken();

    public string ComputeHash(string plaintextToken) => TrackingTokenService.ComputeHash(plaintextToken);

    public string Protect(string plaintextToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextToken);
        return _protector.Protect(plaintextToken.Trim());
    }

    public string Unprotect(string encryptedToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedToken);
        return _protector.Unprotect(encryptedToken.Trim());
    }
}
