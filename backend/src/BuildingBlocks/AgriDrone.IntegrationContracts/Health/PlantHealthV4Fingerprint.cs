using System.Security.Cryptography;
using System.Text.Json;

namespace AgriDrone.IntegrationContracts.Health;

public static class PlantHealthV4Fingerprint
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string ForPayload(PlantHealthAnalysisReadyV4 payload) => ForValue(payload with
    {
        CausationId = Guid.Empty,
        HandoffId = Guid.Empty,
        ProducedAt = default,
        Findings = payload.Findings.OrderBy(f => f.FindingId).Select(Normalize).ToArray(),
        BoundaryExceptions = payload.BoundaryExceptions.OrderBy(e => e.SourceReferenceId, StringComparer.Ordinal).Select(e => e with
        {
            EvidenceMedia = e.EvidenceMedia.OrderBy(m => m.MediaAssetId).ThenBy(m => m.Role, StringComparer.Ordinal).ToArray()
        }).ToArray(),
        DiseaseZones = payload.DiseaseZones.OrderBy(z => z.SourceProposalId, StringComparer.Ordinal).Select(z => z with
        {
            Memberships = z.Memberships.OrderBy(m => m.PlantId).ThenBy(m => m.MappingCandidateId).ToArray(),
            GuidanceCandidates = z.GuidanceCandidates.OrderBy(g => g.GuidanceId).ThenBy(g => g.VersionNumber).ToArray(),
            EvidenceMedia = z.EvidenceMedia.OrderBy(m => m.MediaAssetId).ThenBy(m => m.Role, StringComparer.Ordinal).ToArray()
        }).ToArray()
    });

    public static string ForFinding(PlantHealthFindingV4 finding) => ForValue(Normalize(finding));

    private static PlantHealthFindingV4 Normalize(PlantHealthFindingV4 finding) => finding with
    {
        Limitations = finding.Limitations.Order(StringComparer.Ordinal).ToArray(),
        EvidenceMedia = finding.EvidenceMedia.OrderBy(m => m.MediaAssetId).ThenBy(m => m.Role, StringComparer.Ordinal).ToArray()
    };

    public static string ForValue<T>(T value) => Convert.ToHexStringLower(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions)));
}
