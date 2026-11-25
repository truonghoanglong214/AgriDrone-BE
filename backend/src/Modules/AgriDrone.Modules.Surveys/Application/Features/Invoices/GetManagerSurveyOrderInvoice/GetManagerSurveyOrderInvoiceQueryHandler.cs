using AgriDrone.Modules.Surveys.Application.Abstractions.Invoices;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.GetManagerSurveyOrderInvoice;

internal sealed class GetManagerSurveyOrderInvoiceQueryHandler(
    ISurveyInvoiceQueries queries,
    IExecutionContext executionContext)
    : IRequestHandler<GetManagerSurveyOrderInvoiceQuery, Result<ManagerSurveyOrderInvoiceResponse>>
{
    public async Task<Result<ManagerSurveyOrderInvoiceResponse>> Handle(
        GetManagerSurveyOrderInvoiceQuery request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actorId || actorId == Guid.Empty)
        {
            return Result.Failure<ManagerSurveyOrderInvoiceResponse>(
                AppError.Unauthorized("Invoice.Unauthorized", "An authenticated assigned SystemManager is required."));
        }

        return await queries.GetManagerInvoiceAsync(request.OrderId, actorId, cancellationToken);
    }
}
