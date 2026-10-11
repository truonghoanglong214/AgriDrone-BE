using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyApprovalIdempotencyRepository(
    SurveyApprovalDbContext context)
    : ISurveyApprovalIdempotencyRepository
{
    public Task<SurveyApprovalIdempotencyRecord?> GetByActorAndKeyAsync(
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        context.EnsureApprovalTransaction();
        var normalizedKey = idempotencyKey.Trim();
        return context.SurveyApprovalIdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.ActorId == actorId &&
                          record.IdempotencyKey == normalizedKey,
                cancellationToken);
    }

    public Task<SurveyApprovalIdempotencyRecord?> GetByRequestIdAsync(
        Guid surveyRequestId,
        CancellationToken cancellationToken = default)
    {
        context.EnsureApprovalTransaction();
        return context.SurveyApprovalIdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.SurveyRequestId == surveyRequestId,
                cancellationToken);
    }

    public async Task AddClaimAsync(
        SurveyApprovalIdempotencyRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        context.EnsureApprovalTransaction();
        context.SurveyApprovalIdempotencyRecords.Add(record);
        await context.FlushCheckpointAsync(
            SurveyApprovalPersistenceCheckpoint.IdempotencyClaim,
            cancellationToken);
    }
}
