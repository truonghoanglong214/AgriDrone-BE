using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.GetManagerSurveyOrderInvoice;

internal sealed class GetManagerSurveyOrderInvoiceQueryValidator
    : AbstractValidator<GetManagerSurveyOrderInvoiceQuery>
{
    public GetManagerSurveyOrderInvoiceQueryValidator()
    {
        RuleFor(q => q.OrderId).NotEmpty();
    }
}
