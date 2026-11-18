# AgriDrone — Glossary hiện hành v3

Status: **Approved, 2026-11-10**. Source: [proposal v3](../../Codex-Plan/Be-Plan-v3.md), [Business Rules](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](../adr/0018-v3-paid-map-combo-business-policy.md).

| Thuật ngữ | Nghĩa chuẩn |
|---|---|
| Public Applicant | Gửi first-service request/theo dõi tracking code, không tự provision Tenant/Farm. |
| SystemAdmin | Duyệt onboarding, cấu hình giá/cọc/staff/drone, refund proposals/bank proof, financial dashboard/export. |
| SystemManager | Nhân sự AgriDrone assigned Farm; field work, review identity/mapping/disease, xác minh claims/corrections. |
| TenantOwner | Chủ Tenant/Farm, book/pay, xem released history, báo plant changes, claim/accept refund proposal. |
| MAP | Paid service mapping, pole count/positions. Demo 1.000 VND/trụ. |
| COMBO | MAP + 2–3 team-labelled suspected diseases, Disease Zones, cited guidance; không severity/diagnosis. Demo 2.000 VND/trụ. |
| First survey | Paid Order đầu tiên của Farm, tạo approved Baseline/profiles ban đầu; không free prerequisite service. |
| Follow-up | Paid Order mới, cùng Tenant/Farm có Baseline đã bàn giao, chọn MAP/COMBO và snapshot giá/cọc mới. |
| Quality Re-flight | Capture/Mission bổ sung cùng Order vì ảnh không đạt; không phí/cọc thêm, không Follow-up. |
| Deposit | Cọc cố định chung snapshot cho mọi Order, trước capture và credit một lần. Demo 100.000 VND, chưa sửa runtime. |
| PricePerPoleSnapshot | Giá theo service/version tại lúc tạo Order; catalogue đổi không sửa lịch sử. |
| VerifiedBillablePoleCount | Trụ có evidence đã review trong approved boundary của Order; khác estimated count/current active inventory. |
| Total | VerifiedBillablePoleCount × snapshotted rate; không minimum charge ngầm bằng cọc. |
| RemainingDue | Trước release: max(Total − confirmed receipts + completed refunds, 0). Complaint refund sau release không tạo lại balance. |
| Overpayment | Actual net received trước release vượt Total; refund obligation, không service revenue, không cần claim. |
| FarmBoundary | Farm polygon versioned; near/outside candidates NeedsReview; outside không billable. First capture có thể cung cấp imagery tạo boundary. |
| Baseline | Map tham chiếu đầu tiên approved từ paid Order, linked source Order/Mission/version; chỉ correction có review mới thay current pointer. |
| Approved internal map | Mapping được dùng cho pipeline nội bộ trước settlement, không tự mở Owner visibility. |
| Survey map snapshot | Output riêng từng Order, tham chiếu Baseline; Follow-up không overwrite Baseline. |
| Digital Plant Profile | Persistent ID cho verified physical pole, tối đa một active profile/pole, append-only observations/history. |
| Plant lifecycle change | Sau Baseline cần later-survey evidence + Manager approval cho new/retire/replace; missing một lần không retire, relocation giữ ID. |
| NeedsReview | Uncertain boundary/match/new/missing/finding; không tự biến thành inventory hoặc billing decision. |
| Disease Zone | Reviewed suspected-disease polygon từ Combo; khác operational Farm Zone. |
| Sourced Guidance | Cited/versioned reference advice, không severity-based prescription hoặc free-form AI advice. |
| NotAssessed | Không screening bệnh ở observation, điển hình MAP; không phải Healthy hoặc clear disease history. |
| ApprovedAt | Manager duyệt immutable revision, chưa đồng nghĩa Owner xem được. |
| PublishedAt / UnlockedAt | Review đạt và Due=0 thì publication/unlock/durable in-app notice cùng transaction, cùng timestamp trong MVP. |
| Claim deadline | Notice local date D; deadlineExclusive=00:00 D+8 UTC+7. Nhận hết D+7, không cộng 168 giờ, không tính từ lần đọc/email delivery. |
| ComplaintRefund | Admin proposal + Owner acceptance cho error của released service; bank proof/audit. |
| CancellationReturn | Trả tiền đơn hủy/chưa bàn giao; không service revenue refund nếu chưa publication. |
| CancellationRetention | Cọc giữ khi khách đổi ý sau capture đạt; nhóm báo cáo riêng, không published service value/active deposit liability. |
| Refund reservation | Completed + reserved refund amounts không vượt receipts toàn Order; retry bank transfer phải đối soát. |
| Substantive correction | Reviewed replacement thay nội dung thực chất, notice/window mới theo revision, giữ old version; không phí/sale lần hai. |
| Cosmetic edit | Đổi trình bày không đổi kết quả, không reset claim window. |
| Net service revenue | Published service value − completed service-value refunds; tiền trả thừa/cọc chưa ghi revenue chỉ ảnh hưởng cash. |
| Net cash | Actual receipts − tất cả completed cash refunds, theo ngày transaction/completion. |
| Online-only Manager Mobile | Checklist/notes/uploads cần mạng, retry/resume an toàn; không offline cache hoặc mutation queue. |
| Policy v3 / wire V3,V4 | Business version khác event schema version. Released payload/enum không đổi tại chỗ. |
| Request quote snapshot | Indicative configuration at submit; khác immutable commercial snapshot được resolve tại Order approval. |
| Commercial policy snapshot | Price/deposit/service/claim/cancellation version IDs và resolved values/fingerprint pin cùng Order creation transaction; không re-policy ở invoice/release/refund. |
| VersionNumber / predecessor | Explicit business version chain, không dùng xmin/concurrency token làm immutable revision identity. |
| CaptureInputApproval | Audited acceptance of uploaded input manifest before processing; khác terminal Accepted state sau processing/review. |
| Current projection | State/pointer cập nhật atomic cùng append-only history, không overwrite evidence; không đòi full event sourcing. |
| V3-01 DesignLocked | Model/contracts/disposition đã chốt theo ADR 0019, source audit hoàn tất; chưa live DB inventory/runtime implementation. |

Legacy `PLANT_HEALTH`, `HARVEST_READINESS`, free Baseline, baseline-only deposit và full-paid-before-flight mô tả compatibility tại ngày artifact, không phải target v3. Giữ audit/history, không gán lại fake service/payment evidence cho dữ liệu cũ.
