using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class PreflightChecklistRepository(
    MissionsDbContext dbContext)
    : IPreflightChecklistRepository
{
    public Task<PreflightChecklistDefinition?> GetActiveDefinitionAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        dbContext.PreflightChecklistDefinitions.SingleOrDefaultAsync(
            definition =>
                definition.Code == code &&
                definition.Status == PreflightChecklistDefinitionStatus.Active,
            cancellationToken);

    public Task<PreflightChecklistDefinition?> GetActiveDefinitionByIdAsync(
        Guid definitionId,
        string code,
        CancellationToken cancellationToken = default) =>
        dbContext.PreflightChecklistDefinitions.SingleOrDefaultAsync(
            definition =>
                definition.Id == definitionId &&
                definition.Code == code &&
                definition.Status == PreflightChecklistDefinitionStatus.Active,
            cancellationToken);

    public Task<MissionPreflightChecklist?> GetByOperationIdAsync(
        Guid missionId,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        dbContext.MissionPreflightChecklists.SingleOrDefaultAsync(
            checklist =>
                checklist.MissionId == missionId &&
                checklist.ClientOperationId == operationId,
            cancellationToken);

    public async Task<int> GetNextVersionNumberAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var currentVersion = await dbContext.PreflightChecklistDefinitions
            .Where(definition => definition.Code == code)
            .Select(definition => (int?)definition.VersionNumber)
            .MaxAsync(cancellationToken);
        return (currentVersion ?? 0) + 1;
    }

    public async Task RetireActiveDefinitionsAsync(
        string code,
        DateTimeOffset retiredAt,
        CancellationToken cancellationToken = default)
    {
        var activeDefinitions = await dbContext.PreflightChecklistDefinitions
            .Where(definition =>
                definition.Code == code &&
                definition.Status == PreflightChecklistDefinitionStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var definition in activeDefinitions)
        {
            definition.Retire(retiredAt);
        }
    }

    public void AddDefinition(PreflightChecklistDefinition definition) =>
        dbContext.PreflightChecklistDefinitions.Add(definition);

    public void AddCompleted(MissionPreflightChecklist checklist) =>
        dbContext.MissionPreflightChecklists.Add(checklist);
}
