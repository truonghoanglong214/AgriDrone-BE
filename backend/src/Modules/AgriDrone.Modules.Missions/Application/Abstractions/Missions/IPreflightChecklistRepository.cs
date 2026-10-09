using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal interface IPreflightChecklistRepository
{
    Task<PreflightChecklistDefinition?> GetActiveDefinitionAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<PreflightChecklistDefinition?> GetActiveDefinitionByIdAsync(
        Guid definitionId,
        string code,
        CancellationToken cancellationToken = default);

    Task<MissionPreflightChecklist?> GetByOperationIdAsync(
        Guid missionId,
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionNumberAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task RetireActiveDefinitionsAsync(
        string code,
        DateTimeOffset retiredAt,
        CancellationToken cancellationToken = default);

    void AddDefinition(PreflightChecklistDefinition definition);

    void AddCompleted(MissionPreflightChecklist checklist);
}
