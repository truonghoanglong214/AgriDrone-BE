using System.Reflection;
using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Features.Media.CompleteUploadSession;
using AgriDrone.Modules.Missions.Application.Features.Media.CreateUploadSession;
using AgriDrone.Modules.Missions.Application.Features.Media.UploadMissionMedia;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class UploadMissionMediaTests
{
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13];

    [Fact]
    public async Task UploadComputesMetadataAndRetryDoesNotWriteAgain()
    {
        var fixture = new Fixture();
        var first = await fixture.Upload(Png);
        var second = await fixture.Upload(Png);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.MediaAssetId, second.Value.MediaAssetId);
        Assert.True(second.Value.ReusedCompletion);
        Assert.Equal(1, fixture.WriteCount);
        Assert.Equal(Png, fixture.Written);
        Assert.Equal("image/png", first.Value.MimeType);
        Assert.Equal(Png.Length, first.Value.FileSizeBytes);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Png))
            .ToLowerInvariant(), first.Value.Sha256Checksum);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an image")]
    [InlineData("<html>file.jpg</html>")]
    public async Task InvalidContentDoesNotCreateSession(string text)
    {
        var fixture = new Fixture();
        var result = await fixture.Upload(System.Text.Encoding.UTF8.GetBytes(text));
        Assert.True(result.IsFailure);
        Assert.Equal("MediaUpload.InvalidFile", result.Error.Code);
        Assert.Null(fixture.Session);
        Assert.Equal(0, fixture.WriteCount);
    }

    [Fact]
    public async Task TransferFailureCanRetryPendingSession()
    {
        var fixture = new Fixture { FailWrite = true };
        await Assert.ThrowsAsync<IOException>(() => fixture.Upload(Png));
        var sessionId = fixture.Session!.Id;
        Assert.Equal(MediaUploadSessionStatus.Pending, fixture.Session.Status);
        fixture.FailWrite = false;
        var result = await fixture.Upload(Png);
        Assert.True(result.IsSuccess);
        Assert.Equal(sessionId, result.Value.UploadSessionId);
    }

    [Fact]
    public async Task OtherTenantCannotUpload()
    {
        var fixture = new Fixture();
        var result = await fixture.Upload(Png, Guid.NewGuid());
        Assert.True(result.IsFailure);
        Assert.Null(fixture.Session);
    }

    [Fact]
    public void AvifIsNotMisclassifiedAsMp4()
    {
        Assert.Null(UploadedMediaFormat.Detect(new byte[]
            { 0, 0, 0, 20, 102, 116, 121, 112, 97, 118, 105, 102 }));
    }

    private sealed class Fixture : IDroneMissionRepository, IMediaUploadSessionRepository,
        IObjectStorageWriter, IExecutionContext
    {
        private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
        private readonly DroneMission mission;
        private readonly UploadMissionMediaCommandHandler handler;
        public MediaUploadSession? Session;
        public int WriteCount;
        public byte[]? Written;
        public bool FailWrite;
        public Guid? TenantId { get; } = Guid.NewGuid();
        public Guid? ActorId { get; } = Guid.NewGuid();
        public bool IsInitialized => true;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => default;

        public Fixture()
        {
            using var parameters = JsonDocument.Parse("{}");
            mission = DroneMission.Create(TenantId!.Value, Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), null, "UPLOAD-TEST", MissionType.Mapping, null,
                parameters, null, ActorId!.Value, now);
            var sender = DispatchProxy.Create<ISender, SenderProxy>();
            ((SenderProxy)(object)sender).OnSend = message =>
            {
                if (message is CreateUploadSessionCommand create)
                {
                    Session ??= MediaUploadSession.Create(create.TenantId, create.FarmId,
                        create.MissionId, create.OperationId, Guid.NewGuid(), ActorId.Value,
                        create.FileName, create.MimeType, create.MediaType, create.FileSizeBytes,
                        create.Sha256Checksum, "minio://media/test", now, now.AddHours(1));
                    return Task.FromResult(Result.Success(new CreateUploadSessionResult(
                        Session.Id, Session.MediaAssetId, new Uri("https://example.invalid"),
                        Session.ExpiresAt, Session.Status, 1, false)));
                }
                if (message is CompleteUploadSessionCommand)
                {
                    var session = Session!;
                    var reused = session.Status == MediaUploadSessionStatus.Completed;
                    if (!reused)
                    {
                        session.BeginVerification(now);
                        session.CompleteVerification(now);
                    }
                    return Task.FromResult(Result.Success(new CompleteUploadSessionResult(
                        session.Id, session.MediaAssetId, session.Status, session.MimeType,
                        session.FileSizeBytes, session.ExpectedChecksum, reused)));
                }
                throw new NotSupportedException();
            };
            handler = new(this, this, this, this, sender);
        }

        public async Task<Result<CompleteUploadSessionResult>> Upload(byte[] bytes, Guid? tenant = null)
        {
            using var stream = new MemoryStream(bytes);
            return await handler.Handle(new UploadMissionMediaCommand(tenant ?? TenantId!.Value,
                mission.FarmId, mission.Id, stream), CancellationToken.None);
        }

        public Task<DroneMission?> GetByIdAsync(Guid id, Guid tenant, Guid farm,
            CancellationToken cancellationToken = default) => Task.FromResult<DroneMission?>(
                id == mission.Id && tenant == mission.TenantId && farm == mission.FarmId ? mission : null);
        public Task<bool> CodeExistsAsync(Guid farm, string code, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public void Add(DroneMission value) => throw new NotSupportedException();
        public void Add(MediaUploadSession value) => Session = value;
        public Task<MediaUploadSession?> GetByIdAsync(Guid tenant, Guid farm, Guid missionId,
            Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Session);
        public Task<MediaUploadSession?> GetByOperationIdAsync(Guid tenant, Guid farm, Guid missionId,
            Guid operation, CancellationToken cancellationToken = default) =>
            Task.FromResult(Session?.OperationId == operation ? Session : null);
        public async Task UploadAsync(string uri, Stream content, long length, string mime,
            CancellationToken cancellationToken = default)
        {
            if (FailWrite) throw new IOException("Simulated storage outage");
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Written = buffer.ToArray();
            WriteCount++;
        }
    }

    public class SenderProxy : DispatchProxy
    {
        public Func<object, object> OnSend { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => OnSend(args![0]!);
    }
}
