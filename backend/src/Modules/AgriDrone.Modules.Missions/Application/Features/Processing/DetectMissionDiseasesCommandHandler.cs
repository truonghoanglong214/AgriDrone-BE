using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal sealed class DetectMissionDiseasesCommandHandler(
    MissionsDbContext dbContext,
    MissionImageJobStore jobs,
    IMissionDiseaseJobProcessor processor,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<DetectMissionDiseasesCommand, Result<MissionDiseaseJobResponse>>,
      IRequestHandler<GetMissionDiseaseJobQuery, Result<MissionDiseaseJobResponse>>
{
    public async Task<Result<MissionDiseaseJobResponse>> Handle(
        DetectMissionDiseasesCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId || actorId == Guid.Empty)
        {
            return Result.Failure<MissionDiseaseJobResponse>(MissionError.CurrentUserRequired());
        }

        if (!await CanAccessAsync(request.TenantId, request.FarmId, cancellationToken))
        {
            return Denied();
        }

        if (!double.IsFinite(request.ConfidenceThreshold) || request.ConfidenceThreshold is < 0 or > 1 ||
            (request.MatchingToleranceMeters is { } mt && (!double.IsFinite(mt) || mt <= 0)) ||
            (request.ZoneRadiusMeters is { } zr && (!double.IsFinite(zr) || zr <= 0)) ||
            request.MinDiseasedTrees is <= 0 ||
            request.PolygonGeoJson is { ValueKind: not (JsonValueKind.Object or JsonValueKind.Null) })
        {
            return Result.Failure<MissionDiseaseJobResponse>(
                AppError.Validation("DetectDiseases.InvalidInput", "Provide valid confidence, tolerances and GeoJSON."));
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var locked = await dbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM mission.drone_missions
            WHERE id = {request.MissionId} AND tenant_id = {request.TenantId} AND farm_id = {request.FarmId}
            FOR UPDATE
            """).ToListAsync(cancellationToken);

        if (locked.Count == 0)
        {
            return Result.Failure<MissionDiseaseJobResponse>(MissionError.NotFound(request.MissionId));
        }

        var input = JsonSerializer.Serialize(new MissionDiseaseInput(
            actorId,
            request.ConfidenceThreshold,
            request.PolygonGeoJson,
            request.MatchingToleranceMeters,
            request.ZoneRadiusMeters,
            request.MinDiseasedTrees));

        var active = await jobs.FindActiveAsync(request.MissionId, MissionImageJobStore.Disease, cancellationToken);
        if (active is not null)
        {
            if (!await jobs.MatchesAsync(active.Id, input, cancellationToken))
            {
                return Result.Failure<MissionDiseaseJobResponse>(AppError.Conflict(
                    "Disease.JobAlreadyActive",
                    "Wait for the current disease detection job before changing its inputs."));
            }

            await tx.CommitAsync(cancellationToken);
            return Result.Success(active.ToDiseaseResponse());
        }

        var contextResult = await processor.PrepareContextAsync(
            request.TenantId, request.FarmId, request.MissionId, cancellationToken);
        if (contextResult.IsFailure)
        {
            return Result.Failure<MissionDiseaseJobResponse>(contextResult.Error);
        }

        var id = Guid.CreateVersion7();
        var now = timeProvider.GetUtcNow();
        await jobs.InsertAsync(
            id, request.TenantId, request.FarmId, request.MissionId,
            MissionImageJobStore.Disease, input, now, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Result.Success(new MissionDiseaseJobResponse(
            id, request.MissionId, "QUEUED", now, null, null, null, null));
    }

    public async Task<Result<MissionDiseaseJobResponse>> Handle(
        GetMissionDiseaseJobQuery request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessAsync(request.TenantId, request.FarmId, cancellationToken))
        {
            return Denied();
        }

        var job = await jobs.FindAsync(
            request.TenantId, request.FarmId, request.MissionId,
            MissionImageJobStore.Disease, request.JobId, cancellationToken);

        return job is null
            ? Result.Failure<MissionDiseaseJobResponse>(AppError.NotFound("Disease.JobNotFound", "Disease detection job was not found."))
            : Result.Success(job.ToDiseaseResponse());
    }

    private async Task<bool> CanAccessAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        return access.IsAllowed && access.TenantId == tenantId && access.FarmId == farmId;
    }

    private static Result<MissionDiseaseJobResponse> Denied() =>
        Result.Failure<MissionDiseaseJobResponse>(AppError.Forbidden(
            "DetectDiseases.FarmAccessDenied", "The manager is not assigned to this Farm."));

    // Ảnh gốc của detection: ưu tiên khóa MinIO đầy đủ (chính xác), sau đó mới tới tên file (service cũ chưa trả khóa).
    internal sealed class DetectionSourceIndex(IEnumerable<MissionMedia> media)
    {
        private readonly Dictionary<string, MissionMedia> _byKey = media.Where(m => !string.IsNullOrEmpty(m.Media?.StorageKey))
            .GroupBy(m => m.Media.StorageKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        private readonly Dictionary<string, MissionMedia> _byName = media.Where(m => !string.IsNullOrEmpty(m.Media?.StorageKey))
            .GroupBy(m => Path.GetFileName(m.Media.StorageKey), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        public MissionMedia? Find(DiseaseDetectionItem detection) =>
            detection.SourceStorageKey is { } key ? _byKey.GetValueOrDefault(key)
            : string.IsNullOrEmpty(detection.ImageName) ? null : _byName.GetValueOrDefault(detection.ImageName);
    }

    internal sealed record DerivedImage(MissionMedia Source, string StorageUri, long SizeBytes, string Sha256);

    // Chỉ nhận ảnh nằm trong thư mục disease của đúng Tenant/Mission, có SHA-256 hợp lệ và ảnh gốc đã đăng ký.
    internal static string? DerivedStorageUri(string key, string? sha256, MissionMedia? source, Guid tenantId, Guid missionId)
    {
        var prefix = $"tenants/{tenantId:D}/media/{missionId:D}/disease/";
        if (source is null || !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || key.Contains("..", StringComparison.Ordinal) ||
            sha256 is not { Length: 64 } || !sha256.All(char.IsAsciiHexDigit) ||
            !source.Media.Url.EndsWith(source.Media.StorageKey, StringComparison.Ordinal))
            return null;
        // Cùng bucket với ảnh gốc: thay phần khóa trong storage URI (minio://bucket/<khóa>).
        return source.Media.Url[..^source.Media.StorageKey.Length] + key;
    }

    internal static decimal? GetGpsDistanceMeters(double? distance, string imageName)
    {
        if (distance is null)
        {
            return null;
        }

        // PostgreSQL numeric(8,3) cannot store 100,000m, including after rounding.
        if (!double.IsFinite(distance.Value) || distance.Value < 0 ||
            Math.Round(distance.Value, 3, MidpointRounding.AwayFromZero) >= 100_000)
        {
            throw new DiseaseDetectionLocationException(imageName);
        }

        return (decimal)Math.Round(distance.Value, 3, MidpointRounding.AwayFromZero);
    }
}
