# Rà soát BE-1 đến BE-11 và kế hoạch sửa

Đối chiếu mã nguồn tại HEAD `90625e2` ngày 27/11/2026. Bảng ghi trạng thái trước khi triển khai bản sửa. Xem mục bàn giao FE để biết hợp đồng API sau thay đổi. Nghiệm thu tích hợp ở V3-10.

| Mục | Trạng thái | Bằng chứng và điểm tích hợp |
| --- | --- | --- |
| BE-1 | Đã sửa | GET invoice V3 cho Owner và Manager trả số dư hiện tại qua `dueAmount` khi hóa đơn đã chốt. |
| BE-2 | Đã sửa | GET danh sách đơn Manager hỗ trợ bộ lọc trạng thái V3, gồm `AWAITING_DEPOSIT` và `READY_FOR_CAPTURE`. Owner có bộ lọc trên GET V3. |
| BE-3 | Đã sửa | GET `/api/v3/system-manager/survey-orders/{id}/capture-attempts` trả danh sách lần bay. |
| BE-4 | Đã sửa | `V3SystemAdminCommercialPoliciesController` có 8 GET dưới `/api/v3/system-admin/commercial-policies`, gồm services, revisions, prices, deposit-policies, claim-policies, cancellation-policies, bundle-versions và activations. |
| BE-5 | Đã sửa | `MissionPlantGpsPublisher` dùng `mission.CaptureAttemptId`. `MappingCandidatesReadyV4Handler` đọc attempt theo ID và kiểm tra liên kết trên Mission. `FarmBaseMapInternalApprovalOrchestrator` đối chiếu bằng `drone_missions.capture_attempt_id`. |
| BE-6 | Sửa một phần | Owner dùng GET list/detail V3 để đọc trạng thái đơn và lịch Capture. Route GET cũ vẫn có nhãn `PAID_SERVICE`. Response của API thao tác lịch hẹn dùng `SurveyAppointmentResponseMapper`, có thể trả `AWAITINGDEPOSIT` qua nhánh fallback. |
| BE-7 | Đã sửa | `GetSurveyPaymentHistoryQueryHandler.MapPurpose` hỗ trợ `ORDER_DEPOSIT`. Owner đọc lịch sử tại `GET /api/tenant-owner/survey-orders/{id}/payments`. |
| BE-8 | Sửa một phần | Đơn Manager đã trả `currentAppointmentStartAt` và `currentAppointmentEndAt`. GET drone theo đơn vẫn qua `SurveyOrderMissionPlanningQuery` với service legacy. |
| BE-9 | Đã sửa qua API CaptureAttempt | GET `/api/v3/system-manager/survey-orders/{id}/capture-attempts` trả manifest hiện hành, fingerprint, processingJobId và orderVersion. Phê duyệt chuyển Mission sang `ReadyForProcessing`. Đã có `input-approval/recover` cho bản ghi cần phục hồi. |
| BE-10 | Còn lỗi | `DetectMissionDiseasesCommandHandler` truy vấn sai tên bảng hai lần. DTO của `MissionOrthophotoController` thiếu ID job nhận diện bệnh và revision cần cho handoff. |
| BE-11 | Sửa một phần | Route `farm-boundary/mark-orthophoto-corners` hiện gắn ranh giới vào Order ở các trạng thái legacy. V3 First có thể đến bước mapping ở `Processing` và publisher cần boundary trên Mission khớp với Order. |

## Thứ tự triển khai

### 1. BE-10 · Khôi phục luồng COMBO

- Sửa cả truy vấn trước inference và truy vấn trong transaction ở `DetectMissionDiseasesCommandHandler` theo schema `survey.order_commercial_snapshots`. Giữ điều kiện COMBO, capability `ScreensDisease` và các kiểm tra lineage hiện hành.
- Trả `jobId` và `sourceRevision` từ kết quả đã commit, xuyên qua lớp Application, DTO `DetectMissionDiseasesResponse` và `MissionOrthophotoController`. Lấy ID từ job DiseaseDetection và revision từ snapshot COMBO tương ứng. Đồng bộ tên trường với request tại `combo-analysis/handoffs`.
- Nghiệm thu V3-10 gồm COMBO có và không có detections đều trả được cặp giá trị dùng để tạo handoff. MAP bị chặn và response legacy vẫn tương thích.

### 2. BE-11 · Gắn ranh giới vào luồng V3 First

