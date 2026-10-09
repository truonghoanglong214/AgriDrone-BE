using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Missions;

public sealed record SurveyMissionContext
{
    public SurveyMissionContext(
        Guid surveyOrderId,
        Guid tenantId,
        Guid farmId,
        MissionPurpose purpose)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);

        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        SurveyOrderId = surveyOrderId;
        TenantId = tenantId;
        FarmId = farmId;
        Purpose = purpose;
    }

    public Guid SurveyOrderId { get; }
    public Guid TenantId { get; }
    public Guid FarmId { get; }
    public MissionPurpose Purpose { get; }
}
