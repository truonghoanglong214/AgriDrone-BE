using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyComboDiscount;

public sealed record GetCurrentSurveyComboDiscountPolicyQuery
    : IRequest<Result<SurveyComboDiscountPolicyResponse>>;

