using AgriDrone.Modules.Identity.Domain.SystemManagerInvitations;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories;

internal sealed class SystemManagerInvitationRepository(
    IdentityDbContext context)
    : ISystemManagerInvitationRepository
{
    public Task<SystemManagerInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        context.SystemManagerInvitations.SingleOrDefaultAsync(
            invitation => invitation.TokenHash == tokenHash,
            cancellationToken);

    public Task<SystemManagerInvitation?> GetPendingByEmailAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        context.SystemManagerInvitations.SingleOrDefaultAsync(
            invitation =>
                invitation.Email == email &&
                invitation.Status ==
                    SystemManagerInvitationStatus.Pending,
            cancellationToken);

    public void Add(SystemManagerInvitation invitation) =>
        context.SystemManagerInvitations.Add(invitation);
}