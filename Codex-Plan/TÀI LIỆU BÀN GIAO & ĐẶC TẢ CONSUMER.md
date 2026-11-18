# Bàn giao BE1–BE2: Plant Health V4 pending ingestion (7A)

> **V3 scope notice — 2026-11-10.**
> Runtime handoff baseline 7A: the V4 wire/queue and strict ingestion details below remain unchanged. For new v3 work, review MAP/COMBO and internal approved map semantics under a new compatible contract; current pending ingestion is not proof of settlement-gated release support.
> Current: [Business Rules v3](Business-Rules-v3.md), [ADR 0018](../docs/adr/0018-v3-paid-map-combo-business-policy.md), [target contracts](../docs/contracts/survey-business-v3.md). Documentation-only update; no runtime change.

> Cập nhật 2026-11-09. Thay thế hướng dẫn ghi 5 bảng legacy và gán MODERATE trước đây.
> Implementation acceptance vẫn chờ Final Test Phase.

## 1. Ownership và luồng

- BE2: AI processing/source snapshots, Outbox producer, V4 processor/handler, Inbox,
  retry/DLQ và đăng ký DI/queue.
- BE1: target no-severity domain/schema, `IPlantHealthPendingImport`, validation,
  transaction writes và dữ liệu cho review/publication 7B–7E.
- Handler gọi port trong cùng scoped publication DbContext; không gọi repository
  chéo module, không có SQL INSERT legacy mẫu để copy vào consumer.

```text
Completed AI job + immutable source snapshot
  → PlantHealthAnalysisReadyV4 Outbox
  → RabbitMQ
  → V4 strict JSON/envelope/payload validation
  → InboxExecutionCoordinator
  → IPlantHealthPendingImport
  → pending + audit + Order PendingReview + Inbox Completed
  → commit → ACK
```

## 2. Contract và routing

- DTO: `AgriDrone.IntegrationContracts.Health.PlantHealthAnalysisReadyV4`.
- Event/routing: `health.analysis-ready.v4`; SchemaVersion: `4`.
- Consumer name: `be1-plant-health-analysis-ready-v4`.
- Queue: `agridrone.be1.plant-health-analysis-ready.v4`.
- ResultNature: `PRELIMINARY_SUSPECTED`; ClassificationNature: `SUSPECTED`.
- Giữ `Findings`, `BoundaryExceptions`, `DiseaseZones` là collections, có thể rỗng.
- Severity/health-level/diagnosis/free-form-advice fields không thuộc payload V4;
  unknown/duplicate fields bị reject tại processor, không silently bỏ qua.
- Full golden envelopes và arrange notes:
  [docs/contracts/plant-health-v4](../docs/contracts/plant-health-v4/README.md).

## 3. Pending persistence

| Thành phần | Cách lưu |
|---|---|
| `survey.survey_results` | Một result/Order, PendingReview, active batch/source/map/nature/limitation metadata |
| `survey.plant_health_import_batches` | Handoff/job/source version, immutable payload/provenance, fingerprint, Supersedes và blocking summary |
| `plant.plant_health_findings` | Canonical source revision, immutable original, observed time, geometry, nullable Plant/candidate và pending/NeedsReview |
| `plant.plant_health_batch_findings` | Link batch tới canonical revisions để không nhân bản cùng source output |
| `plant.plant_health_boundary_exceptions` | Original geometry/evidence và blocking state |
| `plant.disease_zones` | V4 discriminator, HealthLevelId null, original geometry/membership version, guidance suggestions riêng và blocking flag |
| `plant.disease_zone_memberships` | Proposed members, nullable Plant/candidate references; không tự tạo Plant |

Không ghi V4 vào `ConditionDetection`/`PlantScan` bằng severity tạm, không cập nhật
Plant health/latest-reviewed state hoặc publish zone/guidance trong ingestion.
SUNBURN/unknown/Healthy không tự chuyển sang bệnh khác. Nhãn không thuộc configured
suspected set giữ Original và NeedsReview, target suspected class null.

## 4. Idempotency, ordering và lỗi

- MessageId dedup ở Inbox; fingerprint/Tenant được kiểm tra trước ACK duplicate.
- Business dedup theo Tenant/HandoffId, Mission/job và source finding/version.
  Cùng identity khác nội dung trả `IdentityPayloadConflict`.
- FindingVersion bằng monotonic source run version trong job snapshot; reprocess giữ
  source ID khi detector giữ cùng observation ID, thêm revision/Supersedes, không overwrite.
- Chỉ activate revision mới hơn khi result PendingReview. Duplicate hợp lệ vẫn ACK
  sau approval/publication; output mới cho locked result bị từ chối.
- Validate Tenant/Order/Mission/service/capture versions/provenance trước writes.
- Missing dependency/transient failure retry hữu hạn theo RabbitMQ options rồi DLQ.
  Invalid payload/context/version/locked result là permanent failure.
- Validate toàn batch trước stage; lỗi sau stage phải rollback, không return
  PermanentFailure làm coordinator commit dữ liệu dở.

## 5. Source job/provenance prerequisite

Detector chỉ nhận Order-bound Plant Health Mission có published capture map/boundary.
Registry phải có đúng một active DiseaseDetection model với ModelName bằng detector
response và các string metadata trong `AiModelVersion.Metrics`:

```text
datasetId, datasetVersion, annotationRuleVersion,
boundaryPolicyVersion, teamLabelLimitation
```

Các giá trị phải phản ánh pipeline thực tế, khớp catalogue dataset/annotation; không
điền giá trị giả để mở gate. Threshold snapshot lấy confidence threshold thực tế.
Job/observations cũ thiếu provenance phải reprocess; không tự backfill source evidence.
GPS match chỉ là suggestion. Zones/guidance chỉ phát khi có source proposal hợp lệ;
producer hiện không tạo fake guidance UUID từ clustering.

Chi tiết ADR, runbook và gate:

- [ADR 0013](../docs/adr/0013-plant-health-v4-pending-ingestion.md).
- [Plant Health V4 contract và fixtures](../docs/contracts/plant-health-v4/README.md).
- [Pending ingestion SQL runbook](../docs/operations/phase7a-pending-ingestion.sql).
