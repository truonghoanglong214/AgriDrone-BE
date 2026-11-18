using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyDeposit;

internal sealed class GetCurrentSurveyDepositPolicyQueryHandler(
    ISurveyDepositPolicyRepository repository)
    : IRequestHandler<GetCurrentSurveyDepositPolicyQuery,
        Result<SurveyDepositPolicyResponse>>
{
    public async Task<Result<SurveyDepositPolicyResponse>> Handle(
        GetCurrentSurveyDepositPolicyQuery request,
        CancellationToken cancellationToken)
    {
        var current = await repository.GetCurrentAsync(cancellationToken);
        if (current is null)
        {
            return Result.Success(new SurveyDepositPolicyResponse(
                Guid.Empty,
                0,
                SurveyOrder.DefaultBaselineDepositAmountVnd,
                DateTimeOffset.UtcNow,
                null,
                Guid.Empty));
        }

        return Result.Success(new SurveyDepositPolicyResponse(
            current.Id,
            current.VersionNumber,
            current.AmountVnd,
            current.EffectiveFrom,
            current.EffectiveTo,
            current.CreatedBy));
    }
}
