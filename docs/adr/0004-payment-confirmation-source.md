# ADR-0004: Nguồn xác nhận thanh toán

> **V3 scope notice — 2026-11-10.**
> Historical ADR: accepted/superseded status below records its original checkpoint. ADR 0018 partially supersedes service, funding, publication, comparison and architecture semantics; unchanged isolation, history and immutable wire guarantees remain applicable.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](0018-v3-paid-map-combo-business-policy.md), [target contracts](../contracts/survey-business-v3.md). Documentation-only update; no runtime change.

- Status: Accepted
- Date: 2026-09-22
- Decision owners: Backend 1, Operations

> Amended by ADR-0007: payment remains a hard gate for the selected paid service Mission, but is not required for prerequisite Baseline Mapping.

## Context

Payment là hard gate trước khi bay. Cho phép TenantOwner hoặc client tùy ý đặt `Confirmed` sẽ phá vỡ integrity; callback có thể retry, đến sai thứ tự hoặc cần đối soát thủ công.

## Decision

- TenantOwner có thể khởi tạo payment/redirect nhưng không thể đặt trạng thái `Confirmed`.
- Nguồn chuẩn là callback/webhook đã xác thực chữ ký của payment provider, định danh idempotent bằng `(Provider, ProviderReference, ProviderEventId)`.
- SystemAdmin chỉ được reconciliation thủ công khi có provider reference/evidence và reason; thao tác phải audit before/after.
- Amount và currency phải khớp snapshot còn hiệu lực của SurveyOrder. Mismatch chuyển sang `AdjustmentRequired` hoặc review, không mở operational gate.
- Callback duplicate trả thành công idempotent; transition lùi hoặc xung đột bị từ chối và ghi nhận.
- Order chỉ ready khi payment hiện tại là `Confirmed` đồng thời scope, appointment và manager assignment đều hợp lệ. Refund/chargeback đóng gate cho flight chưa bắt đầu.

## Consequences

Phase 6 cần inbox/idempotency cho provider event, lưu raw reference tối thiểu phục vụ audit và tách API customer khỏi reconciliation API của SystemAdmin.
