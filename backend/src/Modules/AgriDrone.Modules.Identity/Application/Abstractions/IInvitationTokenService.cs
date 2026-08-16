using AgriDrone.Modules.Identity.Application.Contracts.Invitations;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Abstractions
{
    

    public interface IInvitationTokenService
    {
        InvitationTokenResult Generate();

        string Hash(string plainTextToken); 
    }
}
