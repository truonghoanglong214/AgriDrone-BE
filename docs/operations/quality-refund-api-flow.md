# Luồng API báo sai kết quả cây và hoàn tiền

Tài liệu này mô tả luồng hiện có trong BE: Owner chọn cây của một farm, báo kết quả Plant Health sai; Manager xác minh; System Admin quyết định và ghi nhận khoản hoàn đã chuyển qua ứng dụng ngân hàng.

## Điều kiện trước khi thử

- Survey Order thuộc tenant của Owner, có trạng thái `COMPLETED`.
- Đơn có kết quả `PLANT_HEALTH` đã `PUBLISHED` và chưa bị vô hiệu hóa.
- Đơn có ít nhất một khoản thanh toán `PAID` đã xác nhận.
- Chưa có hồ sơ hoàn tiền đang hoạt động hoặc đã hoàn cho đơn. Hồ sơ `REJECTED` không chặn việc gửi hồ sơ mới.
- BE đã chạy phiên bản chứa endpoint `GET /api/farms/{farmId}/plants` và các migration của luồng hoàn tiền đã được áp dụng trên môi trường thử nghiệm.

Trong các URL bên dưới, thay `{orderId}`, `{farmId}`, `{caseId}` bằng ID thực tế. Mỗi request dùng `Authorization: Bearer <token>` của đúng vai trò; các request có JSON dùng thêm `Content-Type: application/json`.

## 1. Owner lấy farm của đơn

```http
GET /api/tenant-owner/survey-orders/{orderId}
```

Lấy `farmId`, `serviceType` và `status` từ response. Chỉ đi tiếp khi `serviceType` là `PLANT_HEALTH` và đơn đủ điều kiện báo sai.

## 2. Owner lấy cây theo farm

```http
GET /api/farms/{farmId}/plants
```

Endpoint này yêu cầu token Owner và chỉ trả cây khi farm thuộc tenant hiện tại. Response là một mảng lấy từ `plant.plants`; ví dụ rút gọn:

```json
[
  {
    "id": "11111111-1111-1111-1111-111111111111",
    "plantCode": "P-001",
    "zoneId": "22222222-2222-2222-2222-222222222222",
    "row": 1,
    "col": 1,
    "lat": 10.123456,
    "lon": 106.123456,
    "lifecycleStatus": "ACTIVE"
  }
]
```

FE đặt điểm cây theo `lat/lon`; cây thiếu tọa độ vẫn có thể hiện trong danh sách bằng `plantCode`. Khi Owner chọn cây, dùng **`plantCode` làm `issues[].poleCode`** ở bước 3. `id` là Plant ID, không phải mã gửi trong báo cáo hoàn tiền.

Nếu FE cần ảnh nền và trạng thái sức khỏe để hiển thị bản đồ, có thể gọi thêm `GET /api/farms/{farmId}/plant-health-map`. API này tổng hợp dữ liệu của cả farm, không giới hạn theo Survey Order. Sau khi Admin duyệt hoàn tiền và kết quả bị vô hiệu hóa, API bản đồ có thể trả `409` cho kết quả đã rút; API danh sách cây ở trên vẫn là endpoint riêng.

## 3. Owner gửi một báo cáo cho nhiều cây

```http
POST /api/tenant-owner/survey-orders/{orderId}/quality-refunds
Content-Type: application/json
```

```json
{
  "issues": [
    {
      "poleCode": "P-001",
      "issueType": "FALSE_POSITIVE",
      "details": "Kết quả báo cây bệnh nhưng cây thực tế khỏe."
    },
    {
      "poleCode": "P-002",
      "issueType": "FALSE_NEGATIVE",
      "details": "Kết quả báo cây khỏe nhưng cây thực tế bệnh."
    }
  ],
  "description": "Hai cây có kết quả không đúng với tình trạng tại vườn.",
  "recipientBankName": "Vietcombank",
  "recipientAccountNumber": "0123456789",
  "recipientAccountName": "NGUYEN VAN A"
}
```

`issueType` nhận `FALSE_POSITIVE`, `FALSE_NEGATIVE` hoặc `WRONG_POLE`. Mỗi cây có một `poleCode` và `details`; mã trong cùng báo cáo không được trùng, không phân biệt chữ hoa/thường. BE yêu cầu ít nhất một cây và hiện **không đặt giới hạn số cây theo nghiệp vụ**. `description` là mô tả chung. `evidenceReference` là trường tùy chọn nên có thể bỏ khỏi JSON.

Response `201 Created` chứa hồ sơ có `id` (`caseId`), `status: "SUBMITTED"` và `version`. Lưu `caseId`; các bước sau luôn lấy `version` mới nhất từ response hoặc API GET, không dùng lại phiên bản cũ.

```http
GET /api/tenant-owner/survey-orders/{orderId}/quality-refunds
```

Owner dùng GET này để xem báo cáo và trạng thái xử lý. Tại thời điểm viết tài liệu, BE kiểm tra định dạng và tính duy nhất của `poleCode` nhưng **chưa đối chiếu mã được POST với bảng `plant.plants`**. FE nên lấy mã từ bước 2; request tự gửi vẫn có thể chứa mã không tồn tại.

## 4. Manager xác minh từng cây

```http
GET /api/system-manager/survey-orders/{orderId}/quality-refunds
```

