using AgriDrone.Integrations.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class MultipartPresignedUrlTests
{
    [Fact]
    public async Task PartUrlSignsTheUploadIdAndPartNumberWithoutContactingStorage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Enabled"] = "true",
                ["ObjectStorage:Endpoint"] = "http://localhost:9000",
                ["ObjectStorage:Bucket"] = "agridrone-media",
                ["ObjectStorage:AccessKey"] = "test-access",
                ["ObjectStorage:SecretKey"] = "test-secret",
                ["ObjectStorage:UseSsl"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediaIntegration(configuration);
        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IMultipartObjectStorage>();

        var uri = await storage.CreatePartUploadUriAsync(
            "minio://agridrone-media/tenants/t/media/m/video.mp4",
            "upload-123", 3, TimeSpan.FromMinutes(5));

        Assert.Equal("http", uri.Scheme);
        Assert.Equal("localhost", uri.Host);
        Assert.Contains("partNumber=3", uri.Query, StringComparison.Ordinal);
        Assert.Contains("uploadId=upload-123", uri.Query, StringComparison.Ordinal);
        Assert.Contains("X-Amz-Signature=", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-secret", uri.AbsoluteUri, StringComparison.Ordinal);
    }
}
