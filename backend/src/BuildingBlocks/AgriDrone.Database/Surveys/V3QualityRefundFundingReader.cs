using System.Data.Common;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class V3QualityRefundFundingReader(DbContextOptions<AgriDroneSchemaDbContext> options)
    : IV3QualityRefundFundingReader
{
    public async Task<Result<V3QualityRefundFunding>> ReadAsync(
        SurveyOrder order,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var db = new AgriDroneSchemaDbContext(options);
        if (transaction is not null)
        {
            db.Database.SetDbConnection(transaction.Connection!);
            await db.Database.UseTransactionAsync(transaction, cancellationToken);
        }

        var deposit = order.DepositObligationId is Guid depositId
            ? await db.Set<SurveyDepositObligation>().AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == depositId && x.SurveyOrderId == order.Id && x.TenantId == order.TenantId,
                cancellationToken)
            : null;

        if (order.DepositObligationId.HasValue && deposit is null)
        {
            return Result.Failure<V3QualityRefundFunding>(AppError.Conflict(
                "QualityRefund.DepositUnavailable", "The V3 deposit obligation could not be verified."));
        }

        var funding = await SurveyInvoiceFunding.ReadAsync(
            db, order, deposit, order.CurrentInvoiceRevisionId,
            lockRows: transaction is not null, cancellationToken);

        return funding.IsFailure
            ? Result.Failure<V3QualityRefundFunding>(funding.Error)
            : Result.Success(new V3QualityRefundFunding(
                funding.Value.GrossConfirmedReceipts, funding.Value.TotalCompletedRefunds,
                funding.Value.OrderConfirmedNetCash, funding.Value.AvailableRefundCap,
                funding.Value.HasOpenReconciliation));
    }
}
