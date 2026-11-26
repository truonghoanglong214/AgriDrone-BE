using System.Data.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface IV3QualityRefundFundingReader
{
    // Writers supply the transaction holding the order lock.
    Task<Result<V3QualityRefundFunding>> ReadAsync(
        SurveyOrder order,
        DbTransaction? transaction,
        CancellationToken cancellationToken);
}
