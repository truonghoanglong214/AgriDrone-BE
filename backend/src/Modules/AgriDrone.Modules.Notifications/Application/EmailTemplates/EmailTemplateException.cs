namespace AgriDrone.Modules.Notifications.Application.EmailTemplates;

internal sealed class EmailTemplateException(
    string code,
    string message) : Exception(message)
{
    public string Code { get; } = code;
}
