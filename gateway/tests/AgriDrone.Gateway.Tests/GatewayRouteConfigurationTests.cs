using System.Text.Json;
using Xunit;

namespace AgriDrone.Gateway.Tests;

public sealed class GatewayRouteConfigurationTests
{
    [Fact]
    public void IdentityRoutesAreExplicitAndDefaultToTheSafeBe2Upstream()
    {
        using var document = LoadConfiguration();
        var routes = GetRoutes(document);
        var expectedRoutes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["identity-login"] = "/api/auth/login",
            ["identity-forgot-password"] = "/api/auth/forgot-password",
            ["identity-reset-password"] = "/api/auth/reset-password"
        };

        foreach (var expected in expectedRoutes)
        {
            var route = routes.GetProperty(expected.Key);
            Assert.Equal("be2", route.GetProperty("ClusterId").GetString());
            Assert.Equal(expected.Value,
                route.GetProperty("Match").GetProperty("Path").GetString());
            Assert.True(route.GetProperty("Order").GetInt32() < 0);
        }

        Assert.Equal("be2", document.RootElement
            .GetProperty("Gateway")
            .GetProperty("IdentityUpstream")
            .GetString());
    }

    [Fact]
    public void ApiFallbackRemainsOnBe2AndHasLowerPriority()
    {
        using var document = LoadConfiguration();
        var fallback = GetRoutes(document).GetProperty("be2-api-fallback");

        Assert.Equal("be2", fallback.GetProperty("ClusterId").GetString());
        Assert.Equal("/api/{**catch-all}",
            fallback.GetProperty("Match").GetProperty("Path").GetString());
        Assert.True(fallback.GetProperty("Order").GetInt32() > 0);
    }

    [Fact]
    public void LargeMediaUploadHasAnExplicitBodyLimitOverride()
    {
        using var document = LoadConfiguration();
        var route = GetRoutes(document).GetProperty("be2-media-upload");

        Assert.Equal("be2", route.GetProperty("ClusterId").GetString());
        Assert.True(route.GetProperty("MaxRequestBodySize").GetInt64() >=
                    5L * 1024 * 1024 * 1024);
    }

    [Fact]
    public void GatewayDoesNotExposeAnUnscopedCatchAllRoute()
    {
        using var document = LoadConfiguration();
        var routes = GetRoutes(document);

        foreach (var route in routes.EnumerateObject())
        {
            var path = route.Value.GetProperty("Match").GetProperty("Path").GetString();
            Assert.NotEqual("/{**catch-all}", path);
            Assert.NotEqual("{**catch-all}", path);
        }
    }

    [Fact]
    public void ClustersUseRegisteredYarpHealthPolicies()
    {
        using var document = LoadConfiguration();
        var clusters = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Clusters");

        foreach (var cluster in clusters.EnumerateObject())
        {
            var healthCheck = cluster.Value.GetProperty("HealthCheck");
            Assert.Equal("ConsecutiveFailures", healthCheck
                .GetProperty("Active")
                .GetProperty("Policy")
                .GetString());
            Assert.Equal("TransportFailureRate", healthCheck
                .GetProperty("Passive")
                .GetProperty("Policy")
                .GetString());
        }
    }

    private static JsonDocument LoadConfiguration() => JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));

    private static JsonElement GetRoutes(JsonDocument document) => document.RootElement
        .GetProperty("ReverseProxy")
        .GetProperty("Routes");
}
