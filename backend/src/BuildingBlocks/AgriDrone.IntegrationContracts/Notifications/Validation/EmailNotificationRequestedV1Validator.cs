using System.Net.Mail;

namespace AgriDrone.IntegrationContracts.Notifications.Validation;

public static class EmailNotificationRequestedV1Validator
{
    private const int MaximumRecipientCount = 50;
    private const int MaximumTemplateKeyLength = 100;
    private const int MaximumVariableCount = 64;
    private const int MaximumVariableKeyLength = 100;
    private const int MaximumVariableValueLength = 4_000;

    public static IReadOnlyList<string> Validate(
        EmailNotificationRequestedV1? payload)
    {
        var errors = new List<string>();

        if (payload is null)
        {
            errors.Add("Payload is required.");
            return errors;
        }

        if (payload.NotificationId == Guid.Empty)
        {
            errors.Add("NotificationId is required.");
        }

        ValidateTemplateKey(payload.TemplateKey, errors);
        ValidateRecipients(payload.Recipients, errors);
        ValidateVariables(payload.Variables, errors);

        return errors;
    }

    private static void ValidateTemplateKey(
        string? templateKey,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            errors.Add("TemplateKey is required.");
        }
        else if (templateKey.Length > MaximumTemplateKeyLength)
        {
            errors.Add(
                $"TemplateKey cannot exceed {MaximumTemplateKeyLength} characters.");
        }
    }

    private static void ValidateRecipients(
        IReadOnlyList<EmailRecipientV1>? recipients,
        List<string> errors)
    {
        if (recipients is null || recipients.Count == 0)
        {
            errors.Add("At least one recipient is required.");
            return;
        }

        if (recipients.Count > MaximumRecipientCount)
        {
            errors.Add(
                $"Recipients cannot contain more than {MaximumRecipientCount} items.");
        }

        var uniqueAddresses = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < recipients.Count; index++)
        {
            var recipient = recipients[index];

            if (recipient is null ||
                string.IsNullOrWhiteSpace(recipient.Address) ||
                !MailAddress.TryCreate(recipient.Address, out _))
            {
                errors.Add($"Recipients[{index}].Address is invalid.");
                continue;
            }

            if (!uniqueAddresses.Add(recipient.Address.Trim()))
            {
                errors.Add(
                    $"Recipients[{index}].Address is duplicated.");
            }
        }
    }

    private static void ValidateVariables(
        IReadOnlyDictionary<string, string>? variables,
        List<string> errors)
    {
        if (variables is null)
        {
            errors.Add("Variables are required.");
            return;
        }

        if (variables.Count > MaximumVariableCount)
        {
            errors.Add(
                $"Variables cannot contain more than {MaximumVariableCount} items.");
        }

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Key) ||
                variable.Key.Length > MaximumVariableKeyLength)
            {
                errors.Add(
                    $"Variable key must contain 1 to {MaximumVariableKeyLength} characters.");
            }

            if (variable.Value is null ||
                variable.Value.Length > MaximumVariableValueLength)
            {
                errors.Add(
                    $"Variable '{variable.Key}' cannot exceed {MaximumVariableValueLength} characters.");
            }
        }
    }
}
