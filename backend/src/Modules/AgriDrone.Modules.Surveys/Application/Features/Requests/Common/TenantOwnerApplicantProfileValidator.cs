using System.Net.Mail;
using AgriDrone.IntegrationContracts.Identity;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.Common;

internal static class TenantOwnerApplicantProfileValidator
{
    public static bool IsComplete(TenantOwnerRequestReference owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (string.IsNullOrWhiteSpace(owner.FullName) ||
            owner.FullName.Trim().Length > 150 ||
            string.IsNullOrWhiteSpace(owner.Email) ||
            owner.Email.Trim().Length > 320 ||
            !MailAddress.TryCreate(owner.Email.Trim(), out var address) ||
            !string.Equals(
                address.Address,
                owner.Email.Trim(),
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(owner.Phone) ||
            owner.Phone.Trim().Length > 30)
        {
            return false;
        }

        var phone = owner.Phone.Trim();
        var digits = 0;
        for (var index = 0; index < phone.Length; index++)
        {
            var character = phone[index];
            if (character is >= '0' and <= '9')
            {
                digits++;
                continue;
            }

            if (character == '+' && index == 0 ||
                character is ' ' or '-' or '(' or ')' or '.')
            {
                continue;
            }

            return false;
        }

        return digits is >= 7 and <= 15;
    }
}
