using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyApprovalCaptureAuthorizationRepository(
    SurveyApprovalDbContext context)
    : ISurveyApprovalCaptureAuthorizationRepository
{
    public void Add(SurveyCaptureAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        context.EnsureApprovalTransaction();
        context.SurveyCaptureAuthorizations.Add(authorization);
    }
}
