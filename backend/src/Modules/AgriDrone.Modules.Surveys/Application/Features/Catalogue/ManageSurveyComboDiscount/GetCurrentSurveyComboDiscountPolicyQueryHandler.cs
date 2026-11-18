using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyComboDiscount;

internal sealed class GetCurrentSurveyComboDiscountPolicyQueryHandler(
    ISurveyComboDiscountPolicyRepository repository)
    : IRequestHandler<GetCurrentSurveyComboDiscountPolicyQuery,
        Result<SurveyComboDiscountPolicyResponse>>
{
    public async Task<Result<SurveyComboDiscountPolicyResponse>> Handle(
        GetCurrentSurveyComboDiscountPolicyQuery request,
        CancellationToken cancellationToken)
    {
        var current = await repository.GetCurrentAsync(cancellationToken);
        if (current is null)
        {
            return Result.Success(new SurveyComboDiscountPolicyResponse(
                Guid.Empty,
                0,
                SurveyOrder.DefaultComboDiscountPercentage,
                DateTimeOffset.UtcNow,
                null,
                Guid.Empty));
        }

        return Result.Success(new SurveyComboDiscountPolicyResponse(
            current.Id,
            current.VersionNumber,
            current.DiscountPercentage,
            current.EffectiveFrom,
            current.EffectiveTo,
            current.CreatedBy));
    }
}

