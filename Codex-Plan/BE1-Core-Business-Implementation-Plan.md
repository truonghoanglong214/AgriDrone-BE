# BE1 — V3 Business Rebaseline và .NET Implementation Plan

**Cập nhật 2026-11-25. Trọng tâm hiện tại là V3-06.** Backlog dưới đây theo code trong working tree `LongTH`. V3-00/01 đã chốt tài liệu và thiết kế. V3-02–05 đã có implementation. V3-09 đã có API báo cáo và Excel. Nghiệm thu runtime tập trung tại V3-10.

[Business Rules V3](Business-Rules-v3.md) · [Use cases BE1](BE1-Use-Case-Specification.md) · [Use cases BE2](BE2-Use-Case-Specification.md) · [Acceptance matrix](../docs/testing/v3-business-acceptance.md)

<details>
<summary>Lịch sử cập nhật trước checkpoint hiện tại</summary>

| Phiên bản | Ngày cập nhật | Người thực hiện | Nội dung sửa đổi chính & Lý do điều chỉnh |
|---|---|---|---|
| **v3.0** | 2026-11-10 | Product Owner / Tech Lead | Phê duyệt V3 Business Rebaseline: 2 gói thương mại MAP/COMBO, cọc chung trước bay, số dư thanh toán trước release, Follow-up trả phí trên Baseline bàn giao, 1 Baseline duy nhất, Manager Mobile online-only. |
| **v3.1** | 2026-11-10 | System Architect | Khóa chặt hai nguyên tắc kiến trúc bắt buộc: (1) **Zero-Hardcoding Invariant**: Tuyệt đối không hardcode bất kỳ tham số tiền tệ, đơn giá, cọc, ngưỡng dung sai không gian, hạn khiếu nại hay version chính sách trong mã nguồn; tất cả phải là cấu hình động có phiên bản (`Versioned Configuration`) và được snapshot nguyên tử vào giao dịch; (2) **Full Version Lineage & Immutable History Invariant**: Mọi chỉnh sửa dữ liệu và trạng thái quan trọng (cấu hình, trạng thái đơn, quyết định duyệt, bản đồ, định danh cây, số trụ, giám định bệnh, khiếu nại, hoàn tiền) bắt buộc lưu vết lịch sử bất biến với người sửa, thời điểm, lý do, bằng chứng và liên kết phiên bản tiền nhiệm (`PredecessorVersionId`), cấm ghi đè hoặc xóa cứng. |
| **v3.2** | 2026-11-10 | System Architect | Chuẩn hóa mô hình Quản lý Lượt bay (CaptureAttempt / FlightAttempt Lifecycle): Cấm triệt để việc ghi đè hoặc xóa trắng dữ liệu chuyến bay cũ (`RecoverFailedFlight`). Mọi lần bay lại (Re-flight) bắt buộc sinh `CaptureAttempt` mới (1-N với Order), bảo toàn nguyên vẹn raw evidence, telemetry, checklist và media của lần bay cũ. Phân lập media theo attempt để bảo vệ AI/Metashape pipeline; bay lại chất lượng cùng Order không phát sinh thêm chi phí. |
| **v3.3** | 2026-11-11 | Thiết kế theo ủy quyền chủ dự án | Chốt V3-01: model/policy snapshot cadence/lineage, input approval khác terminal Accepted, additive v3 HTTP/event contracts và legacy disposition. Lập V3-02A–F với file manifest/DB-concurrency acceptance. Chỉ tài liệu, chưa implementation hoặc live DB inventory. |
| **v3.4** | 2026-11-13 | Lập kế hoạch theo yêu cầu chủ dự án | Lập V3-03 A–H/19 work items: all-Order deposit, appointment, atomic capture start, new attempt/Mission cho quality Re-flight, attempt-scoped input approval và durable cancellation obligations. Cập nhật đúng V3-02 DB acceptance pending; giữ Follow-up release-proof unavailable và public activation off. Không sửa code trong phiên lập kế hoạch này. |
| **v3.5** | 2026-11-13 | Thực hiện 03-00 theo yêu cầu chủ dự án | Hoàn thành execution baseline và ADR 0020: W01–W26 writer inventory, additive Order/attempt states, canonical Mission binding, total lock order, funding/cancellation IDs và exact next-slice manifest. Chỉ source/design acceptance; không chạy code/DB tests hoặc thay backend. |
| **v3.6** | 2026-11-14 | Cập nhật implementation checkpoint | Ghi nhận 03A-1 domain remediation verified và 03A-2 operational/funding/cancellation contracts cùng pinned-cancellation remediation đã áp dụng. Đồng bộ next step thành 03B-1 persistence/expand migration; giữ PostgreSQL acceptance pending và public activation off. |
| **v3.7** | 2026-11-14 | Thực hiện 03B-1 theo kế hoạch | Thêm EF mappings và expand migration cho CaptureAttempt/history/manifest/approval, canonical Mission binding, same-owner/lineage/V3-scope constraints; nghiệm thu fresh và populated-Legacy PostgreSQL, rollback khi expand artifacts rỗng và negative Legacy insert. Bước kế tiếp là 03B-2; không đổi các V3-02 race gates hay public activation. |
| **v3.8** | 2026-11-15 | Hoàn tất source implementation V3-03 | Thêm funding/cancellation persistence, deposit callback/allocation, V3 appointments, atomic capture coordinator, manifest/input approval, quality Re-flight, cancellation liabilities, V3 APIs, audit/Outbox và old-route guards. Build, Unit 937/937, Architecture 87/87, Integration 39/39 và model-drift check pass. 03H-2 chưa ký vì race/fault/object-storage evidence cùng V3-02 DB prerequisite còn mở; public activation off. |