Manager đang phụ trách farm lấy `caseId`, toàn bộ `issues` và `version`. Response dành cho Manager không chứa tài khoản ngân hàng nhận hoàn. Sau khi đối chiếu kết quả đã công bố với thực tế, Manager gửi **một finding cho mỗi `poleCode` đã báo**, không thiếu và không trùng:

```http
POST /api/system-manager/survey-orders/{orderId}/quality-refunds/{caseId}/verify
Content-Type: application/json
```

```json
{
  "findings": [
    {
      "poleCode": "P-001",
      "confirmedError": true,
      "reason": "Đối chiếu hiện trường: cây khỏe, kết quả đã công bố báo bệnh."
    },
    {
      "poleCode": "P-002",
      "confirmedError": false,
      "reason": "Đối chiếu hiện trường: kết quả của cây này phù hợp."
    }
  ],
  "reason": "Đã kiểm tra đầy đủ hai cây Owner báo.",
  "expectedVersion": 1
}
```

`expectedVersion: 1` chỉ là ví dụ. Dùng giá trị `version` thực tế vừa lấy. Manager chỉ xác minh; Manager không có quyền duyệt hoặc ghi nhận chuyển khoản.

## 5. Admin quyết định

```http
GET /api/system-admin/survey-orders/{orderId}/quality-refunds
```

Response có `cases[]`, `grossPaid`, `refunded`, `netCollected` và `currency`. Admin lấy `version` mới của hồ sơ sau bước xác minh. Có thể xem hàng đợi qua `GET /api/system-admin/survey-orders/quality-refunds?pageNumber=1&pageSize=20`.

```http
POST /api/system-admin/survey-orders/{orderId}/quality-refunds/{caseId}/decision
Content-Type: application/json
```

Ví dụ duyệt:

```json
{
  "approve": true,
  "reason": "Manager đã xác nhận ít nhất một cây có kết quả sai.",
  "expectedVersion": 2
}
```

Ví dụ từ chối: gửi cùng endpoint với `"approve": false`, lý do từ chối và `expectedVersion` hiện tại. Admin chỉ được quyết định sau khi Manager đã xác minh. Muốn duyệt, ít nhất một finding phải có `confirmedError: true`.

Khi duyệt, BE tự tính `approvedAmount = grossPaid - refunded` từ các khoản thanh toán đã xác nhận và các khoản đã hoàn; client **không gửi số tiền**. Hồ sơ chuyển sang `APPROVED`. Kết quả Plant Health liên quan bị vô hiệu hóa ngay lúc duyệt, trước bước chuyển khoản. Khi từ chối, hồ sơ chuyển sang `REJECTED` và không ghi nhận tiền hoàn.

## 6. Admin chuyển tiền và ghi nhận giao dịch

Admin chuyển số tiền `approvedAmount` cho tài khoản nhận hoàn bằng ứng dụng ngân hàng. Sau khi ngân hàng xác nhận giao dịch thành công, Admin ghi mã giao dịch vào BE:

```http
POST /api/system-admin/survey-orders/{orderId}/quality-refunds/{caseId}/record-transfer
Content-Type: application/json
```

```json
{
  "bankTransferReference": "1029TC3478192301",
  "expectedVersion": 3
}
```

Lấy `expectedVersion` từ response duyệt hoặc GET Admin mới nhất. Không gửi `approvedAmount`, `evidenceReference` hay `transferredAt` trong request này. BE tự lưu thời điểm xác nhận làm `transferredAt` và đổi hồ sơ thành `REFUNDED`.

## 7. Kiểm tra hoàn tất và doanh thu thực thu

```http
GET /api/system-admin/survey-orders/{orderId}/quality-refunds
```

Kiểm tra `cases[].status = "REFUNDED"`, `cases[].approvedAmount`, `cases[].bankTransferReference`, `cases[].transferredAt` và ba số tiền cấp đơn:

```text
grossPaid    = tổng thanh toán PAID đã xác nhận của Survey Order
refunded     = tổng approvedAmount của các hồ sơ REFUNDED
netCollected = grossPaid - refunded
```

Hồ sơ mới `APPROVED` nhưng chưa ghi nhận chuyển khoản **chưa được cộng vào `refunded`**.

## Lưu ý khi nối FE và thử API

- FE dùng `plants[].plantCode` để tự điền `issues[].poleCode`; không dùng `Plant ID` hoặc mã tự tạo.
- BE không còn giới hạn 50 cây trong một báo cáo. Form FE hiện tại vẫn khóa nút thêm cây ở 50; thay đổi FE là bước riêng.
- Danh sách cây là dữ liệu hiện tại của farm, chưa phải snapshot riêng của kết quả theo `orderId`. Khi một farm có nhiều đợt khảo sát, FE cần hiển thị đúng kết quả mà Owner đang khiếu nại.
- Mỗi request thay đổi trạng thái phải dùng `expectedVersion` mới nhất; phiên bản cũ sẽ gây xung đột.
- Nếu API trả `401/403`, kiểm tra token và vai trò. Nếu trả `404` ở API cây, kiểm tra `farmId` và tenant. Nếu trả `409` ở luồng hoàn tiền, kiểm tra trạng thái đơn, kết quả, hồ sơ và `version` trước khi thử lại.
- Sau khi cập nhật BE trên máy đang chạy, khởi động lại API để route lấy cây mới có hiệu lực. Tài liệu này không thực thi migration hay giao dịch ngân hàng.
