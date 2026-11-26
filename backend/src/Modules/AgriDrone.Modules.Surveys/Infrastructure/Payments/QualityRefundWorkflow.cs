using System.Data;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AgriDrone.Modules.Surveys.Infrastructure.Payments;

internal sealed class QualityRefundWorkflow(
    SurveysDbContext db,
    IExecutionContext executionContext,
    ISystemManagerAccessService managerAccess,
    IAuditWriter auditWriter,
    TimeProvider timeProvider,
    IQualityRefundNoticeQuery noticeQuery,
    AgriDrone.SharedKernel.Application.Abstractions.IRefundEvidenceStorage evidenceStorage,
    IV3ResultReleaseProofReader? releaseProofReader = null,
    IV3QualityRefundFundingReader? v3FundingReader = null) : IQualityRefundWorkflow
{
    public async Task<Result<QualityRefundCaseView>> ReportAsync(
        Guid orderId,
        IReadOnlyList<ReportedPoleIssue> issues,
        string description,
        string? evidenceReference,
        string recipientBankName,
        string recipientAccountNumber,
        string recipientAccountName,
        CancellationToken cancellationToken,
        Guid? publicationId = null)
    {
        if (executionContext.ActorId is not Guid actorId ||
            executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<QualityRefundCaseView>(Forbidden());
        }

        var receivedAt = timeProvider.GetUtcNow();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var order = await LockOrderAsync(orderId, tenantId, cancellationToken);
            if (order is null)
            {
                return Result.Failure<QualityRefundCaseView>(NotFound());
            }

            if (await db.QualityRefundCases.AnyAsync(item =>
                    item.SurveyOrderId == orderId &&
                    item.Status != QualityRefundStatus.Rejected,
                    cancellationToken))
            {
                return Result.Failure<QualityRefundCaseView>(
                    AppError.Conflict("QualityRefund.ExistingCase", "An active or completed quality refund case already exists."));
            }

            var claim = await ReadClaimContextAsync(order, cancellationToken);
            if (claim.IsFailure)
                return Result.Failure<QualityRefundCaseView>(claim.Error);

            var context = claim.Value;
            if (context.Release is not null && publicationId != context.Release.PublicationId)
                return Result.Failure<QualityRefundCaseView>(AppError.Conflict(
                    "QualityRefund.PublicationChanged", "Reload the claim window and submit the current V3 publication ID."));

            if (receivedAt < context.NoticeCreatedAt || receivedAt >= context.DeadlineExclusive)
                return Result.Failure<QualityRefundCaseView>(AppError.Conflict(
                    "QualityRefund.ClaimWindowClosed", "The deadline for submitting a quality claim has passed."));

            if (context.Release is { ScreensDisease: false } &&
                issues.Any(issue => issue is not null && issue.IssueType != "WRONG_POLE"))
                return Result.Failure<QualityRefundCaseView>(AppError.Validation(
                    "QualityRefund.InvalidMapIssue", "Only pole mapping issues may be reported for a MAP publication."));

            QualityRefundCase refundCase;
            try
            {
                refundCase = context.Release is { } release
                    ? QualityRefundCase.ReportV3(
                        order, release.PublicationId, actorId, issues, description, evidenceReference,
                        recipientBankName, recipientAccountNumber, recipientAccountName, receivedAt)
                    : QualityRefundCase.Report(
                        order, context.LegacyResult!, actorId, issues, description, evidenceReference,
                        recipientBankName, recipientAccountNumber, recipientAccountName, receivedAt);
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

            var releasedPoleCodes = context.Release is { } released
                ? await GetV3ReleasedPoleCodesAsync(released, cancellationToken)
                : await GetReleasedPoleCodesAsync(
                    order.Id, context.LegacyResult!.Id, tenantId, order.FarmId, cancellationToken);
            var allowedPoleCodes = releasedPoleCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (issues.Any(issue => !allowedPoleCodes.Contains(issue.PoleCode.Trim())))
            {
                return Result.Failure<QualityRefundCaseView>(AppError.Validation(
                    "QualityRefund.PoleOutsideReleasedResult",
                    "One or more pole codes are outside this published survey result."));
            }

            db.QualityRefundCases.Add(refundCase);
            using var auditData = JsonSerializer.SerializeToDocument(new
            {
                refundCase.SurveyOrderId,
                refundCase.SurveyResultId,
                refundCase.V3PublicationId,
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

    public async Task<Result<QualityRefundClaimWindowView>> GetOwnerClaimWindowAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null || executionContext.TenantId is not Guid tenantId)
            return Result.Failure<QualityRefundClaimWindowView>(Forbidden());
        var now = timeProvider.GetUtcNow();
        var order = await db.SurveyOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId && x.TenantId == tenantId, cancellationToken);
        if (order is null) return Result.Failure<QualityRefundClaimWindowView>(NotFound());
        if (order.Scope == ContractScope.Legacy)
            return await GetLegacyClaimWindowAsync(order, now, cancellationToken);

        var claim = await ReadClaimContextAsync(order, cancellationToken);
        if (claim.IsFailure)
            return Result.Success(new QualityRefundClaimWindowView(now, null, null, false, claim.Error.Description));

        var context = claim.Value;
        string? reason = null;
        if (await db.QualityRefundCases.AnyAsync(x =>
                x.SurveyOrderId == orderId && x.Status != QualityRefundStatus.Rejected, cancellationToken))
            reason = "The order already has a claim being handled or refunded.";
        else if (now < context.NoticeCreatedAt || now >= context.DeadlineExclusive)
            reason = "The deadline for submitting a quality claim has passed.";

        return Result.Success(new QualityRefundClaimWindowView(
            now, context.NoticeCreatedAt, context.DeadlineExclusive, reason is null, reason,
            context.Release?.PublicationId));
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
                refundCase.V3PublicationId,
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
        if (order.Scope == ContractScope.V3)
        {
            var funding = await ReadV3FundingAsync(order, cancellationToken);
            return funding.IsFailure
                ? Result.Failure<QualityRefundOrderView>(funding.Error)
                : Result.Success(new QualityRefundOrderView(
                    orderId, funding.Value.GrossConfirmedReceipts, funding.Value.CompletedRefunds,
                    funding.Value.OrderConfirmedNetCash, order.Currency ?? "VND", cases.Select(Map).ToArray()));
        }

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
            var order = await LockOrderAsync(orderId, tenantId: null, cancellationToken);
            if (order is null)
                return Result.Failure<QualityRefundCaseView>(NotFound());

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
                    if (order.Scope == ContractScope.V3)
                    {
                        var release = await ReadV3ReleaseAsync(order, cancellationToken);
                        if (release is null || refundCase.V3PublicationId != release.PublicationId)
                            return Result.Failure<QualityRefundCaseView>(AppError.Conflict(
                                "QualityRefund.ResultChanged", "The V3 publication on this claim is no longer current."));

                        var funding = await ReadV3FundingAsync(order, cancellationToken);
                        if (funding.IsFailure)
                            return Result.Failure<QualityRefundCaseView>(funding.Error);
                        if (funding.Value.RequiresReconciliation)
                            return Result.Failure<QualityRefundCaseView>(FundingOnHold());
                        if (funding.Value.AvailableRefundCap <= 0m)
                            return Result.Failure<QualityRefundCaseView>(NoRefundableAmount());

                        refundCase.Approve(adminId, reason, funding.Value.AvailableRefundCap, now);
                    }
                    else
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
                            return Result.Failure<QualityRefundCaseView>(NoRefundableAmount());

                        refundCase.Approve(adminId, reason, amount, now);
                        result.InvalidateForQualityRefund(refundCase.Id, adminId, now, result.Version);
                    }
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
                refundCase.V3PublicationId,
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
        string transferEvidenceReference,
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
            var order = await LockOrderAsync(orderId, tenantId: null, cancellationToken);
            if (order is null)
                return Result.Failure<QualityRefundCaseView>(NotFound());

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
                if (string.IsNullOrWhiteSpace(transferEvidenceReference) ||
                    !await evidenceStorage.ExistsAsync(refundCase.TenantId, caseId, transferEvidenceReference, cancellationToken))
                    return Result.Failure<QualityRefundCaseView>(AppError.Validation("QualityRefund.InvalidEvidence", "Ảnh biên lai không hợp lệ hoặc chưa upload thành công."));
                if (order.Scope == ContractScope.V3 && refundCase.Status == QualityRefundStatus.Approved)
                {
                    var funding = await ReadV3FundingAsync(order, cancellationToken);
                    if (funding.IsFailure)
                        return Result.Failure<QualityRefundCaseView>(funding.Error);
                    if (funding.Value.RequiresReconciliation)
                        return Result.Failure<QualityRefundCaseView>(FundingOnHold());
                }

                refundCase.RecordTransfer(adminId, bankTransferReference,
                    now, expectedVersion, transferEvidenceReference);
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
                refundCase.TransferEvidenceReference,
                refundCase.TransferredAt,
                refundCase.RecordedBy,
                refundCase.V3PublicationId
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

    public async Task<Result<string>> UploadTransferEvidenceAsync(Guid orderId, Guid caseId, Stream content, CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null) return Result.Failure<string>(Forbidden());
        var item = await db.QualityRefundCases.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == caseId && x.SurveyOrderId == orderId, cancellationToken);
        if (item is null) return Result.Failure<string>(NotFound());
        if (item.Status != QualityRefundStatus.Approved)
            return Result.Failure<string>(AppError.Conflict("QualityRefund.NotApproved", "Chỉ upload biên lai cho hồ sơ đã duyệt và chưa hoàn tiền."));
        try
        {
            return Result.Success(await evidenceStorage.UploadAsync(item.TenantId, caseId, content, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<string>(AppError.Validation("QualityRefund.InvalidEvidence", exception.Message));
        }
    }

    public async Task<Result<Uri>> GetTransferEvidenceAsync(Guid orderId, Guid caseId, bool owner, CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null || (owner && executionContext.TenantId is null))
            return Result.Failure<Uri>(Forbidden());
        var item = await db.QualityRefundCases.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == caseId && x.SurveyOrderId == orderId &&
                 (!owner || x.TenantId == executionContext.TenantId), cancellationToken);
        if (item is null || item.Status != QualityRefundStatus.Refunded || string.IsNullOrWhiteSpace(item.TransferEvidenceReference))
            return Result.Failure<Uri>(NotFound());
        if (!await evidenceStorage.ExistsAsync(item.TenantId, caseId, item.TransferEvidenceReference, cancellationToken))
            return Result.Failure<Uri>(NotFound());
        return Result.Success(await evidenceStorage.GetDownloadUriAsync(item.TransferEvidenceReference, cancellationToken));
    }

    private Task<SurveyOrder?> LockOrderAsync(Guid orderId, Guid? tenantId, CancellationToken cancellationToken) =>
        db.SurveyOrders.FromSqlInterpolated($"""
            SELECT o.*, o.xmin FROM survey.survey_orders o
            WHERE o.id = {orderId} AND ({tenantId}::uuid IS NULL OR o.tenant_id = {tenantId}::uuid)
            FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);

    private sealed record ClaimContext(
        DateTimeOffset NoticeCreatedAt,
        DateTimeOffset DeadlineExclusive,
        SurveyResult? LegacyResult,
        V3ResultReleaseProof? Release);

    private async Task<Result<QualityRefundClaimWindowView>> GetLegacyClaimWindowAsync(
        SurveyOrder order, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var result = await db.SurveyResults.AsNoTracking().SingleOrDefaultAsync(x =>
            x.SurveyOrderId == order.Id && x.TenantId == order.TenantId, cancellationToken);
        var notice = result is null ? null : await noticeQuery.GetCreatedAtAsync(
            order.TenantId, order.Id, result.Id, cancellationToken);
        DateTimeOffset? deadline = notice is null ? null : ClaimPolicy.CalculateSevenDayDeadline(notice.Value);
        string? reason = null;
        if (order.Status != SurveyOrderStatus.Completed || result is null || result.Status != SurveyResultStatus.Published ||
            result.InvalidatedAt.HasValue || result.ServiceType != SurveyServiceType.PlantHealth)
            reason = "Đơn chưa có kết quả đủ điều kiện khiếu nại.";
        else if (await db.QualityRefundCases.AnyAsync(x => x.SurveyOrderId == order.Id && x.Status != QualityRefundStatus.Rejected, cancellationToken))
            reason = "Đơn đã có hồ sơ đang xử lý hoặc đã hoàn tiền.";
        else if (notice is null) reason = "Chưa có thông báo mở kết quả để xác định thời hạn khiếu nại.";
        else if (now < notice.Value || now >= deadline!.Value) reason = "Đã hết thời hạn 7 ngày gửi khiếu nại hoàn tiền.";
        else if (await GrossPaidAsync(order.Id, cancellationToken) <= 0) reason = "Đơn chưa có khoản thanh toán được xác nhận.";
        return Result.Success(new QualityRefundClaimWindowView(now, notice, deadline, reason is null, reason));
    }

    private async Task<Result<ClaimContext>> ReadClaimContextAsync(SurveyOrder order, CancellationToken cancellationToken)
    {
        if (order.Scope == ContractScope.V3)
        {
            var release = await ReadV3ReleaseAsync(order, cancellationToken);
            if (release is null)
                return Result.Failure<ClaimContext>(AppError.Conflict(
                    "QualityRefund.ReleaseUnavailable", "A current V3 publication and claim window are required."));

            var funding = await ReadV3FundingAsync(order, cancellationToken);
            if (funding.IsFailure)
                return Result.Failure<ClaimContext>(funding.Error);
            if (funding.Value.RequiresReconciliation)
                return Result.Failure<ClaimContext>(FundingOnHold());
            if (funding.Value.AvailableRefundCap <= 0m)
                return Result.Failure<ClaimContext>(NoRefundableAmount());

            return Result.Success(new ClaimContext(
                release.NoticeCreatedAt, release.ClaimDeadlineExclusiveUtc, null, release));
        }

        var result = await db.SurveyResults.AsNoTracking().SingleOrDefaultAsync(x =>
            x.SurveyOrderId == order.Id && x.TenantId == order.TenantId && x.FarmId == order.FarmId,
            cancellationToken);
        if (order.Status != SurveyOrderStatus.Completed || result is null ||
            result.Status != SurveyResultStatus.Published || result.InvalidatedAt.HasValue ||
            result.ServiceType != SurveyServiceType.PlantHealth)
            return Result.Failure<ClaimContext>(AppError.Conflict(
                "QualityRefund.ResultUnavailable", "A current published Plant Health result is required."));
        if (await GrossPaidAsync(order.Id, cancellationToken) <= 0m)
            return Result.Failure<ClaimContext>(AppError.Conflict(
                "QualityRefund.NoCapturedPayment", "The order has no captured service payment."));

        var notice = await noticeQuery.GetCreatedAtAsync(order.TenantId, order.Id, result.Id, cancellationToken);
        if (notice is null)
            return Result.Failure<ClaimContext>(AppError.Conflict(
                "QualityRefund.NoticeUnavailable", "A notification is required to determine the claim deadline."));

        return Result.Success(new ClaimContext(notice.Value,
            ClaimPolicy.CalculateSevenDayDeadline(notice.Value), result, null));
    }

    private async Task<V3ResultReleaseProof?> ReadV3ReleaseAsync(SurveyOrder order, CancellationToken cancellationToken)
    {
        if (order.Status != SurveyOrderStatus.Completed || releaseProofReader is null)
            return null;

        var release = await releaseProofReader.GetByOrderIdAsync(order.TenantId, order.Id, cancellationToken);
        return release is not null && release.AllowsOwnerActiveAccess(order.TenantId, order.FarmId) &&
            release.OrderId == order.Id &&
            ((release.ServiceCode, release.ScreensDisease) is
                (SurveyInternalApprovedResultRevision.ServiceMap, false) or
                (SurveyInternalApprovedResultRevision.ServiceCombo, true)) &&
            release.ApprovedResultRevisionId != Guid.Empty &&
            release.ApprovedResultRevisionId == order.CurrentApprovedResultRevisionId &&
            release.InvoiceRevisionId != Guid.Empty && release.InvoiceRevisionId == order.CurrentInvoiceRevisionId &&
            release.ClaimWindowId != Guid.Empty && release.NoticeCreatedAt >= release.UnlockedAt &&
            release.ClaimDeadlineExclusiveUtc > release.NoticeCreatedAt
            ? release
            : null;
    }

    private Task<Result<V3QualityRefundFunding>> ReadV3FundingAsync(SurveyOrder order, CancellationToken cancellationToken) =>
        v3FundingReader?.ReadAsync(order, db.Database.CurrentTransaction?.GetDbTransaction(), cancellationToken) ??
        Task.FromResult(Result.Failure<V3QualityRefundFunding>(FundingOnHold()));

    private static AppError FundingOnHold() =>
        AppError.Conflict("QualityRefund.FundingOnHold", "V3 funding must be reconciled before approving a refund.");

    private static AppError NoRefundableAmount() =>
        AppError.Conflict("QualityRefund.NoRefundableAmount", "No confirmed amount remains refundable.");

    private Task<List<string>> GetV3ReleasedPoleCodesAsync(V3ResultReleaseProof release, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<string>($"""
            SELECT DISTINCT p.plant_code AS "Value"
            FROM survey.survey_internal_approved_result_revisions approval
            JOIN survey.survey_order_pole_count_snapshots snapshot
              ON snapshot.id = approval.pole_count_snapshot_id
             AND snapshot.survey_order_id = approval.survey_order_id
             AND snapshot.tenant_id = approval.tenant_id AND snapshot.farm_id = approval.farm_id
            JOIN survey.survey_order_pole_count_snapshot_items item
              ON item.snapshot_id = snapshot.id AND item.tenant_id = snapshot.tenant_id
             AND item.farm_id = snapshot.farm_id AND item.mapping_candidate_batch_id = snapshot.mapping_candidate_batch_id
             AND item.is_accepted AND item.is_strictly_in_boundary
            JOIN plant.plants p
              ON p.farm_id = snapshot.farm_id AND p.plant_profile_id = item.plant_profile_id
            WHERE approval.id = {release.ApprovedResultRevisionId}
              AND approval.survey_order_id = {release.OrderId}
              AND approval.tenant_id = {release.TenantId} AND approval.farm_id = {release.FarmId}
              AND approval.evidence_fingerprint = {release.ApprovedResultFingerprint}
              AND p.created_at <= {release.PublishedAt}
            """).ToListAsync(cancellationToken);

    private Task<List<string>> GetReleasedPoleCodesAsync(
        Guid orderId,
        Guid resultId,
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken) =>
        db.Database.SqlQuery<string>($"""
            WITH released AS (
                SELECT r.survey_order_id, r.tenant_id, r.farm_id,
                       r.farm_base_map_version_id, r.farm_boundary_version_id,
                       o.verified_pole_count_snapshot_id, publication.published_at,
                       publication.payload
                FROM survey.survey_results r
                JOIN survey.survey_orders o
                  ON o.id = r.survey_order_id
                 AND o.tenant_id = r.tenant_id
                 AND o.farm_id = r.farm_id
                JOIN survey.plant_health_publications publication
                  ON publication.survey_result_id = r.id
                 AND publication.tenant_id = r.tenant_id
                 AND publication.farm_id = r.farm_id
                 AND publication.batch_id = r.approved_batch_id
                 AND publication.approval_fingerprint = r.approval_fingerprint
                 AND publication.published_at = r.published_at
                WHERE r.id = {resultId}
                  AND r.survey_order_id = {orderId}
                  AND r.tenant_id = {tenantId}
                  AND r.farm_id = {farmId}
                  AND r.status = 'PUBLISHED'
                  AND r.invalidated_at IS NULL
            )
            SELECT p.plant_code AS "Value"
            FROM released r
            JOIN survey.survey_order_pole_count_snapshots snapshot
              ON snapshot.id = r.verified_pole_count_snapshot_id
             AND snapshot.survey_order_id = r.survey_order_id
             AND snapshot.tenant_id = r.tenant_id
             AND snapshot.farm_id = r.farm_id
             AND snapshot.farm_base_map_version_id = r.farm_base_map_version_id
             AND snapshot.farm_boundary_version_id = r.farm_boundary_version_id
             AND snapshot.verified_at <= r.published_at
            JOIN survey.survey_order_pole_count_snapshot_items item
              ON item.snapshot_id = snapshot.id
             AND item.tenant_id = snapshot.tenant_id
             AND item.farm_id = snapshot.farm_id
             AND item.mapping_candidate_batch_id = snapshot.mapping_candidate_batch_id
             AND item.is_accepted
             AND item.is_strictly_in_boundary
            JOIN plant.plants p
              ON p.farm_id = r.farm_id
             AND p.plant_profile_id = item.plant_profile_id
             AND p.created_at <= r.published_at

            UNION

            SELECT p.plant_code AS "Value"
            FROM released r
            CROSS JOIN LATERAL jsonb_array_elements(r.payload -> 'Findings') AS finding(value)
            JOIN plant.plants p
              ON p.farm_id = r.farm_id
             AND p.id = (finding.value ->> 'PlantId')::uuid
            """).ToListAsync(cancellationToken);

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
        item.Version, item.CreatedAt, item.UpdatedAt, item.V3PublicationId);

    private static ManagerQualityRefundCaseView MapManager(QualityRefundCase item) => new(
        item.Id, item.SurveyOrderId, item.SurveyResultId, item.Status,
        item.ReportedIssues.RootElement.Deserialize<ReportedPoleIssue[]>() ?? [],
        item.Description, item.EvidenceReference,
        item.ManagerFindings?.RootElement.Deserialize<VerifiedPoleIssue[]>(),
        item.ManagerVerifiedBy, item.ManagerVerifiedAt, item.ManagerVerificationReason,
        item.Version, item.CreatedAt, item.UpdatedAt, item.V3PublicationId);

    private static AppError Forbidden() =>
        AppError.Forbidden("QualityRefund.Forbidden", "Actor is not authorized.");

    private static AppError NotFound() =>
        AppError.NotFound("QualityRefund.NotFound", "Order or refund case was not found.");

    private static AppError Conflict() =>
        AppError.Conflict("QualityRefund.Concurrency", "Refund case changed concurrently. Reload and retry.");
}
