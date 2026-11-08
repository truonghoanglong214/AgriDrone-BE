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
    IMetashapeService metashapeService,
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
                "No photogrammetry processing task initiated yet."));
        }

        // 3. Hỏi Metashape service tiến độ
        var status = await metashapeService.GetTaskStatusAsync(taskId, cancellationToken);
        if (!status.IsSuccess)
        {
            if (mission.Status == MissionStatus.Processing &&
                status.ErrorMessage is not null &&
                (status.ErrorMessage.Contains("NotFound", StringComparison.OrdinalIgnoreCase) ||
                 status.ErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogWarning(
                    "Metashape task {TaskId} not found on service for Mission {MissionId}. Releasing from PROCESSING state.",
                    taskId,
                    mission.Id);

                mission.FailProcessing(timeProvider.GetUtcNow());
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

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

        if (status.StatusCode == 40 && status.Output is not null) // COMPLETED
        {
            logger.LogInformation(
                "Metashape task {TaskId} is COMPLETED for Mission {MissionId}. Persisting assets to DB...",
                taskId,
                mission.Id);

            var processSuccess = await ProcessCompletedMetashapeTaskAsync(
                mission, status.Output, cancellationToken);

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
            status.ErrorMessage ?? status.Stage));
    }

    private async Task<bool> ProcessCompletedMetashapeTaskAsync(
        DroneMission mission,
        MetashapeOutputMetadata output,
        CancellationToken cancellationToken)
    {
        try
        {
            var now = timeProvider.GetUtcNow();
            var actorId = executionContext.ActorId ?? Guid.NewGuid();

            var tifKey = output.TifStorageKey ?? $"tenants/{mission.TenantId}/media/{mission.Id}/orthophoto.tif";
            var tifUri = $"minio://agridrone-media/{tifKey}";
            var tifSize = output.TifSizeBytes ?? 0;
            var tifChecksum = output.TifChecksum ?? string.Empty;

            var tifAsset = MediaAsset.Create(
                Guid.NewGuid(),
                mission.TenantId,
                mission.FarmId,
                "minio",
                tifKey,
                tifUri,
                MediaType.Image,
                "image/tiff",
                tifSize,
                tifChecksum,
                actorId,
                now);

            var jpgKey = output.JpgStorageKey ?? $"tenants/{mission.TenantId}/media/{mission.Id}/map_preview.jpg";
            var jpgUri = $"minio://agridrone-media/{jpgKey}";
            var jpgSize = output.JpgSizeBytes ?? 0;
            var jpgChecksum = output.JpgChecksum ?? string.Empty;

            var jpgAsset = MediaAsset.Create(
                Guid.NewGuid(),
                mission.TenantId,
                mission.FarmId,
                "minio",
                jpgKey,
                jpgUri,
                MediaType.Image,
                "image/jpeg",
                jpgSize,
                jpgChecksum,
                actorId,
                now);

            // Ghi metadata 4 góc và bounds vào jpgAsset theo đúng chuẩn snake_case và mảng tọa độ
            var metadataJson = JsonSerializer.Serialize(new
            {
                bounds = output.Bounds is not null ? new
                {
                    min_lat = output.Bounds.MinLat,
                    max_lat = output.Bounds.MaxLat,
                    min_lon = output.Bounds.MinLon,
                    max_lon = output.Bounds.MaxLon
                } : null,
                corners = output.Corners is not null ? new
                {
                    north_west = new[] { output.Corners.NorthWest.Latitude, output.Corners.NorthWest.Longitude },
                    north_east = new[] { output.Corners.NorthEast.Latitude, output.Corners.NorthEast.Longitude },
                    south_east = new[] { output.Corners.SouthEast.Latitude, output.Corners.SouthEast.Longitude },
                    south_west = new[] { output.Corners.SouthWest.Latitude, output.Corners.SouthWest.Longitude }
                } : null,
                preview_width = output.OriginalWidth,
                preview_height = output.OriginalHeight,
                original_width = output.OriginalWidth,
                original_height = output.OriginalHeight
            });

            var metadataProp = typeof(MediaAsset).GetProperty(nameof(MediaAsset.Metadata));
            metadataProp?.SetValue(jpgAsset, JsonDocument.Parse(metadataJson));

            if (output.OriginalWidth.HasValue)
            {
                var widthProp = typeof(MediaAsset).GetProperty(nameof(MediaAsset.WidthPx));
                widthProp?.SetValue(jpgAsset, output.OriginalWidth.Value);
            }

            if (output.OriginalHeight.HasValue)
            {
                var heightProp = typeof(MediaAsset).GetProperty(nameof(MediaAsset.HeightPx));
                heightProp?.SetValue(jpgAsset, output.OriginalHeight.Value);
            }

            // Lưu vào Database
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
                "Successfully persisted Metashape orthophoto and map preview with 4 corners for Mission {MissionId}",
                mission.Id);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process completed Metashape task for Mission {MissionId}", mission.Id);
            return false;
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
            if (root.TryGetProperty("bounds", out var bElem) && bElem.ValueKind == JsonValueKind.Object)
            {
                var minLat = GetDoubleProperty(bElem, "min_lat", "MinLat");
                var maxLat = GetDoubleProperty(bElem, "max_lat", "MaxLat");
                var minLon = GetDoubleProperty(bElem, "min_lon", "MinLon");
                var maxLon = GetDoubleProperty(bElem, "max_lon", "MaxLon");

                if (minLat.HasValue && maxLat.HasValue && minLon.HasValue && maxLon.HasValue)
                {
                    bounds = new MapBounds(minLat.Value, maxLat.Value, minLon.Value, maxLon.Value);
                }
            }

            if (root.TryGetProperty("corners", out var cElem) && cElem.ValueKind == JsonValueKind.Object)
            {
                var nw = ParseCoordinate(cElem, "north_west", "NorthWest");
                var ne = ParseCoordinate(cElem, "north_east", "NorthEast");
                var se = ParseCoordinate(cElem, "south_east", "SouthEast");
                var sw = ParseCoordinate(cElem, "south_west", "SouthWest");

                if (nw != null && ne != null && se != null && sw != null)
                {
                    corners = new MapCorners(nw, ne, se, sw);
                }
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

    private static double? GetDoubleProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            if (element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number)
            {
                return val.GetDouble();
            }
        }
        return null;
    }

    private static MapCoordinate? ParseCoordinate(JsonElement element, string snakeName, string pascalName)
    {
        if (element.TryGetProperty(snakeName, out var elem) || element.TryGetProperty(pascalName, out elem))
        {
            if (elem.ValueKind == JsonValueKind.Array && elem.GetArrayLength() >= 2)
            {
                return new MapCoordinate(elem[0].GetDouble(), elem[1].GetDouble());
            }
            if (elem.ValueKind == JsonValueKind.Object)
            {
                var lat = GetDoubleProperty(elem, "Latitude", "latitude", "lat");
                var lon = GetDoubleProperty(elem, "Longitude", "longitude", "lon");
                if (lat.HasValue && lon.HasValue)
                {
                    return new MapCoordinate(lat.Value, lon.Value);
                }
            }
        }
        return null;
    }

    private static string? ExtractTaskId(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;

        var match = TaskIdRegex().Match(notes);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"(?:TaskId:|uuid:)([a-zA-Z0-9\-]+)")]
    private static partial Regex TaskIdRegex();
}
