using AgriDrone.SharedKernel.Domain;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AgriDrone.Modules.Surveys.Domain;

public enum BoundaryCaptureSessionStatus
{
    InProgress,
    Finalized,
    Aborted,
    Superseded
}

/// <summary>
/// Evidence aggregate for the ordered GNSS corner samples collected during the
/// confirmed Baseline appointment. A finalized session is immutable.
/// </summary>
public sealed class BoundaryCaptureSession : AggregateRoot
{
    private readonly List<BoundaryCaptureSample> _samples = [];

    private BoundaryCaptureSession() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid StartOperationId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid AppointmentId { get; private set; }
    public uint AppointmentVersion { get; private set; }
    public Guid SystemManagerProfileId { get; private set; }
    public Guid DroneId { get; private set; }
    public BoundaryCaptureSessionStatus Status { get; private set; }
    public Guid? FarmBoundaryVersionId { get; private set; }
    public string? FinalizationEvidenceReference { get; private set; }
    public Guid StartedBy { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public Guid? FinalizedBy { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public Guid? AbortedBy { get; private set; }
    public DateTimeOffset? AbortedAt { get; private set; }
    public string? AbortReason { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyCollection<BoundaryCaptureSample> Samples => _samples;

    public static BoundaryCaptureSession Start(
        Guid surveyOrderId,
        Guid startOperationId,
        Guid farmId,
        Guid appointmentId,
        uint appointmentVersion,
        Guid systemManagerProfileId,
        Guid droneId,
        Guid startedBy,
        DateTimeOffset startedAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(startOperationId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(appointmentId);
        DomainGuard.NotEmpty(systemManagerProfileId);
        DomainGuard.NotEmpty(droneId);
        DomainGuard.NotEmpty(startedBy);
        DomainGuard.Utc(startedAt);

        return new BoundaryCaptureSession
        {
            Id = Guid.CreateVersion7(),
            SurveyOrderId = surveyOrderId,
            StartOperationId = startOperationId,
            FarmId = farmId,
            AppointmentId = appointmentId,
            AppointmentVersion = appointmentVersion,
            SystemManagerProfileId = systemManagerProfileId,
            DroneId = droneId,
            StartedBy = startedBy,
            StartedAt = startedAt,
            Status = BoundaryCaptureSessionStatus.InProgress
        };
    }

    public BoundaryCaptureSample AddSample(
        Guid clientOperationId,
        int sequenceNumber,
        decimal latitude,
        decimal longitude,
        decimal horizontalAccuracyMeters,
        DateTimeOffset capturedAt,
        DateTimeOffset receivedAt)
    {
        EnsureInProgress();
        if (_samples.Any(item => item.ClientOperationId == clientOperationId))
        {
            var existing = _samples.Single(item =>
                item.ClientOperationId == clientOperationId);
            var retryHash = BoundaryCaptureSample.CalculatePayloadHash(
                sequenceNumber,
                latitude,
                longitude,
                horizontalAccuracyMeters,
                capturedAt);
            if (!string.Equals(
                    existing.PayloadHash,
                    retryHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A client operation ID cannot be reused with a different GNSS payload.");
            }

            return existing;
        }

        if (_samples.Any(item => item.SequenceNumber == sequenceNumber))
        {
            throw new InvalidOperationException(
                $"Boundary sample sequence {sequenceNumber} already exists.");
        }

        var sample = BoundaryCaptureSample.Create(
            Id,
            clientOperationId,
            sequenceNumber,
            latitude,
            longitude,
            horizontalAccuracyMeters,
            capturedAt,
            receivedAt);
        _samples.Add(sample);
        Version++;
        return sample;
    }

    public void Finalize(
        Guid farmBoundaryVersionId,
        string evidenceReference,
        Guid finalizedBy,
        DateTimeOffset finalizedAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureInProgress();
        DomainGuard.NotEmpty(farmBoundaryVersionId);
        DomainGuard.NotEmpty(finalizedBy);
        DomainGuard.Utc(finalizedAt);
        if (_samples.Count < 3)
        {
            throw new InvalidOperationException(
                "At least three ordered GNSS samples are required to finalize a boundary.");
        }

        FinalizationEvidenceReference = Normalize(evidenceReference, 1000);
        FarmBoundaryVersionId = farmBoundaryVersionId;
        FinalizedBy = finalizedBy;
        FinalizedAt = finalizedAt;
        Status = BoundaryCaptureSessionStatus.Finalized;
        Version++;
    }

    public void Abort(
        string reason,
        Guid abortedBy,
        DateTimeOffset abortedAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureInProgress();
        DomainGuard.NotEmpty(abortedBy);
        DomainGuard.Utc(abortedAt);
        AbortReason = Normalize(reason, 2000);
        AbortedBy = abortedBy;
        AbortedAt = abortedAt;
        Status = BoundaryCaptureSessionStatus.Aborted;
        Version++;
    }

    public void Supersede(DateTimeOffset supersededAt)
    {
        EnsureInProgress();
        DomainGuard.Utc(supersededAt);
        Status = BoundaryCaptureSessionStatus.Superseded;
        Version++;
    }

    private void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException(
                $"Boundary capture version conflict: expected {expectedVersion}, current {Version}.");
        }
    }

    private void EnsureInProgress()
    {
        if (Status != BoundaryCaptureSessionStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Boundary capture session in state '{Status}' is immutable.");
        }
    }

    private static string Normalize(string value, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            normalized.Length,
            maximumLength,
            nameof(value));
        return normalized;
    }
}

public sealed class BoundaryCaptureSample : Entity
{
    private BoundaryCaptureSample() { }

    public Guid BoundaryCaptureSessionId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public int SequenceNumber { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public decimal HorizontalAccuracyMeters { get; private set; }
    public string PayloadHash { get; private set; } = null!;
    public DateTimeOffset CapturedAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public BoundaryCaptureSession Session { get; private set; } = null!;

    internal static BoundaryCaptureSample Create(
        Guid sessionId,
        Guid clientOperationId,
        int sequenceNumber,
        decimal latitude,
        decimal longitude,
        decimal horizontalAccuracyMeters,
        DateTimeOffset capturedAt,
        DateTimeOffset receivedAt)
    {
        DomainGuard.NotEmpty(sessionId);
        DomainGuard.NotEmpty(clientOperationId);
        ArgumentOutOfRangeException.ThrowIfNegative(sequenceNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, -90m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, 90m);
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180m);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            horizontalAccuracyMeters);
        DomainGuard.Utc(capturedAt);
        DomainGuard.Utc(receivedAt);
        if (capturedAt > receivedAt)
        {
            throw new ArgumentException(
                "A GNSS sample cannot be captured after it was received.",
                nameof(capturedAt));
        }

        return new BoundaryCaptureSample
        {
            Id = Guid.CreateVersion7(),
            BoundaryCaptureSessionId = sessionId,
            ClientOperationId = clientOperationId,
            SequenceNumber = sequenceNumber,
            Latitude = latitude,
            Longitude = longitude,
            HorizontalAccuracyMeters = horizontalAccuracyMeters,
            PayloadHash = CalculatePayloadHash(
                sequenceNumber,
                latitude,
                longitude,
                horizontalAccuracyMeters,
                capturedAt),
            CapturedAt = capturedAt,
            ReceivedAt = receivedAt
        };
    }

    internal static string CalculatePayloadHash(
        int sequenceNumber,
        decimal latitude,
        decimal longitude,
        decimal horizontalAccuracyMeters,
        DateTimeOffset capturedAt)
    {
        var canonical = string.Join(
            "|",
            sequenceNumber.ToString(CultureInfo.InvariantCulture),
            latitude.ToString(CultureInfo.InvariantCulture),
            longitude.ToString(CultureInfo.InvariantCulture),
            horizontalAccuracyMeters.ToString(CultureInfo.InvariantCulture),
            capturedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
