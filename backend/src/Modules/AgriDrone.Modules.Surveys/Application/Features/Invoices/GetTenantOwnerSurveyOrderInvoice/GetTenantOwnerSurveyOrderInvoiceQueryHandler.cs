using AgriDrone.Modules.Surveys.Application.Abstractions.Invoices;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.GetTenantOwnerSurveyOrderInvoice;

internal sealed class GetTenantOwnerSurveyOrderInvoiceQueryHandler(
    ISurveyInvoiceQueries queries,
    IExecutionContext executionContext)
    : IRequestHandler<GetTenantOwnerSurveyOrderInvoiceQuery, Result<TenantOwnerSurveyOrderInvoiceResponse>>
{
    public async Task<Result<TenantOwnerSurveyOrderInvoiceResponse>> Handle(
        GetTenantOwnerSurveyOrderInvoiceQuery request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            actorId == Guid.Empty ||
            executionContext.TenantId is not Guid tenantId ||
            tenantId == Guid.Empty)
        {
            return Result.Failure<TenantOwnerSurveyOrderInvoiceResponse>(
                AppError.Unauthorized("Invoice.Unauthorized", "An authenticated TenantOwner with active tenant context is required."));
        }

        return await queries.GetTenantOwnerInvoiceAsync(request.OrderId, actorId, tenantId, cancellationToken);
    }
}
