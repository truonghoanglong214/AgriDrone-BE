# ADR 0018 — Paid Map/Combo, settlement/release và processing boundary

- Status: **Accepted business/architecture decision; not implemented by this documentation change**
- Date: 2026-11-10
- Decision owner: Chủ dự án, xác nhận bộ khuyến nghị trong phiên rà soát proposal v3.
- Canonical detail: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md).

Implementation refinement accepted 2026-11-11: [ADR 0019](0019-v3-01-model-contract-and-migration-design.md) và [V3-01 locked design](../domain/v3-01-locked-design.md) khóa model/config snapshot cadence/lineage/contracts/legacy handling; không thay commercial policy của ADR này.

## Context

Proposal v3 chuyển MVP từ Plant Health + prerequisite Baseline sang hai paid services Map/Combo, cọc trước capture và balance trước release. Tài liệu cũ có free Baseline, cọc chỉ cho unmapped Farm, paid-before-flight, offline mobile và Java/.NET-only assumptions mâu thuẫn nhau. Runtime hiện tại vẫn là baseline cũ; đợt sửa này không thay source/migrations/config.

## Decision

1. `MAP` và `COMBO` là service tokens target. Snapshot giá/cọc/version khi tạo Order; confirmed billable count từ evidence sau capture/review. Demo rates: 1.000/2.000 VND/trụ; chung cọc 100.000 VND. Không đưa số demo vào runtime trong lần sửa tài liệu.
2. First và Follow-up đều paid Order; Follow-up tái sử dụng Tenant/Farm và released Baseline. Re-flight chất lượng cùng Order, không phí dịch vụ thêm.
3. Cọc là commercial capture gate cho cả hai services; remaining balance là release gate, không preflight gate. Full operational safety/authorization vẫn áp dụng.
4. Baseline và profile ban đầu có thể Approved nội bộ trước settlement; chỉ release đúng approved revision khi đủ tiền. Publication, unlock, durable in-app notice, claim window, one-time service value và audit/Outbox atomic. PublishedAt=UnlockedAt=NoticeCreatedAt.
5. Refund tiền thừa không chặn result release nếu review đạt và Due=0. Trước capture hủy hoàn đủ; sau capture đạt, khách đổi ý giữ tối đa cọc và hoàn excess; service failure không bàn giao hoàn đủ. Cancellation retention báo cáo riêng.
6. Claim theo lịch Việt Nam: notice ngày D, nhận đến trước 00:00 D+8 UTC+7. Substantive replacement có window mới theo revision; cosmetic edit/notification retry không reset. Refund có cap toàn Order, concurrent reservation và bank proof.
7. Tách published service value/net service revenue với cash. Overpayment/cancellation returns không từng ghi service value chỉ giảm cash; complaint refund hoàn service value giảm cả net revenue và cash theo completion date. Không ghi sale lần hai khi corrected publication.
8. .NET modular monolith sở hữu transactional business state; FastAPI là ảnh/AI processing service. BE1/BE2 dùng internal typed ports trong .NET, FastAPI dùng authenticated/versioned processing API. SePay giữ làm gateway MVP.
9. Manager Mobile online-only; upload resume/retry, không offline checklist cache hoặc mutation sync engine.
10. Proposal v3 không là event schema V3. Contracts/snapshots đã phát hành bất biến; nếu service/context semantics khác thì thiết kế revision mới, không phát `COMBO` dưới legacy payload/validator chỉ hiểu PlantHealth.

## Supersession map

| Tài liệu cũ | Giữ | Supersede cho target v3 |
|---|---|---|
| ADR 0001 | Actor isolation, assignments | Mọi service/readiness assumptions cũ nếu khác rules v3 |
| ADR 0002 | Farm scope, version history | Không dùng một current published map cho cả Baseline và mọi Follow-up snapshot |
| ADR 0003/0007 | VND/decimal, per-pole, audit | Giá snapshot lúc tạo Order; fixed demo config; verified count sau review; bỏ free baseline/full-paid flight |
| ADR 0004 | Authenticated provider evidence, dedup | Deposit-before-capture và balance-before-release, không full-paid gate trước service flight |
| ADR 0005/0011 | Versioned messages, compatibility/drain | Service/context target phải review contract mới |
| ADR 0006 | Atomic transactions, audit/Outbox | Thêm atomic release/in-app notice/service-value và refund reservation boundaries |
| ADR 0008 | Versioned boundary, original geometry | Billable candidates phải trong boundary; first capture không cần boundary đã tạo từ chính ảnh tương lai |
| ADR 0009 | Review zone/evidence | Severity/expert advice legacy không trở lại MVP |
| ADR 0010 (hai file)/0012 | Team labels, cited guidance, lifecycle history | Lifecycle changes sau Baseline cần later-survey evidence; Map-only neutral; scope Combo only cho disease |
| ADR 0013–0015 | Pending ingestion, strict provenance/review/frozen approval | First Combo được xử lý từ approved internal map; không yêu cầu Owner-published/paid-before-disease capture |
| ADR 0016–0017 | Atomic publish, immutable provenance/idempotency | Approval không tự complete Order/release; target publication phải settlement-gated, notice atomic, comparison basis tách mapping/health |

Các ADR cũ giữ lại làm lịch sử, không sửa wire facts hoặc bằng chứng đã ghi. [Traceability](../domain/v3-traceability-matrix.md) và [implementation plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md) xác định remediation cần triển khai.

## Consequences

- Bổ sung policy versions và classifications thay vì tự đổi nghĩa legacy PLANT_HEALTH/payment purposes/money snapshots.
- Active Orders cũ cần inventory/disposition và quyết định explicit migration, không giả định mọi legacy Order là COMBO hoặc gán fake paid/released evidence.
- Existing V4 review implementation là reusable baseline; current manual publish/completion và async-only notice không chứng nhận target release policy.
- Runtime release chỉ sau contract, DB/concurrency, authorization/media access và financial reconciliation acceptance. Documentation-complete không đồng nghĩa feature-complete.
