using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;

public interface ISurveyApprovalCaptureAuthorizationRepository
{
    void Add(SurveyCaptureAuthorization authorization);
}
