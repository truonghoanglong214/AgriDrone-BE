namespace AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;

internal interface ISurveyApprovalNumberGenerator
{
    string CreateTenantCode();

    string CreateFarmCode();

    string CreateOrderNumber();
}
