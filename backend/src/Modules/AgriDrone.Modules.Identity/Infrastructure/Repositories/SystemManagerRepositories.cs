using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories;

internal sealed class SystemManagerProfileRepository(IdentityDbContext dbContext)
    : ISystemManagerProfileRepository
{
    public Task<SystemManagerProfile?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        dbContext.SystemManagerProfiles
            .Include(profile => profile.User)
            .SingleOrDefaultAsync(
            profile => profile.Id == id,
            cancellationToken);

    public Task<SystemManagerProfile?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.SystemManagerProfiles
            .Include(profile => profile.User)
            .SingleOrDefaultAsync(
            profile => profile.UserId == userId,
            cancellationToken);

    public void Add(SystemManagerProfile profile) =>
        dbContext.SystemManagerProfiles.Add(profile);
}

internal sealed class FarmManagerAssignmentRepository(IdentityDbContext dbContext)
    : IFarmManagerAssignmentRepository
{
    public Task<FarmManagerAssignment?> GetActiveByFarmIdAsync(
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        dbContext.FarmManagerAssignments.SingleOrDefaultAsync(
            assignment => assignment.FarmId == farmId && assignment.EndedAt == null,
            cancellationToken);

    public async Task<IReadOnlyCollection<FarmManagerAssignment>>
        GetActiveByProfileIdAsync(
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
