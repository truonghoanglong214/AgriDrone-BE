# BE2 Handover Note: Baseline Survey Without Pre-Existing Boundary & Lifecycle Sync

**Tài liệu bàn giao tích hợp giữa BE1 (Surveys, Farms, Identity) và BE2 (Missions, Media, Processing)**  
**Ngày phát hành:** 21/10/2026  
**Nhánh Git:** `LongTH`  
**Phiên bản:** 1.0  

---

## 1. Bối cảnh & Thay đổi kiến trúc (Architectural Pivot)

Trong kiến trúc ban đầu, hệ thống yêu cầu nông trại phải có ranh giới chính thức (`FarmBoundaryVersionId`) trước khi lập kế hoạch và bay Baseline. Điều này tạo ra rào cản luẩn quẩn:
- Nông trại mới chưa khảo sát thì chưa có ranh giới chính thức.
- Nhưng hệ thống lại chặn không cho bay Baseline nếu chưa có ranh giới (`FarmBoundaryVersionId == null`).

### Kiến trúc chuẩn hóa mới:
1. **Trước khi bay Baseline:** Khách hàng (`TenantOwner`) xác nhận lịch hẹn và thanh toán tiền cọc cam kết cố định **500.000 VNĐ** (`PaymentPurpose = BaselineDeposit`). Khi tiền cọc xác nhận `Confirmed` + Giấy phép bay & Cam kết dữ liệu hợp lệ $\rightarrow$ Đơn hàng chuyển sang `BaselineReady`.
2. **Lập kế hoạch bay khảo sát toàn cảnh (Candidate Survey Envelope):** Drone bay khảo sát toàn cảnh nông trại dựa trên **tâm tọa độ ước lượng và bán kính vùng quét** (`CandidateCenterLongitude`, `CandidateCenterLatitude`, `SurveyRadiusMeters`) lấy từ ngữ cảnh nông trại của BE1. **Không bắt buộc `FarmBoundaryVersionId`**.
3. **Xử lý ảnh Orthophoto & Chấm 4 góc ranh giới:** Sau chuyến bay, ảnh không gian (aerial imagery) được pipeline Metashape ghép thành ảnh trực giao Orthophoto. SystemManager mở Orthophoto và chấm 4 điểm góc thực tế (`MarkFarmBoundaryOnOrthophoto`) để tạo và phê duyệt `FarmBoundary` chính thức.
4. **Đếm trụ & Chốt giá:** AI đếm số trụ thanh long trong ranh giới đã duyệt $\rightarrow$ SystemManager duyệt số trụ $\rightarrow$ Xuất bản bản đồ cơ sở $\rightarrow$ Hệ thống chốt giá và tự động cấn trừ 100% tiền cọc 500.000 VNĐ vào giá trị hợp đồng dịch vụ chính thức.

---

## 2. Các thành phần BE1 đã cung cấp và sẵn sàng

### 2.1. Cập nhật Ngữ cảnh vận hành đơn hàng (`SurveyOrderOperationalContextV3` & `V4`)
Tại namespace `AgriDrone.IntegrationContracts.Surveys`:
- Thuộc tính `FarmBoundaryVersionId` **được phép mang giá trị `null`** đối với đơn hàng khảo sát cơ sở (`RequiresBaselineMapping = true`).
- Bổ sung thông tin bao đóng không gian ứng viên (Candidate Envelope):
  - `double? CandidateCenterLongitude`: Kinh độ tâm ước lượng của nông trại (WGS84).
  - `double? CandidateCenterLatitude`: Vĩ độ tâm ước lượng của nông trại (WGS84).
  - `decimal? ApproximateAreaHa`: Diện tích ước tính (hecta).
  - `double? SurveyRadiusMeters`: Bán kính khảo sát an toàn (mét), được BE1 tính toán từ diện tích ước tính ($R = \sqrt{\frac{Area \times 10000}{\pi}} \times 1.25$).

### 2.2. Hợp đồng sự kiện tích hợp (Integration Events)
Tại namespace `AgriDrone.IntegrationContracts.Missions`:

1. **`BaselineMissionStartedV1`**:
   ```csharp
   public sealed record BaselineMissionStartedV1(
       Guid EventId,
       DateTimeOffset OccurredAt,
       Guid TenantId,
       Guid FarmId,
       Guid SurveyOrderId,
       Guid MissionId,
       Guid DroneId,
       Guid PilotUserId,
       DateTimeOffset StartedAt);
   ```
   - **Topic/Type:** `system.missions.baseline_mission_started.v1`
   - **Tác vụ:** Khi BE2 phát sự kiện này, BE1 Consumer (`BaselineMissionStartedHandler`) sẽ tự động chuyển trạng thái của `SurveyOrder` từ `BaselineReady` sang `BaselineInProgress`.

