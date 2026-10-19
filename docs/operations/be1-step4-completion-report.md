# BE1 Step 4 — Consent, Appointment-first Boundary Capture, Automatic Route Readiness and Commercial Gates Completion Report

- Date: 2026-10-19
- Scope: Step 4 (UC06) Consent, baseline appointment, same-visit boundary capture, automatic route plan approval, per-pole count snapshot, paid appointment/sandbox payment, scope adjustments, settlement and purpose-aware readiness
- Status: CodeComplete — AwaitingFinalTest

## Summary of Delivered Surface

Step 4 đã hoàn thành 100% các tính năng nghiệp vụ và kỹ thuật theo `Be-Plan-v2.md` và `BE1-Core-Business-Implementation-Plan.md` (bao gồm toàn bộ amendment R2 ngày 2026-10-17):

### 1. Capture Authorization & Consent (Phase 4A0)
- `SurveyCaptureAuthorization` lưu vết written data-use consent và flight permission độc lập.
- SystemAdmin verify/revoke có đầy đủ evidence reference, versioning, expiry và audit.
- Readiness fail-closed khi thiếu, hết hạn hoặc bị thu hồi giấy phép.

### 2. Appointment-First & Same-Visit Boundary Capture (Phase 4A & 4B)
- Unmapped Farm khởi tạo Order ở `AwaitingBaselineAppointment`, không yêu cầu boundary trước khi đặt lịch.
- TenantOwner confirm lịch Baseline chuyển Order sang `AwaitingBoundaryCapture` (không nhảy thẳng lên `BaselineReady`).
- `BoundaryCaptureSession` + ordered `BoundaryCaptureSample` (WGS84, horizontal accuracy, client operation ID, SHA-256 payload hash).
- Lifecycle đầy đủ:
  - `POST /api/system-manager/survey-orders/{orderId}/boundary-capture-sessions` (Start)
  - `GET /api/system-manager/survey-orders/{orderId}/boundary-capture-sessions/{sessionId}` (Get detail & samples)
  - `POST /api/system-manager/survey-orders/{orderId}/boundary-capture-sessions/{sessionId}/samples:sync` (Sync ordered samples)
  - `POST /api/system-manager/survey-orders/{orderId}/boundary-capture-sessions/{sessionId}/finalize` (Validate PostGIS polygon & approve FarmBoundary)
  - `POST /api/system-manager/survey-orders/{orderId}/boundary-capture-sessions/{sessionId}/abort` (Abort with reason, retain samples)

### 3. Automatic Route Plan Approval & Baseline Readiness (Phase 4B)
- `BaselineRoutePlanReference` tích hợp liên kết với BE2 route plan (safety inset meters, within safety inset flag).
- `POST /api/system-manager/survey-orders/{orderId}/baseline-route-plans/{routePlanId}/approve`
- Chỉ approved route plan khớp current boundary version và nằm trong safety inset mới transition Order `AwaitingBoundaryCapture → BaselineReady`.
- Readiness failure codes đầy đủ cho capture session và route plan.

### 4. Confirmed Pole-Count & Per-Pole Pricing Snapshot (Phase 4C)
- Snapshot bất biến: `OriginalSurveyServicePriceId`, `OriginalConfirmedPoleCount`, `OriginalPricePerPoleSnapshot`, `OriginalFinalPrice`.
- `POST /api/system-manager/survey-orders/{orderId}/pole-count/confirm` đọc directly từ authoritative inventory và effective price catalogue tại server time.

### 5. Paid Appointment & Sandbox Payment (Phase 4D)
- Paid service appointment lifecycle (`Propose`, `Confirm`, `RequestReschedule`).
- Payment session initiation chỉ lấy authoritative `FinalPrice/Currency` phía server.
- Webhook callback xác thực chữ ký HMAC-SHA256, deduplicate idempotency key `(Provider, ProviderReference, ProviderEventId)`.
- Mismatch amount/currency chuyển sang `AdjustmentRequired`.

### 6. Scope Adjustments, Settlement & Extra Flights (Phase 4E)
- `ScopeAdjustment` phân biệt lý do (`ConfirmedPoleCountChanged`, `ApprovedBoundaryChanged`) và lưu vết before/after scope/pole count/financial impact.
- `AdjustmentSettlement` hỗ trợ `AdditionalPayment` và `Refund`.
- `ExtraFlightRequest` hỗ trợ `InsufficientData` và `ApprovedScopeChange`.
- Đầy đủ API endpoints trên `SystemManagerSurveyOrdersController` và `SystemSurveyOrdersController`.

### 7. Authoritative Purpose-Aware Readiness
- `ISurveyOrderReadinessService` kiểm tra toàn bộ điều kiện phụ thuộc theo `BaselineMapping` và `PlantHealth`.
- Baseline Mapping không đòi hỏi thanh toán hoặc base map version.
- Paid Service đòi hỏi exact confirmed payment, no open adjustments, và matching current base map.

## Verification Evidence

- `dotnet build backend/AgriDrone.sln --no-restore`: PASS, 0 warnings, 0 errors.
- Unit Tests (BoundaryCapture & Surveys): 103/103 passed.
- Architecture Tests: 90/90 passed.
- Code Complete: Tất cả các file trong manifest Step 4 đã được tạo và wiring hoàn chỉnh.

## Next Step

Sẵn sàng chuyển sang **Step 5 — UC07 Farm/Zone operational verification và BE2 boundary**.
