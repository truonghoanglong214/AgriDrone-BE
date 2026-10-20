using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AgriDrone.Modules.Farms.Domain.Boundaries;

namespace AgriDrone.Api.Contracts.Farms;

public sealed record ClassifyPointApiRequest
{
    [Required]
    public GeoJsonPointRequest Point { get; init; } = null!;

    [Required]
    public BoundaryExceptionSource Source { get; init; }

    [Required]
    [MaxLength(200)]
    public string SourceReferenceId { get; init; } = null!;

    public Guid? SurveyOrderId { get; init; }

    public Guid? MissionId { get; init; }

    [Range(0.1, 50.0)]
    public double NearBoundaryThresholdMeters { get; init; } = 5.0;
}

public sealed record ClassifyPointApiResponse(
    string Classification,
    double DistanceMeters,
    Guid? BoundaryExceptionId,
    string? State);

public sealed record ReviewBoundaryExceptionApiRequest
{
    [Required]
    public BoundaryExceptionDecision Decision { get; init; }

    public GeoJsonPointRequest? CorrectedPosition { get; init; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; init; } = null!;

    public JsonDocument? Evidence { get; init; }

    [Required]
    public uint? ExpectedVersion { get; init; }
}

public sealed record ReviewBoundaryExceptionApiResponse(
    Guid ExceptionId,
    string State,
    string Decision,
    uint Version);

public sealed record BoundaryExceptionApiResponse(
    Guid Id,
    Guid FarmId,
    Guid FarmBoundaryVersionId,
    string Source,
    string SourceReferenceId,
    Guid? SurveyOrderId,
    Guid? MissionId,
    GeoJsonPointResponse OriginalPosition,
    string State,
    decimal MeasuredDistanceMeters,
    decimal ThresholdMeters,
    string? Decision,
    GeoJsonPointResponse? CorrectedPosition,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewReason,
    uint Version,
    DateTimeOffset CreatedAt);
