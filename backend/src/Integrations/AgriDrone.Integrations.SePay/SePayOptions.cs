namespace AgriDrone.Integrations.SePay;

public sealed class SePayOptions
{
    public const string SectionName = "SePay";

    public string ApiKey { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankShortName { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string PaymentCodePrefix { get; set; } = "AGD";
    public string QrTemplate { get; set; } = "compact";
}
