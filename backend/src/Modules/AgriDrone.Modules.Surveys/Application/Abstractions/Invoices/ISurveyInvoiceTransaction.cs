using AgriDrone.Modules.Surveys.Application.Features.Invoices.CorrectSurveyOrderPreReleaseInvoice;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.FinalizeSurveyOrderInvoice;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Invoices;

public interface ISurveyInvoiceTransaction
{
    Task<Result<FinalizeSurveyOrderInvoiceResponse>> FinalizeInvoiceAsync(
        FinalizeSurveyOrderInvoiceCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<Result<CorrectSurveyOrderPreReleaseInvoiceResponse>> CorrectPreReleaseInvoiceAsync(
        CorrectSurveyOrderPreReleaseInvoiceCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
