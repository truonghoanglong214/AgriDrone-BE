using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.GetTenantOwnerSurveyOrderInvoice;

public sealed record GetTenantOwnerSurveyOrderInvoiceQuery(Guid OrderId)
    : IRequest<Result<TenantOwnerSurveyOrderInvoiceResponse>>;

public sealed record TenantOwnerSurveyOrderInvoiceResponse(
    Guid OrderId,
    string OrderNumber,
    string OrderStatus,
    int InvoiceRevisionNumber,
    int VerifiedBillablePoleCount,
    decimal PricePerPole,
    string Currency,
    decimal TotalAmount,
    decimal DueAmount,
    DateTimeOffset FinalizedAt);
