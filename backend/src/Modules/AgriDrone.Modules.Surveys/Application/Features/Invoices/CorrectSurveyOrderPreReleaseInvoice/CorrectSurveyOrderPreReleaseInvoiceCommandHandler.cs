using AgriDrone.Modules.Surveys.Application.Abstractions.Invoices;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.CorrectSurveyOrderPreReleaseInvoice;

internal sealed class CorrectSurveyOrderPreReleaseInvoiceCommandHandler(
    ISurveyInvoiceTransaction transaction,
    IExecutionContext executionContext)
    : IRequestHandler<CorrectSurveyOrderPreReleaseInvoiceCommand, Result<CorrectSurveyOrderPreReleaseInvoiceResponse>>
{
    public async Task<Result<CorrectSurveyOrderPreReleaseInvoiceResponse>> Handle(
        CorrectSurveyOrderPreReleaseInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actorId || actorId == Guid.Empty)
        {
            return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(
                AppError.Unauthorized("InvoiceCorrection.Unauthorized", "An authenticated assigned SystemManager is required."));
        }

        return await transaction.CorrectPreReleaseInvoiceAsync(request, actorId, cancellationToken);
    }
}
