# BE2-UC00 — Shared Contracts & Integration Foundation

## 1. Thông tin chung

- **Dự án**: AgriDrone — Smart Dragon Fruit Farm Management System Using Drone and AI
- **Mã đồ án**: FA26SE218
- **Use case**: BE2-UC00 — Shared Contracts & Integration Foundation
- **Ngày cập nhật**: 24/08/2026
- **Trạng thái**: `Implemented — chờ BE1 xác nhận Health V1 contracts`
- **Tiến độ BE2**: khoảng `98%`
- **Phạm vi đánh giá**: Integration contracts, validation, Application ports, Outbox/Inbox foundation, RabbitMQ consumer, domain integration state, persistence, migration và build; không bao gồm unit test, integration test hoặc contract test.

## 2. Mục tiêu

UC00 chuẩn hóa nền tảng giao tiếp trước khi BE2 triển khai Mission, Media và AI processing pipeline.

UC00 bảo đảm:

- BE1 và BE2 trao đổi integration event qua contract có schema version.
- Publish event sử dụng Transactional Outbox.
- Consume event sử dụng Transactional Inbox, manual ACK, retry và dead-letter queue.
- Background consumer phục hồi đúng `TenantId`, `ActorId`, `CorrelationId` và `MessageId`.
- AI service nhận request và gửi callback theo contract thống nhất.
- Media được tham chiếu bằng URI và checksum, không truyền file lớn trong event hoặc AI request.
- Callback và integration event có dữ liệu cần thiết để chống xử lý trùng hoặc sai thứ tự.
- BE2 không truy cập trực tiếp Plants/Farms DbContext của BE1.
- Raw AI output không được xem là Official Inspection và không tự cập nhật Plant Current Health.

## 3. Kiến trúc integration foundation

Luồng integration event sử dụng kiến trúc:

```text
Business transaction
    -> ghi Outbox cùng transaction
    -> Outbox Dispatcher
    -> RabbitMQ topic exchange
    -> Consumer queue
    -> Processor deserialize + validate
    -> phục hồi execution context
    -> Handler + Inbox transaction
    -> cập nhật business data
    -> ACK sau khi commit
```

Quy tắc bắt buộc:

- Business handler không gọi `IRabbitMqPublisher` trực tiếp.
- Event chỉ được ghi vào Outbox trong cùng transaction với dữ liệu nghiệp vụ.
- Consumer phải đăng ký bằng `AddIntegrationConsumer<TProcessor>`; chỉ khai báo queue trong configuration không đồng nghĩa với consume queue.
- Payload V1 đã được phát hành phải giữ bất biến; breaking change phải tạo V2.
- Contract không chứa EF entity, DbContext hoặc transport-specific implementation.

## 4. Event envelope và messaging

`IntegrationEventEnvelope<TPayload>` chứa:

```text
MessageId
CorrelationId
TenantId
ActorId
OccurredAt
SchemaVersion
EventType
Payload
```

Messaging foundation đã có:

- Event descriptors và schema version.
- Contract limits và envelope validation.
- Transactional Outbox cho publish.
- Transactional Inbox cho consume và replay protection.
- Publisher confirm và mandatory routing.
- Manual ACK.
- Retry queue có TTL.
- Dead-letter queue riêng cho từng consumer.
- Execution context được phục hồi từ envelope trước khi gọi handler.

## 5. Mapping integration contracts

### 5.1. MappingCandidatesApprovedV1

BE2 sử dụng `MappingCandidatesApprovedV1` để gửi candidate snapshot đã được Manager duyệt cho BE1.

Contract chứa:

- `ApprovalId` làm business idempotency key.
- Mission, Farm và Zone context.
- Expected current map version.
- Algorithm version và parameters.
- Immutable candidate snapshot.

### 5.2. ZoneMapPublishedV1

BE2 consume `ZoneMapPublishedV1` sau khi BE1 publish map version.

Consumer thực hiện:

- Validate envelope và payload.
- Phục hồi Tenant/Correlation context.
- Inbox deduplication theo `MessageId`.
- Kiểm tra Mission thuộc đúng Tenant, Farm và Zone.
- Gắn `MapVersionId` và `ApprovalId` vào Mission.
- Không ghi đè một published map khác đã được liên kết.

Ba thành phần `ZoneMapPublished*` đã được đặt tại:

```text
AgriDrone.Modules.Missions/Infrastructure/Integration/
```

Vị trí này phù hợp vì processor và handler phụ thuộc RabbitMQ consumer infrastructure, EF Core, Inbox coordinator và `MissionsDbContext`.

## 6. AI contracts

Các contract AI đã triển khai:

```text
AI/
├── AiJobRequestV1.cs
├── AiJobCallbackV1.cs
├── AiJobInputV1.cs
├── AiJobOutputV1.cs
├── AiJobTypes.cs
├── AiJobStatuses.cs
├── AiJobInputRoles.cs
└── Validation/
    ├── AiJobRequestV1Validator.cs
    └── AiJobCallbackV1Validator.cs
```

