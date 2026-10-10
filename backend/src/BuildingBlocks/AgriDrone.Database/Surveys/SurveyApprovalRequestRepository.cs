using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyApprovalRequestRepository(
    SurveyApprovalDbContext context)
    : ISurveyApprovalRequestRepository
{
    public async Task<SurveyRequest?> GetForApprovalAsync(
        Guid surveyRequestId,
        CancellationToken cancellationToken = default)
    {
        var exists = await SurveyApprovalRowLock.ExistsAsync(
            context,
            """
            SELECT id
            FROM survey.survey_requests
            WHERE id = @request_id
            FOR UPDATE
            """,
            cancellationToken,
            SurveyApprovalRowLock.Id("request_id", surveyRequestId));

        if (!exists)
        {
            return null;
        }

        return await context.SurveyRequests
            .Include(request => request.SurveyService)
            .Include(request => request.Reviews)
            .SingleAsync(
                request => request.Id == surveyRequestId,
                cancellationToken);
    }
}