2. **`BaselineOrthophotoCompletedV1`**:
   ```csharp
   public sealed record BaselineOrthophotoCompletedV1(
       Guid EventId,
       DateTimeOffset OccurredAt,
       Guid TenantId,
       Guid FarmId,
       Guid SurveyOrderId,
       Guid MissionId,
       string OrthophotoStoragePath,
       string? OrthophotoPublicUrl,
       int? ImageWidth,
       int? ImageHeight,
       DateTimeOffset CompletedAt);
   ```
   - **Topic/Type:** `system.missions.baseline_orthophoto_completed.v1`
   - **Tác vụ:** Khi pipeline xử lý ảnh ghép Orthophoto hoàn tất, BE2 phát sự kiện này. BE1 Consumer (`BaselineOrthophotoCompletedHandler`) sẽ tự động chuyển trạng thái của `SurveyOrder` sang `AwaitingBaselineReview`.

### 2.3. Bổ sung mục đích thanh toán (`PaymentPurpose`)
- `PaymentPurpose.BaselineDeposit`: Dành cho khoản cọc cam kết 500.000 VNĐ khi bắt đầu quy trình Baseline.
- `PaymentPurpose.FinalBalance`: Dành cho thanh toán số tiền còn lại sau khi đã chốt số trụ và cấn trừ tiền cọc.
- Ràng buộc duy nhất 1 giao dịch active cho mỗi mục đích thanh toán trên cùng 1 đơn hàng (`uq_survey_payments_one_active_per_order_purpose`).

---

## 3. Các điểm nhóm BE2 cần điều chỉnh trong module `AgriDrone.Modules.Missions`

### 3.1. Sửa `PrepareMissionSetCommandHandler.cs`
**Hiện trạng:**
- Đang reject nếu `order.FarmBoundaryVersionId == null`.
- Sau đó truy cập `order.FarmBoundaryVersionId!.Value` gây ngoại lệ `NullReferenceException`.

**Hướng dẫn sửa:**
```csharp
// 1. Kiểm tra ranh giới tùy theo mục đích chuyến bay:
if (order.RequiresBaselineMapping)
{
    // Với chuyến bay Baseline, không bắt buộc FarmBoundaryVersionId.
    // Lập kế hoạch bay toàn cảnh dựa trên Candidate Envelope:
    var centerLon = order.CandidateCenterLongitude ?? farm.Longitude;
    var centerLat = order.CandidateCenterLatitude ?? farm.Latitude;
    var radiusMeters = order.SurveyRadiusMeters ?? 200.0;

    // Sinh đường bay bao phủ hình tròn / hình hộp bao quanh tâm ứng viên:
    missionPlan = GenerateCandidateSurveyGrid(centerLon, centerLat, radiusMeters);
}
else
{
    // Với chuyến bay dịch vụ (Plant Health, Harvest Readiness): Bắt buộc có ranh giới đã duyệt.
    if (order.FarmBoundaryVersionId is null)
    {
        return Result.Failure<PrepareMissionSetResponse>(
            MissionPlanningError.FarmBoundaryRequired("Chuyến bay dịch vụ yêu cầu ranh giới nông trại đã được phê duyệt."));
    }

    missionPlan = GeneratePolygonBoundarySurveyGrid(order.FarmBoundaryVersionId.Value);
}
```

### 3.2. Sửa `TransitionMissionCommandHandler.cs`
Bổ sung việc phát sự kiện tích hợp đồng bộ vòng đời sang BE1:
1. Khi bắt đầu bay Baseline (`purpose == MissionPurpose.BaselineMapping` và `targetStatus == MissionStatus.InFlight`):
   - Publish sự kiện `BaselineMissionStartedV1` lên Message Bus (Outbox).
2. Khi hoàn tất xử lý ghép ảnh Orthophoto (`ProcessingStatus.Completed`):
   - Publish sự kiện `BaselineOrthophotoCompletedV1` kèm đường dẫn file ảnh Orthophoto.

---

## 4. Bảng tóm tắt tương tác giữa 2 bên

| Hành động | Phía thực hiện | Hợp đồng / Cơ chế | Kết quả mong đợi |
|---|---|---|---|
| Lấy ngữ cảnh lập kế hoạch | BE2 gọi BE1 | `SurveyOrderOperationalContextV3/V4Query` | Nhận `CandidateCenterLongitude/Latitude`, `SurveyRadiusMeters`, `FarmBoundaryVersionId = null` |
| Bắt đầu chuyến bay Baseline | BE2 | Publish `BaselineMissionStartedV1` | BE1 cập nhật đơn sang `BaselineInProgress` |
| Hoàn tất ghép Orthophoto | BE2 | Publish `BaselineOrthophotoCompletedV1` | BE1 cập nhật đơn sang `AwaitingBaselineReview` |
| Chấm 4 góc trên Orthophoto | BE1 (SystemManager) | `POST /api/system-manager/survey-orders/{id}/boundary/mark-orthophoto-corners` | Phê duyệt và lưu `FarmBoundaryVersionId` vào đơn hàng |
| Đếm trụ & Xuất bản Base Map | BE1 + AI | `FarmBaseMapPublishedV3` | Chốt số trụ, chuyển đơn sang `AwaitingPricing` và cấn trừ cọc |
