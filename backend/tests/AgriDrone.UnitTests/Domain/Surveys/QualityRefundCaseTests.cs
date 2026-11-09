using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class QualityRefundCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VerifiedReportApprovesFullAmountAndRecordsBankTransfer()
    {
        var (order, result) = PublishedCompletedOrder();
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var refundCase = QualityRefundCase.Report(order, result, ownerId,
            [new ReportedPoleIssue("P-12", "FALSE_POSITIVE", "Cây thực tế khỏe")],
            "Kết quả báo cây bệnh sai", "photo-12", "Ngân hàng A", "1234567890", "NGUYEN VAN A",
            Now.AddHours(13));

        Assert.Equal(QualityRefundStatus.Submitted, refundCase.Status);
        Assert.Throws<InvalidOperationException>(() => refundCase.RecordTransfer(
            adminId, "TX-1", Now.AddHours(14), refundCase.Version));
        Assert.Throws<InvalidOperationException>(() => refundCase.Approve(
            adminId, "Chưa có xác minh", 1_600_000m, Now.AddHours(14)));

        refundCase.VerifyByManager(Guid.NewGuid(),
            [new VerifiedPoleIssue("P-12", true, "Đối chiếu hiện trường xác nhận cây khỏe")],
            "Đã kiểm tra kết quả và trụ thực tế", Now.AddHours(13).AddMinutes(30), refundCase.Version);

        refundCase.Approve(adminId, "Đã đối chiếu ảnh và trụ thực tế", 1_600_000m, Now.AddHours(14));
        result.InvalidateForQualityRefund(refundCase.Id, adminId, Now.AddHours(14), result.Version);
        Assert.Equal(refundCase.Id, result.InvalidatedByQualityRefundCaseId);
        Assert.Throws<InvalidOperationException>(() => QualityRefundCase.Report(order, result, ownerId,
            [new ReportedPoleIssue("P-13", "FALSE_NEGATIVE", "Cây bệnh")],
            "Báo lại", null, "Ngân hàng A", "1234567890", "NGUYEN VAN A", Now.AddHours(15)));

        var approvedVersion = refundCase.Version;
        Assert.Throws<InvalidOperationException>(() => refundCase.RecordTransfer(
            adminId, "TX-1", Now.AddHours(15), approvedVersion - 1));
        refundCase.RecordTransfer(adminId, "TX-1", Now.AddHours(15), approvedVersion);
        Assert.Equal(QualityRefundStatus.Refunded, refundCase.Status);
        Assert.Equal("TX-1", refundCase.BankTransferReference);
        Assert.Equal(Now.AddHours(15), refundCase.TransferredAt);
        Assert.Equal(refundCase.TransferredAt, refundCase.RecordedAt);
        Assert.Null(refundCase.TransferEvidenceReference);
    }

    [Fact]
    public void ReportRequiresDistinctPolesAndValidRecipient()
    {
        var (order, result) = PublishedCompletedOrder();
        Assert.Throws<ArgumentException>(() => QualityRefundCase.Report(order, result, Guid.NewGuid(),
            [new ReportedPoleIssue("P-12", "FALSE_POSITIVE", "Sai"), new ReportedPoleIssue("p-12", "WRONG_POLE", "Sai trụ")],
            "Báo sai", null, "Ngân hàng A", "1234567890", "NGUYEN VAN A", Now.AddHours(13)));
        Assert.Throws<ArgumentException>(() => QualityRefundCase.Report(order, result, Guid.NewGuid(),
            [new ReportedPoleIssue("P-12", "FALSE_POSITIVE", "Sai")],
            "Báo sai", null, "Ngân hàng A", "12AB", "NGUYEN VAN A", Now.AddHours(13)));
    }

    [Fact]
    public void RejectedReportKeepsPublishedResultCurrent()
    {
        var (order, result) = PublishedCompletedOrder();
        var refundCase = QualityRefundCase.Report(order, result, Guid.NewGuid(),
            [new ReportedPoleIssue("P-12", "WRONG_POLE", "Kết quả thuộc trụ khác")],
            "Báo sai trụ", null, "Ngân hàng A", "1234567890", "NGUYEN VAN A", Now.AddHours(13));

        refundCase.VerifyByManager(Guid.NewGuid(),
            [new VerifiedPoleIssue("P-12", false, "Kết quả đúng trụ thực tế")],
            "Đã đối chiếu mã trụ", Now.AddHours(13).AddMinutes(30), refundCase.Version);
        Assert.Throws<InvalidOperationException>(() => refundCase.Approve(
            Guid.NewGuid(), "Duyệt sai", 1_600_000m, Now.AddHours(14)));
        refundCase.Reject(Guid.NewGuid(), "Đã đối chiếu, trụ trong kết quả đúng", Now.AddHours(14));

        Assert.Equal(QualityRefundStatus.Rejected, refundCase.Status);
        Assert.Null(result.InvalidatedAt);
        Assert.Throws<InvalidOperationException>(() => refundCase.Approve(
            Guid.NewGuid(), "Duyệt lại", 1_600_000m, Now.AddHours(15)));
    }

    [Fact]
    public void ManagerMustVerifyEveryReportedPoleExactlyOnce()
    {
        var (order, result) = PublishedCompletedOrder();
        var refundCase = QualityRefundCase.Report(order, result, Guid.NewGuid(),
            [new ReportedPoleIssue("P-12", "FALSE_POSITIVE", "Cây khỏe"),
             new ReportedPoleIssue("P-13", "WRONG_POLE", "Sai trụ")],
            "Báo sai", null, "Ngân hàng A", "1234567890", "NGUYEN VAN A", Now.AddHours(13));

        Assert.Throws<ArgumentException>(() => refundCase.VerifyByManager(Guid.NewGuid(),
            [new VerifiedPoleIssue("P-12", true, "Đã kiểm tra")],
            "Thiếu một trụ", Now.AddHours(14), refundCase.Version));
        Assert.Throws<ArgumentException>(() => refundCase.VerifyByManager(Guid.NewGuid(),
            [new VerifiedPoleIssue("P-12", true, "Đã kiểm tra"),
             new VerifiedPoleIssue("P-12", false, "Trùng trụ")],
            "Trùng trụ", Now.AddHours(14), refundCase.Version));
        Assert.Throws<InvalidOperationException>(() => refundCase.Reject(
            Guid.NewGuid(), "Chưa xác minh", Now.AddHours(14)));

        refundCase.VerifyByManager(Guid.NewGuid(),
            [new VerifiedPoleIssue("P-12", true, "Xác nhận cây khỏe"),
             new VerifiedPoleIssue("P-13", false, "Mã trụ khớp")],
            "Đã kiểm tra đủ hai trụ", Now.AddHours(14), refundCase.Version);
        Assert.NotNull(refundCase.ManagerVerifiedAt);
        Assert.Throws<InvalidOperationException>(() => refundCase.VerifyByManager(Guid.NewGuid(),
            [new VerifiedPoleIssue("P-12", true, "Kiểm tra lại"),
             new VerifiedPoleIssue("P-13", false, "Kiểm tra lại")],
            "Không được xác minh lại", Now.AddHours(15), refundCase.Version));
    }

    private static (SurveyOrder Order, SurveyResult Result) PublishedCompletedOrder()
    {
        var order = SurveyOrder.Create($"SO-{Guid.NewGuid():N}"[..20], Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true, null, "test.v1",
            "NO_PREVIOUS_PUBLISHED_ORDER", Now);
        var managerId = Guid.NewGuid();
        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 5_000m, Now.AddMinutes(5));
        order.StartBaselineMapping(Now.AddHours(1));
        order.MarkBaselineCandidatesReady(Now.AddHours(2));
        order.SetFarmBoundaryFromOrthophoto(Guid.NewGuid(), managerId, Now.AddHours(3));
        order.RecordBaselinePublication(Guid.NewGuid(), ConfirmedSurveyPoleCount.Create(80), managerId, Now.AddHours(4));
        order.ConfirmPricing(Guid.NewGuid(), PricePerPole.Create(20_000m), CurrencyCode.Vnd, managerId, Now.AddHours(5));
        order.MarkPaidAppointmentConfirmed(Now.AddHours(6));
        order.MarkPaymentConfirmed(Now.AddHours(7));
        order.StartPaidService(Now.AddHours(8));
        order.MarkAnalysisReady(Now.AddHours(9));
        order.Complete(Now.AddHours(10));

        using var provenance = JsonDocument.Parse("{}");
        var result = SurveyResult.CreateForManagerReview(order.Id, order.TenantId,
            order.FarmId, SurveyServiceType.PlantHealth, provenance, Now.AddHours(11));
        result.Approve(managerId, Now.AddHours(12));
        result.Publish(managerId, Now.AddHours(12).AddMinutes(1));
        return (order, result);
    }
}
