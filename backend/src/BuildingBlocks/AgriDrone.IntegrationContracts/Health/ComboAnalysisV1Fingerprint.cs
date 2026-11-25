using System.Security.Cryptography;
using System.Text.Json;

namespace AgriDrone.IntegrationContracts.Health;

public static class ComboAnalysisV1Fingerprint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string ForPayload(ComboAnalysisReadyV1 payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var normalizedPayload = payload with
        {
            CausationId = Guid.Empty,
            HandoffId = Guid.Empty,
            ProducedAt = default,
            Findings = payload.Findings
                .OrderBy(f => f.FindingId)
                .Select(NormalizeFinding)
                .ToArray(),
            BoundaryExceptions = payload.BoundaryExceptions
                .OrderBy(e => e.SourceReferenceId, StringComparer.Ordinal)
                .Select(e => e with
                {
                    SourceReferenceId = e.SourceReferenceId.Trim(),
                    EvidenceMedia = e.EvidenceMedia
                        .OrderBy(m => m.MediaAssetId)
                        .ThenBy(m => m.Role, StringComparer.Ordinal)
                        .ToArray()
                })
                .ToArray(),
            DiseaseZones = payload.DiseaseZones
                .OrderBy(z => z.SourceProposalId, StringComparer.Ordinal)
                .Select(z => z with
                {
                    SourceProposalId = z.SourceProposalId.Trim(),
                    SuspectedClassCode = z.SuspectedClassCode.Trim().ToUpperInvariant(),
                    Memberships = z.Memberships
                        .OrderBy(m => m.FindingId)
                        .ThenBy(m => m.PlantProfileId)
                        .ThenBy(m => m.MappingCandidateId)
                        .ToArray(),
                    GuidanceCandidates = z.GuidanceCandidates
                        .OrderBy(g => g.GuidanceId)
                        .ThenBy(g => g.VersionNumber)
                        .ToArray(),
                    EvidenceMedia = z.EvidenceMedia
                        .OrderBy(m => m.MediaAssetId)
                        .ThenBy(m => m.Role, StringComparer.Ordinal)
                        .ToArray()
                })
                .ToArray()
        };

        return ForValue(normalizedPayload);
    }

    public static string ForFinding(ComboFindingV1 finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return ForValue(NormalizeFinding(finding));
    }

    public static string ComputeEvidenceFingerprint(ComboAnalysisReadyV1 payload) => ForPayload(payload);

    private static ComboFindingV1 NormalizeFinding(ComboFindingV1 finding)
    {
        var utc = finding.ObservedAt.ToUniversalTime();
        var microTicks = (utc.Ticks / 10L) * 10L;
        var normalizedObservedAt = new DateTimeOffset(microTicks, TimeSpan.Zero);

        return finding with
        {
            ObservedAt = normalizedObservedAt,
            SuspectedClassCode = finding.SuspectedClassCode.Trim().ToUpperInvariant(),
            Uncertainty = finding.Uncertainty.Trim(),
            Limitations = finding.Limitations
                .Select(l => l.Trim())
                .Order(StringComparer.Ordinal)
                .ToArray(),
            EvidenceMedia = finding.EvidenceMedia
                .OrderBy(m => m.MediaAssetId)
                .ThenBy(m => m.Role, StringComparer.Ordinal)
                .ToArray()
        };
    }

    public static string ForValue<T>(T value) => Convert.ToHexStringLower(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions)));
}
