using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Initialization;

internal sealed class SystemAdminBootstrapLock(IdentityDbContext dbContext)
    : ISystemAdminBootstrapLock
{
    public async Task AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "The System Admin bootstrap lock must be acquired inside a database transaction.");
        }

        var initializationLock = await dbContext.InitializationLocks
            .SingleAsync(
                candidate => candidate.Name ==
                    InitializationLock.SystemAdminBootstrapName,
                cancellationToken);

        initializationLock.Acquire();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
