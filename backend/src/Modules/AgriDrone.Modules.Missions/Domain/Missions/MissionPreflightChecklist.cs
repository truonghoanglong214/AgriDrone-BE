using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Missions;

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

    public static MissionPreflightChecklist Complete(
        Guid missionId,
        PreflightChecklistDefinition definition,
        Guid clientOperationId,
        JsonDocument responses,
        string? unsuitableConditionNotes,
        string? failsafeNotes,
        Guid completedBy,
        DateTimeOffset deviceCompletedAt,
        DateTimeOffset serverReceivedAt)
    {
        DomainGuard.NotEmpty(missionId);
        ArgumentNullException.ThrowIfNull(definition);
        DomainGuard.NotEmpty(clientOperationId);
        ArgumentNullException.ThrowIfNull(responses);
        DomainGuard.NotEmpty(completedBy);
        DomainGuard.Utc(deviceCompletedAt);
        DomainGuard.Utc(serverReceivedAt);
        if (responses.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "Checklist responses must be a JSON object.",
                nameof(responses));
        }

        return new MissionPreflightChecklist
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            ChecklistDefinitionId = definition.Id,
            ClientOperationId = clientOperationId,
            Status = MissionPreflightChecklistStatus.Completed,
            DefinitionSnapshot = JsonDocument.Parse(
                definition.Items.RootElement.GetRawText()),
            Responses = JsonDocument.Parse(
                responses.RootElement.GetRawText()),
            UnsuitableConditionNotes = Normalize(unsuitableConditionNotes),
            FailsafeNotes = Normalize(failsafeNotes),
            CompletedBy = completedBy,
            DeviceCompletedAt = deviceCompletedAt,
            CompletedAt = serverReceivedAt,
            ServerReceivedAt = serverReceivedAt
        };
    }

    public bool MatchesPayload(
        Guid definitionId,
        JsonDocument responses,
        string? unsuitableConditionNotes,
        string? failsafeNotes,
        Guid completedBy,
        DateTimeOffset? deviceCompletedAt) =>
        ChecklistDefinitionId == definitionId &&
        string.Equals(
            Responses.RootElement.GetRawText(),
            responses.RootElement.GetRawText(),
            StringComparison.Ordinal) &&
        string.Equals(
            UnsuitableConditionNotes,
            Normalize(unsuitableConditionNotes),
            StringComparison.Ordinal) &&
        string.Equals(
            FailsafeNotes,
            Normalize(failsafeNotes),
            StringComparison.Ordinal) &&
        CompletedBy == completedBy &&
        (!deviceCompletedAt.HasValue ||
         DeviceCompletedAt == deviceCompletedAt.Value);

    public void Supersede()
    {
        if (Status != MissionPreflightChecklistStatus.Completed)
            throw new InvalidOperationException("Only a completed checklist can be superseded.");
        Status = MissionPreflightChecklistStatus.Superseded;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
