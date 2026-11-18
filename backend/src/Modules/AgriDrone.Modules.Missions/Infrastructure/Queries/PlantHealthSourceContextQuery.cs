using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class PlantHealthSourceContextQuery(MissionsDbContext db) : IPlantHealthSourceContextQuery
{
    public async Task<PlantHealthSourceContext?> GetAsync(Guid missionId, Guid jobId, CancellationToken cancellationToken)
    {
        var mission = await db.DroneMissions.AsNoTracking().SingleOrDefaultAsync(m => m.Id == missionId, cancellationToken);
        var job = await db.AiProcessingJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId && j.MissionId == missionId, cancellationToken);
        if (mission is null || job is null) return null;
        PlantHealthProcessingSnapshot? snapshot;
        try { snapshot = job.Parameters.Deserialize<PlantHealthProcessingSnapshot>(); }
        catch (JsonException) { snapshot = null; }
        Guid[]? inputMediaIds = null;
        if (job.InputManifest.RootElement.ValueKind == JsonValueKind.Object && job.InputManifest.RootElement.TryGetProperty("MediaIds", out var inputs))
        {
            try { inputMediaIds = inputs.Deserialize<Guid[]>(); }
            catch (JsonException) { inputMediaIds = null; }
        }
        int? processedImageCount = job.OutputManifest.RootElement.ValueKind == JsonValueKind.Object &&
            job.OutputManifest.RootElement.TryGetProperty("TotalImagesScanned", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var scanned) ? scanned : null;
        var mediaIds = await db.MissionMedia.AsNoTracking().Where(m => m.MissionId == missionId &&
                m.Media.TenantId == mission.TenantId && m.Media.FarmId == mission.FarmId)
            .Select(m => m.MediaId).Union(db.MissionPlantObservations.Where(o => o.MissionId == missionId && o.AiJobId == jobId &&
                o.EvidenceMediaId != null && o.EvidenceMedia!.TenantId == mission.TenantId && o.EvidenceMedia.FarmId == mission.FarmId)
                .Select(o => o.EvidenceMediaId!.Value)).ToListAsync(cancellationToken);
        return new(mission.TenantId, mission.FarmId, mission.SurveyOrderId,
            mission.Purpose == MissionPurpose.PlantHealth && job.JobType == AiJobType.DiseaseDetection,
            snapshot?.FarmBoundaryVersionId, snapshot?.FarmBaseMapVersionId, snapshot?.SourceVersion ?? 0,
            job.Status == AiJobStatus.Completed, snapshot?.Provenance, snapshot?.TeamLabelLimitation, mediaIds, inputMediaIds, processedImageCount);
    }
}
