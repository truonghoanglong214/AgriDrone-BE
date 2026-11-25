using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.GetTenantOwnerSurveyOrderInvoice;

internal sealed class GetTenantOwnerSurveyOrderInvoiceQueryValidator
    : AbstractValidator<GetTenantOwnerSurveyOrderInvoiceQuery>
{
    public GetTenantOwnerSurveyOrderInvoiceQueryValidator()
    {
        RuleFor(q => q.OrderId).NotEmpty();
    }
}
