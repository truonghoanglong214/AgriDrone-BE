# Plan 2 — Owner delivery, Claims và Financial reporting V3

**Chủ trì:** Người thứ hai. Phạm vi gồm **toàn bộ V3-07, workflow khiếu nại V3-08 và toàn bộ V3-09 Financial reporting**, cùng nghiệm thu phần mình ở V3-10.

[Master plan BE1](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md) · [Business Rules V3](../../Codex-Plan/Business-Rules-v3.md) · [Plan 1 — Settlement, Release và Refund](v3-05e-to-v3-10-settlement-release-plan.md)

## B1 — V3-07: Owner đọc kết quả và Manager online

- [ ] Cùng frontend nối bootstrap phiên và gia hạn tập trung khi API Identity sẵn sàng. Xác minh giữ đúng tenant và không lặp mutation khi JWT hết hạn giữa luồng Owner hoặc Manager. Plan 1 bàn giao backend, Plan 2 kiểm luồng màn hình dùng API đã refresh. [Checklist Identity](v3-05e-to-v3-10-settlement-release-plan.md#identity--refresh-token-và-khôi-phục-phiên) · [Handoff FE](../operations/v3-fe-mobile-handoff.md#9-khôi-phục-phiên-đăng-nhập)
- [ ] Dùng inventory từ V3-05F bên dưới. Bổ sung các projection/caches còn thiếu như tiles, latest, report/export và media URLs. Chốt scope, exact publication và cache invalidation trước khi nối release proof thật.
- [ ] Mở rộng guard pre-release đã có bằng release proof từ Plan 1. Áp ở trước materialization, cache access và URL signing. Chặn pending/withdrawn theo exact publication, giữ quyền xem lịch sử đã phát hành hợp lệ và claim context đã có.
- [ ] Comparison giữ mapping/COMBO basis, baseline/revision và reason, biểu diễn MAP bằng NotAssessed. Rà và hoàn thiện chỗ còn thiếu của Manager online checklist/notes/upload retry/resume trên các API capture/media hiện có, recheck assignment/version. FE/mobile dùng status/amount/deadline server trả, công bố route sau khi đã nối runtime.

**Xong khi:** inventory bao phủ mọi surface và có ca sai Owner/tenant, chưa publish, history hợp lệ, withdrawal, cache/signed URL và upload gián đoạn.

## B2 — V3-08: claims workflow

- [ ] **Làm trước:** thiết kế domain/commands/DTO và decision history cho mở claim, Manager xác minh evidence, Admin đề xuất, Owner chấp nhận exact proposal version. Chuẩn bị fixture notice/deadline và storage authorization/audit.
- [ ] **Sau release proof thật:** dùng exact release revision/window, server ReceiveAt và DeadlineExclusive theo ClaimPolicyVersion. Claim kịp hạn vẫn xử lý tiếp sau expiry. Giữ claim/evidence audit, retention và kiểm proposal đổi hoặc replay.
- [ ] Bàn giao accepted claim/proposal ID/version, Order/released revision, actor/scope, evidence và proposed remedy cho Plan 1. Hiển thị trạng thái refund/correction từ receipt phía Plan 1. Plan 1 ghi reservation/transfer/completion và publication/withdrawal; Plan 2 giữ claim state, proposal, evidence và giao diện đọc.

**Xong khi:** trace Owner mở claim → Manager/Admin xử lý → Owner chấp nhận → receipt refund/correction. Chứng minh boundary deadline và stale proposal.

## B3 — V3-09: Nối facts V3 vào báo cáo hiện có

**Đã có trong backend:** `V3SystemAdminRevenueReportsController` có GET tháng và export, filter MAP/COMBO, Admin authorization và audit export. `MonthlyFinancialReportQuery`, `FinancialReportCalculator`, history migration và `RevenueWorkbook` là nền triển khai.

- [ ] Dùng field contract đã chốt với Plan 1. Rà các trường history còn thiếu so với facts A2/A3, gồm exact publication/invoice/receipt linkage, `cash_amount`, `funding_receipt_id`, reconciliation status, liability và refund effective/completed time. Nối original service-value fact và refund completion V3 vào query hiện có. Giữ outcome đối soát riêng cho dữ kiện thiếu provenance.
- [ ] Đồng bộ cutoff/as-of có thể lặp lại giữa GET tháng và export. Hiện hai request tự lấy cutoff theo thời điểm gọi. Kiểm hiệu lực UTC+7 `[start, nextMonth)`, phân biệt event time và recorded time, cùng filters/formula version/fingerprint. Duy trì audit và text an toàn trong Excel.
- [ ] Đối soát với fixtures V3 của Plan 1. Bao phủ một sale/Order, cọc, reversal, excess reserve, refund hoàn tất theo lý do, retention, due và Legacy chưa phân loại. Complaint refund chỉ trừ service value khi đủ căn cứ. Correction giữ một original sale.

**Xong khi:** số và fingerprint dashboard/Excel khớp cho cùng bộ lọc và as-of, có fixtures ranh giới tháng và thu/hoàn xuyên tháng.

## B4 — V3-10: nghiệm thu phần đọc, claims và báo cáo

- [ ] Chủ trì ca isolation và signed URLs, withdrawal/cache, deadline boundary, proposal concurrency và dashboard/Excel reconciliation. Chạy chung demo với Plan 1 và ghi kết quả vào [acceptance matrix](../testing/v3-business-acceptance.md).
- [ ] Trong phase code dùng build chuẩn theo quy định repo. Nghiệm thu tích hợp tập trung V3-10. Báo cáo rõ adapter nào đã nối source thật.
- [ ] Demo Owner/Manager mở lại ứng dụng và tiếp tục phiên khi access token đã hết hạn. Kiểm lỗi mạng, nhiều request cùng chờ, logout và phiên bị thu hồi theo V3-T44–T48.

## Ba việc có thể nhận ngay

1. **V3-07:** Nối release-proof contract vào các đường Owner đọc từ inventory đã có và rà projection/cache còn thiếu.
2. **V3-09:** Đối chiếu fields của query/Excel hiện có với facts A2/A3 và chuẩn bị fixture cutoff, cash reversal, refund.
3. **Workflow V3-08:** Chốt claim revision/window và accepted-proposal handoff. LongTH review các trường cần cho refund/correction, sau đó mỗi người triển khai phần mình.

Dùng nhánh riêng từ checkpoint đã thống nhất. Plan 1 điều phối migration dùng chung và shared ModelSnapshot. Khi Plan 2 cần migration riêng cho claims/reporting, hai người chốt thứ tự migration trước khi merge để giữ một chain.

## Inventory bề mặt đọc Owner & Guard bàn giao từ V3-05F (A1 → B1)

| Bề mặt / Controller | Handler / Adapter | Trạng thái guard hiện tại (V3-05F) | Yêu cầu bổ sung tại V3-06 / V3-07 |
|---|---|---|---|
| `GET /api/v3/tenant-owner/survey-orders/{orderId}/invoice` | `GetTenantOwnerSurveyOrderInvoiceQuery` -> `SurveyInvoiceQueries` | Allowlist tối giản (`OrderId`, `OrderNumber`, `OrderStatus`, `InvoiceRevisionNumber`, `VerifiedBillablePoleCount`, `PricePerPole`, `Currency`, `TotalAmount`, `DueAmount`, `FinalizedAt`); trả `404` khi chưa finalized hoặc khác tenant | Nối status sau settlement/withdrawal |
| `GET /api/tenant-owner/survey-orders*` | `SystemManagerSurveyOrderQueries` | Lọc cứng `order.Scope == ContractScope.Legacy` (trả `404`/rỗng cho V3) | Bổ sung V3 Owner order summary read sau V3-06 release proof |
| `GET /api/farms/{farmId}/base-maps*` (`list`, `detail`, `compare`, `analytics`) | `FarmBaseMapReadAccess` + `IFarmBaseMapPreviewReader.CanOwnerReadAsync` | `list` & `detail` đã chặn; `compare` & `analytics` đã bổ sung cùng guard trong `V3-05F` (từ chối V3 chưa release / khác tenant) | Thay `CanOwnerReadAsync` từ `Legacy`-only sang release-proof V3 cho `PUBLISHED`/`Withdrawn` + signed URL TTL <= 15m |
| `GET /api/farms/{farmId}/plant-health-map` | `GetFarmPlantHealthMapQueryHandler` | Đã chặn fallback `InternalApproved`/`Draft`, loại trừ `plant.plants` & `mission_plant_observations` thuộc đơn `V3` | Chuyển policy controller khỏi `[AllowAnonymous]`, bắt buộc `TenantOwner`/membership + release proof V3-07 |
| `GET /api/farms/{farmId}/plants/{plantId}/lifecycle` | `GetPlantLifecycleHistoryQueryHandler` (`Plants` module) | Đã kiểm farm access. Tenant chưa thấy lifecycle V3, gồm trụ Legacy bị V3 cập nhật hoặc thay thế. Manager đúng assignment vẫn đọc nội bộ | Nối release proof và lineage đã phát hành tại B1 (`V3-07`) trước materialization |
| `GET /api/missions/{missionId}/farms/{farmId}/media*` | `MissionMediaController` | Kiểm tra `SystemManager` hoặc media ACL; không gán `V3` release proof | Cổng Owner media / orthophoto signed URL (TTL + withdrawal revoke) tại `V3-07` |

**Agreed A1 Field & Scope Contracts (Plan 1 ↔ Plan 2):**
- *Release Proof Port (`V3-06 -> V3-07`)*: `OrderId`, `TenantId`, `FarmId`, `PublicationId`, `PublicationVersion`, `PredecessorPublicationId`, `IsReplacement`, `IsSubstantiveReplacement`, `IsWithdrawn`, `WithdrawnAt`, `ApprovedResultRevisionId`, `ApprovedResultFingerprint`, `InvoiceRevisionId`, `InvoiceFingerprint`, `ServiceCode`, `ScreensDisease`, `FarmBaseMapVersionId`, `BaselineMapVersionId`, `ComboReviewRevisionId`, `TotalAmount`, `Currency`, `PaidAt`, `PublishedAt`, `UnlockedAt`, `NoticeCreatedAt`, `ClaimWindowId`, `ClaimDeadlineExclusiveUtc`, `ClaimPolicyVersionId`.
- *Ledger Read Facts (`V3-06/V3-08 -> V3-09`)*: Phân tách bất biến giữa service recognition (`TotalAmount` 1 lần/Order, `ServiceCode` từ `SurveyOrderCommercialSnapshot`), `SurveyFundingReceipt` (`ConfirmedNetReceipts`), `SurveyFundingAllocation` (`ValidAllocatedDepositNet`), `SurveyFundingReversal`, và `SurveyRefundObligation` (phân loại `Overpayment`, `CancellationReturn`, `ServiceFailure`, `ComplaintRefund` và flag `ReturnsServiceValue`).
