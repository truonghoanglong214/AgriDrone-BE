using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyComboDiscount;

public sealed record GetSurveyComboDiscountPoliciesQuery
    : IRequest<Result<IReadOnlyList<SurveyComboDiscountPolicyResponse>>>;

