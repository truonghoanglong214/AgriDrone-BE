using System.Data;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.PublishPlantHealthSurveyResult;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database.Health;

internal sealed partial class PlantHealthReviewService : IPlantHealthPublishedResultReader
{
    private async Task<PlantHealthPublicationComparisonV1> ResolvePublishedComparisonAsync(Session s, CancellationToken token)
    {
        const string policy = "plant-health-publication-comparison.v1";
        PlantHealthPublicationComparisonV1 None(string reason) => new(policy, reason, null, null, null, null, null);
        if (s.Source.Findings.Count == 0) return None("CURRENT_OBSERVATION_TIME_UNAVAILABLE");
        if (!s.Findings.Keys.Any(id => IsAccepted(Current(s, AgriDrone.Modules.Plants.Domain.Health.HealthReviewKind.Finding, id))))
            return None("NO_ACCEPTED_FINDINGS_FOR_COMPARISON");
        var currentStart = DatabaseTime(s.Source.Findings.Min(f => f.ObservedAt));
        var candidates = await (from publication in _db.HealthPublications.AsNoTracking()
            join result in _db.SurveyResults.AsNoTracking() on publication.SurveyResultId equals result.Id
            join order in _db.SurveyOrders.AsNoTracking() on result.SurveyOrderId equals order.Id
            where publication.TenantId == s.Order.TenantId && publication.FarmId == s.Order.FarmId &&
                order.TenantId == s.Order.TenantId && order.FarmId == s.Order.FarmId && order.Id != s.Order.Id &&
                order.SurveyServiceId == s.Order.SurveyServiceId && order.Status == SurveyOrderStatus.Completed &&
                result.TenantId == s.Order.TenantId && result.FarmId == s.Order.FarmId &&
                result.Status == SurveyResultStatus.Published && result.ServiceType == SurveyServiceType.PlantHealth &&
                result.ResultNature == PlantHealthV4Values.ResultNature && result.ObservedAt < currentStart &&
                publication.PublishedAt <= s.Now
            orderby result.ObservedAt descending, publication.PublicationVersion descending, result.Id descending
            select new { Publication = publication, OrderId = order.Id, result.ObservedAt }).ToListAsync(token);
        if (candidates.Count == 0) return None("NO_EARLIER_PUBLISHED_PLANT_HEALTH_ORDER");
        var reason = "NO_COMPATIBLE_PUBLISHED_ORDER";
        foreach (var candidate in candidates)
        {
            var previous = candidate.Publication.Payload.Deserialize<PlantHealthPreliminaryResultPublishedV1>()
                ?? throw new InvalidOperationException("Previous published payload is missing.");
            if (candidate.Publication.Fingerprint != PlantHealthV4Fingerprint.ForValue(previous))
                Conflict("PublicationSnapshotChanged", "Previous publication fingerprint no longer matches its immutable values.");
            if (previous.FarmBaseMapVersionId != s.Batch.FarmBaseMapVersionId || previous.FarmBoundaryVersionId != s.Batch.FarmBoundaryVersionId)
            { reason = "CAPTURE_MAP_OR_BOUNDARY_MISMATCH"; continue; }
            if (previous.Provenance != s.Source.Provenance)
            { reason = "PROVENANCE_SEMANTICS_MISMATCH"; continue; }
            return new(policy, "COMPATIBLE", candidate.OrderId, previous.SurveyResultId,
                previous.PublicationId, previous.PublicationVersion, candidate.ObservedAt);
        }
        return None(reason);
    }

    public async Task<Result<PlantHealthPublishedResultResponse>> GetPublishedAsync(Guid orderId, CancellationToken token)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            var context = await AuthorizeAsync(orderId, false, token);
            if (context.Result.Status != SurveyResultStatus.Published || context.Order.Status != SurveyOrderStatus.Completed) NotFound();
            var publication = await _db.HealthPublications.AsNoTracking().SingleOrDefaultAsync(p => p.SurveyResultId == context.Result.Id &&
                p.TenantId == context.Order.TenantId && p.FarmId == context.Order.FarmId, token);
            if (publication is null) NotFound();
            var payload = publication.Payload.Deserialize<PlantHealthPreliminaryResultPublishedV1>();
            if (payload is null || PlantHealthV4Fingerprint.ForValue(payload) != publication.Fingerprint)
                Conflict("PublicationSnapshotChanged", "Stored publication does not match its fingerprint.");
            await tx.CommitAsync(token);
            return Result.Success(new PlantHealthPublishedResultResponse(payload, publication.Fingerprint));
        }
        catch (ReviewFault error) { return Result.Failure<PlantHealthPublishedResultResponse>(error.Error); }
        catch (PostgresException error) when (IsConcurrency(error))
        { return Result.Failure<PlantHealthPublishedResultResponse>(ConflictError("VersionConflict", "Publication authorization changed; reload and retry.")); }
    }
}
