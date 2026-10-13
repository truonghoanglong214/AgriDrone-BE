using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Requests;

internal sealed record SurveyRequestSubmissionSnapshot(
    SurveyRequestKind Kind,
    Guid? TenantId,
    Guid? FarmId,
    Guid? RequestedByUserId,
    Guid SurveyServiceId,
    string ApplicantName,
    string ApplicantEmail,
    string ApplicantPhone,
    string FarmName,
    string FarmAddress,
    decimal ApproximateAreaHa,
    double Longitude,
    double Latitude,
    int MapSrid,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes)
{
    public static SurveyRequestSubmissionSnapshot Create(
        SurveyRequestKind kind,
        Guid? tenantId,
        Guid? farmId,
        Guid? requestedByUserId,
        Guid surveyServiceId,
        string applicantName,
        string applicantEmail,
        string applicantPhone,
        string farmName,
        string farmAddress,
        decimal approximateAreaHa,
        double longitude,
        double latitude,
        int mapSrid,
        int? estimatedPoleCount,
        DateTimeOffset? preferredStartAt,
        DateTimeOffset? preferredEndAt,
        string? notes)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            surveyServiceId,
            Guid.Empty);
        EnsureOptionalId(tenantId, nameof(tenantId));
        EnsureOptionalId(farmId, nameof(farmId));
        EnsureOptionalId(requestedByUserId, nameof(requestedByUserId));

        return new SurveyRequestSubmissionSnapshot(
            kind,
            tenantId,
            farmId,
            requestedByUserId,
            surveyServiceId,
            NormalizeRequired(applicantName, nameof(applicantName)),
            NormalizeRequired(applicantEmail, nameof(applicantEmail))
                .ToLowerInvariant(),
            NormalizePhone(applicantPhone),
            NormalizeRequired(farmName, nameof(farmName)),
            NormalizeRequired(farmAddress, nameof(farmAddress)),
            approximateAreaHa,
            longitude,
            latitude,
            mapSrid,
            estimatedPoleCount,
            NormalizeTimestamp(preferredStartAt),
            NormalizeTimestamp(preferredEndAt),
            NormalizeOptional(notes));
    }

    public static SurveyRequestSubmissionSnapshot From(
        SurveyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Create(
            request.Kind,
            request.TenantId,
            request.FarmId,
            request.RequestedByUserId,
            request.SurveyServiceId,
            request.ApplicantName,
            request.ApplicantEmail,
            request.ApplicantPhone,
            request.FarmName,
            request.FarmAddress,
            request.ApproximateAreaHa,
            request.MapLocation.X,
            request.MapLocation.Y,
            request.MapLocation.SRID,
            request.EstimatedPoleCount,
            request.PreferredStartAt,
            request.PreferredEndAt,
            request.Notes);
    }

    private static string NormalizeRequired(
        string value,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizePhone(string value)
    {
        var normalized = NormalizeRequired(value, nameof(value));
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

    private static DateTimeOffset? NormalizeTimestamp(DateTimeOffset? value) =>
        value?.ToUniversalTime();

    private static void EnsureOptionalId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Identifier cannot be empty when supplied.",
                parameterName);
        }
    }
}

internal readonly record struct SurveyRequestSubmissionFingerprint
{
    private SurveyRequestSubmissionFingerprint(string value) =>
        Value = value;

    public string Value { get; }

    public static SurveyRequestSubmissionFingerprint Create(
        SurveyRequestSubmissionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var includesApplicantPayload =
            snapshot.Kind == SurveyRequestKind.NewCustomer;
        var includesProposedFarmPayload =
            snapshot.Kind is SurveyRequestKind.NewCustomer or
                SurveyRequestKind.ExistingTenantNewFarm;
        var canonicalPayload = new CanonicalFingerprintPayload(
            (int)snapshot.Kind,
            FormatId(snapshot.TenantId),
            FormatId(snapshot.FarmId),
            FormatId(snapshot.RequestedByUserId),
            snapshot.SurveyServiceId.ToString("D"),
            includesApplicantPayload ? snapshot.ApplicantName : null,
            includesApplicantPayload ? snapshot.ApplicantEmail : null,
            includesApplicantPayload ? snapshot.ApplicantPhone : null,
            includesProposedFarmPayload ? snapshot.FarmName : null,
            includesProposedFarmPayload ? snapshot.FarmAddress : null,
            includesProposedFarmPayload
                ? snapshot.ApproximateAreaHa.ToString(
                    "G29",
                    CultureInfo.InvariantCulture)
                : null,
            includesProposedFarmPayload
                ? FormatCoordinate(snapshot.Longitude)
                : null,
            includesProposedFarmPayload
                ? FormatCoordinate(snapshot.Latitude)
                : null,
            includesProposedFarmPayload ? snapshot.MapSrid : null,
            snapshot.EstimatedPoleCount,
            FormatTimestamp(snapshot.PreferredStartAt),
            FormatTimestamp(snapshot.PreferredEndAt),
            snapshot.Notes);
        var payload = JsonSerializer.SerializeToUtf8Bytes(canonicalPayload);
        var digest = SHA256.HashData(payload);
        return new SurveyRequestSubmissionFingerprint(
            Convert.ToHexStringLower(digest));
    }

    private static string? FormatId(Guid? value) =>
        value?.ToString("D");

    private static string FormatCoordinate(double value) =>
        (value == 0d ? 0d : value).ToString(
            "R",
            CultureInfo.InvariantCulture);

    private static string? FormatTimestamp(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString(
            "O",
            CultureInfo.InvariantCulture);

    private sealed record CanonicalFingerprintPayload(
        int Kind,
        string? TenantId,
        string? FarmId,
        string? RequestedByUserId,
        string SurveyServiceId,
        string? ApplicantName,
        string? ApplicantEmail,
        string? ApplicantPhone,
        string? FarmName,
        string? FarmAddress,
        string? ApproximateAreaHa,
        string? Longitude,
        string? Latitude,
        int? MapSrid,
        int? EstimatedPoleCount,
        string? PreferredStartAt,
        string? PreferredEndAt,
        string? Notes);
}
