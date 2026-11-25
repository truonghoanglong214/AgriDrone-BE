using AgriDrone.Modules.Surveys.Application.Abstractions.Invoices;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.FinalizeSurveyOrderInvoice;

internal sealed class FinalizeSurveyOrderInvoiceCommandHandler(
    ISurveyInvoiceTransaction transaction,
    IExecutionContext executionContext)
    : IRequestHandler<FinalizeSurveyOrderInvoiceCommand, Result<FinalizeSurveyOrderInvoiceResponse>>
{
    public async Task<Result<FinalizeSurveyOrderInvoiceResponse>> Handle(
        FinalizeSurveyOrderInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actorId || actorId == Guid.Empty)
        {
            return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(
                AppError.Unauthorized("InvoiceFinalization.Unauthorized", "An authenticated assigned SystemManager is required."));
        }

        return await transaction.FinalizeInvoiceAsync(request, actorId, cancellationToken);
    }
}
