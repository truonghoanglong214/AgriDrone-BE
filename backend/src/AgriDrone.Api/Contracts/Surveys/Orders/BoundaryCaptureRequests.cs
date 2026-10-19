using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record StartBoundaryCaptureRequest
{
    [Required]
    public Guid? DroneId { get; init; }

    [Required]
    public Guid? OperationId { get; init; }

    [Required]
    public uint? ExpectedOrderVersion { get; init; }
}

public sealed record BoundaryCaptureSampleRequest
{
    [Required]
    public Guid? ClientOperationId { get; init; }

    [Range(0, int.MaxValue)]
    public int SequenceNumber { get; init; }

    [Range(-90, 90)]
    public decimal Latitude { get; init; }

    [Range(-180, 180)]
    public decimal Longitude { get; init; }

    [Range(typeof(decimal), "0.01", "1000")]
    public decimal HorizontalAccuracyMeters { get; init; }

    [Required]
    public DateTimeOffset? CapturedAt { get; init; }
}

public sealed record SyncBoundaryCaptureSamplesRequest
{
    [Required]
    public uint? ExpectedSessionVersion { get; init; }

    [Required, MinLength(1), MaxLength(500)]
    public IReadOnlyCollection<BoundaryCaptureSampleRequest> Samples { get; init; } = [];
}

public sealed record FinalizeBoundaryCaptureRequest
{
    [Required, MaxLength(1000)]
    public string EvidenceReference { get; init; } = string.Empty;

    [Required]
    public uint? ExpectedOrderVersion { get; init; }

    [Required]
    public uint? ExpectedSessionVersion { get; init; }

    public int? ExpectedCurrentBoundaryVersionNumber { get; init; }
}

public sealed record AbortBoundaryCaptureRequest
{
    [Required, MaxLength(2000)]
    public string Reason { get; init; } = string.Empty;

    [Required]
    public uint? ExpectedSessionVersion { get; init; }
}

public sealed record ApproveBaselineRoutePlanRequest
{
    [Required]
    public Guid? SessionId { get; init; }

    [Required]
    public Guid? FarmBoundaryVersionId { get; init; }

    [Required]
    public int? RoutePlanVersion { get; init; }

    [Range(typeof(decimal), "0", "500")]
    public decimal SafetyInsetMeters { get; init; }

    public bool IsWithinSafetyInset { get; init; }

    [Required]
    public uint? ExpectedOrderVersion { get; init; }
}

public sealed record BoundaryCaptureSampleApiResponse(
    Guid Id,
    Guid ClientOperationId,
    int SequenceNumber,
    decimal Latitude,
    decimal Longitude,
    decimal HorizontalAccuracyMeters,
    DateTimeOffset CapturedAt,
    DateTimeOffset ReceivedAt);

public sealed record BoundaryCaptureApiResponse(
    Guid SurveyOrderId,
    Guid SessionId,
    Guid StartOperationId,
    Guid FarmId,
    Guid AppointmentId,
    uint AppointmentVersion,
    Guid SystemManagerProfileId,
    Guid DroneId,
    string Status,
    Guid? FarmBoundaryVersionId,
    Guid? BaselineRoutePlanId,
    string OrderStatus,
    uint OrderVersion,
    uint SessionVersion,
    IReadOnlyCollection<BoundaryCaptureSampleApiResponse> Samples);