</details>

## 1. Kết quả cần đạt

Hóa đơn V3 đã chốt → đối soát và thu số dư → tự động phát hành và thông báo → Owner xem kết quả → claim/refund/correction → đối soát báo cáo tháng và Excel.

## 2. Hai nguyên tắc kiến trúc bắt buộc (Architectural Invariants)

- **Chính sách có phiên bản.** Approval pin đơn giá, cọc, gói, claim và cancellation. Capture/job pin chính sách vận hành và không gian. Invoice, settlement, release và refund dùng dữ kiện đã pin. Hạn claim tính từ ngày thông báo thực tế và policy snapshot. [Snapshot cadence](../docs/domain/v3-01-locked-design.md)
- **Lịch sử bất biến.** Change facts, approval, invoice, receipt, refund và publication có audit và lineage. First tạo Baseline, Follow-up giữ Baseline nguồn và có snapshot riêng, re-flight tạo attempt/mission mới cùng Order. Correction sau phát hành giữ invoice và bút toán gốc. Các con trỏ hiện hành cập nhật nguyên tử cùng history. [Business Rules V3](Business-Rules-v3.md)
- **Transaction và tương thích.** Dùng thứ tự khóa ADR 0020, receipt/idempotency và scope V3 rõ ràng. Provider, FastAPI, broker và bank I/O xử lý ngoài SQL transaction. Dùng migration tiếp nối, giữ historical contracts và migration đã phát hành. [ADR 0020](../docs/adr/0020-v3-capture-execution-and-funding-boundaries.md)

## 4. Status và dependency roadmap

