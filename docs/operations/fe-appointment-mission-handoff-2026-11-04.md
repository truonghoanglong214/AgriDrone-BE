# Bàn giao FE: lịch hẹn, drone available và Prepare Mission

Ngày cập nhật: 04/11/2026. Múi giờ nghiệp vụ: `Asia/Ho_Chi_Minh` (UTC+07:00).

## Tóm tắt thay đổi

- Manager đề xuất lịch hẹn bằng **một** `scheduledDate`. Request không còn dùng `proposedStartAt`, `proposedEndAt`, `scheduledStartDate` hoặc `scheduledEndDate`.
- Nếu chọn hôm nay, BE lưu `proposedStartAt` bằng thời điểm BE nhận request; nếu chọn ngày tương lai, BE lưu 00:00 ngày đó theo giờ Việt Nam. `proposedEndAt` luôn là 00:00 ngày kế tiếp. Ngày đã kết thúc bị từ chối.
- FE tìm drone cho SurveyOrder bằng endpoint mới. BE tự lấy purpose và window của lịch hẹn đã xác nhận; FE không truyền giờ hay `farmId` cho endpoint này.
- Prepare Mission chỉ nhận `droneId` trong body. BE tự quyết định Baseline Mapping hay dịch vụ đã chọn trên SurveyOrder và dùng window lịch hẹn tương ứng. FE không gửi `baselineDate`, `serviceDate` hoặc mission purpose.
- Cọc Baseline mặc định của **SurveyOrder tạo mới** là **5.000 VNĐ** để test. Payment và khoản cấn trừ dùng `depositAmount` đã lưu trên từng Order; FE lấy số tiền từ response của BE, không hard-code 5.000 VNĐ. Order cũ giữ mức cọc đã lưu.

## 1. Đề xuất lịch hẹn

`POST /api/system-manager/survey-orders/{surveyOrderId}/appointments`

```json
{
  "purpose": "BaselineMapping",
  "scheduledDate": "2026-11-04",
  "expectedOrderVersion": 1
}
```

`purpose` là `BaselineMapping` hoặc `PaidService`. Khi đề xuất lại sau yêu cầu đổi lịch, gửi thêm `expectedCurrentAppointmentId` và `expectedCurrentAppointmentVersion` theo flow hiện có.

Ví dụ chọn ngày 04/11/2026:

| Thời điểm đề xuất | `proposedStartAt` BE lưu | `proposedEndAt` BE lưu |
| --- | --- | --- |
| 04/11 lúc 14:20 giờ VN | 04/11 14:20 giờ VN | 05/11 00:00 giờ VN |
| Trước ngày 04/11 | 04/11 00:00 giờ VN | 05/11 00:00 giờ VN |

Response có `appointmentId`, `appointmentStatus`, `appointmentVersion`, `orderVersion`, `scheduledDate`, `proposedStartAt` và `proposedEndAt`. Các timestamp trả về ở UTC; FE hiển thị theo giờ Việt Nam. Response `purpose` dùng mã `BASELINE_MAPPING` hoặc `PAID_SERVICE`.

Tenant Owner xác nhận bằng endpoint hiện có:

`POST /api/tenant-owner/survey-orders/{surveyOrderId}/appointments/{appointmentId}/confirm`

```json
{
  "expectedOrderVersion": 1,
  "expectedAppointmentVersion": 1
}
```

Lấy version mới nhất từ response trước khi xác nhận. BE không cho xác nhận khi window lịch hẹn đã kết thúc.

## 2. Xem drone available theo lịch hẹn

`GET /api/system-manager/survey-orders/{surveyOrderId}/drones/available`

Endpoint mới không có query parameter. Response gồm:

| Field | Ý nghĩa |
| --- | --- |
| `surveyOrderId`, `farmId` | Order và Farm BE suy ra |
| `purpose` | Mission sẽ chuẩn bị: `BaselineMapping`, `PlantHealth` hoặc `HarvestReadiness` |
| `startAt`, `endAt` | Window lấy từ lịch hẹn đã xác nhận, ở UTC |
| `drones` | Danh sách drone available và hỗ trợ purpose đó; dùng `drones[].id` để gọi Prepare |

