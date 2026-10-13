using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories;

internal sealed class FarmManagerAssignmentRepository(IdentityDbContext dbContext)
    : IFarmManagerAssignmentRepository
{
    public Task<FarmManagerAssignment?> GetActiveByFarmIdAsync(
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        dbContext.FarmManagerAssignments.SingleOrDefaultAsync(
            assignment => assignment.FarmId == farmId && assignment.EndedAt == null,
            cancellationToken);

    public async Task<IReadOnlyCollection<FarmManagerAssignment>> GetActiveByProfileIdAsync(
        Guid profileId,
        CancellationToken cancellationToken = default) =>
        await dbContext.FarmManagerAssignments
            .Where(assignment =>
                assignment.SystemManagerProfileId == profileId &&
                assignment.EndedAt == null)
            .OrderBy(assignment => assignment.AssignedAt)
            .ToArrayAsync(cancellationToken);

    public void Add(FarmManagerAssignment assignment) =>
        dbContext.FarmManagerAssignments.Add(assignment);
}
