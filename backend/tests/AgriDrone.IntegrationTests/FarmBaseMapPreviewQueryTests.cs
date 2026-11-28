using System.Data.Common;
using AgriDrone.Database;
using AgriDrone.Database.Mapping;
using AgriDrone.Database.Surveys;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class FarmBaseMapPreviewQueryTests
{
    [Theory]
    [InlineData("preview")]
    [InlineData("owner")]
    [InlineData("v3-release")]
    public async Task PreviewAndOwnerSettlementGateTranslateWithoutStorageAccess(string query)
    {
        var builder = new NpgsqlDataSourceBuilder(
            "Host=127.0.0.1;Port=1;Database=query_translation_test;Username=test;Password=test");
        PostgreSqlEnumMappings.ConfigureDataSource(builder);
        await using var source = builder.Build();
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        AgriDroneSchemaDbContextOptions.Configure(options, source);
        options.AddInterceptors(new StopBeforeConnection());
        await using var context = new AgriDroneSchemaDbContext(options.Options);
        var releases = new V3SurveyReleaseStore(context);
        var reader = new FarmBaseMapPreviewReader(context, releases, new UnusedStorage(),
            NullLogger<FarmBaseMapPreviewReader>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => query switch
        {
            "owner" => reader.CanOwnerReadAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            "v3-release" => releases.GetByMapVersionAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None),
            _ => reader.GetAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        });

        Assert.Equal("Preview query translated; no database connection opened.", exception.Message);
    }

    private sealed class StopBeforeConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult>(
                new InvalidOperationException("Preview query translated; no database connection opened."));
    }

    private sealed class UnusedStorage : IObjectStorage
    {
        public Task<Uri> CreateDownloadUriAsync(string storageUri, TimeSpan lifetime,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ObjectUploadSession> CreateUploadSessionAsync(ObjectUploadRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StoredObjectInfo?> GetInfoAsync(string storageUri,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReadAsync(string storageUri, Func<Stream, CancellationToken, Task> reader,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageUri,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
