using System.Globalization;
using System.Text;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

internal sealed class MissionProcessor(
    IDroneMissionRepository missionRepository,
    IMissionsUnitOfWork unitOfWork,
    MissionsDbContext dbContext,
    IObjectStorage objectStorage,
    IGeotagService geotagService,
    TimeProvider timeProvider,
    ILogger<MissionProcessor> logger) : IMissionProcessor
{
    public async Task ProcessMissionAsync(
        MissionProcessingWorkItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        logger.LogInformation(
            "Starting background processing for Mission {MissionId} (Farm: {FarmId}, Tenant: {TenantId})",
            item.MissionId,
            item.FarmId,
            item.TenantId);

        var mission = await missionRepository.GetByIdAsync(
            item.MissionId,
            item.TenantId,
            item.FarmId,
            cancellationToken);

        if (mission is null)
        {
            logger.LogWarning("Mission {MissionId} not found for processing.", item.MissionId);
            return;
        }

        if (mission.Status != MissionStatus.ReadyForProcessing)
        {
            logger.LogInformation(
                "Mission {MissionId} status is {Status}, skipping processing.",
                item.MissionId,
                mission.Status);
            return;
        }

        var now = timeProvider.GetUtcNow();
        mission.StartProcessing(now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to transition Mission {MissionId} to Processing state.", item.MissionId);
            return;
        }

        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "agridrone",
            "missions",
            item.MissionId.ToString("N"));

        var imagesDirectory = Path.Combine(tempDirectory, "images");
        var csvPath = Path.Combine(tempDirectory, "flight_log.csv");

        try
        {
            Directory.CreateDirectory(imagesDirectory);

            // 1. Lấy telemetry points từ DB (1 query duy nhất)
            var points = await dbContext.MissionTelemetryPoints
                .AsNoTracking()
                .Where(p => p.MissionId == item.MissionId)
                .OrderBy(p => p.RecordedAt)
                .ToListAsync(cancellationToken);

            if (points.Count < 2)
            {
                logger.LogError(
                    "Mission {MissionId} has insufficient telemetry points ({Count}). Failing processing.",
                    item.MissionId,
                    points.Count);

                mission.FailProcessing(timeProvider.GetUtcNow());
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            // Ghi ra file CSV tạm cho script Python
            await using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
            {
                await writer.WriteLineAsync("timestamp,lat,lon,alt");
                foreach (var point in points)
                {
                    var timeStr = point.RecordedAt.UtcDateTime.ToString(
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture);

                    var latStr = point.Location.Y.ToString(
                        CultureInfo.InvariantCulture);

                    var lonStr = point.Location.X.ToString(
                        CultureInfo.InvariantCulture);

                    var altStr = (point.AltitudeM ?? 0m).ToString(
                        CultureInfo.InvariantCulture);

                    await writer.WriteLineAsync($"{timeStr},{latStr},{lonStr},{altStr}");
                }
            }

            // 2. Lấy danh sách ảnh thô (RawImage) của Mission
            var mediaList = await dbContext.MissionMedia
                .Include(mm => mm.Media)
                .AsNoTracking()
                .Where(mm =>
                    mm.MissionId == item.MissionId &&
                    mm.MediaRole == MissionMediaRole.RawImage)
                .ToListAsync(cancellationToken);

            if (mediaList.Count == 0)
            {
                logger.LogError(
                    "Mission {MissionId} has no RawImage media. Failing processing.",
                    item.MissionId);

                mission.FailProcessing(timeProvider.GetUtcNow());
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            logger.LogInformation(
                "Downloading {Count} raw images from MinIO for Mission {MissionId}...",
                mediaList.Count,
                item.MissionId);

            // 3. Tải ảnh từ MinIO về thư mục tạm
            foreach (var mediaItem in mediaList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = $"{mediaItem.MediaId:N}.jpg";
                var localFilePath = Path.Combine(imagesDirectory, fileName);

                await objectStorage.ReadAsync(
                    mediaItem.Media.Url,
                    async (stream, ct) =>
                    {
                        await using var fileStream = File.Create(localFilePath);
                        await stream.CopyToAsync(fileStream, ct);
                    },
                    cancellationToken);
            }

            logger.LogInformation(
                "Downloaded all images to {Directory}. Applying Python geotag...",
                imagesDirectory);

            // 4. Gọi Python script gắn GPS EXIF
            var geotagResult = await geotagService.ApplyGeotagAsync(
                imagesDirectory,
                csvPath,
                timeOffsetSeconds: 0,
                maxDiffSeconds: 3.0,
                cancellationToken);

            if (!geotagResult.IsSuccess)
            {
                logger.LogError(
                    "Geotagging failed for Mission {MissionId}: {Error}",
                    item.MissionId,
                    geotagResult.ErrorMessage);

                mission.FailProcessing(timeProvider.GetUtcNow());
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            logger.LogInformation(
                "Geotagging completed successfully for Mission {MissionId}. Images in {Directory} are ready for NodeODM.",
                item.MissionId,
                imagesDirectory);

            // Bước tiếp theo (NodeODM) sẽ tiếp nhận thư mục imagesDirectory tại đây.
            // Khi hoàn thành ghép bản đồ và upload file .tif về MinIO, thư mục tạm sẽ được dọn dẹp.
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled error during processing of Mission {MissionId}",
                item.MissionId);

            try
            {
                mission.FailProcessing(timeProvider.GetUtcNow());
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "Failed to mark Mission {MissionId} as Failed.", item.MissionId);
            }
        }
    }
}