### 6.1. Request contract

`AiJobRequestV1` cung cấp:

- Job, Mission, Tenant và Correlation identifiers.
- Job type và attempt number.
- Model version snapshot.
- Threshold profile snapshot.
- Processing parameters.
- Danh sách input media bằng storage URI và checksum.
- Callback URL.
- UTC request timestamp.

### 6.2. Callback contract

`AiJobCallbackV1` cung cấp:

- Internal Job ID và external AI job ID.
- Tenant và Correlation ID.
- Attempt number.
- Sequence number.
- Processing status và progress.
- Output manifest bằng storage URI.
- Error code, error message và retryable flag.
- UTC event timestamp.

`AttemptNumber` ngăn callback của lần chạy cũ ghi đè lần retry mới. `SequenceNumber` ngăn callback đến sai thứ tự ghi đè trạng thái mới hơn.

### 6.3. Application port

Application định nghĩa `IAiJobClient` với hai operation:

```text
SubmitAsync
CancelAsync
```

Application không phụ thuộc HTTP, Python framework, API key hoặc AI endpoint. Concrete `HttpAiJobClient` thuộc BE2-UC05 — Processing Job Orchestration.

## 7. Media và checksum contracts

Các contract media đã triển khai:

```text
Media/
├── ChecksumAlgorithms.cs
├── MediaChecksumV1.cs
├── MediaUploadRequestV1.cs
├── MediaUploadSessionV1.cs
├── MediaUploadCompletionV1.cs
├── MediaAssetReferenceV1.cs
└── Validation/
    └── MediaUploadRequestV1Validator.cs
```

Thuật toán checksum được hỗ trợ:

```text
MD5
SHA256
```

`SHA256` là lựa chọn ưu tiên. MD5 chỉ được giữ cho trường hợp cần tương thích upload legacy.

Media contract bảo đảm:

- `OperationId` dùng làm idempotency key cho upload.
- Client nhận upload URI thay vì truyền toàn bộ file qua business API.
- Media có `MediaAssetId` bất biến.
- Server có thể đối chiếu file size và checksum khi hoàn thành upload.
- BE1, BE2 và AI service tham chiếu cùng một object qua storage URI.

Application ports đã có:

```text
IObjectStorage
IChecksumCalculator
```

Concrete S3/MinIO adapter và resumable upload implementation thuộc BE2-UC03 và BE2-UC11.

## 8. Health integration contracts

### 8.1. HealthObservationsReadyV1

BE2 phát `HealthObservationsReadyV1` cho BE1 sau khi AI output đã được parse và observation đã resolve được Plant.

Event chứa:

- `HandoffId` làm business idempotency key.
- Mission, Farm, Zone và processing Job context.
- Model version snapshot.
- Threshold profile snapshot.
- Danh sách observation đã resolved.

Mỗi `HealthObservationV1` chứa:

- Observation ID và version.
- Resolved Plant ID.
- Evidence Media Asset ID và storage URI.
- Observed timestamp.
- Condition code và health-level code.
- Severity và confidence.

Ambiguous hoặc unmatched observation không được gửi như resolved health finding.

### 8.2. HealthReviewStateChangedV1

BE2 consume `HealthReviewStateChangedV1` để theo dõi kết quả review phía BE1.

Event chứa:

- Handoff, Mission, Farm và Zone context.
- Monotonic `ReviewVersion`.
- Review state.
- Tổng observation.
- Số pending review.
- Số awaiting field verification.
- Số resolved review.
- UTC changed timestamp.

Các trạng thái contract:

```text
PENDING
AWAITING_FIELD_VERIFICATION
RESOLVED
```

Validator yêu cầu tổng các counter phải bằng `TotalObservations`. Trạng thái `RESOLVED` không được còn pending review hoặc field verification.

## 9. Health Review consumer và domain state

Consumer phía BE2 gồm:

```text
Infrastructure/Integration/
├── HealthReviewStateChangedProcessor.cs
├── HealthReviewStateChangedHandler.cs
└── HealthReviewStateChangedErrorCodes.cs
```

Luồng xử lý:

1. Processor deserialize envelope và validate payload.
2. Execution context được phục hồi từ envelope.
3. Handler mở Inbox transaction.
4. Tìm Mission theo `MissionId`.
5. Kiểm tra Tenant, Farm và Zone context.
6. Gọi `DroneMission.ApplyHealthReviewState`.
7. Lưu Inbox và Mission state cùng transaction.
8. ACK chỉ sau khi commit thành công.

Domain chống replay và out-of-order bằng:

```text
Inbox MessageId
+ HandoffId
+ ReviewVersion
```

Quy tắc state:

- Event có version nhỏ hơn version hiện tại bị bỏ qua.
- Cùng version và cùng snapshot được xử lý idempotent.
- Cùng version nhưng khác snapshot bị xem là conflict.
- Mission không được `ProcessingStatus.Completed` nếu còn pending review hoặc field verification.
- Event Health Review chỉ được áp dụng cho Mission loại `HealthInspection`.

