using System.Data;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Modules.Surveys.Infrastructure.Payments;

internal sealed class QualityRefundWorkflow(
    SurveysDbContext db,
    IExecutionContext executionContext,
    ISystemManagerAccessService managerAccess,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : IQualityRefundWorkflow
{
    public async Task<Result<QualityRefundCaseView>> ReportAsync(
        Guid orderId,
        IReadOnlyList<ReportedPoleIssue> issues,
        string description,
        string? evidenceReference,
        string recipientBankName,
        string recipientAccountNumber,
        string recipientAccountName,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId ||
            executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<QualityRefundCaseView>(Forbidden());
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var order = await db.SurveyOrders.SingleOrDefaultAsync(item =>
                item.Id == orderId && item.TenantId == tenantId,
                cancellationToken);
            if (order is null)
            {
                return Result.Failure<QualityRefundCaseView>(NotFound());
            }

            var result = await db.SurveyResults.SingleOrDefaultAsync(item =>
                item.SurveyOrderId == orderId,
                cancellationToken);
            if (result is null)
            {
                return Result.Failure<QualityRefundCaseView>(
                    AppError.Conflict("QualityRefund.ResultUnavailable", "A published result is required."));
            }

            if (await db.QualityRefundCases.AnyAsync(item =>
                    item.SurveyOrderId == orderId &&
                    item.Status != QualityRefundStatus.Rejected,
                    cancellationToken))
            {
                return Result.Failure<QualityRefundCaseView>(
                    AppError.Conflict("QualityRefund.ExistingCase", "An active or completed quality refund case already exists."));
            }

            if (await GrossPaidAsync(orderId, cancellationToken) <= 0)
            {
                return Result.Failure<QualityRefundCaseView>(
                    AppError.Conflict("QualityRefund.NoCapturedPayment", "The order has no captured service payment."));
            }

            QualityRefundCase refundCase;
            try
            {
                refundCase = QualityRefundCase.Report(
                    order, result, actorId, issues, description, evidenceReference,
                    recipientBankName, recipientAccountNumber, recipientAccountName,
                    timeProvider.GetUtcNow());
            }
            catch (ArgumentException exception)
            {
                return Result.Failure<QualityRefundCaseView>(
                    AppError.Validation("QualityRefund.InvalidReport", exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                return Result.Failure<QualityRefundCaseView>(
                    AppError.Conflict("QualityRefund.InvalidResult", exception.Message));
            }

            db.QualityRefundCases.Add(refundCase);
            using var auditData = JsonSerializer.SerializeToDocument(new
            {
                refundCase.SurveyOrderId,
                refundCase.SurveyResultId,
                refundCase.Status,
                refundCase.ReportedIssues,
                refundCase.Description,
                refundCase.RecipientBankName,
                refundCase.RecipientAccountNumber,
                refundCase.RecipientAccountName
            });
            auditWriter.AddUserAction(db, tenantId, order.FarmId, actorId,
                executionContext.CorrelationId, nameof(QualityRefundCase),
                refundCase.Id, "REPORT", null, auditData, refundCase.CreatedAt);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(Map(refundCase));
        }
        catch (DbUpdateException)
        {
            return Result.Failure<QualityRefundCaseView>(Conflict());
        }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "23505")
        {
            return Result.Failure<QualityRefundCaseView>(Conflict());
        }
    }

    public async Task<Result<IReadOnlyList<QualityRefundCaseView>>> GetOwnerCasesAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId ||
            executionContext.ActorId is null)
        {
            return Result.Failure<IReadOnlyList<QualityRefundCaseView>>(Forbidden());
        }

        if (!await db.SurveyOrders.AnyAsync(item =>
                item.Id == orderId && item.TenantId == tenantId,
                cancellationToken))
        {
            return Result.Failure<IReadOnlyList<QualityRefundCaseView>>(NotFound());
        }

        var cases = await db.QualityRefundCases.AsNoTracking()
            .Where(item => item.SurveyOrderId == orderId && item.TenantId == tenantId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<QualityRefundCaseView>>(
            cases.Select(Map).ToArray());
    }

    public async Task<Result<IReadOnlyList<ManagerQualityRefundCaseView>>> GetManagerCasesAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<IReadOnlyList<ManagerQualityRefundCaseView>>(Forbidden());
        }

        var order = await db.SurveyOrders.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<IReadOnlyList<ManagerQualityRefundCaseView>>(NotFound());
        }

        var access = await managerAccess.ResolveFarmAccessAsync(order.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != order.TenantId)
        {
            return Result.Failure<IReadOnlyList<ManagerQualityRefundCaseView>>(Forbidden());
        }

        var cases = await db.QualityRefundCases.AsNoTracking()
            .Where(item => item.SurveyOrderId == orderId && item.TenantId == order.TenantId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ManagerQualityRefundCaseView>>(
            cases.Select(MapManager).ToArray());
    }

    public async Task<Result<ManagerQualityRefundCaseView>> VerifyByManagerAsync(
        Guid orderId,
        Guid caseId,
        IReadOnlyList<VerifiedPoleIssue> findings,
        string reason,
        uint expectedVersion,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid managerId)
        {
            return Result.Failure<ManagerQualityRefundCaseView>(Forbidden());
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var refundCase = await db.QualityRefundCases.SingleOrDefaultAsync(item =>
                item.Id == caseId && item.SurveyOrderId == orderId,
                cancellationToken);
            if (refundCase is null)
            {
                return Result.Failure<ManagerQualityRefundCaseView>(NotFound());
            }

            var access = await managerAccess.ResolveFarmAccessAsync(
                refundCase.FarmId, cancellationToken);
            if (!access.IsAllowed || access.TenantId != refundCase.TenantId)
            {
                return Result.Failure<ManagerQualityRefundCaseView>(Forbidden());
            }

            var now = timeProvider.GetUtcNow();
            try
            {
                refundCase.VerifyByManager(managerId, findings, reason, now, expectedVersion);
            }
            catch (ArgumentException exception)
            {
                return Result.Failure<ManagerQualityRefundCaseView>(
                    AppError.Validation("QualityRefund.InvalidVerification", exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                return Result.Failure<ManagerQualityRefundCaseView>(
                    AppError.Conflict("QualityRefund.VerificationConflict", exception.Message));
            }

            using var auditData = JsonSerializer.SerializeToDocument(new
            {
                refundCase.SurveyOrderId,
                refundCase.SurveyResultId,
                refundCase.ManagerFindings,
                refundCase.ManagerVerificationReason,
                refundCase.ManagerVerifiedBy,
                refundCase.ManagerVerifiedAt
            });
            auditWriter.AddUserAction(db, refundCase.TenantId, refundCase.FarmId,
                managerId, executionContext.CorrelationId, nameof(QualityRefundCase),
                refundCase.Id, "VERIFY", null, auditData, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(MapManager(refundCase));
        }
        catch (DbUpdateException)
        {
            return Result.Failure<ManagerQualityRefundCaseView>(Conflict());
        }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "23505")
        {
            return Result.Failure<ManagerQualityRefundCaseView>(Conflict());
        }
    }

    public async Task<Result<QualityRefundOrderView>> GetAdminCasesAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<QualityRefundOrderView>(Forbidden());
        }

        var order = await db.SurveyOrders.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<QualityRefundOrderView>(NotFound());
        }

        var cases = await db.QualityRefundCases.AsNoTracking()
            .Where(item => item.SurveyOrderId == orderId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var gross = await GrossPaidAsync(orderId, cancellationToken);
        var refunded = cases.Where(item => item.Status == QualityRefundStatus.Refunded)
            .Sum(item => item.ApprovedAmount ?? 0m);
        return Result.Success(new QualityRefundOrderView(orderId, gross, refunded,
            gross - refunded, order.Currency ?? "VND", cases.Select(Map).ToArray()));
    }

    public async Task<Result<PagedResult<QualityRefundQueueItem>>> GetAdminQueueAsync(
        QualityRefundStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<PagedResult<QualityRefundQueueItem>>(Forbidden());
        }
        if (pageNumber < 1 || pageSize is < 1 or > 100 ||
            pageNumber - 1 > int.MaxValue / pageSize ||
            (status.HasValue && !Enum.IsDefined(status.Value)))
        {
            return Result.Failure<PagedResult<QualityRefundQueueItem>>(
                AppError.Validation("QualityRefund.InvalidPage", "Invalid status or page size."));
        }

        var query = db.QualityRefundCases.AsNoTracking()
            .Where(item => !status.HasValue || item.Status == status.Value);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(item => new { RefundCase = item, item.SurveyOrder.OrderNumber })
            .ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<QualityRefundQueueItem>(
            rows.Select(item => new QualityRefundQueueItem(item.OrderNumber, Map(item.RefundCase))).ToArray(),
            pageNumber, pageSize, total));
    }

    public async Task<Result<QualityRefundCaseView>> DecideAsync(
        Guid orderId,
        Guid caseId,
        bool approve,
        string reason,
        uint expectedVersion,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid adminId)
        {
            return Result.Failure<QualityRefundCaseView>(Forbidden());
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var refundCase = await db.QualityRefundCases.SingleOrDefaultAsync(item =>
                item.Id == caseId && item.SurveyOrderId == orderId,
                cancellationToken);
            if (refundCase is null)
            {
                return Result.Failure<QualityRefundCaseView>(NotFound());
            }

            var now = timeProvider.GetUtcNow();
            try
            {
                refundCase.EnsureVersion(expectedVersion);
                if (approve)
                {
                    var result = await db.SurveyResults.SingleOrDefaultAsync(item =>
                        item.Id == refundCase.SurveyResultId &&
                        item.SurveyOrderId == orderId,
                        cancellationToken);
                    if (result is null || result.InvalidatedAt.HasValue ||
                        result.Status != SurveyResultStatus.Published)
                    {
                        return Result.Failure<QualityRefundCaseView>(
                            AppError.Conflict("QualityRefund.ResultChanged", "The published result is no longer current."));
                    }

                    var gross = await GrossPaidAsync(orderId, cancellationToken);
                    var alreadyRefunded = await RefundedAsync(orderId, cancellationToken);
                    var amount = gross - alreadyRefunded;
                    if (amount <= 0)
                    {
                        return Result.Failure<QualityRefundCaseView>(
                            AppError.Conflict("QualityRefund.NoRefundableAmount", "No captured amount remains refundable."));
                    }

                    refundCase.Approve(adminId, reason, amount, now);
                    result.InvalidateForQualityRefund(refundCase.Id, adminId, now, result.Version);
                }
                else
                {
                    refundCase.Reject(adminId, reason, now);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return Result.Failure<QualityRefundCaseView>(
                    AppError.Conflict("QualityRefund.DecisionConflict", exception.Message));
            }

            using var auditData = JsonSerializer.SerializeToDocument(new
            {
                refundCase.Status,
                refundCase.SurveyResultId,
                refundCase.DecisionReason,
                refundCase.ApprovedAmount
            });
            auditWriter.AddSystemAdminAction(db, adminId,
                executionContext.CorrelationId, nameof(QualityRefundCase),
                refundCase.Id, approve ? "APPROVE" : "REJECT", null, auditData, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(Map(refundCase));
        }
        catch (DbUpdateException)
        {
            return Result.Failure<QualityRefundCaseView>(Conflict());
        }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "23505")
        {
            return Result.Failure<QualityRefundCaseView>(Conflict());
        }
    }

    public async Task<Result<QualityRefundCaseView>> RecordTransferAsync(
        Guid orderId,
        Guid caseId,
        string bankTransferReference,
        uint expectedVersion,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid adminId)
        {
            return Result.Failure<QualityRefundCaseView>(Forbidden());
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var refundCase = await db.QualityRefundCases.SingleOrDefaultAsync(item =>
                item.Id == caseId && item.SurveyOrderId == orderId,
                cancellationToken);
            if (refundCase is null)
            {
                return Result.Failure<QualityRefundCaseView>(NotFound());
            }

            var now = timeProvider.GetUtcNow();
            try
            {
                refundCase.RecordTransfer(adminId, bankTransferReference,
                    now, expectedVersion);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return Result.Failure<QualityRefundCaseView>(
                    AppError.Conflict("QualityRefund.TransferConflict", exception.Message));
            }

            using var auditData = JsonSerializer.SerializeToDocument(new
            {
                refundCase.ApprovedAmount,
                refundCase.BankTransferReference,
                refundCase.TransferredAt,
                refundCase.RecordedBy
            });
            auditWriter.AddSystemAdminAction(db, adminId,
                executionContext.CorrelationId, nameof(QualityRefundCase),
                refundCase.Id, "RECORD_BANK_REFUND", null, auditData, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(Map(refundCase));
        }
        catch (DbUpdateException)
        {
            return Result.Failure<QualityRefundCaseView>(Conflict());
        }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "23505")
        {
            return Result.Failure<QualityRefundCaseView>(Conflict());
        }
    }

    private async Task<decimal> GrossPaidAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.SurveyPayments.AsNoTracking()
            .Where(item => item.SurveyOrderId == orderId &&
                           item.Status == SurveyPaymentStatus.Paid &&
                           item.ConfirmedAt != null)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

    private async Task<decimal> RefundedAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.QualityRefundCases.AsNoTracking()
            .Where(item => item.SurveyOrderId == orderId &&
                           item.Status == QualityRefundStatus.Refunded)
            .SumAsync(item => (decimal?)item.ApprovedAmount, cancellationToken) ?? 0m;

    private static QualityRefundCaseView Map(QualityRefundCase item) => new(
        item.Id, item.SurveyOrderId, item.SurveyResultId, item.Status,
        item.ReportedIssues.RootElement.Deserialize<ReportedPoleIssue[]>() ?? [],
        item.Description, item.EvidenceReference,
        item.RecipientBankName, item.RecipientAccountNumber, item.RecipientAccountName,
        item.ManagerFindings?.RootElement.Deserialize<VerifiedPoleIssue[]>(),
        item.ManagerVerifiedBy, item.ManagerVerifiedAt, item.ManagerVerificationReason,
        item.DecisionReason,
        item.ApprovedAmount, item.BankTransferReference,
        item.TransferEvidenceReference, item.TransferredAt,
        item.Version, item.CreatedAt, item.UpdatedAt);

    private static ManagerQualityRefundCaseView MapManager(QualityRefundCase item) => new(
        item.Id, item.SurveyOrderId, item.SurveyResultId, item.Status,
        item.ReportedIssues.RootElement.Deserialize<ReportedPoleIssue[]>() ?? [],
        item.Description, item.EvidenceReference,
        item.ManagerFindings?.RootElement.Deserialize<VerifiedPoleIssue[]>(),
        item.ManagerVerifiedBy, item.ManagerVerifiedAt, item.ManagerVerificationReason,
        item.Version, item.CreatedAt, item.UpdatedAt);

    private static AppError Forbidden() =>
        AppError.Forbidden("QualityRefund.Forbidden", "Actor is not authorized.");

    private static AppError NotFound() =>
        AppError.NotFound("QualityRefund.NotFound", "Order or refund case was not found.");

    private static AppError Conflict() =>
        AppError.Conflict("QualityRefund.Concurrency", "Refund case changed concurrently. Reload and retry.");
}
