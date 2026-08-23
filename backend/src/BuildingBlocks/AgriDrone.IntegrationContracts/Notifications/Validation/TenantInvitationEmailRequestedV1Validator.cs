using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.IntegrationContracts.Notifications.Validation
{
    public static class TenantInvitationEmailRequestedV1Validator
    {
        private const int InvitationTokenLength = 64;

        public static IReadOnlyList<string> Validate(
            TenantInvitationEmailRequestedV1? payload)
        {
            var errors = new List<string>();

            if (payload is null)
            {
                errors.Add("Payload is required.");
                return errors;
            }

            if (payload.InvitationId == Guid.Empty)
            {
                errors.Add("InvitationId is required.");
            }

            if (string.IsNullOrWhiteSpace(payload.PlainTextToken))
            {
                errors.Add("PlainTextToken is required.");
            }
            else if (payload.PlainTextToken.Length != InvitationTokenLength)
            {
                errors.Add(
                    $"PlainTextToken must contain exactly {InvitationTokenLength} characters.");
            }
            else if (payload.PlainTextToken.Any(character =>
                         !Uri.IsHexDigit(character)))
            {
                errors.Add("PlainTextToken must be a hexadecimal value.");
            }

            return errors;
        }
    }
}
