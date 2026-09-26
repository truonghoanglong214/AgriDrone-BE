using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Missions;

public enum MissionPurpose
{
    BaselineMapping,
    PlantHealth,
    HarvestReadiness
}

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

public enum PreflightChecklistDefinitionStatus
{
    Draft,
    Active,
    Retired
}

public enum MissionPreflightChecklistStatus
{
    Draft,
    Completed,
    Superseded
}

public sealed class PreflightChecklistDefinition : AggregateRoot
{
    private PreflightChecklistDefinition() { }

    public string Code { get; private set; } = null!;
    public int VersionNumber { get; private set; }
    public PreflightChecklistDefinitionStatus Status { get; private set; }
    public JsonDocument Items { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public ICollection<MissionPreflightChecklist> MissionChecklists { get; private set; } = [];
}

public sealed class MissionPreflightChecklist : Entity
{
    private MissionPreflightChecklist() { }

    public Guid MissionId { get; private set; }
    public Guid ChecklistDefinitionId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public MissionPreflightChecklistStatus Status { get; private set; }
    public JsonDocument DefinitionSnapshot { get; private set; } = null!;
    public JsonDocument Responses { get; private set; } = null!;
    public string? UnsuitableConditionNotes { get; private set; }
    public string? FailsafeNotes { get; private set; }
    public Guid? CompletedBy { get; private set; }
    public DateTimeOffset? DeviceCompletedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset ServerReceivedAt { get; private set; }
    public uint Version { get; private set; }
    public PreflightChecklistDefinition ChecklistDefinition { get; private set; } = null!;
    public DroneMission Mission { get; private set; } = null!;
}
