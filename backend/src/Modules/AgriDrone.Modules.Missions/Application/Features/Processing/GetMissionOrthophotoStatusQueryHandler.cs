using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
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
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal sealed partial class GetMissionOrthophotoStatusQueryHandler(
    IDroneMissionRepository missionRepository,
    IMissionsUnitOfWork unitOfWork,
    MissionsDbContext dbContext,
    IObjectStorage objectStorage,
    IObjectStorageWriter objectStorageWriter,
    INodeOdmService nodeOdmService,
    IMapPreviewService mapPreviewService,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ILogger<GetMissionOrthophotoStatusQueryHandler> logger)
    : IRequestHandler<GetMissionOrthophotoStatusQuery, Result<MissionOrthophotoStatusResult>>
{
    public async Task<Result<MissionOrthophotoStatusResult>> Handle(
        GetMissionOrthophotoStatusQuery request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId || access.FarmId != request.FarmId)
        {
            return Result.Failure<MissionOrthophotoStatusResult>(AppError.Forbidden(
                "OrthophotoStatus.FarmAccessDenied", "The manager is not assigned to this Farm."));
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<MissionOrthophotoStatusResult>(
                MissionError.NotFound(request.MissionId));
        }

        // 1. Kiểm tra xem đã có preview media asset trong database chưa
        var existingPreview = await dbContext.MissionMedia
            .Include(mm => mm.Media)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                mm => mm.MissionId == request.MissionId &&
                      mm.MediaRole == MissionMediaRole.ProcessedImage &&
                      mm.Media.MimeType == "image/jpeg",
                cancellationToken);

        var existingTif = await dbContext.MissionMedia
            .Include(mm => mm.Media)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                mm => mm.MissionId == request.MissionId &&
                      mm.MediaRole == MissionMediaRole.ProcessedImage &&
                      mm.Media.MimeType == "image/tiff",
                cancellationToken);

        if (existingPreview is not null)
        {
            return await BuildCompletedResultAsync(
                mission, existingPreview, existingTif, cancellationToken);
        }

        // 2. Lấy TaskId từ mission.Notes
        var taskId = ExtractTaskId(mission.Notes);
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return Result.Success(new MissionOrthophotoStatusResult(
                mission.Id,
                null,
                "NOT_STARTED",
                0,
                0,
                null,
                null,
                null,
                null,
                "No NodeODM task initiated yet."));
        }

        // 3. Hỏi NodeODM tiến độ
        var status = await nodeOdmService.GetTaskStatusAsync(taskId, cancellationToken);
        if (!status.IsSuccess)
        {
            return Result.Success(new MissionOrthophotoStatusResult(
                mission.Id,
                taskId,
                "ERROR",
                0,
                0,
                null,
                null,
                null,
                null,
                status.ErrorMessage));
        }

        if (status.StatusCode == 40) // COMPLETED
        {
            logger.LogInformation(
                "NodeODM task {TaskId} is COMPLETED for Mission {MissionId}. Downloading and extracting map preview...",
                taskId,
                mission.Id);

            var processSuccess = await ProcessCompletedOdmTaskAsync(
                mission, taskId, cancellationToken);

            if (processSuccess)
            {
                // Re-query saved assets
                var newlySavedPreview = await dbContext.MissionMedia
                    .Include(mm => mm.Media)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        mm => mm.MissionId == request.MissionId &&
                              mm.MediaRole == MissionMediaRole.ProcessedImage &&
                              mm.Media.MimeType == "image/jpeg",
                        cancellationToken);

                var newlySavedTif = await dbContext.MissionMedia
                    .Include(mm => mm.Media)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        mm => mm.MissionId == request.MissionId &&
                              mm.MediaRole == MissionMediaRole.ProcessedImage &&
                              mm.Media.MimeType == "image/tiff",
                        cancellationToken);

                if (newlySavedPreview is not null)
                {
                    return await BuildCompletedResultAsync(
                        mission, newlySavedPreview, newlySavedTif, cancellationToken);
                }
            }
        }
        else if (status.StatusCode == 30) // FAILED
        {
            if (mission.Status == MissionStatus.Processing)
            {
                mission.FailProcessing(timeProvider.GetUtcNow());
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return Result.Success(new MissionOrthophotoStatusResult(
            mission.Id,
            taskId,
            status.StatusName,
            status.Progress,
            status.ProcessingTimeMs,
            null,
            null,
            null,
            null,
            status.ErrorMessage));
    }

    private async Task<bool> ProcessCompletedOdmTaskAsync(
        DroneMission mission,
        string taskId,
        CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "agridrone",
            "odm_results",
            mission.Id.ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, "all.zip");
            var tifPath = Path.Combine(tempDir, "orthophoto.tif");
            var jpgPath = Path.Combine(tempDir, "map_preview.jpg");

            // 1. Download all.zip from NodeODM (NodeODM bundles all task outputs in all.zip)
            await using (var stream = await nodeOdmService.DownloadAssetAsync(taskId, "all.zip", cancellationToken))
            {
                if (stream is null)
                {
                    logger.LogError("Failed to download all.zip from NodeODM for task {TaskId}", taskId);
                    return false;
                }

                await using var fileStream = File.Create(zipPath);
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            // Extract odm_orthophoto.tif from all.zip
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var tifEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith("odm_orthophoto.tif", StringComparison.OrdinalIgnoreCase) ||
                    e.FullName.EndsWith("orthophoto.tif", StringComparison.OrdinalIgnoreCase));

                if (tifEntry is null)
                {
                    logger.LogError("odm_orthophoto.tif not found in all.zip for task {TaskId}", taskId);
                    return false;
                }

                tifEntry.ExtractToFile(tifPath, overwrite: true);
            }

            // 2. Run Python script to extract map preview & 4 corners
            var previewResult = await mapPreviewService.GeneratePreviewAsync(
                tifPath, jpgPath, maxSize: 2048, quality: 85, cancellationToken);

            if (!previewResult.IsSuccess)
            {
                logger.LogError(
                    "Failed to generate map preview for Mission {MissionId}: {Error}",
                    mission.Id,
                    previewResult.ErrorMessage);
                return false;
            }

            var now = timeProvider.GetUtcNow();
            var actorId = executionContext.ActorId ?? Guid.NewGuid();

            // 3. Upload orthophoto.tif to MinIO
            var tifBytes = await File.ReadAllBytesAsync(tifPath, cancellationToken);
            var tifChecksum = Convert.ToHexString(SHA256.HashData(tifBytes)).ToLowerInvariant();
            var tifKey = $"tenants/{mission.TenantId}/media/{mission.Id}/orthophoto.tif";
            var tifUri = $"minio://agridrone-media/{tifKey}";

            await using (var tifUploadStream = new MemoryStream(tifBytes))
            {
                await objectStorageWriter.UploadAsync(
                    tifUri, tifUploadStream, tifBytes.Length, "image/tiff", cancellationToken);
            }

            var tifAsset = MediaAsset.Create(
                Guid.NewGuid(),
                mission.TenantId,
                mission.FarmId,
                "minio",
                tifKey,
                tifUri,
                MediaType.Image,
                "image/tiff",
                tifBytes.Length,
                tifChecksum,
                actorId,
                now);

            // 4. Upload map_preview.jpg to MinIO
            var jpgBytes = await File.ReadAllBytesAsync(jpgPath, cancellationToken);
            var jpgChecksum = Convert.ToHexString(SHA256.HashData(jpgBytes)).ToLowerInvariant();
            var jpgKey = $"tenants/{mission.TenantId}/media/{mission.Id}/map_preview.jpg";
            var jpgUri = $"minio://agridrone-media/{jpgKey}";

            await using (var jpgUploadStream = new MemoryStream(jpgBytes))
            {
                await objectStorageWriter.UploadAsync(
                    jpgUri, jpgUploadStream, jpgBytes.Length, "image/jpeg", cancellationToken);
            }

            var jpgAsset = MediaAsset.Create(
                Guid.NewGuid(),
                mission.TenantId,
                mission.FarmId,
                "minio",
                jpgKey,
                jpgUri,
                MediaType.Image,
                "image/jpeg",
                jpgBytes.Length,
                jpgChecksum,
                actorId,
                now);

            // Ghi metadata 4 góc và bounds vào jpgAsset
            var metadataJson = JsonSerializer.Serialize(new
            {
                bounds = previewResult.Bounds,
                corners = previewResult.Corners,
                preview_width = previewResult.PreviewWidth,
                preview_height = previewResult.PreviewHeight,
                original_width = previewResult.OriginalWidth,
                original_height = previewResult.OriginalHeight
            });

            // Set metadata via reflection / private set
            var metadataProp = typeof(MediaAsset).GetProperty(nameof(MediaAsset.Metadata));
            metadataProp?.SetValue(jpgAsset, JsonDocument.Parse(metadataJson));

            var widthProp = typeof(MediaAsset).GetProperty(nameof(MediaAsset.WidthPx));
            widthProp?.SetValue(jpgAsset, previewResult.PreviewWidth);

            var heightProp = typeof(MediaAsset).GetProperty(nameof(MediaAsset.HeightPx));
            heightProp?.SetValue(jpgAsset, previewResult.PreviewHeight);

            // 5. Lưu vào Database
            dbContext.MediaAssets.Add(tifAsset);
            dbContext.MediaAssets.Add(jpgAsset);

            var tifMissionMedia = MissionMedia.Create(
                mission.Id, tifAsset.Id, MissionMediaRole.ProcessedImage, now);
            var jpgMissionMedia = MissionMedia.Create(
                mission.Id, jpgAsset.Id, MissionMediaRole.ProcessedImage, now);

            dbContext.MissionMedia.Add(tifMissionMedia);
            dbContext.MissionMedia.Add(jpgMissionMedia);

            mission.MarkAwaitingReview(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Successfully persisted orthophoto and map preview with 4 corners for Mission {MissionId}",
                mission.Id);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process completed ODM task for Mission {MissionId}", mission.Id);
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cleanup temp directory {Dir}", tempDir);
            }
        }
    }

    private async Task<Result<MissionOrthophotoStatusResult>> BuildCompletedResultAsync(
        DroneMission mission,
        MissionMedia previewMedia,
        MissionMedia? tifMedia,
        CancellationToken cancellationToken)
    {
        MapBounds? bounds = null;
        MapCorners? corners = null;

        try
        {
            var root = previewMedia.Media.Metadata.RootElement;
            if (root.TryGetProperty("bounds", out var bElem))
            {
                bounds = new MapBounds(
                    bElem.GetProperty("min_lat").GetDouble(),
                    bElem.GetProperty("max_lat").GetDouble(),
                    bElem.GetProperty("min_lon").GetDouble(),
                    bElem.GetProperty("max_lon").GetDouble());
            }

            if (root.TryGetProperty("corners", out var cElem))
            {
                var nw = cElem.GetProperty("north_west");
                var ne = cElem.GetProperty("north_east");
                var se = cElem.GetProperty("south_east");
                var sw = cElem.GetProperty("south_west");

                corners = new MapCorners(
                    new MapCoordinate(nw[0].GetDouble(), nw[1].GetDouble()),
                    new MapCoordinate(ne[0].GetDouble(), ne[1].GetDouble()),
                    new MapCoordinate(se[0].GetDouble(), se[1].GetDouble()),
                    new MapCoordinate(sw[0].GetDouble(), sw[1].GetDouble()));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse bounds/corners from media metadata for Mission {MissionId}", mission.Id);
        }

        var previewDownloadUri = await objectStorage.CreateDownloadUriAsync(
            previewMedia.Media.Url,
            TimeSpan.FromHours(2),
            cancellationToken);

        Uri? tifDownloadUri = null;
        if (tifMedia is not null)
        {
            tifDownloadUri = await objectStorage.CreateDownloadUriAsync(
                tifMedia.Media.Url,
                TimeSpan.FromHours(2),
                cancellationToken);
        }

        return Result.Success(new MissionOrthophotoStatusResult(
            mission.Id,
            ExtractTaskId(mission.Notes),
            "COMPLETED",
            100,
            0,
            previewDownloadUri.ToString(),
            tifDownloadUri?.ToString(),
            bounds,
            corners,
            null));
    }

    private static string? ExtractTaskId(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;

        var match = TaskIdRegex().Match(notes);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"TaskId:([a-zA-Z0-9\-]+)")]
    private static partial Regex TaskIdRegex();
}
