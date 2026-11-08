using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionPlantHealthPublisher(
    MissionsDbContext db,
    OutboxMessageFactory outboxMessageFactory,
    TimeProvider timeProvider) : IMissionPlantHealthPublisher
{
    private sealed record BaseMapRefRow(Guid? BaseMapVersionId, Guid? BoundaryVersionId);
    private sealed record GuidRow(Guid Value);

    public async Task<MissionPlantHealthPublishResult> PublishAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid actorId,
        Guid correlationId,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra Idempotency chống gửi trùng
        var existingMessage = await db.OutboxMessages.AsNoTracking()
            .SingleOrDefaultAsync(message => message.MessageId == idempotencyKey, cancellationToken);
        if (existingMessage is not null)
        {
            return existingMessage.EventType == IntegrationEventTypes.PlantHealthAnalysisReadyV4
                ? new MissionPlantHealthPublishResult(MissionPlantHealthPublishStatus.Queued, idempotencyKey)
                : new MissionPlantHealthPublishResult(
                    MissionPlantHealthPublishStatus.Conflict,
                    Errors: ["Idempotency-Key has already been used for another event."]);
        }

        // 2. Kiểm tra DroneMission
        var mission = await db.DroneMissions
            .Where(item => item.Id == missionId && item.FarmId == farmId && item.TenantId == tenantId)
            .SingleOrDefaultAsync(cancellationToken);
        if (mission is null)
        {
            return new MissionPlantHealthPublishResult(MissionPlantHealthPublishStatus.NotFound);
        }

        if (mission.SurveyOrderId is null || mission.SurveyOrderId == Guid.Empty)
        {
            return new MissionPlantHealthPublishResult(
                MissionPlantHealthPublishStatus.Conflict,
                Errors: ["Mission is not bound to a survey order."]);
        }

        // 3. Lấy thông tin Farm Boundary và Base Map version
        var baseMapRef = await db.Database.SqlQueryRaw<BaseMapRefRow>(
            @"SELECT fbm.id AS ""BaseMapVersionId"", fb.id AS ""BoundaryVersionId""
              FROM farm.farms f
              LEFT JOIN farm.farm_boundaries fb ON fb.farm_id = f.id AND fb.status = 'APPROVED'
              LEFT JOIN farm.farm_base_map_versions fbm ON fbm.farm_id = f.id AND fbm.status = 'PUBLISHED'
              WHERE f.id = {0}
              LIMIT 1",
            farmId)
            .FirstOrDefaultAsync(cancellationToken);

        var boundaryVersionId = baseMapRef?.BoundaryVersionId ?? Guid.Empty;
        var baseMapVersionId = baseMapRef?.BaseMapVersionId ?? Guid.Empty;

        if (boundaryVersionId == Guid.Empty)
        {
            var fb = await db.Database.SqlQueryRaw<GuidRow>(
                @"SELECT id AS ""Value"" FROM farm.farm_boundaries WHERE farm_id = {0} ORDER BY (status = 'APPROVED') DESC, version_number DESC LIMIT 1",
                farmId).FirstOrDefaultAsync(cancellationToken);
            boundaryVersionId = fb?.Value ?? Guid.Empty;
        }

        if (baseMapVersionId == Guid.Empty)
        {
            var fbm = await db.Database.SqlQueryRaw<GuidRow>(
                @"SELECT id AS ""Value"" FROM farm.farm_base_map_versions WHERE farm_id = {0} ORDER BY (status = 'PUBLISHED') DESC, version_number DESC LIMIT 1",
                farmId).FirstOrDefaultAsync(cancellationToken);
            baseMapVersionId = fbm?.Value ?? Guid.Empty;
        }

        // 4. Lấy danh sách kết quả nhận diện (Observations) của mission
        var observations = await db.MissionPlantObservations
            .Where(o => o.MissionId == missionId && o.FarmId == farmId)
            .ToListAsync(cancellationToken);

        if (observations.Count == 0)
        {
            return new MissionPlantHealthPublishResult(
                MissionPlantHealthPublishStatus.Conflict,
                Errors: ["No plant health observations found for this mission. Please run disease detection first."]);
        }

        // 5. Chuyển đổi observations thành Findings
        var findings = new List<PlantHealthFindingV4>();
        var infectedNodes = new List<InfectedPlantNode>();

        foreach (var obs in observations)
        {
            var className = "ANTHRACNOSE";
            if (obs.MatchingParameters != null &&
                obs.MatchingParameters.RootElement.TryGetProperty("class_name", out var pCls))
            {
                var str = pCls.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    className = str;
                }
            }

            var suspectedClassCode = MapClassToConditionCode(className);
            var isDisease = !className.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase);

            var mediaRefs = new List<EvidenceMediaReferenceV4>();
            if (obs.EvidenceMediaId.HasValue)
            {
                mediaRefs.Add(new EvidenceMediaReferenceV4(obs.EvidenceMediaId.Value, "crop_thumbnail"));
            }

            var plantId = obs.ResolvedPlantId ?? obs.SuggestedPlantId;
            var lon = obs.DetectedLocation?.X ?? 0.0;
            var lat = obs.DetectedLocation?.Y ?? 0.0;

            findings.Add(new PlantHealthFindingV4(
                FindingId: obs.Id,
                FindingVersion: 1,
                PlantId: plantId,
                MappingCandidateId: null,
                Position: new GeoJsonPointV3("Point", [lon, lat]),
                ObservedAt: obs.CreatedAt,
                SuspectedClassCode: suspectedClassCode,
                ClassificationNature: "SUSPECTED",
                Confidence: obs.DetectionConfidence ?? 0.85m,
                Uncertainty: "NONE",
                Limitations: ["DRONE_AERIAL_VIEW_ONLY"],
                EvidenceMedia: mediaRefs));

            if (isDisease && plantId.HasValue)
            {
                infectedNodes.Add(new InfectedPlantNode(plantId.Value, lat, lon, suspectedClassCode));
            }
        }

        // 6. Gom cụm các cây bị bệnh thành DiseaseZones đề xuất
        var diseaseZones = BuildProposedDiseaseZones(missionId, infectedNodes);

        // 7. Provenance metadata
        var provenance = new PlantHealthProvenanceV4(
            DatasetId: "dragonfruit_health_v1",
            DatasetVersion: "v1.0",
            AnnotationRuleVersion: "v1.0",
            ModelVersionId: Guid.Parse("9b1c2d3e-4f5a-6b7c-8d9e-0f1a2b3c4d5e"),
            ModelVersion: "best_disease_v1",
            ThresholdProfileVersion: "default_0.25",
            BoundaryPolicyVersion: "strict_v1");

        var occurredAt = timeProvider.GetUtcNow();

        // 8. Đóng gói payload PlantHealthAnalysisReadyV4
        var payload = new PlantHealthAnalysisReadyV4(
            CausationId: correlationId,
            HandoffId: idempotencyKey,
            SurveyOrderId: mission.SurveyOrderId.Value,
            MissionId: missionId,
            FarmId: farmId,
            FarmBoundaryVersionId: boundaryVersionId,
            FarmBaseMapVersionId: baseMapVersionId,
            JobId: Guid.NewGuid(),
            ProducedAt: occurredAt,
            ResultNature: "PRELIMINARY_SUSPECTED",
            TeamLabelLimitation: "SUNBURN, BROWN_SPOT, ANTHRACNOSE",
            Provenance: provenance,
            Findings: findings,
            BoundaryExceptions: [],
            DiseaseZones: diseaseZones);

        // 9. Đóng gói Envelope và lưu vào OutboxMessages
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.PlantHealthAnalysisReadyV4,
            idempotencyKey,
            correlationId,
            tenantId,
            actorId,
            occurredAt,
            payload);

        db.OutboxMessages.Add(outboxMessageFactory.Create(
            envelope,
            IntegrationEventTypes.PlantHealthAnalysisReadyV4,
            missionId.ToString("D")));

        await db.SaveChangesAsync(cancellationToken);

        return new MissionPlantHealthPublishResult(
            MissionPlantHealthPublishStatus.Queued, idempotencyKey);
    }

    private sealed record InfectedPlantNode(Guid PlantId, double Latitude, double Longitude, string DiseaseCode);

    private static List<ProposedDiseaseZoneV4> BuildProposedDiseaseZones(
        Guid missionId,
        List<InfectedPlantNode> infectedNodes)
    {
        var zones = new List<ProposedDiseaseZoneV4>();
        if (infectedNodes.Count == 0) return zones;

        const double zoneRadiusMeters = 4.5;
        const int minDiseasedTrees = 3;

        // Cụm 1: High-Density Outbreak (Tất cả các bệnh)
        var clusters = FindClusters(infectedNodes, zoneRadiusMeters);
        var zoneIndex = 1;

        foreach (var cluster in clusters)
        {
            if (cluster.Count < minDiseasedTrees) continue;

            var centerLat = cluster.Average(p => p.Latitude);
            var centerLon = cluster.Average(p => p.Longitude);
            var maxDist = cluster.Max(p => CalculateHaversineMeters(centerLat, centerLon, p.Latitude, p.Longitude));
            var radius = Math.Round(Math.Max(maxDist + 1.5, 3.5), 1);

            // Bệnh chiếm đa số
            var primaryDisease = cluster
                .GroupBy(p => p.DiseaseCode)
                .OrderByDescending(g => g.Count())
                .First().Key;

            var proposalId = $"mission_{missionId:N}_high_density_{zoneIndex++}";
            var geometry = CreateCircleGeoJsonPolygon(centerLon, centerLat, radius);

            var memberships = cluster
                .Select(p => new ProposedDiseaseZoneMembershipV4(
                    MappingCandidateId: null,
                    PlantId: p.PlantId,
                    Confidence: 1.0m))
                .ToList();

            var guidanceCandidates = new List<SourcedGuidanceReferenceV4>
            {
                new(
                    GuidanceId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                    Code: "ISOLATION_AND_SPRAY",
                    VersionNumber: 1,
                    SourceReference: "Quy trình xử lý Thán thư diện rộng")
            };

            zones.Add(new ProposedDiseaseZoneV4(
                SourceProposalId: proposalId,
                Geometry: geometry,
                SuspectedClassCode: primaryDisease,
                MembershipVersion: 1,
                Memberships: memberships,
                GuidanceCandidates: guidanceCandidates,
                EvidenceMedia: []));
        }

        return zones;
    }

    private static List<List<InfectedPlantNode>> FindClusters(
        List<InfectedPlantNode> nodes,
        double maxDistanceM)
    {
        var visited = new HashSet<Guid>();
        var components = new List<List<InfectedPlantNode>>();

        foreach (var node in nodes)
        {
            if (visited.Contains(node.PlantId)) continue;

            var cluster = new List<InfectedPlantNode>();
            var queue = new Queue<InfectedPlantNode>();
            queue.Enqueue(node);
            visited.Add(node.PlantId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                cluster.Add(current);

                foreach (var other in nodes)
                {
                    if (visited.Contains(other.PlantId)) continue;

                    var dist = CalculateHaversineMeters(current.Latitude, current.Longitude, other.Latitude, other.Longitude);
                    if (dist <= maxDistanceM)
                    {
                        visited.Add(other.PlantId);
                        queue.Enqueue(other);
                    }
                }
            }

            components.Add(cluster);
        }

        return components;
    }

    private static GeoJsonPolygonV3 CreateCircleGeoJsonPolygon(
        double centerLon,
        double centerLat,
        double radiusMeters,
        int points = 16)
    {
        var latDelta = radiusMeters / 111320.0;
        var lonDelta = radiusMeters / (111320.0 * Math.Cos(centerLat * Math.PI / 180.0));
        var coords = new List<IReadOnlyList<double>>();

        for (var i = 0; i < points; i++)
        {
            var angle = i * 2.0 * Math.PI / points;
            var lat = centerLat + latDelta * Math.Sin(angle);
            var lon = centerLon + lonDelta * Math.Cos(angle);
            coords.Add([Math.Round(lon, 6), Math.Round(lat, 6)]);
        }

        // Đóng vòng tròn polygon (điểm đầu = điểm cuối)
        coords.Add(coords[0]);
        return new GeoJsonPolygonV3("Polygon", [coords]);
    }

    private static string MapClassToConditionCode(string className) => className switch
    {
        var s when s.Contains("Sun_Burn", StringComparison.OrdinalIgnoreCase) => "SUNBURN",
        var s when s.Contains("Brown_Spot", StringComparison.OrdinalIgnoreCase) => "BROWN_SPOT",
        var s when s.Contains("White_Spot", StringComparison.OrdinalIgnoreCase) => "BROWN_SPOT",
        var s when s.Contains("Anthracnose", StringComparison.OrdinalIgnoreCase) => "ANTHRACNOSE",
        var s when s.Contains("Soft_Rot", StringComparison.OrdinalIgnoreCase) => "ANTHRACNOSE",
        _ => "BROWN_SPOT"
    };

    private static double CalculateHaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return r * c;
    }
}

