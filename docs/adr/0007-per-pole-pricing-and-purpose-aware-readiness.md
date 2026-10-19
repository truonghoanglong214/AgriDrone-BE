# ADR-0007: Per-pole pricing và purpose-aware SurveyOrder readiness

- Status: Accepted
- Date: 2026-10-07
- Amended: 2026-10-17 — Baseline appointment precedes same-visit boundary capture and automatic route generation
- Decision owners: Product, Backend 1, Backend 2

## Context

Baseline Phase 5 tính giá theo diện tích và dùng một readiness gate yêu cầu payment cho mọi Mission. Business baseline mới tính phí theo số trụ đang hoạt động trong approved scope. Farm chưa có published base map phải chạy Baseline Mapping trước payment để xác lập số trụ; paid Plant Health hoặc Harvest Readiness Mission chỉ được chạy sau khi giá và payment đã được xác nhận.

## Decision

### Pricing unit và snapshot

- Đơn vị tính phí là một active dragon-fruit pole position, không phải diện tích và không phải số nhánh trên trụ.
- `ConfirmedSurveyPoleCount` chỉ gồm pole/profile đang `Active`, nằm trong approved `FarmBoundary` và selected scope. Removed, inactive, duplicate, pending hoặc unresolved boundary-exception records không được tính.
- `PricePerPole` là positive `decimal(18,2)` bằng VND. `ConfirmedSurveyPoleCount` là positive integer.
- Giá catalogue được resolve tại server time khi assigned SystemManager xác nhận pole count và pricing. Giá hiển thị trước thời điểm đó chỉ mang tính tham khảo.
- Order snapshot tối thiểu gồm price-version ID, `PricePerPole`, currency, confirmed pole count, FarmBoundary version, Farm base-map version, confirmer và UTC confirmation time.
- Công thức duy nhất là `FinalPrice = Round(ConfirmedSurveyPoleCount × PricePerPole, 2, MidpointRounding.AwayFromZero)`.
- Farm có published base map dùng active count từ current published map, nhưng assigned SystemManager vẫn phải xác nhận count và map version cho từng order.
- Base map không hết hạn theo TTL. Baseline Mapping chỉ bắt buộc khi Farm chưa có published map; inventory changes đi qua reviewed amendment/change workflow.

### Legacy price

- Target flow không đọc, tính hoặc snapshot `PricePerHa`.
- Không tự quy đổi `PricePerHa` sang `PricePerPole` bằng area hoặc mật độ ước tính.
- Legacy columns/rows được giữ read-only trong expand window để đọc lịch sử. PricePerPole versions mới phải được cấu hình rõ ràng.
- Contract migration chỉ xóa legacy price sau inventory, retention và upgrade/replay gate.

### Order và appointment model

- Một `SurveyOrder` sở hữu cả prerequisite Baseline stage và selected paid-service stage; không tạo Baseline SurveyOrder riêng.
- Appointment có `Purpose = BaselineMapping | PaidService`. Mỗi order có tối đa một active appointment cho mỗi purpose.
- Unmapped Farm không yêu cầu approved boundary để đề xuất/xác nhận Baseline appointment. Approval đưa Order vào `AwaitingBaselineAppointment`; Owner confirmation đưa Order vào `AwaitingBoundaryCapture`.
- Trong chính appointment đã confirm, assigned Manager dùng drone GNSS chấm các góc, finalize/approve `FarmBoundary`, sau đó hệ thống sinh automatic mapping route nằm trong polygon đã approve. Boundary capture và Baseline flight là hai bước liên tiếp trong cùng field visit, không phải hai appointment.
- Unmapped Farm đi theo chuỗi:
  `AwaitingBaselineAppointment → AwaitingBoundaryCapture → BaselineReady → BaselineInProgress → AwaitingBaselineReview → AwaitingPricing → AwaitingPaidAppointment → AwaitingPayment → ReadyForPaidService → InProgress → PendingReview → Completed`.
- Mapped Farm bỏ qua các trạng thái Baseline và đi từ verified boundary/scope tới `AwaitingPricing`.
- Paid-service ordering là: confirmed count → pricing snapshot → paid appointment confirmation → payment confirmation → `ReadyForPaidService`.
- Cancellation được phép trước khi Mission tương ứng bắt đầu. Sau khi work đã bắt đầu, hệ thống dùng failure/abort/compensation state và không giả lịch sử thành `Cancelled`.

### Purpose-aware readiness

- Baseline appointment proposal/confirmation chỉ yêu cầu Order thuộc unmapped Farm và eligible primary assignment; chưa yêu cầu approved boundary.
- Baseline readiness tại thời điểm start yêu cầu confirmed Baseline appointment còn hiệu lực, boundary vừa được Manager capture/approve, automatic route plan được sinh từ đúng boundary version và mọi waypoint nằm trong safety-inset polygon, assigned active/available/flight-qualified primary SystemManager cùng safety/pre-flight gates; không yêu cầu price hoặc payment.
- Paid-service readiness yêu cầu approved boundary/scope, published base map, confirmed active-pole count, immutable price snapshot, confirmed PaidService appointment, confirmed payment, eligible assigned manager, không có pending price adjustment và các safety/pre-flight gates.
- Readiness là server-owned decision theo `MissionPurpose`; client không được gửi `IsReady` hoặc derived order state.

## Consequences

`SurveyServicePrice`, `SurveyOrder`, appointment uniqueness, boundary-capture lifecycle, automatic route-plan snapshot, readiness policy, operational-context contract và database constraints phải được rebaseline. ADR-0003 bị supersede đối với pricing unit nhưng giữ nguyên currency/rounding conventions.