- Bổ sung action Manager V3 theo Order và CaptureAttempt. Tái sử dụng xác thực bốn góc, nghiệp vụ version boundary và audit từ `MarkFarmBoundaryOnOrthophotoCommandHandler` / `SurveyOrderBoundaryRepository`. Kiểm tra bằng chứng ảnh trực giao thuộc đúng Capture Mission, cùng tenant/farm, và Manager đang được phân công.
- Bổ sung phương thức domain trên `SurveyOrder` và `DroneMission` để gắn cùng một boundary version ở giai đoạn xử lý hợp lệ, trước khi publish GPS. Kiểm tra expected versions, khóa theo ADR 0020 và commit boundary, Order, Mission, audit trong cùng transaction. Nếu đã có mapping evidence dùng version cũ, trả conflict và yêu cầu xử lý qua luồng revision phù hợp. Trả boundary ID và versions cho FE.
- Nghiệm thu V3-10 gồm First → xác nhận ranh giới → gửi GPS → duyệt nội bộ bản đồ. Kiểm tra lưu lặp, version cũ, attempt đã bị thay thế, sai tenant và rollback khi cập nhật Mission thất bại.

### 3. BE-8 · Liệt kê drone đúng nghiệp vụ Capture

- Thêm GET V3 theo Order tại `V3SystemManagerSurveyOrdersController` với query của Missions dùng contract ngữ cảnh Capture. Đọc lịch đã xác nhận cho lần bay đầu. Khi chọn cho bay lại, dùng khung giờ dự kiến mà Manager gửi, cùng ngữ cảnh quyết định bay lại. Trả khung giờ thực tế đã dùng để lọc.
- Dùng truy vấn availability hiện có và áp dụng cùng điều kiện capability từ commercial snapshot như `SurveyCaptureExecutionCoordinator` ở bước chuẩn bị và bay lại. Kiểm tra assignment và tenant/farm. Writer vẫn kiểm tra lại availability khi ghi để xử lý cạnh tranh giữa hai lượt đặt.
- Nghiệm thu V3-10 gồm MAP, COMBO, lịch đã dời, bay lại, drone trùng lịch, drone thiếu capability và Manager ngoài phạm vi phân công.

### 4. BE-6 · Chuẩn hóa trạng thái trong response thao tác lịch hẹn

- Dùng mapping trạng thái đơn đầy đủ từ `SurveyOrderResponseMapper` trong `SurveyAppointmentResponseMapper`. Giữ các giá trị legacy đã công bố và kiểm tra các response tạo lịch, xác nhận, yêu cầu dời lịch.
- Nghiệm thu V3-10 gồm lịch Capture trả `CAPTURE` và khi xác nhận chuyển đến AwaitingDeposit thì response trả `AWAITING_DEPOSIT`. Xác nhận FE dùng GET Owner V3 để tải lại đơn.

## Kiểm tra sau triển khai

Sau mỗi nhóm, cập nhật contract bàn giao FE và chạy `dotnet build backend/AgriDrone.sln --no-restore --nologo`. Chạy nghiệm thu PostgreSQL/PostGIS ở V3-10 theo quy định dự án.

## Bàn giao FE sau triển khai

Tiền tố Manager V3 là `/api/v3/system-manager/survey-orders`.

- **BE-6** · Response tạo, xác nhận và yêu cầu dời lịch sử dụng cùng mã trạng thái đơn với API Manager. Khi xác nhận và đơn chuyển sang chờ cọc, FE đọc `orderStatus: "AWAITING_DEPOSIT"`.
- **BE-8** · Gọi `GET /{orderId}/drones/available` dưới tiền tố Manager V3. Lần đầu dùng lịch Capture đã xác nhận. Khi bay lại, truyền `startAt` và `endAt` theo khung giờ dự kiến. Response trả `purpose: "Capture"`, khung giờ đã dùng và danh sách `drones`.
- **BE-10** · Lấy `jobId` và `sourceRevision` từ response 200 của thao tác nhận diện bệnh V3 COMBO rồi truyền nguyên cặp vào `combo-analysis/handoffs`.
- **BE-11** · Gọi `POST /{orderId}/capture-attempts/{attemptId}/farm-boundary/mark-orthophoto-corners` dưới tiền tố Manager V3 trước khi gửi GPS. Body gồm `expectedOrderVersion`, `expectedMissionVersion`, bốn `corners` có `sequence`, `longitude`, `latitude` và `expectedBoundaryVersionNumber` nếu farm đã có ranh giới được duyệt. Server dùng ảnh preview trực giao của Mission làm bằng chứng. Response trả `farmBoundaryId`, `boundaryVersionNumber`, `orderVersion` và `missionVersion`. Khi version cũ hoặc attempt đã có mapping batch, API từ chối cập nhật. FE tải lại phiên bản hiện hành khi gặp conflict.

Cả bốn mục đã có thay đổi mã nguồn. Ngày 27/11/2026, build toàn solution bằng .NET SDK 10.0.404 đạt 0 cảnh báo, 0 lỗi. Nghiệm thu runtime ở V3-10.
