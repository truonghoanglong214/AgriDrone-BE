using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Farms;

public sealed record VerifyFarmBoundaryApiRequest
{
    [Required]
    public GeoJsonPolygonRequest Boundary { get; init; } = null!;

    [Required, MaxLength(2000)]
    public string Reason { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? EvidenceReference { get; init; }

    [Required]
    public long? ExpectedFarmVersion { get; init; }

    public int? ExpectedCurrentBoundaryVersionNumber { get; init; }
}

public sealed record VerifyFarmBoundaryApiResponse(
    Guid BoundaryId,
    Guid FarmId,
    int VersionNumber,
    GeoJsonPolygonResponse? Geometry,
    string Status,
    string Source,
    string? EvidenceReference,
    Guid ReviewedBy,
    DateTimeOffset ReviewedAt,
    string ReviewReason,
    uint Version);