## 10. RabbitMQ topology

Các consumer name và queue liên quan BE2:

| Consumer | Queue | Routing key | Process nhận |
|---|---|---|---|
| `be2-zone-map-published-v1` | `agridrone.be2.zone-map-published.v1` | `mapping.zone-map-published.v1` | BE2 |
| `be2-health-review-state-changed-v1` | `agridrone.be2.health-review-state-changed.v1` | `health.review-state-changed.v1` | BE2 |

Các queue dành cho BE1 nhưng được khai báo trong shared topology:

| Consumer | Queue | Routing key | Process nhận |
|---|---|---|---|
| `be1-mapping-candidates-approved-v1` | `agridrone.be1.mapping-candidates-approved.v1` | `mapping.candidates-approved.v1` | BE1 |
| `be1-health-observations-ready-v1` | `agridrone.be1.health-observations-ready.v1` | `health.observations-ready.v1` | BE1 |

BE2 chỉ đăng ký processor cho hai consumer của BE2. Việc khai báo queue của BE1 không làm BE2 consume queue đó.

## 11. Database và migration

Migration UC00 Health Review:

```text
20260824152712_AddHealthReviewIntegrationFoundation
```

Migration bổ sung vào `mission.drone_missions`:

```text
health_review_handoff_id
health_review_version
health_review_state
health_review_total
health_review_pending
health_review_awaiting_field_verification
health_review_resolved
health_review_changed_at
```

Migration cũng tạo:

- Unique filtered index `ux_drone_missions_health_review_handoff`.
- Check constraint `ck_drone_missions_health_review_counts`.

Migration đã được áp dụng vào database local. Kết quả `dotnet ef migrations list` không còn migration nào ở trạng thái pending.

Trong lần `database update`, các migration được áp dụng thành công nhưng bước `SystemRoleSeeder` sau migration báo lỗi vì alias PostgreSQL `current_role`. Lỗi seeder thuộc shared Database/Identity foundation từ BE1, không làm rollback migration Health Review và không được tính vào tiến độ UC00 của BE2.

## 12. Thành phần đã triển khai

```text
AgriDrone.IntegrationContracts/
├── AI/
├── Health/
├── Media/
├── Mapping/
└── Messaging/

AgriDrone.Modules.Missions/
├── Application/Abstractions/
│   ├── AI/
│   │   └── IAiJobClient.cs
│   └── Media/
│       ├── IObjectStorage.cs
│       └── IChecksumCalculator.cs
├── Domain/Missions/
│   ├── DroneMission.cs
│   └── MissionHealthReviewState.cs
├── Infrastructure/Integration/
│   ├── ZoneMapPublished*.cs
│   └── HealthReviewStateChanged*.cs
├── Infrastructure/Persistence/Configurations/
│   └── DroneMissionConfiguration.cs
└── DependencyInjection.cs

AgriDrone.Database/Migrations/
└── 20260824152712_AddHealthReviewIntegrationFoundation*
```

## 13. Kết quả xác minh

Các project sau đã build thành công:

```text
AgriDrone.IntegrationContracts
AgriDrone.Modules.Missions
AgriDrone.Database
AgriDrone.Api
AgriDrone.sln
```

Kết quả build toàn solution:

```text
Build succeeded
0 Warning(s)
0 Error(s)
```

Test không nằm trong phạm vi tiến độ theo quyết định của nhóm BE2 nên không được dùng làm điều kiện đánh dấu hoàn thành tài liệu này.

## 14. Phần còn lại trước khi khóa UC00

BE2 đã hoàn thiện phần triển khai kỹ thuật của UC00. Công việc phối hợp còn lại:

1. BE1 xác nhận payload `HealthObservationsReadyV1`.
2. BE1 xác nhận payload và ordering policy của `HealthReviewStateChangedV1`.
3. Hai bên thống nhất rằng V1 là immutable; breaking change sử dụng V2.
4. Đưa toàn bộ file UC00 hiện còn untracked/modified vào commit phù hợp trước khi merge.

Các hạng mục sau không thuộc UC00 và không chặn việc hoàn tất foundation:

- Concrete HTTP AI adapter: BE2-UC05.
- AI callback endpoint và attempt persistence: BE2-UC05.
- S3/MinIO storage adapter và resumable upload: BE2-UC03/UC11.
- Parse mapping AI output: BE2-UC06.
- Parse và handoff health observation thực tế: BE2-UC09.
- Sửa `SystemRoleSeeder`: shared Database/Identity foundation của BE1.

## 15. Bước tiếp theo

Sau khi BE1 xác nhận hai Health V1 contracts, UC00 có thể chuyển sang trạng thái `Done`.

Use case BE2 tiếp theo trong dependency chain:

```text
BE2-UC02 — Mission Lifecycle
```

UC02 sử dụng Drone availability từ UC01 và integration/correlation foundation từ UC00 để triển khai state machine của `DroneMission`.
