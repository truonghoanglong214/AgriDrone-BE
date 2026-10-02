using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AgriDrone.SharedInfrastructure.Authentication;

internal sealed class JwksConfigurationRetriever :
    IConfigurationRetriever<OpenIdConnectConfiguration>
{
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string address,
        IDocumentRetriever retriever,
        CancellationToken cancel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentNullException.ThrowIfNull(retriever);

        var document = await retriever
            .GetDocumentAsync(address, cancel)
            .ConfigureAwait(false);
        var keySet = new JsonWebKeySet(document);
        var configuration = new OpenIdConnectConfiguration();

        foreach (var signingKey in keySet.GetSigningKeys())
        {
            configuration.SigningKeys.Add(signingKey);
        }

        if (configuration.SigningKeys.Count == 0)
        {
            throw new InvalidOperationException(
                "The configured JWKS endpoint returned no signing keys.");
        }

        return configuration;
    }
}
