namespace AgriDrone.Modules.Notifications.Application.EmailTemplates;

internal static class EmailTemplateVariables
{
    public static string Require(
        IReadOnlyDictionary<string, string> variables,
        string key)
    {
        if (!variables.TryGetValue(key, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            throw new EmailTemplateException(
                "EMAIL_TEMPLATE_VARIABLE_MISSING",
                $"Email template variable '{key}' is required.");
        }

        return value.Trim();
    }

    public static string ForSubject(string value) =>
        value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
}
