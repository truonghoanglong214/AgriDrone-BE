namespace AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;

internal interface ISurveyApprovalNumberGenerator
{
    string CreateFarmCode();

    string CreateOrderNumber();
}
