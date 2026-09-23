# ADR-0004: Nguồn xác nhận thanh toán

- Status: Accepted
- Date: 2026-09-22
- Decision owners: Backend 1, Operations

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
