using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Contracts.Invitations
{
    public sealed record InvitationTokenResult(string PlainTextToken, string TokenHash);
}
