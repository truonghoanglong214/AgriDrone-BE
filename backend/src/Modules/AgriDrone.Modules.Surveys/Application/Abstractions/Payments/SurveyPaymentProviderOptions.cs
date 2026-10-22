namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public sealed class SurveyPaymentProviderOptions
{
    public const string SectionName = "SurveyPayments";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "agridrone-hmac";
    public string CheckoutBaseUrl { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
    public int SessionLifetimeMinutes { get; set; } = 15;
    public int CallbackToleranceMinutes { get; set; } = 5;
    public int MaximumCallbackBytes { get; set; } = 65_536;
}
