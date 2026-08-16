using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Contracts.Invitations;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace AgriDrone.Modules.Identity.Infrastructure.Security
{
    internal sealed class InvitationTokenService() : IInvitationTokenService
    {
        public InvitationTokenResult Generate()
        {
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var plainTextToken = Convert.ToHexString(tokenBytes);
            var tokenHash = Hash(plainTextToken);

            return new InvitationTokenResult(
                plainTextToken,
                tokenHash);
        }

        public string Hash(string plainTextToken)
        {
            var hashBytes = SHA256.HashData(
                Encoding.UTF8.GetBytes(plainTextToken));

            return Convert.ToHexString(hashBytes);
        }
    }
}
