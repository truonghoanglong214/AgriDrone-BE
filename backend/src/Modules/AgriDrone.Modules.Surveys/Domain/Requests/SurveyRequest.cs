using System.Text.Json;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyRequest : AggregateRoot
{
    private static readonly Dictionary<SurveyRequestStatus, IReadOnlySet<SurveyRequestStatus>>
        AllowedTransitions = new Dictionary<SurveyRequestStatus, IReadOnlySet<SurveyRequestStatus>>
        {
            [SurveyRequestStatus.Submitted] = new HashSet<SurveyRequestStatus>
            {
                SurveyRequestStatus.UnderReview,
                SurveyRequestStatus.Withdrawn
            },
            [SurveyRequestStatus.UnderReview] = new HashSet<SurveyRequestStatus>
            {
                SurveyRequestStatus.Approved,
                SurveyRequestStatus.Rejected,
                SurveyRequestStatus.Withdrawn
            }
        };

    private SurveyRequest() { }

    public string RequestNumber { get; private set; } = null!;
    public SurveyRequestKind Kind { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? FarmId { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public Guid SurveyServiceId { get; private set; }
    public string CallerScope { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public string ApplicantName { get; private set; } = null!;
    public string ApplicantEmail { get; private set; } = null!;
    public string ApplicantPhone { get; private set; } = null!;
    public string FarmName { get; private set; } = null!;
    public string FarmAddress { get; private set; } = null!;
    public decimal ApproximateAreaHa { get; private set; }
    public Point MapLocation { get; private set; } = null!;
    public int? EstimatedPoleCount { get; private set; }
    public DateTimeOffset? PreferredStartAt { get; private set; }
    public DateTimeOffset? PreferredEndAt { get; private set; }
    public string? Notes { get; private set; }
    public SurveyRequestStatus Status { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyService SurveyService { get; private set; } = null!;
    public ICollection<SurveyRequestReview> Reviews { get; private set; } = [];

    public static SurveyRequest CreateNewCustomer(
        string requestNumber,
        SurveyService surveyService,
        RequestIdempotency idempotency,
        string applicantName,
        string applicantEmail,
        string applicantPhone,
        string farmName,
        string farmAddress,
        decimal approximateAreaHa,
        Point mapLocation,
        int? estimatedPoleCount,
        DateTimeOffset? preferredStartAt,
        DateTimeOffset? preferredEndAt,
        string? notes,
        DateTimeOffset createdAt) =>
        CreateProposedFarmRequest(
            SurveyRequestKind.NewCustomer,
            tenantId: null,
            farmId: null,
            requestedByUserId: null,
            requestNumber,
            surveyService,
            idempotency,
            applicantName,
            applicantEmail,
            applicantPhone,
            farmName,
            farmAddress,
            approximateAreaHa,
            mapLocation,
            estimatedPoleCount,
            preferredStartAt,
            preferredEndAt,
            notes,
            createdAt);

    public static SurveyRequest CreateExistingTenantNewFarm(
        Guid tenantId,
        Guid requestedByUserId,
        string requestNumber,
        SurveyService surveyService,
        RequestIdempotency idempotency,
        string applicantName,
        string applicantEmail,
        string applicantPhone,
        string farmName,
        string farmAddress,
        decimal approximateAreaHa,
        Point mapLocation,
        int? estimatedPoleCount,
        DateTimeOffset? preferredStartAt,
        DateTimeOffset? preferredEndAt,
        string? notes,
        DateTimeOffset createdAt) =>
        CreateProposedFarmRequest(
            SurveyRequestKind.ExistingTenantNewFarm,
            tenantId,
            farmId: null,
            requestedByUserId,
            requestNumber,
            surveyService,
            idempotency,
            applicantName,
            applicantEmail,
            applicantPhone,
            farmName,
            farmAddress,
            approximateAreaHa,
            mapLocation,
            estimatedPoleCount,
            preferredStartAt,
            preferredEndAt,
            notes,
            createdAt);

    public static SurveyRequest CreateExistingFarmSurvey(
        Guid tenantId,
        Guid farmId,
        Guid requestedByUserId,
        string requestNumber,
        SurveyService surveyService,
        RequestIdempotency idempotency,
        string applicantName,
        string applicantEmail,
        string applicantPhone,
        string farmName,
        string farmAddress,
        decimal approximateAreaHa,
        Point mapLocation,
        int? estimatedPoleCount,
        DateTimeOffset? preferredStartAt,
        DateTimeOffset? preferredEndAt,
        string? notes,
        DateTimeOffset createdAt) =>
        CreateProposedFarmRequest(
            SurveyRequestKind.ExistingFarmSurvey,
            tenantId,
            farmId,
            requestedByUserId,
            requestNumber,
            surveyService,
            idempotency,
            applicantName,
            applicantEmail,
            applicantPhone,
            farmName,
            farmAddress,
            approximateAreaHa,
            mapLocation,
            estimatedPoleCount,
            preferredStartAt,
            preferredEndAt,
            notes,
            createdAt);

    private static SurveyRequest CreateProposedFarmRequest(
        SurveyRequestKind kind,
        Guid? tenantId,
        Guid? farmId,
        Guid? requestedByUserId,
        string requestNumber,
        SurveyService surveyService,
        RequestIdempotency idempotency,
        string applicantName,
        string applicantEmail,
        string applicantPhone,
        string farmName,
        string farmAddress,
        decimal approximateAreaHa,
        Point mapLocation,
        int? estimatedPoleCount,
        DateTimeOffset? preferredStartAt,
        DateTimeOffset? preferredEndAt,
        string? notes,
        DateTimeOffset createdAt)
    {
        switch (kind)
        {
            case SurveyRequestKind.NewCustomer
                when !tenantId.HasValue &&
                     !farmId.HasValue &&
                     !requestedByUserId.HasValue:
                break;
            case SurveyRequestKind.ExistingTenantNewFarm
                when tenantId.HasValue &&
                     tenantId.Value != Guid.Empty &&
                     !farmId.HasValue &&
                     requestedByUserId.HasValue &&
                     requestedByUserId.Value != Guid.Empty:
                break;
            case SurveyRequestKind.ExistingFarmSurvey
                when tenantId.HasValue &&
                     tenantId.Value != Guid.Empty &&
                     farmId.HasValue &&
                     farmId.Value != Guid.Empty &&
                     requestedByUserId.HasValue &&
                     requestedByUserId.Value != Guid.Empty:
                break;
            default:
                throw new ArgumentException(
                    "Survey request kind and tenant/user context are inconsistent.",
                    nameof(kind));
        }

        ArgumentNullException.ThrowIfNull(surveyService);
        if (surveyService.Status is not SurveyServiceStatus.Active and
            not SurveyServiceStatus.Experimental)
        {
            throw new ArgumentException(
                "Survey service is not accepting new requests.",
                nameof(surveyService));
        }

        if (idempotency == default)
        {
            throw new ArgumentException(
                "Idempotency identity is required.",
                nameof(idempotency));
        }

        var normalizedRequestNumber = NormalizeRequired(
            requestNumber,
            40,
            nameof(requestNumber));
        var normalizedApplicantName = NormalizeRequired(
            applicantName,
            150,
            nameof(applicantName));
        var normalizedApplicantEmail = NormalizeRequired(
                applicantEmail,
                320,
                nameof(applicantEmail))
            .ToLowerInvariant();
        var normalizedApplicantPhone = NormalizePhone(applicantPhone);
        var normalizedFarmName = NormalizeRequired(
            farmName,
            200,
            nameof(farmName));
        var normalizedFarmAddress = NormalizeRequired(
            farmAddress,
            2_000,
            nameof(farmAddress));
        var normalizedNotes = NormalizeOptional(notes, 4_000, nameof(notes));

        if (approximateAreaHa <= 0 ||
            approximateAreaHa >= 100_000_000m ||
            DecimalScale(approximateAreaHa) > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(approximateAreaHa),
                "Approximate area must be positive and fit numeric(12,4).");
        }

        ArgumentNullException.ThrowIfNull(mapLocation);
        if (mapLocation.IsEmpty ||
            mapLocation.SRID != 4326 ||
            !double.IsFinite(mapLocation.X) ||
            !double.IsFinite(mapLocation.Y) ||
            mapLocation.X is < -180d or > 180d ||
            mapLocation.Y is < -90d or > 90d)
        {
            throw new ArgumentException(
                "Map location must be a valid SRID 4326 longitude/latitude point.",
                nameof(mapLocation));
        }

        if (estimatedPoleCount.HasValue && estimatedPoleCount.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(estimatedPoleCount),
                "Estimated pole count must be positive when supplied.");
        }

        EnsurePreferredWindow(preferredStartAt, preferredEndAt);
        DomainGuard.Utc(createdAt);

        return new SurveyRequest
        {
            Id = Guid.NewGuid(),
            RequestNumber = normalizedRequestNumber,
            Kind = kind,
            TenantId = tenantId,
            FarmId = farmId,
            RequestedByUserId = requestedByUserId,
            SurveyServiceId = surveyService.Id,
            SurveyService = surveyService,
            CallerScope = idempotency.CallerScope,
            IdempotencyKey = idempotency.Key,
            ApplicantName = normalizedApplicantName,
            ApplicantEmail = normalizedApplicantEmail,
            ApplicantPhone = normalizedApplicantPhone,
            FarmName = normalizedFarmName,
            FarmAddress = normalizedFarmAddress,
            ApproximateAreaHa = approximateAreaHa,
            MapLocation = new Point(mapLocation.X, mapLocation.Y)
            {
                SRID = 4326
            },
            EstimatedPoleCount = estimatedPoleCount,
            PreferredStartAt = preferredStartAt,
            PreferredEndAt = preferredEndAt,
            Notes = normalizedNotes,
            Status = SurveyRequestStatus.Submitted,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public static bool CanTransition(
        SurveyRequestStatus from,
        SurveyRequestStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw SurveyTransitionGuard.VersionConflict(
                SurveyRequestDomainErrorCodes.VersionConflict,
                expectedVersion,
                Version);
        }
    }

    public void StartReview(DateTimeOffset reviewedAt) =>
        TransitionTo(SurveyRequestStatus.UnderReview, reviewedAt);

    public void Approve(
        JsonDocument checklistSnapshot,
        string reason,
        Guid reviewedBy,
        DateTimeOffset reviewedAt) =>
        RecordDecision(
            SurveyReviewDecision.Approved,
            SurveyRequestStatus.Approved,
            checklistSnapshot,
            reason,
            reviewedBy,
            reviewedAt);

    public void Reject(
        JsonDocument checklistSnapshot,
        string reason,
        Guid reviewedBy,
        DateTimeOffset reviewedAt) =>
        RecordDecision(
            SurveyReviewDecision.Rejected,
            SurveyRequestStatus.Rejected,
            checklistSnapshot,
            reason,
            reviewedBy,
            reviewedAt);

    public void Withdraw(DateTimeOffset withdrawnAt) =>
        TransitionTo(SurveyRequestStatus.Withdrawn, withdrawnAt);

    private void RecordDecision(
        SurveyReviewDecision decision,
        SurveyRequestStatus target,
        JsonDocument checklistSnapshot,
        string reason,
        Guid reviewedBy,
        DateTimeOffset reviewedAt)
    {
        ArgumentNullException.ThrowIfNull(checklistSnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        DomainGuard.NotEmpty(reviewedBy);
        SurveyTransitionGuard.EnsureTimestamp(reviewedAt, UpdatedAt);
        EnsureTransition(target);

        Reviews.Add(SurveyRequestReview.Create(
            Id,
            decision,
            checklistSnapshot,
            reason.Trim(),
            reviewedBy,
            reviewedAt));
        SetState(target, reviewedAt);
    }

    private void TransitionTo(SurveyRequestStatus target, DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        EnsureTransition(target);
        SetState(target, occurredAt);
    }

    private void EnsureTransition(SurveyRequestStatus target) =>
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, target),
            Status,
            target,
            SurveyRequestDomainErrorCodes.InvalidTransition);

    private void SetState(SurveyRequestStatus target, DateTimeOffset occurredAt)
    {
        Status = target;
        UpdatedAt = occurredAt;
        Version++;
    }

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static string NormalizePhone(string value)
    {
        var normalized = NormalizeRequired(value, 30, nameof(value));
        var result = new System.Text.StringBuilder(normalized.Length);
        var digitCount = 0;

        for (var index = 0; index < normalized.Length; index++)
        {
            var character = normalized[index];
            if (character is >= '0' and <= '9')
            {
                result.Append(character);
                digitCount++;
                continue;
            }

            if (character == '+' && index == 0)
            {
                result.Append(character);
                continue;
            }

            if (character is ' ' or '-' or '(' or ')' or '.')
            {
                continue;
            }

            throw new ArgumentException(
                "Phone number contains unsupported characters.",
                nameof(value));
        }

        if (digitCount is < 7 or > 15)
        {
            throw new ArgumentException(
                "Phone number must contain 7 to 15 digits and may start with '+'.",
                nameof(value));
        }

        return result.ToString();
    }

    private static void EnsurePreferredWindow(
        DateTimeOffset? preferredStartAt,
        DateTimeOffset? preferredEndAt)
    {
        if (preferredStartAt.HasValue != preferredEndAt.HasValue)
        {
            throw new ArgumentException(
                "Preferred start and end must either both be supplied or both be omitted.");
        }

        if (!preferredStartAt.HasValue)
        {
            return;
        }

        DomainGuard.Utc(preferredStartAt.Value);
        DomainGuard.Utc(preferredEndAt!.Value);
        if (preferredEndAt.Value <= preferredStartAt.Value)
        {
            throw new ArgumentException(
                "Preferred end must be later than preferred start.");
        }
    }

    private static int DecimalScale(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0x7F;
}
