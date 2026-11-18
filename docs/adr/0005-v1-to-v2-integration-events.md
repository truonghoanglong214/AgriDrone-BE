# ADR-0005: Tiến hóa integration event từ V1 sang V2

> **V3 scope notice — 2026-11-10.**
> Historical ADR: accepted/superseded status below records its original checkpoint. ADR 0018 partially supersedes service, funding, publication, comparison and architecture semantics; unchanged isolation, history and immutable wire guarantees remain applicable.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](0018-v3-paid-map-combo-business-policy.md), [target contracts](../contracts/survey-business-v3.md). Documentation-only update; no runtime change.

- Status: Accepted
- Date: 2026-09-22
- Decision owners: Backend 1, Backend 2

> Extended by ADR-0011. V1/V2 remain immutable; the rebaseline capabilities use independent V3 contracts.

## Context

Mapping V1 đã phát hành nhưng thiếu SurveyOrder, service và Farm base-map semantics. Sửa payload V1 tại chỗ có thể làm consumer cũ deserialize sai hoặc thay đổi ý nghĩa của event đã lưu trong Outbox/Inbox/DLQ.

## Decision

- V1 là immutable: không đổi event name, schema version hoặc ý nghĩa field đã phát hành.
- Phát hành event V2 với routing key/queue/consumer name riêng. V2 gồm `SurveyOrderId`, `FarmId`, base-map expectation/version và Zone slices cần thiết.
- Producer chuyển sang V2 trước; trong cửa sổ tương thích có thể dual-publish nếu có consumer V1 thực sự cần duy trì.
- Consumer V2 có Inbox/idempotency độc lập. Không dùng chung consumer name với V1.
- Theo dõi backlog Outbox, queue, retry và DLQ theo version. Chỉ dừng V1 sau khi producer V1 đã tắt, queue/retry/DLQ đã drain, retention window qua và replay test đạt.
- Event lịch sử, Inbox/Outbox record và migration cũ không bị rewrite/xóa.

## Consequences

Contract test/golden JSON phải tồn tại song song cho từng version. Removal V1 là bước contract có release note và rollback window, không phải refactor nội bộ.
