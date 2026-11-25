using AgriDrone.Modules.Surveys.Application.Features.Invoices.GetManagerSurveyOrderInvoice;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.GetTenantOwnerSurveyOrderInvoice;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Invoices;

public interface ISurveyInvoiceQueries
{
    Task<Result<ManagerSurveyOrderInvoiceResponse>> GetManagerInvoiceAsync(
        Guid orderId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<Result<TenantOwnerSurveyOrderInvoiceResponse>> GetTenantOwnerInvoiceAsync(
        Guid orderId,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
