namespace AgriDrone.Integrations.SePay;

public sealed class SePayOptions
{
    public const string SectionName = "SePay";

    public string Environment { get; set; } = "Sandbox";
    public string MerchantId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string IpnSecretKey { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = string.Empty;
    public string ErrorUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string PaymentCodePrefix { get; set; } = "AGD";
}
