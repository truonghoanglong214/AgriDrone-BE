namespace AgriDrone.Integrations.Email;

public sealed class SmtpEmailOptions
{
    public const string SectionName = "Email:Smtp";

    public bool Enabled { get; init; }

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 587;

    public SmtpSecurityMode SecurityMode { get; init; } = SmtpSecurityMode.StartTls;

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string FromAddress { get; init; } = string.Empty;

    public string? FromName { get; init; }

    public int TimeoutSeconds { get; init; } = 30;
}
