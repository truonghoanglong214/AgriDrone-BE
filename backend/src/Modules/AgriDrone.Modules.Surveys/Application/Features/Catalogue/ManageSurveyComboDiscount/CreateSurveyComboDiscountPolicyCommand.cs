using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyComboDiscount;

public sealed record CreateSurveyComboDiscountPolicyCommand(
    decimal DiscountPercentage,
    int ExpectedVersion)
    : IRequest<Result<SurveyComboDiscountPolicyResponse>>;