| Phase | Trạng thái source | Việc còn lại |
|---|---|---|
| V3-00–01 | Business rules, thiết kế và source audit đã chốt | Live inventory và migration disposition tại V3-10 |
| V3-02–05 | Đã có code catalogue, approval, cọc, capture/hủy, mapping, COMBO, invoice và API | Các gate PostgreSQL, broker, storage, COMBO runtime tại V3-10. Sửa cap hủy sau đảo tiền trong V3-06 |
| **Identity** | Backend refresh/logout và sửa sau review RT-03/04 đã build sạch | Nối frontend RT-06, kiểm cookie trên domain đích và nghiệm thu V3-10. Có thể làm song song với A2.2. [Checklist Identity](../docs/plans/v3-05e-to-v3-10-settlement-release-plan.md#identity--refresh-token-và-khôi-phục-phiên) |
| **V3-06** | Tái sử dụng invoice, funding và callback cọc | Checkout số dư, settlement, excess reservation, phát hành, notice/window và release proof |
| V3-07 | Có guard pre-release và các đường đọc nền | Nối release proof, Owner delivery/comparison và các khoảng trống Manager online |
| V3-08 | Có cancellation obligation và luồng `QualityRefundCase` hiện hữu | Claim theo publication, thực thi hoàn V3 và lineage withdrawal/replacement |
| V3-09 | Có API Admin, query tháng, lịch sử báo cáo và Excel | Nối facts chuẩn V3-06/08, đồng bộ cutoff và đối soát |
| V3-10 | Có kế hoạch và ma trận | Nghiệm thu PostgreSQL, tích hợp, demo và quyết định cutover |

- **Plan 1 · LongTH** sở hữu settlement, release, financial facts, refund execution và withdrawal/replacement. [Plan 1](../docs/plans/v3-05e-to-v3-10-settlement-release-plan.md)
- **Plan 2 · Người thứ hai** sở hữu Owner delivery, workflow claim và dashboard/Excel. Có thể chuẩn bị contract và fixture song song với V3-06. [Plan 2](../docs/plans/v3-07-to-v3-10-owner-claims-reporting-plan.md)

## 5. V3-01 — Policy model, contracts và migration design

Thiết kế và source audit đã chốt. Live inventory của Order, payment, map, job và queue thuộc V3-10. [Thiết kế đã chốt](../docs/domain/v3-01-locked-design.md) · [ADR 0019](../docs/adr/0019-v3-01-model-contract-and-migration-design.md)

## 6. V3-02 — Catalogue và atomic request approval

Catalogue, versioned price/deposit và approval snapshots đã có code. Còn các gate `02A-2`, `02D-3`, `02F-1` và evidence `02F-2` trên PostgreSQL tại V3-10. Follow-up production chờ proof V3-06. [Checklist V3-02](../docs/plans/v3-02-execution-checklist.md)

## 7. V3-03 — Capture, funding, Re-flight và cancellation

Appointment, thu cọc và phân bổ, capture/re-flight, hủy và tạo nghĩa vụ hoàn do hủy/service failure đã có code. Đưa phần nghiệm thu còn mở của `03H-2/3` vào V3-10. Sửa phép tính cap hủy sau reversal trong V3-06, rồi mới dùng chung cho excess. [Checklist V3-03](../docs/plans/v3-03-execution-checklist.md) · [A2.2](../docs/plans/v3-05e-to-v3-10-settlement-release-plan.md#a22--settlement-và-cap-hoàn-sau-reversal)

## 8. V3-04/05 — Mapping, identity, Combo và count/invoice

Đã có internal mapping, lineage trụ, COMBO pending/review, freeze, invoice/correction và API Manager/Owner. V3-05F đã có build 0 warning, 0 error tại checkpoint review. Còn nghiệm thu migration/runtime và xác minh COMBO model policy trên môi trường đích tại V3-10. [V3-04](../docs/plans/v3-04-execution-checklist.md) · [V3-05](../docs/plans/v3-05-combo-review-and-invoice-implementation-plan.md)

## 9. V3-06 — Atomic settlement/release

- [ ] Tính lại due từ funding facts hợp lệ. Dùng chung cap khả dụng cho settlement, excess reservation và hủy đơn, có xét reversal, refund đã hoàn và khoản đang giữ chỗ. `SurveyCaptureExecutionCoordinator` hiện tính cap hủy từ receipts trừ completed/refund reservations, cần bổ sung reversal đã xác minh.
- [ ] Tạo checkout `FinalBalance` gắn exact invoice revision. Nối xác nhận tiền, dedup và xử lý late receipt/stale checkout. Tái dùng callback cọc và dữ kiện hoàn do hủy đã có.
- [ ] Nối auto-release từ approval/invoice sẵn sàng và xác nhận tiền. Dưới khóa, recheck quyền, current approval/invoice/source, due = 0 và yêu cầu release còn hiệu lực. Commit publication/visibility, paid proof, notice/window, original service value, completion/history, audit và Outbox cùng nhau. `PublishedAt = UnlockedAt = NoticeCreatedAt`. Deadline đọc từ policy snapshot.
- [ ] Reserve excess một lần và bàn giao release proof cùng ledger facts. Xác nhận V3-09 đọc đúng nguồn original sale theo Order. [Checklist A2](../docs/plans/v3-05e-to-v3-10-settlement-release-plan.md#a2--v3-06--settlement-release-notice-và-ledger-facts)

## 10. V3-07 — Owner delivery và online Manager workflow

- [ ] Nối release proof V3 vào các đường đọc đã có. Áp ownership, exact publication và withdrawal trước projection, cache và ký URL. Giữ lịch sử hợp lệ khi một Order mới đang pending.
- [ ] Hoàn thiện phần thiếu của Owner timeline/comparison/report/media V3 và kiểm phần còn thiếu của Manager online notes/checklist/upload retry/resume. Dữ liệu MAP dùng `NotAssessed`. FE/mobile đọc giá, due, status và deadline do server cấp. [Checklist B1](../docs/plans/v3-07-to-v3-10-owner-claims-reporting-plan.md#b1--v3-07-owner-đọc-kết-quả-và-manager-online)

## 11. V3-08 — Claim/refund/correction

- [ ] Plan 2 triển khai claim theo exact publication/window, Manager xác minh evidence, Admin đề xuất có version và Owner chấp nhận đúng proposal. Claim gửi kịp hạn tiếp tục xử lý sau deadline.
- [ ] Plan 1 nối `SurveyRefundObligation` V3 với bằng chứng và completion chuyển khoản. Reuse obligation hủy/service failure và reserve tiền thừa sẵn có khi luồng tương ứng đã commit. Bổ sung refund khiếu nại, cap dùng chung, retry theo bank reference và audit. Tái dùng luồng upload chứng từ hiện có sau khi kiểm quyền và scope.
- [ ] Phát hành withdrawal/replacement bất biến. Substantive correction tạo revision, notice/window mới. Cosmetic giữ window. Giữ doanh thu gốc, lịch sử và claim đã nhận. [Plan 1 A3](../docs/plans/v3-05e-to-v3-10-settlement-release-plan.md#a3--v3-08-thực-thi-refund-và-phát-hành-correction) · [Plan 2 B2](../docs/plans/v3-07-to-v3-10-owner-claims-reporting-plan.md#b2--v3-08-claims-workflow)

## 12. V3-09 — Financial reporting

- [ ] Nối release/refund facts V3 vào `MonthlyFinancialReportQuery` và `FinancialReportCalculator` hiện có. Bổ sung lineage còn thiếu trong history, nhất là invoice/payment/receipt, cash reversal và trạng thái đối soát. Giữ một original sale theo Order và số liệu Legacy có provenance.
- [ ] Dùng chung cutoff/as-of có thể lặp lại cho API tháng và Excel. Đối soát doanh thu dịch vụ, tiền thu/hoàn, cọc, reserve excess, retention và số còn phải thu theo BR-FIN-01–05. Xác minh số và fingerprint của dashboard/Excel với cùng bộ lọc và cutoff. [Checklist B3](../docs/plans/v3-07-to-v3-10-owner-claims-reporting-plan.md#b3--v3-09-nối-facts-v3-vào-báo-cáo-hiện-có)

## 14. V3-10 — Acceptance, migration và release

- [ ] Chạy [acceptance matrix](../docs/testing/v3-business-acceptance.md) theo [traceability](../docs/domain/v3-traceability-matrix.md). Nghiệm thu các gate PostgreSQL V3-02/03 còn mở, COMBO policy/coverage, mapping/invoice/correction, migration tương thích, lock races, rollback, storage và broker.
- [ ] Nghiệm thu release/callback, quyền đọc và withdrawal/cache/URL, deadline, refund transfer/reconciliation, dashboard–Excel. Demo First MAP/COMBO → Follow-up → re-flight → excess/claim refund → correction → báo cáo tháng.
- [ ] Nghiệm thu duy trì phiên frontend qua reload và JWT hết hạn, rotation đồng thời, logout/thu hồi và quyền tenant trên cấu hình domain đích. Dùng V3-T44–T48 trong ma trận nghiệm thu.
- [ ] Thực hiện inventory/preflight, xác minh dữ liệu có nguồn, trình tự migration, cutover, replay/retention và rollback/roll-forward. Giữ `PublicIntakeEnabled = false` và `SurveyPayments:Enabled = false` đến khi có quyết định cutover dựa trên bằng chứng.
- [ ] Hoàn tất RQ1 GNSS và GNSS+grid/ID switches, RQ2 metrics theo nhãn, split theo nguồn bay trước augmentation, ba seed và đo pilot p95 read ≤2s, review ≤30 phút, pole count error ≤10%, macro-F1 ≥0,70 trên dataset/hardware đã công bố.

Trong phase code dùng `dotnet build backend/AgriDrone.sln --no-restore --nologo`. Integration suite và nghiệm thu tập trung ở V3-10. [Master Plan Phase Tracker](../PROJECT_MEMORY.md#2-master-plan-phase-tracker-be1)
