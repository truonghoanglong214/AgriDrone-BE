using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Infrastructure.Queries;

internal sealed class SurveyRequestQueries(SurveysDbContext context)
    : ISurveyRequestQueries
{
    private const int MaximumPageSize = 100;

    public Task<PagedResult<SurveyRequestInboxItem>> GetSystemAdminInboxAsync(
        SurveyRequestInboxFilter filter,
        PagedRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        EnsureValidPage(page);
        EnsureValidFilter(filter);

        var requests = context.SurveyRequests.AsNoTracking();

        if (filter.Status.HasValue)
        {
            requests = requests.Where(request =>
                request.Status == filter.Status.Value);
        }

        if (filter.Kind.HasValue)
        {
            requests = requests.Where(request =>
                request.Kind == filter.Kind.Value);
        }

        if (filter.SurveyServiceId.HasValue)
        {
            requests = requests.Where(request =>
                request.SurveyServiceId == filter.SurveyServiceId.Value);
        }

        if (filter.CreatedFrom.HasValue)
        {
            requests = requests.Where(request =>
                request.CreatedAt >= filter.CreatedFrom.Value);
        }

        if (filter.CreatedTo.HasValue)
        {
            requests = requests.Where(request =>
                request.CreatedAt < filter.CreatedTo.Value);
        }

        return requests
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.Id)
            .Select(request => new SurveyRequestInboxItem(
                request.Id,
                request.RequestNumber,
                request.Kind,
                request.Status,
                request.SurveyServiceId,
                request.SurveyService.Code,
                request.TenantId,
                request.FarmId,
                request.ApplicantName,
                request.FarmName,
                request.ApproximateAreaHa,
                request.EstimatedPoleCount,
                request.CreatedAt,
                request.UpdatedAt,
                request.Version))
            .ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<SurveyRequestDetail?> GetSystemAdminDetailAsync(
        Guid surveyRequestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            surveyRequestId,
            Guid.Empty);

        var request = await context.SurveyRequests
            .AsNoTracking()
            .Where(candidate => candidate.Id == surveyRequestId)
            .Select(candidate => new DetailRow(
                candidate.Id,
                candidate.RequestNumber,
                candidate.Kind,
                candidate.Status,
                candidate.TenantId,
                candidate.FarmId,
                candidate.RequestedByUserId,
                candidate.SurveyServiceId,
                candidate.SurveyService.Code,
                candidate.SurveyService.Name,
                candidate.ApplicantName,
                candidate.ApplicantEmail,
                candidate.ApplicantPhone,
                candidate.FarmName,
                candidate.FarmAddress,
                candidate.ApproximateAreaHa,
                candidate.MapLocation,
                candidate.EstimatedPoleCount,
                candidate.PreferredStartAt,
                candidate.PreferredEndAt,
                candidate.Notes,
                candidate.Version,
                candidate.CreatedAt,
                candidate.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (request is null)
        {
            return null;
        }

        var reviewRows = await context.SurveyRequestReviews
            .AsNoTracking()
            .Where(review => review.SurveyRequestId == surveyRequestId)
            .OrderBy(review => review.ReviewedAt)
            .ThenBy(review => review.Id)
            .Select(review => new ReviewRow(
                review.Id,
                review.Decision,
                review.ChecklistSnapshot,
                review.Reason,
                review.ReviewedBy,
                review.ReviewedAt))
            .ToListAsync(cancellationToken);

        var reviews = reviewRows.Select(review => new SurveyRequestReviewItem(
                review.Id,
                review.Decision,
                JsonDocument.Parse(
                    review.ChecklistSnapshot.RootElement.GetRawText()),
                review.Reason,
                review.ReviewedBy,
                review.ReviewedAt))
            .ToArray();

        return new SurveyRequestDetail(
            request.Id,
            request.RequestNumber,
            request.Kind,
            request.Status,
            request.TenantId,
            request.FarmId,
            request.RequestedByUserId,
            request.SurveyServiceId,
            request.SurveyServiceCode,
            request.SurveyServiceName,
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
            request.Notes,
            request.Version,
            request.CreatedAt,
            request.UpdatedAt,
            reviews);
    }

    public Task<PagedResult<SurveyRequestHistoryItem>>
        GetTenantOwnerHistoryAsync(
            Guid tenantId,
            PagedRequest page,
            CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        EnsureValidPage(page);

        return context.SurveyRequests
            .AsNoTracking()
            .Where(request => request.TenantId == tenantId)
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.Id)
            .Select(request => new SurveyRequestHistoryItem(
                request.Id,
                request.RequestNumber,
                request.Kind,
                request.Status,
                request.SurveyServiceId,
                request.SurveyService.Code,
                request.FarmId,
                request.FarmName,
                request.CreatedAt,
                request.UpdatedAt))
            .ToPagedResultAsync(page, cancellationToken);
    }

    private static void EnsureValidPage(PagedRequest page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.PageNumber < 1 ||
            page.PageSize < 1 ||
            page.PageSize > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                $"Page number must be positive and page size must be between 1 and {MaximumPageSize}.");
        }
    }

    private static void EnsureValidFilter(SurveyRequestInboxFilter filter)
    {
        if (filter.SurveyServiceId == Guid.Empty)
        {
            throw new ArgumentException(
                "Survey service ID cannot be empty.",
                nameof(filter));
        }

        EnsureUtc(filter.CreatedFrom, nameof(filter.CreatedFrom));
        EnsureUtc(filter.CreatedTo, nameof(filter.CreatedTo));
        if (filter.CreatedFrom.HasValue &&
            filter.CreatedTo.HasValue &&
            filter.CreatedFrom.Value >= filter.CreatedTo.Value)
        {
            throw new ArgumentException(
                "Created-from must be earlier than created-to.",
                nameof(filter));
        }
    }

    private static void EnsureUtc(
        DateTimeOffset? value,
        string parameterName)
    {
        if (value.HasValue && value.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use the UTC offset.",
                parameterName);
        }
    }

    private sealed record DetailRow(
        Guid Id,
        string RequestNumber,
        SurveyRequestKind Kind,
        SurveyRequestStatus Status,
        Guid? TenantId,
        Guid? FarmId,
        Guid? RequestedByUserId,
        Guid SurveyServiceId,
        string SurveyServiceCode,
        string SurveyServiceName,
        string ApplicantName,
        string ApplicantEmail,
        string ApplicantPhone,
        string FarmName,
        string FarmAddress,
        decimal ApproximateAreaHa,
        Point MapLocation,
        int? EstimatedPoleCount,
        DateTimeOffset? PreferredStartAt,
        DateTimeOffset? PreferredEndAt,
        string? Notes,
        uint Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record ReviewRow(
        Guid Id,
        SurveyReviewDecision Decision,
        JsonDocument ChecklistSnapshot,
        string Reason,
        Guid ReviewedBy,
        DateTimeOffset ReviewedAt);
}