BE loại drone không đủ điều kiện đăng ký/bảo trì, không hỗ trợ purpose, hoặc đã có mission `Scheduled`/`InFlight` chồng **bất kỳ phần nào** của window này. Mission `Draft` chưa giữ lịch. Nếu Order chưa đủ điều kiện schedule theo lịch hẹn đã xác nhận, endpoint trả lỗi `MissionPlanning.OrderNotReady`. Danh sách chỉ là kết quả tại thời điểm truy vấn; Prepare sẽ kiểm tra lại.

Endpoint cũ vẫn còn cho các màn hình cần tự chọn Farm và khoảng giờ:

`GET /api/system-manager/farms/{farmId}/drones/available?startAt=...&endAt=...&purpose=...`

## 3. Prepare Mission

`POST /api/system-manager/survey-orders/{surveyOrderId}/missions/prepare`

Header bắt buộc: `Idempotency-Key: <UUID>`.

```json
{
  "droneId": "<UUID-lay-tu-drones-id>"
}
```

FE không gửi `farmId`, ngày bay, giờ bay hoặc mission purpose. BE lấy Farm và purpose từ SurveyOrder, lấy window từ lịch hẹn đúng purpose, kiểm tra drone available lại rồi lưu mission. Response có `surveyOrderId`, `farmId`, `reusedExistingSet` và `missions[]` (`missionId`, `purpose`, `status`, `scheduledAt`, `scheduledEndAt`, `version`). `scheduledAt`/`scheduledEndAt` là window mission; giờ bay thực tế được ghi khi chuyển trạng thái chuyến bay.

Khi retry **cùng một lần Prepare**, dùng lại `Idempotency-Key`. Khi chuyển từ giai đoạn Baseline sang dịch vụ sau khi base map đã được duyệt/publish và Order đủ điều kiện, gọi Prepare lần nữa với key mới.

### BE chọn mission theo trạng thái Farm

| Trạng thái Farm/Order | Mission của giai đoạn hiện tại |
| --- | --- |
| Farm chưa có approved base map, kể cả Tenant đã có Farm khác | `BaselineMapping` trước |
| Baseline đã được publish, Order đã qua điều kiện giá/thanh toán và có lịch hẹn dịch vụ đã xác nhận | Dịch vụ đã chọn trên Order, ví dụ `PlantHealth` |
| Farm đã có approved base map và Order đủ điều kiện dịch vụ | Dịch vụ đã chọn trên Order, ví dụ `PlantHealth` |

Người dùng có thể chọn dịch vụ PlantHealth khi tạo Order cho Farm mới. Lựa chọn dịch vụ vẫn nằm trên Order; BE chuẩn bị Baseline trước, sau đó mới bổ sung PlantHealth khi các điều kiện tiếp theo hoàn tất. Hai giai đoạn có lịch hẹn theo purpose riêng.

Nếu lịch hẹn đổi và mission đã schedule nằm ngoài window mới, Prepare trả `MissionPlanning.ScheduleNeedsReschedule`. Flow đổi lịch mission hiện tại vẫn nhận `scheduledDate`; BE chỉ lưu phần window nằm trong lịch hẹn đã xác nhận.

## 4. Cọc Baseline để test

Với Order Baseline tạo mới, BE đặt `depositAmount = 5000` VND. Sau khi Tenant Owner xác nhận lịch hẹn Baseline, gọi endpoint payment hiện có với `purpose: "BASELINE_DEPOSIT"` và `expectedOrderVersion` mới nhất. FE dùng `amount` trong response payment/Order để hiển thị; không tự tính hoặc gửi số tiền cọc. Sau khi BE xác nhận payment, Order mới có thể qua gate cọc để bay Baseline. Khi tính giá cuối, BE cấn trừ đúng `depositAmount` đã trả.

## FE cần cập nhật

1. Form đề xuất lịch hẹn chỉ gửi `scheduledDate` cùng purpose và version; hiển thị window từ response BE.
2. Màn hình chọn drone gọi endpoint available theo `surveyOrderId`; lấy `drones[].id`.
3. Prepare chỉ gửi `droneId` và header `Idempotency-Key`; đọc `missions[].purpose`/`status` từ response để hiển thị giai đoạn BE đã chuẩn bị.
4. Hiển thị cọc Baseline từ dữ liệu Order/payment của BE, vì 5.000 VNĐ là mức mặc định test cho Order mới và có thể đổi sau này.

## Kiểm tra BE

Build toàn solution thành công; 733 unit test và 80 architecture test đã qua trong quá trình thay đổi. Chưa chạy integration test cần database, migration hoặc kiểm thử API/payment gateway thủ công.
