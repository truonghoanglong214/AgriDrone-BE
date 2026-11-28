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

        var observations = await db.MissionPlantObservations.AsNoTracking()
            .Where(o => o.MissionId == mission.Id && o.FarmId == mission.FarmId && o.AiJobId == job.Id)
            .Select(o => new { o.EvidenceMediaId, o.MatchingParameters })
            .ToListAsync(cancellationToken);

        var candidateMediaIds = (inputMediaIds ?? [])
            .Concat(observations.SelectMany(o => MissionPlantHealthPublisher
                .EvidenceMedia(o.EvidenceMediaId, o.MatchingParameters.RootElement)
                .Select(evidence => evidence.MediaAssetId)))
            .Distinct()
            .ToArray();

        var mediaIds = await db.MissionMedia.AsNoTracking()
            .Where(m => m.MissionId == mission.Id && candidateMediaIds.Contains(m.MediaId) &&
                        m.Media.TenantId == mission.TenantId && m.Media.FarmId == mission.FarmId &&
                        m.Media.DeletedAt == null)
            .Select(m => m.MediaId)
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
