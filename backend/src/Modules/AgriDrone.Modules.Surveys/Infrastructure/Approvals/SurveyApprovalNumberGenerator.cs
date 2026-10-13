using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;

namespace AgriDrone.Modules.Surveys.Infrastructure.Approvals;

internal sealed class SurveyApprovalNumberGenerator
    : ISurveyApprovalNumberGenerator
{
    public string CreateTenantCode() => Create("TEN", suffixLength: 26);

    public string CreateFarmCode() => Create("FARM", suffixLength: 25);

    public string CreateOrderNumber() => Create("SO", suffixLength: 32);

    private static string Create(string prefix, int suffixLength)
    {
        var suffix = Guid.CreateVersion7().ToString("N");
        return $"{prefix}-{suffix[..suffixLength]}";
    }
}
