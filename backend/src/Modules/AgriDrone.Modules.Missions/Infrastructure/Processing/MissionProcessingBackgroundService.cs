using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

internal sealed class MissionProcessingBackgroundService(
    IMissionProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<MissionProcessingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MissionProcessingBackgroundService started and awaiting finalize events.");

        // Khôi phục các mission bị dở dang (nếu server khởi động lại khi có mission ReadyForProcessing)
        await EnqueuePendingMissionsOnStartupAsync(stoppingToken);

        // Lắng nghe sự kiện từ Channel khi có request Finalize
        await foreach (var workItem in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                logger.LogInformation(
                    "Mission processing event received for Mission {MissionId}.",
                    workItem.MissionId);

                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IMissionProcessor>();

                await processor.ProcessMissionAsync(workItem, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unhandled exception while processing Mission {MissionId}",
                    workItem.MissionId);
            }
        }

        logger.LogInformation("MissionProcessingBackgroundService stopped.");
    }

    private async Task EnqueuePendingMissionsOnStartupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MissionsDbContext>();

            var pendingMissions = await dbContext.DroneMissions
                .AsNoTracking()
                .Where(m => m.Status == MissionStatus.ReadyForProcessing)
                .Select(m => new MissionProcessingWorkItem(m.TenantId, m.FarmId, m.Id))
                .ToListAsync(cancellationToken);

            if (pendingMissions.Count > 0)
            {
                logger.LogInformation(
                    "Recovered {Count} pending ReadyForProcessing missions on startup. Enqueuing for processing.",
                    pendingMissions.Count);

                foreach (var item in pendingMissions)
                {
                    await queue.EnqueueAsync(item, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check pending missions on startup.");
        }
    }
}
