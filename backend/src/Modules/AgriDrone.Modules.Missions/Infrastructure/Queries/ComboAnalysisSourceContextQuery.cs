using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class ComboAnalysisSourceContextQuery(MissionsDbContext db) : IComboAnalysisSourceContextQuery
{
    public async Task<ComboAnalysisSourceContext?> GetAsync(Guid missionId, Guid jobId, CancellationToken cancellationToken)
    {
        var mission = await db.DroneMissions.AsNoTracking().SingleOrDefaultAsync(m => m.Id == missionId, cancellationToken);
        var job = await db.AiProcessingJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId && j.MissionId == missionId, cancellationToken);
        if (mission is null || job is null) return null;

        ComboAnalysisProcessingSnapshot? comboSnapshot;
        try
        {
            comboSnapshot = job.Parameters.Deserialize<ComboAnalysisProcessingSnapshot>();
        }
        catch (JsonException)
        {
            comboSnapshot = null;
        }

        if (comboSnapshot is null) return null;

        Guid[]? inputMediaIds = null;
        if (job.InputManifest.RootElement.ValueKind == JsonValueKind.Object && job.InputManifest.RootElement.TryGetProperty("MediaIds", out var inputs))
        {
            try
            {
                inputMediaIds = inputs.Deserialize<Guid[]>();
            }
            catch (JsonException)
            {
                inputMediaIds = null;
            }
        }

        var inputMediaIdList = inputMediaIds ?? [];
        var mediaIds = await db.MissionMedia.AsNoTracking()
            .Where(m => m.MissionId == missionId && inputMediaIdList.Contains(m.MediaId))
            .Select(m => m.MediaId)
            .Union(db.MissionPlantObservations.Where(o => o.MissionId == missionId && o.AiJobId == jobId &&
                o.EvidenceMediaId != null)
                .Select(o => o.EvidenceMediaId!.Value))
            .ToListAsync(cancellationToken);

        var isComboEligible = mission.Purpose == MissionPurpose.Capture && job.JobType == AiJobType.DiseaseDetection &&
                              job.HasCompleteComboInputCoverage();

        return new ComboAnalysisSourceContext(
            mission.TenantId,
            mission.FarmId,
            mission.SurveyOrderId,
            mission.CaptureAttemptId,
            mission.Id,
            job.Id,
            comboSnapshot.SourceRevision,
            job.Status == AiJobStatus.Completed,
            isComboEligible,
            comboSnapshot.FarmBoundaryVersionId,
            comboSnapshot.FarmBaseMapVersionId,
            comboSnapshot.BaselineMapVersionId,
            comboSnapshot.MappingCandidateBatchId,
            comboSnapshot.MappingKind,
            comboSnapshot.BusinessPolicyVersionId,
            comboSnapshot.InputApprovalId,
            comboSnapshot.InputManifestId,
            comboSnapshot.InputManifestHash,
            comboSnapshot.SupersedesHandoffId,
            comboSnapshot.Provenance,
            comboSnapshot.LimitationNotice,
            mediaIds,
            inputMediaIds,
            comboSnapshot.ApprovedJobReference);
    }
}
