using AgriDrone.Modules.Identity.Application.Contracts.Invitations;

namespace AgriDrone.Modules.Identity.Application.Abstractions;

public interface IInvitationTokenService
{
    InvitationTokenResult Generate();

    string Hash(string plainTextToken);
}
