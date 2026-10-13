namespace AgriDrone.Modules.Identity.Domain.SystemManagerInvitations;

public interface ISystemManagerInvitationRepository
{
    Task<SystemManagerInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<SystemManagerInvitation?> GetPendingByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    void Add(SystemManagerInvitation invitation);
}