using System.Security.Cryptography;
using System.Text;

namespace AgriDrone.Modules.Surveys.Domain;

public readonly record struct PaymentEventIdentity
{
    private PaymentEventIdentity(
        string provider,
        string providerReference,
        string providerEventId,
        string deduplicationKey)
    {
        Provider = provider;
        ProviderReference = providerReference;
        ProviderEventId = providerEventId;
        DeduplicationKey = deduplicationKey;
    }

    public string Provider { get; }
    public string ProviderReference { get; }
    public string ProviderEventId { get; }
    public string DeduplicationKey { get; }

    public static PaymentEventIdentity Create(
        string provider,
        string providerReference,
        string providerEventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId);
        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var normalizedReference = providerReference.Trim();
        var normalizedEventId = providerEventId.Trim();
        if (normalizedProvider.Length > 50 ||
            normalizedReference.Length > 150 ||
            normalizedEventId.Length > 150)
        {
            throw new ArgumentOutOfRangeException(
                nameof(providerEventId),
                "Payment event identity exceeds the supported provider limits.");
        }
        var canonicalValue = string.Join(
            '\n',
            normalizedProvider,
            normalizedReference,
            normalizedEventId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalValue));
        return new PaymentEventIdentity(
            normalizedProvider,
            normalizedReference,
            normalizedEventId,
            Convert.ToHexString(hash).ToLowerInvariant());
    }
}
