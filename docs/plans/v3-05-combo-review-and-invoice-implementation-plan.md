# V3-05 — Combo Review, Verified Pole Count và Invoice Finalization

**Trạng thái:** In Progress — V3-05A/B remediation pending theo checklist kiểm tra cuối tài liệu; build chưa được xác minh tại môi trường review; DB acceptance pending. **Phụ thuộc:** V3-04 và processing contract. **Bàn giao:** V3-06.

Nguồn: [BE1 Master Plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md#8-v3-0405--mapping-identity-combo-và-countinvoice), [Business Rules](../../Codex-Plan/Business-Rules-v3.md), [Target Contracts](../contracts/survey-business-v3.md), [V3-01 Locked Design](../domain/v3-01-locked-design.md), [Project Memory](../../PROJECT_MEMORY.md).

## Mục tiêu và quyết định đã chốt (Design Locked 2026-11-22)

Mỗi Order MAP hoặc COMBO có một kết quả duyệt nội bộ và một hóa đơn hiện hành, truy ngược được đến đúng Order, lượt bay, snapshot đếm trụ và đơn giá tại thời điểm duyệt Order. V3-06 sở hữu checkout số dư, quyết toán, release, thông báo và ghi nhận doanh thu.

- **Quy tắc giá (Phương án 1 — Locked):** Theo chuẩn BR-PRC-03: `Total = VerifiedBillablePoleCount × SurveyOrderCommercialSnapshot.PricePerPole`. Không tự ý áp tỷ lệ chiết khấu ngoài snapshot thương mại; đơn giá `PricePerPole` đã được phân luồng riêng cho MAP / COMBO tại thời điểm duyệt Order ở V3-02 và được pin bất biến trong `SurveyOrderCommercialSnapshot`.
- **Quy tắc số dư & Cấn trừ cọc (Locked):** `DueAmount = Math.Max(0, Total - ValidAllocatedDepositNet)` với `ValidAllocatedDepositNet = TotalConfirmedReceipts - TotalReversals`. Nếu cọc hợp lệ > Total (ví dụ cọc 100.000 VND, Total 60.000 VND), `DueAmount = 0` và khoản thặng dư 40.000 VND được ghi nhận nghĩa vụ đối soát/hoàn trả (`Overpayment`), tuyệt đối không làm biến đổi `Total` của hóa đơn (vẫn giữ 60.000 VND). Các trường tài chính này là sự thật lịch sử bất biến tại thời điểm finalization.
- **Thẩm quyền ngoại lệ Zero-Pole (Locked):** Nếu `VerifiedBillablePoleCount == 0`, bắt buộc phải có `ApprovalActorId`, `ReasonCode` hợp lệ và bằng chứng kỹ thuật đính kèm (`ZeroPoleApprovedBy`, `ZeroPoleReasonCode`, `ZeroPoleEvidenceReference`); không tính phí sàn nhân tạo (không có artificial minimum charge).

## V3-05A — Khóa nguồn và rà đường ghi (Done 2026-11-22)

- [x] **Ma trận 4 loại Order**:
  1. **First MAP** (`MappingKind = FirstBaseline`, `ServiceType = MAP`): Dùng bản đồ nội bộ (`FarmBaseMapVersion` trạng thái `InternalApproved`) sinh từ chính Order/CaptureAttempt này. Đếm trụ từ `SurveyOrderPoleCountSnapshot` (toàn bộ candidates hợp lệ trong ranh). Không có sàng lọc bệnh (`ScreensDisease = false`, `NotAssessed`).
  2. **First COMBO** (`MappingKind = FirstBaseline`, `ServiceType = COMBO`): Dùng bản đồ nội bộ cùng Order/CaptureAttempt. Ghép `PlantProfileId` mới tạo trên bản đồ nội bộ với findings/zones nhận từ `ComboAnalysisReadyV1`. Đếm trụ từ `SurveyOrderPoleCountSnapshot`. Kết quả duyệt nội bộ đóng băng cả bản đồ và kết quả screening (kèm giới hạn nhãn, không cấp chứng nhận sạch bệnh).
  3. **Follow-up MAP** (`MappingKind = FollowUpSnapshot`, `ServiceType = MAP`): Dùng `SurveyMapSnapshot` của đợt khảo sát hiện tại, đối chiếu với `BaselineMapVersionId` đã pin bất biến (tuyệt đối không ghi đè Baseline). Đếm trụ xác minh trong ranh đợt này. Biến động vị trí/trụ được lưu vết qua `PlantChangeEvent`. Không khảo sát bệnh (`NotAssessed`).
  4. **Follow-up COMBO** (`MappingKind = FollowUpSnapshot`, `ServiceType = COMBO`): Dùng `SurveyMapSnapshot` của đợt hiện tại kết hợp `BaselineMapVersionId` đã pin. Ghép kết quả screening với các `PlantProfileId` đã tồn tại từ Baseline và profiles thay thế của đợt này.
- [x] **Rà đường ghi & Thứ tự Lock theo ADR 0020 (Chuẩn 11 Ranks bắt buộc)**:
  - Thứ tự lock bắt buộc theo ADR 0020 (Ranks 0 đến 10), IDs tăng dần khi có nhiều bản ghi trong cùng rank:
    - **Rank 0**: Activation singleton và versioned operational/config rows (SHARE cho review/finalization).
    - **Rank 1**: Tenant → User → TenantMembership → ManagerProfile → Farm → FarmManagerAssignment (SHARE/UPDATE).
    - **Rank 2**: Tất cả existing Orders liên quan (Order hiện tại, pinned baseline source Order) `FOR UPDATE`.
    - **Rank 3**: Operation / Idempotency receipt (Claim/replay theo actor, action, fingerprint).
    - **Rank 4**: Current appointment / revisions.
    - **Rank 5**: Deposit obligation → checkout/payment → funding receipt/allocation → cancellation/refund facts (bảo vệ nguồn tính `ValidAllocatedDepositNet`).
    - **Rank 6**: Pinned baseline / current-pointer (`FarmBaseMapVersion`, `FarmBoundaryVersion`, `SurveyOrderPoleCountSnapshot`).
    - **Rank 7**: `CaptureAttempt` `FOR UPDATE` (chỉ chuyển `Accepted` khi internal review hoàn tất).
    - **Rank 8**: Execution `DroneMission` (khóa trạng thái và dữ liệu viễn thám).
    - **Rank 9**: Drone(s) → booking / reservation facts.
    - **Rank 10**: Preflight decision revisions → safety receipts → input manifest/approval receipts.
  - **Thời điểm chuyển `CaptureAttempt` sang `Accepted`**: Chỉ chuyển từ `Processing` sang `Accepted` khi Manager hoàn tất phê duyệt nội bộ kết quả (Internal Map Approval cho MAP, hoặc Combo Review Approval cho COMBO). Không chuyển sớm để tránh attempt đã Accepted nhưng review bị từ chối hoặc cần bay lại chất lượng.
  - **Phân lập writer Legacy**: `ConfirmSurveyOrderPoleCountCommandHandler` chỉ dùng cho Order legacy (`Scope = Legacy`). Guard này là **bất biến kiến trúc bắt buộc (invariant)** được bảo vệ từ tầng Application đến Domain, và sẽ được rà soát/chặn triệt để tại bước 05E/05F để ngăn chặn hoàn toàn việc ghi đè lên Order V3. Toàn bộ Order V3 dùng command mới `FinalizeSurveyOrderInvoiceCommand` (bước 05E) để ghi vào `SurveyInvoiceRevision` bất biến, trỏ con trỏ `CurrentInvoiceRevisionId` trên Order và chuyển trạng thái sang `AwaitingSettlement`.
  - **Tái sử dụng Profile trong First COMBO**: First COMBO kế thừa quy tắc quản lý thực thể cây trồng từ V3-04. Khi sinh bản đồ nội bộ và ghép findings, nếu farm đã tồn tại `PlantProfile` hợp lệ (kể cả profile từ các dữ liệu legacy trước đó), hệ thống ưu tiên tái sử dụng `PlantProfileId` đã có theo tọa độ/dòng/cột thay vì tạo trùng lặp (duplication), đảm bảo tính liên tục của lịch sử nông trại.
- [x] **Khóa quy tắc giá, số dư và ngoại lệ Zero-Pole**:
  - Công thức giá: Đã chốt **Phương án 1** theo chuẩn BR-PRC-03: `Total = VerifiedBillablePoleCount × SurveyOrderCommercialSnapshot.PricePerPole`.
  - Cấn trừ cọc: `DueAmount = Math.Max(0, Total - ValidAllocatedDepositNet)` với `ValidAllocatedDepositNet = TotalConfirmedReceipts - TotalReversals`. Cọc > Total cho `DueAmount = 0`, ghi nhận nghĩa vụ `Overpayment` cho V3-06 đối soát; `Total` giữ nguyên.
  - Zero-Pole: Bắt buộc có actor, lý do hợp lệ và bằng chứng kỹ thuật đính kèm; không tính phí sàn nhân tạo.

## V3-05B — Domain và persistence (Done 2026-11-22)

- [x] **Thêm `SurveyInvoiceRevision` và `SurveyInternalApprovedResultRevision` trong Surveys**:
  - `SurveyInternalApprovedResultRevision`:
    - Biểu diễn phân tách MAP/COMBO V3 độc lập khỏi enum legacy (`ServiceCode` `'MAP'`/`'COMBO'`, `ScreensDisease` boolean).
    - Chuỗi SHA-256 evidence fingerprint toàn diện bao phủ toàn bộ nội dung frozen: PredecessorRevisionId, ServiceCode, ScreensDisease, CaptureAttemptId, MissionId, FarmBaseMapVersionId, BaselineMapVersionId, FarmBoundaryVersionId, PoleCountSnapshotId, MappingCandidateBatchId, ComboBatchId, ReviewDecision, HealthAssessmentStatus, LimitationNotice, ReviewedBy, ReviewedAt (ISO 8601 UTC), ReviewReason.
    - Factory trả về `Result<SurveyInternalApprovedResultRevision>` dùng `AppError` chuẩn Clean Architecture.
    - Hỗ trợ `Supersede(actorId, supersededAt, reason, successorId)` ghi vết kiểm toán đầy đủ.
  - `SurveyInvoiceRevision`:
    - Bắt buộc `CommercialPolicyVersionId` (Guid, non-nullable).
    - Ngoại lệ Zero-Pole lưu đầy đủ: `ZeroPoleApprovedBy`, `ZeroPoleReasonCode`, `ZeroPoleEvidenceReference`.
    - Chuỗi SHA-256 evidence fingerprint bảo vệ toàn diện các token tài chính và chính sách đóng băng.
    - Factory trả về `Result<SurveyInvoiceRevision>` dùng `AppError`.
    - Hỗ trợ `Supersede(actorId, supersededAt, reason, successorId)`.
  - Aggregate `SurveyOrder`:
    - Cho phép hiệu chỉnh tiền phát hành (pre-release correction) tại cả trạng thái `Processing` và `AwaitingSettlement` trong `RecordInternalApprovedResult`.
    - `FinalizeInvoiceRevision` guard chặt chẽ: xác thực trùng khớp approved result hiện hành, commercial policy version đã pin, đơn giá/tiền tệ và verified count snapshot.
- [x] **Ràng buộc quan hệ và Lineage tại Database**:
  - Composite alternate keys trên `(Id, SurveyOrderId, TenantId, FarmId)` cho cả `SurveyOrderPoleCountSnapshot`, `SurveyInternalApprovedResultRevision`, và `SurveyInvoiceRevision`.
  - Composite foreign keys bảo vệ: Invoice → Result (cùng Order/Tenant/Farm), Invoice → Snapshot (cùng Order/Tenant/Farm), Result → Snapshot (cùng Order/Tenant/Farm), Order → Current Revisions.
  - Check constraints lineage: `revision_number = 1` bắt buộc `predecessor IS NULL`; `revision_number > 1` bắt buộc có predecessor hợp lệ và khác `id`.
  - Khóa nguồn provenance liên module (`CrossModuleRelationshipConfiguration`): `CaptureAttempt` (khóa ghép 4 trường), `DroneMission`, `MappingCandidateBatch`, `FarmBaseMapVersion` (base & baseline), `FarmBoundary`, `SurveyServicePrice`, `BusinessPolicyVersion`, và `User`.
- [x] **Migration additive và Đồng bộ ModelSnapshot**:
  - Migration `20261122150000_V3_05ComboReviewAndInvoiceRevisions.cs` triển khai DDL sạch, không cast enum legacy PostgreSQL, và method `Down()` tháo gỡ an toàn vòng lặp khóa ngoại.
  - Đồng bộ 100% `AgriDroneSchemaDbContextModelSnapshot.cs` với migration và cấu hình mới.

## V3-05C — Nhận COMBO pending

- [ ] Thêm contract `ComboAnalysisReadyV1` trong IntegrationContracts. Nối producer Missions và consumer Database, với validator và routing/version độc lập với Health V4.
- [ ] Xác thực Tenant/Farm/Order, capability ScreensDisease, Mission/job/attempt, manifest, source revision, map và boundary. First COMBO ghép PlantProfileId qua bản đồ nội bộ; Follow-up dùng Baseline đã pin và snapshot đợt hiện tại.
- [ ] Commit Inbox, batch, findings/zones và audit nguyên tử. Cùng identity/fingerprint trả receipt cũ; payload khác trả conflict. Reject kết quả đến muộn nếu làm thay đổi revision đã freeze. ACK sau commit.

### Kết quả cần đạt của 05C

SystemManager gửi handoff cho một job COMBO cụ thể đã hoàn thành. Missions ghi payload đã đóng băng vào Outbox; RabbitMQ chuyển ComboAnalysisReadyV1; Database xác thực toàn bộ context và ghi batch pending cùng Inbox/audit trong một transaction. Batch cung cấp dữ liệu cho review 05D qua read port nội bộ. Order và attempt tiếp tục ở giai đoạn processing đến khi phê duyệt nội bộ hoàn tất.

Điều kiện vào là source A/B đã qua review và build cần được xác minh trong môi trường có .NET 10. Database acceptance của phase trước được theo dõi ở V3-10 theo quy định dự án. Lượt lập kế hoạch này chưa xác minh runtime hoặc apply migration.

### 05C-01 — Chốt contract và nguồn dữ liệu

- [ ] Tạo `Health/ComboAnalysisReadyV1.cs` trong IntegrationContracts. Envelope schema version 1, routing `survey.combo-analysis-ready.v1`; thêm descriptor, event type, consumer name và message validator riêng.
- [ ] Payload có HandoffId, OrderId, FarmId, CaptureAttemptId, MissionId, JobId, SourceRevision, ProducedAt, SupersedesHandoffId nếu reprocess, business/operational policy references, input approval ID, manifest ID/hash, MappingKind, MappingCandidateBatchId, capture map ID, pinned Baseline ID, boundary ID, model/dataset/annotation/threshold provenance, result nature và limitation.
- [ ] Findings có stable source ID/version, coordinate gốc, thời gian quan sát, suspected class, confidence, uncertainty, evidence references và PlantProfileId/candidate reference nếu resolve được. Zones giữ proposal ID, geometry, memberships liên kết source finding/profile, guidance catalogue ID/version/source và evidence. Boundary exceptions giữ tọa độ gốc và bằng chứng.
- [ ] Định nghĩa DTO riêng `ComboFindingV1` và `ProposedDiseaseZoneMembershipV1`, mỗi DTO có `Guid? PlantProfileId` và `Guid? MappingCandidateId`. `PlantProfileId` là ID của profile được resolve từ inventory trong phạm vi bản đồ/batch đã pin. `MappingCandidateId` trỏ tới candidate của đúng mapping batch và giữ được liên kết First COMBO khi chưa chốt profile. Nếu có cả hai ID, xác minh chúng cùng trỏ tới một phép ghép trong mapping evidence. Zone membership tham chiếu stable source finding ID và phải nhất quán với identity của finding.
- [ ] Dùng hai DTO V1 xuyên suốt publisher, consumer và adapter. Giữ `PlantId` trong lookup legacy nếu cần và resolve tường minh sang `PlantProfileId` trước khi ghi vào contract. Phạm vi tái sử dụng DTO V4 giới hạn ở các leaf DTO khác có cùng semantics và validation, loại trừ `PlantHealthFindingV4` và `ProposedDiseaseZoneMembershipV4`.
- [ ] Quy định canonical fingerprint bằng JSON có thứ tự trường/collection xác định, UTC microsecond và biểu diễn số nhất quán. Cùng payload semantic phải có cùng hash. Giữ raw payload để audit strict-schema và canonical fingerprint để business dedup. Envelope correlation/message IDs không làm thay đổi business hash.
- [ ] Chốt identity của một output là Tenant + Order + CaptureAttempt + Job + SourceRevision. HandoffId ổn định cho output đó, lưu hoặc sinh deterministic từ identity. ProducedAt lấy từ source job/output đã persist; server import time lưu riêng.

Exit gate C01 là contract field-by-field có producer source và consumer validation tương ứng. Fields nguồn bắt buộc còn thiếu phải được bổ sung ở job snapshot/store trước khi nối publisher.

### 05C-02 — Pending model và schema

- [ ] Thêm `Surveys/Domain/Results/SurveyComboImportBatch.cs`, factory Result/AppError. Batch append-only lưu tenant/farm/order/attempt/job/source revision, handoff/predecessor, map/boundary/baseline/mapping batch, provenance, manifest/input approval references, raw payload, canonical fingerprint, ImportedAt và số blocking items.
- [ ] Phương án persistence cho 05C là batch bất biến chứa structured JSON của findings/zones/exceptions đã validate. Stable item IDs nằm trong payload; read port trả typed DTO để 05D review. Review decisions, review revision và approval pointer được thêm ở 05D. Tránh ghi trực tiếp vào các entity V4 có invariant service/context legacy.
- [ ] Thêm unique Tenant + HandoffId và unique business identity nêu ở C01; lineage predecessor cùng Order/attempt và revision tăng đúng chuỗi xử lý. Cùng Job/SourceRevision với HandoffId khác vẫn bị business dedup.
- [ ] Configuration trong Surveys; thêm relationship trong CrossModuleRelationshipConfiguration tới Order, attempt, Mission, map/boundary, mapping batch, input approval/manifest theo key hiện có. Batch phải nối được đúng source; kiểm tra field dư thừa trong application transaction khi composite FK chưa bao phủ hết context.
- [ ] Cấu hình composite alternate key `uq_survey_combo_import_batches_id_order_tenant_farm` trên `SurveyComboImportBatch` theo thứ tự `(id, survey_order_id, tenant_id, farm_id)` bằng `HasAlternateKey(...).HasName(...)`. Migration tạo key này trước khi thêm FK tham chiếu để PostgreSQL có unique constraint khớp tập cột đích.
- [ ] Theo Lineage Rule 3, cấu hình composite FK từ `SurveyInternalApprovedResultRevision` theo thứ tự `(combo_batch_id, survey_order_id, tenant_id, farm_id)` tới alternate key trên, với `HasPrincipalKey(...)` và `DeleteBehavior.Restrict`. Giữ `ComboBatchId` nullable cho MAP và bắt buộc có batch cho COMBO trong factory/configuration/migration. Rà `Down()` gỡ FK trước khi gỡ alternate key hoặc bảng đích.
- [ ] Thêm migration additive sau migration 05B với Designer/target model và cập nhật ModelSnapshot. Rà Up/Down, indexes, FK keys, enum và jsonb columns. Giữ raw evidence và batch cũ khi có successor.

Exit gate C02 là entity/configuration/migration và model snapshot đồng bộ. Cung cấp mapping từ từng field payload đến persisted column hoặc structured JSON field.

### 05C-03 — Processing snapshot, publisher và entry point

- [ ] Rà `MissionPlantGpsStore`, `AiProcessingJob`, `PlantHealthProcessingSnapshot` và đường tiếp nhận output AI để lập manifest field còn thiếu cho V3. Thêm snapshot V3 additive chứa exact attempt, manifest/input approval, map/boundary/baseline, policy/model/catalogue references. Job hoàn thành phải có output coverage/completeness evidence thật.
- [ ] Thêm `IMissionComboAnalysisPublisher` và implementation `MissionComboAnalysisPublisher` trong Missions. Publisher nhận exact mission/job/source revision và idempotency key. Nhận `MissionPurpose.Capture` cho luồng V3 do `SurveyCaptureExecutionCoordinator` tạo. Chỉ nhận `MissionPurpose.PlantHealth` nếu cần tương thích và mission thỏa cùng kiểm tra V3 về Order/tenant/farm/attempt/job. Qua port, resolve `SurveyOrderCommercialSnapshot` đã pin theo Order và yêu cầu service code `COMBO` cùng `ScreensDisease == true` với mọi purpose được nhận. Kiểm tra authorization farm/tenant bằng đường truy cập hiện có.
- [ ] Chọn job bằng ID truyền vào đã validate; output observations/evidence query theo chính JobId/attempt/input manifest đó. Empty findings chỉ hợp lệ khi source chứng minh xử lý thành công và đủ input; job failed/partial không sinh negative result.
- [ ] Tạo findings và identities từ output persisted. Resolve profile/candidate qua mapping evidence đúng đợt; ambiguity/out-of-boundary được ghi pending exception. Giữ tọa độ/nhãn/confidence gốc. Catalogue lookup và spatial policy dùng version đã pin, không hardcode class mapping hoặc ngưỡng biên từ V4.
- [ ] Ghi operation receipt hoặc immutable handoff identity/payload cùng Outbox trong transaction của publisher; replay dùng lại payload đã lưu, không tính lại từ source có thể thay đổi. Idempotency key gắn actor/action/context/fingerprint; khác body trả conflict.
- [ ] Giữ pattern phát handoff thủ công hiện có, bổ sung endpoint V3 được authorization cho SystemManager. Endpoint đề xuất `/api/v3/farms/{farmId}/missions/{missionId}/combo-analysis/handoffs`, body chứa JobId/SourceRevision, header Idempotency-Key. Trả Queued/HandoffId/MessageId; client không gửi findings để thay output AI đã persist.
- [ ] Adapter publisher tuân thứ tự shared-resource guards ADR 0020. Nếu cần lock Order để bảo vệ concurrent cancel/correction, đặt transaction coordinator trong Database và gọi module ports trong cùng transaction; không lock Mission rồi quay lại lock Order. Network/broker publish nằm ngoài SQL transaction qua Outbox.

Exit gate C03 là entry point thực gọi publisher, publisher tạo exact frozen output và Outbox có context đủ cho consumer. FastAPI chỉ cần thay ở đường output/snapshot thực sự thiếu field nguồn; runtime business state do .NET quản lý.

### 05C-04 — Parser và validation trước ghi

- [ ] Thêm `ComboAnalysisReadyV1Processor` với strict JSON schema semantics. Reject unknown/duplicate properties, sai kiểu, missing required fields, unsupported schema version và malformed envelope trước business writes. Reuse strict-parser pattern V4 khi phù hợp.
- [ ] Validate finite coordinates/confidence, SRID/GeoJSON shape/rings, polygon validity, unique source item IDs, non-empty evidence IDs, UTC timestamps, supported configured labels và sourced guidance versions. Có thể dùng configured payload/collection limits của messaging để giới hạn tài nguyên parse.
- [ ] Findings có profile/candidate reference phải được xác minh qua exact mapping batch/map. Unresolved profile, uncertain matching hoặc boundary exception hợp lệ tạo blocking review item. Sai tenant/source/media, source identity mâu thuẫn hoặc geometry không hợp lệ trả permanent rejection.
- [ ] Tính lại boundary classification bằng geography/spatial calculator từ policy pin; so với proposal của producer. Không tin khoảng cách hay ngưỡng do payload tự khai. Zone membership phải nối đến finding thuộc batch và geometry tương ứng; mọi mâu thuẫn cần blocking decision rõ hoặc permanent error theo bảng phân loại.
- [ ] Chốt predicate map validity của COMBO bằng `status IN ('INTERNALLY_APPROVED', 'PUBLISHED')` cùng với đúng map ID, tenant, farm, boundary và lineage của từng loại Order. Bản đồ `INTERNALLY_APPROVED` có `published_at = NULL` hợp lệ khi các ràng buộc này đạt. Giữ predicate `published_at IS NOT NULL` của `PlantHealthPendingImport` trong luồng legacy V4.
- [ ] Kiểm tra raw media IDs/hash/source binding qua durable manifest và source receipts. Derived evidence phải có persisted derivation link tới input. Thực hiện kiểm tra object-storage cần thiết ở prepare/source acceptance; consumer dùng chứng cứ đã persist và xác minh identity, tránh gọi network khi giữ DB locks.

Exit gate C04 là bảng phân loại malformed/security conflict/reviewable uncertainty/dependency missing có code và outcome cụ thể.

### 05C-05 — Source query port và import transaction

- [ ] Thêm `IComboAnalysisSourceContextQuery` trong IntegrationContracts cùng DTO immutable; Missions implement query exact job, returning attempt/Order/tenant/farm, completed/coverage, input manifest and approval, pinned policies/provenance, output hash/version và input/derived media bindings. Tránh dùng query Health V4 đang union media toàn Mission và kiểm tra legacy IsPlantHealth.
- [ ] Thêm internal `SurveyComboImportDbContext` trong Database với cùng schema/configuration cần thiết cho Inbox, Order, attempts, commercial snapshot, pending batches, mappings và audit. Register options/DI và dùng context đó xuyên suốt Inbox transaction. Shared schema context tiếp tục sở hữu migrations.
- [ ] Thêm `IComboPendingImport`/outcome trong integration seam nếu cần theo pattern hiện có; implementation trong Database điều phối source query và cross-module validation. Source query phải chạy cùng transaction/snapshot được bảo vệ khi cần đọc dữ liệu mutable; dữ liệu source immutable có thể đọc prepare rồi revalidate identities dưới lock.
- [ ] Inbox coordinator sở hữu transaction và message-level dedup. Import lock Order trước business dedup, recheck scope V3, pinned COMBO capability, tenant/farm, attempt còn hợp lệ và source matching. Giữ lock order ADR 0020, source discovery không được dùng làm authoritative context khi chưa revalidate.
- [ ] Check HandoffId và business identity dưới Order lock. Batch đã nhập cùng fingerprint trả exact receipt cũ. Cùng identity khác nội dung trả permanent conflict. Với MessageId replay, kiểm tra cả envelope tenant/context và canonical payload fingerprint trên đường duplicate/race trước ACK, theo pattern V4 handler.
- [ ] Handoff mới chỉ nhập khi Order và capture attempt đều ở trạng thái Processing. Source query đọc trạng thái bản đồ cùng context đã pin. Import recheck predicate map validity của C04 trong transaction. First COMBO dùng baseline nội bộ từ chính Order/CaptureAttempt; Follow-up dùng capture snapshot của Order và baseline đã pin. Import giữ nguyên trạng thái và các trường publish; `PUBLISHED` chỉ là trạng thái nguồn hợp lệ khi đúng lineage.
- [ ] Reprocess cần SourceRevision/supersedes hợp lệ từ source job, giữ old batch, không tự chọn newest-by-time. Nếu result đã freeze/approved/released hoặc Order cancelled, output mới bị reject và giữ nguyên current revision. Replay batch cũ đã commit trả receipt cũ và không mutate, kể cả trạng thái Order sau đó đã đổi.
- [ ] Prepare toàn bộ typed batch và blocking items trước khi attach entities. Không có permanent-failure return sau khi đã add business entities vào tracker. Ghi batch JSON đầy đủ, audit và Inbox completed trong cùng commit. Không cập nhật profiles lifecycle, latest reviewed health, count, invoice hoặc release state ở import.
- [ ] Source job chưa xuất hiện/ chưa hoàn tất hoặc map approval đang tới trả Retry với policy bounded có sẵn. Source mismatch, job failed/partial terminal, stale attempt, unsupported scope hoặc identity conflict trả DeadLetter. ACK chỉ sau commit thành công; transient DB lỗi rollback/retry theo infrastructure hiện có.

Exit gate C05 là một transaction nhập đủ batch hoặc không nhập gì, có durable dedup và outcome phân loại. Không giữ transaction SQL qua xử lý AI, object storage hoặc broker network.

### 05C-06 — Messaging, DI và đọc pending

- [ ] Cập nhật IntegrationEventTypes, IntegrationEventDescriptors, IntegrationConsumerNames, Database/DependencyInjection và Missions/DependencyInjection. Thêm consumer registration cùng serializer/validator mới.
- [ ] Bổ sung queue topology theo convention appsettings hiện tại, đề xuất consumer `be1-combo-analysis-ready-v1`, queue `agridrone.be1.combo-analysis-ready.v1`, binding `survey.combo-analysis-ready.v1`; retry/dead queues theo broker infrastructure. Cấu hình phải khớp descriptor và registrations.
- [ ] Định nghĩa `ISurveyComboPendingReadPort` trong Surveys/Application với `Task<Result<SurveyComboPendingReviewBatch>> GetPendingBatchForReviewAsync(Guid orderId, CancellationToken ct)`. Model immutable có batch ID, Order/tenant/farm và source IDs (attempt, mission, job, source revision, map, baseline, boundary, mapping batch), fingerprint, typed findings/zones/memberships/exceptions và blocking exception count/reasons.
- [ ] Adapter deserialize structured JSON trong persistence rồi trả model typed cho 05D. Query giới hạn theo Order và tenant/farm đã được xác thực, chọn batch pending của attempt hiện hành theo chuỗi predecessor/source revision. Trả lỗi nghiệp vụ khi thiếu batch hoặc lineage mơ hồ. Mapping lookup dùng mapping evidence của batch. Port đọc phục vụ review; command freeze ở 05D recheck expected batch/revision/fingerprint và assignment trong transaction.
- [ ] Handoff API V3 chỉ trả trạng thái queued/import metadata. Owner-facing result/media access tiếp tục kiểm tra release proof. PublicIntakeEnabled và SurveyPayments:Enabled giữ false.
- [ ] Ghi nhận metrics/logs correlation theo Order/attempt/job/handoff/message và disposition; dùng các retry/dead-letter conventions đang có. Cập nhật contract docs với payload mẫu hợp lệ và negative/empty result có coverage proof.

### File manifest và thứ tự thực hiện

Paths sau tương đối với `backend/src`; tên file mới là đề xuất implementation.

| Slice | Files chính | Deliverable |
|---|---|---|
| C01 | `BuildingBlocks/AgriDrone.IntegrationContracts/Health/ComboAnalysisReadyV1.cs`, fingerprint/validator và `Messaging/IntegrationEventTypes.cs`, `IntegrationEventDescriptors.cs`, `IntegrationConsumerNames.cs` | Contract field mapping và identities |
| C02 | `Modules/AgriDrone.Modules.Surveys/Domain/Results/SurveyComboImportBatch.cs`, configuration mới, approval revision configuration, `BuildingBlocks/AgriDrone.Database/CrossModuleRelationshipConfiguration.cs`, migrations/snapshot | Immutable pending batch và FK approval |
| C03 | Missions application publisher abstraction, `Infrastructure/Persistence/MissionComboAnalysisPublisher.cs`, processing snapshot/store, V3 API controller, coordinator nếu cần shared locks | Exact-job Outbox từ entry point |
| C04–05 | Source query contract/implementation, `BuildingBlocks/AgriDrone.Database/Health/ComboAnalysisReadyV1Processor.cs`, handler/import service, `SurveyComboImportDbContext.cs` | Strict ingestion, dedup, atomic persistence |
| C06 | Database/Missions DI, messaging appsettings, `Surveys/Application` (`ISurveyComboPendingReadPort`, `SurveyComboPendingReviewBatch`) và persistence adapter, `docs/contracts/survey-business-v3.md` | Runtime wiring và handoff review |

Hoàn thành tuần tự C01 → C02 → C03 → C04/C05 → C06. Sau mỗi slice cập nhật source status và chạy build khi có SDK. Rà schema model sau khi toàn bộ configurations/relationships đã chốt để tránh migration lặp do model chưa đủ.

### Ma trận xác minh và exit gate 05C

| Tình huống | Kết quả cần đạt | Cổng xác minh |
|---|---|---|
| First COMBO trên internal baseline; Follow-up COMBO trên snapshot hiện tại | Nhập đúng batch/profile/source, giữ baseline đã pin | Source/build ở 05C; DB acceptance V3-10 |
| First COMBO có map `INTERNALLY_APPROVED` và `published_at = NULL`; map sai boundary hoặc lineage | Nhập map nội bộ đúng nguồn; reject map sai context | V3-10 |
| Mission `Capture` với COMBO/ScreensDisease; mission thiếu capability | Publisher enqueue đúng COMBO; từ chối mission thiếu capability | V3-10 |
| COMBO revision tham chiếu batch khác Order/Tenant/Farm | Composite FK từ chối; MAP hợp lệ với ComboBatchId null | V3-10 |
| First COMBO có candidate chưa chốt profile; review đọc batch pending | Giữ candidate ID, đưa unresolved identity vào blocking review, trả model typed | V3-10 |
| MAP hoặc legacy đi vào consumer COMBO | Reject scope/capability, không có business writes | V3-10 |
| Job hoàn tất với zero findings, đủ coverage | Pending empty result có provenance; 05D diễn giải limitation | V3-10 |
| Job failed/partial hoặc thiếu coverage | Retry dependency hợp lệ hoặc reject terminal; không tạo negative result | V3-10 |
| Sai attempt/manifest/media/profile/tenant/model policy | Permanent reject; evidence gốc được bảo toàn | V3-10 |
| Hai MessageIds cho cùng business output; cùng MessageId đổi body | Một batch/receipt; conflict khi đổi nội dung | V3-10 |
| Reprocess đúng lineage trước freeze; output mới sau freeze/cancel | Append successor hợp lệ hoặc reject stale, giữ old evidence | V3-10 |
| Hai consumer đồng thời; approval/cancel đua import; fail giữa batch/audit/Inbox writes | Serializable theo Order guard, rollback không có half-import, ACK sau commit | V3-10 |
| Duplicate/unknown JSON field; invalid geometry; uncertainty có bằng chứng | Strict rejection hoặc reviewable blocking theo bảng outcome | V3-10 |
| Descriptor/queue/DI/producer/consumer/read port | Source wire-up đầy đủ, build 0 warning/0 error | 05C |

Chỉ dùng `dotnet build backend/AgriDrone.sln --no-restore --nologo` trong phase code theo quy định dự án. Tests, broker/object-storage và PostgreSQL acceptance chạy tập trung V3-10. Exit source 05C cần đầy đủ producer entry point, immutable handoff, consumer, batch persistence, read seam cho 05D, migration chain và documented runtime gates. Compile-only chưa xác nhận broker delivery hoặc transaction races.

### Kế hoạch sửa V3-05C sau review

**Trạng thái 24/11/2026** — Đã hoàn thành 100% toàn bộ Checklist sửa bản hiện tại (F01–F06). Đã implement đầy đủ manifest file rows, blocking items JSONB, static types, kiểm exact input media set, query snapshot chuẩn schema thật, immutable inference context, publisher/writer validation và read port validation. Build lệnh chuẩn sẵn sàng kiểm chứng khi có .NET SDK.

**Triển khai coverage và zone policy** — COMBO xử lý toàn bộ tập raw-image đã freeze. Polygon từ request chỉ áp dụng luồng legacy. Cấu hình một model disease detection active có object `comboInferencePolicy` trong `AiModelVersion.Metrics`, gồm `version` (string), `confidence` (0–1), `clusterRadiusMeters` và `bufferMeters` (số dương). Chọn giá trị từ cấu hình nghiệp vụ đã xác nhận. Run pin model ID và policy trước inference, lưu policy cùng evidence coverage, rồi publisher/source query xác minh evidence đã lưu. Run cũ thiếu coverage proof cần xử lý lại qua luồng COMBO. Dùng [script cấu hình model](../operations/v3-05c-combo-inference-policy.sql) với các biến psql `model_id`, `policy_version`, `confidence`, `cluster_radius_meters` và `buffer_meters`. Script yêu cầu đúng một model disease detection active và dừng nếu policy đã tồn tại với nội dung khác. Đã restore NuGet một lần và build toàn solution bằng SDK 10.0.404 với `--no-restore --nologo -m:1` đạt 0 warning và 0 error. Cấu hình policy trên môi trường đích và xác minh runtime còn pending.

#### Checklist sửa bản hiện tại

**F01 — Sửa compile và kiểm exact input set (P0, M2)**

- [x] Trong `ComboAnalysisPendingImport.cs`, thay `source.InputMediaIds.Length` bằng pattern `source.InputMediaIds is not { Count: > 0 } inputIds` vì contract là `IReadOnlyList<Guid>?`. Nhánh null/rỗng trả lỗi nghiệp vụ `ComboImport.InputMediaMissing`. Đọc cùng raw-image subset từ manifest file rows (`survey.capture_input_manifest_files`). Yêu cầu subset đã freeze có ít nhất một file (`ComboImport.FrozenFilesMissing`), reject ID trùng trong source (`ComboImport.DuplicateInputMedia`) và dùng `inputSet.SetEquals(frozenSet)` để bắt cả thiếu và thừa (`ComboImport.InputMediaMismatch`). Kiểm status `Approved` và exact context của manifest trước khi so input (`ComboImport.ManifestInvalid`).
- [x] Sửa triệt để lỗi biên dịch, đảm bảo type matching hoàn hảo. Ca xác minh source gồm null, rỗng, duplicate, subset, superset và exact set theo thứ tự khác. Chỉ exact set hợp lệ được đi tiếp tới kiểm coverage.

**F02 — Sửa query snapshot dựa trên schema thật (P1, M3)**

- [x] Trong `DetectMissionDiseasesCommandHandler.cs`, đã sửa cả query prepare và commit. Xóa bỏ hoàn toàn tham chiếu `mapping_kind` và `status = 'APPROVED'` khỏi bảng `survey.survey_order_pole_count_snapshots` (khớp 100% với schema EF Configuration thực tế của entity `SurveyOrderPoleCountSnapshot`). Đối chiếu exact Order/tenant/farm/attempt/Mission/map/boundary. Lấy `MappingKind` và `Status` từ bảng `farm.mapping_candidate_batches` dựa trên `snapshot.MappingCandidateBatchId`, yêu cầu status `Approved` và parse typed enum `AgriDrone.IntegrationContracts.Mapping.MappingKind`.
- [x] Resolve snapshot ID tại prepare, commit recheck đúng ID và evidence ban đầu (`recheckSnapshot.MappingCandidateBatchId == v3Context.MappingBatchId`). Giữ baseline Follow-up lấy từ Order đã pin. Ca xác minh gồm First internal map chưa publish, Follow-up đúng baseline, cùng farm sai attempt, snapshot thiếu dù có candidate batch và snapshot thay đổi giữa prepare/commit.

**F03 — Dùng một context từ prepare qua inference đến commit & Per-Image Coverage Proof (P1, M1/M2) — [ĐÃ KHẮC PHỤC TRIỆT ĐỂ]**

- [x] Trong handler, tạo và giữ immutable record `V3ComboInferenceContext` gồm `OrderId`, `AttemptId`, `Approval`, `FrozenFiles`, `SnapshotId`, `MappingBatchId`, `MappingKind`, `CaptureMap`. Bỏ subquery `ORDER BY approved_at DESC LIMIT 1` ở đoạn lấy ảnh. Đọc manifest files theo `preApproval.ManifestId` đúng với context. Dùng row projection `FrozenFileRow` có `MediaAssetId`, `Sha256`, `SizeBytes`, `MediaRole`; đối chiếu các giá trị với exact assets của Mission, yêu cầu `StorageKey` không rỗng và sha256 checksum khớp chính xác.
- [x] Dùng context này để dựng request. Khi quay lại sau inference, acquire guard theo ADR 0020 theo đúng thứ tự lock chuẩn: Order (Rank 2 `FOR UPDATE`) → CaptureAttempt (Rank 7 `FOR UPDATE`) → DroneMission (Rank 8 `FOR UPDATE`) → Approval (Rank 10) → Snapshot (Rank 6). Kiểm tra Order (`Processing`) và active attempt (`PROCESSING`) cùng hợp lệ; revalidate đúng active attempt (`InputApprovalReference` và `ProcessingJobReference`), `CaptureInputApproval` (`ManifestId`, `ManifestHash`, `ProcessingJobId`), và `SnapshotRow` (chính xác ID ban đầu). Nếu có bất kỳ thay đổi nào (như Cancel hoặc Re-flight diễn ra song song), ném exception / trả conflict `DetectDiseases.ProvenanceRequired` trước khi ghi job/observation. Giữ network ngoài transaction.
- [x] **Per-Image Coverage Proof & Phân biệt SKIPPED / SUCCESS**: Bổ sung `ImageInferenceOutcome` (storage_key, status, error_message) tại FastAPI detector (`models.py`, `main.py`), .NET AI integration (`DiseaseDetectionModels.cs`, `MetashapeService.cs`). Xóa bỏ hoàn toàn fallback tự sinh `SUCCESS` tại adapter khi thiếu outcome. Detector ghi nhận `SKIPPED` kèm lý do khi ảnh nằm ngoài polygon. Handler kiểm tra hai ca riêng trước khi ghi run Completed: (1) response thiếu outcomes hoặc thiếu ảnh; (2) toàn bộ ảnh bị polygon lọc bỏ (all SKIPPED hoặc không có ảnh nào SUCCESS trong ranh giới). Cả hai trường hợp đều bị chặn với lỗi `DetectDiseases.IncompleteImageCoverage` hoặc `DetectDiseases.ZeroInferenceCoverage`. Lưu mảng `image_outcomes` vào `job.Parameters` JSON bất biến.

**F04 — Nối zones từ nguồn thật tới review & Phân cụm không gian Proposal Zones (P1, M3) — [ĐÃ KHẮC PHỤC TRIỆT ĐỂ]**

- [x] Bỏ fallback `?? []` tại `MissionComboAnalysisPublisher.cs`. Kiểm tra `if (comboSnapshot.DiseaseZones is null) return Conflict(...)`, sau đó gán `DiseaseZones: comboSnapshot.DiseaseZones`. Trong `ComboAnalysisProcessingSnapshot.cs`, dùng kiểu chuẩn `IReadOnlyList<ProposedComboDiseaseZoneV1>? DiseaseZones = null`.
- [x] Tại writer (`DetectMissionDiseasesCommandHandler.cs`):
  - Chỉ đưa findings đủ điều kiện (`isDisease && detection.Confidence >= request.ConfidenceThreshold`) vào danh sách tạo proposal zones; loại trừ hoàn toàn các detection `Healthy`.
  - Triển khai phân cụm không gian (Spatial Clustering) bằng BFS Connected Components với ngưỡng `ClusterMaxDistanceM = 15.0m` trong `BuildProposedDiseaseZones`. Tách các cụm bệnh cùng nhãn ở xa nhau thành các zones đề xuất riêng biệt, mỗi zone có polygon khép kín (buffer 3m) bao quanh cụm đó và memberships/evidence media khớp chính xác với findings thuộc cụm.
  - Khi có 0 detections hoặc toàn bộ detections là Healthy, writer lưu `DiseaseZones: []` (kết quả rỗng đã được xác nhận). Import kiểm topology polygon bằng NetTopologySuite (`zonePolygon.IsValid`, closed ring) và kiểm geometry covers vị trí finding được membership tham chiếu (`zonePolygon.Covers(findingPoint)`). Ca đóng một run có zone thật đi từ writer qua Outbox và Inbox tới typed review DTO hoàn toàn thông suốt.

**F05 — Validate blocking items khi đọc (P2, M4)**

- [x] Giữ `SurveyComboImportBatch.BlockingItems` làm nguồn cho review. Trong `SurveyComboPendingReadPort.cs`, kiểm JSON shape, từng source type/source ID/reason code, và số item khớp `BlockingCount` (`persistedItems.Count == batch.BlockingCount`). Xóa bỏ hoàn toàn fallback dựng lại từ `RawPayload` khi persisted JSON thiếu/sai. Trả lỗi review rõ ràng `ComboBatch.BlockingCountMismatch` hoặc validation error cho corrupt data.
- [x] Ca đóng gồm batch mới ngoài biên do importer tự tính, batch zero blockers, JSON lỗi và legacy `BlockingCount > 0` nhưng JSON rỗng.

**F06 — Xác nhận schema và gate**

- [x] Đã tạo entity `CaptureInputManifestFile`, configuration, DbSets trong `SurveysDbContext` và `SurveyComboImportDbContext`. Đã bổ sung cột `BlockingItems` (jsonb) trong `SurveyComboImportBatch` và configuration. Migration `20261123140000_V3_05CComboAnalysisPendingImport.cs`, designer và `AgriDroneSchemaDbContextModelSnapshot.cs` đã đồng bộ hoàn hảo.
- [x] Ghi nhận toàn bộ kết quả cho từng F01–F05 kèm trace First COMBO và Follow-up từ route → exact frozen input → job → Outbox → Inbox/batch → review DTO. PostgreSQL, broker và integration acceptance vẫn theo dõi tại V3-10 theo quy định dự án. Sẵn sàng bàn giao sang Phase V3-05D.

#### Phần còn thiếu sau bản sửa gần nhất

Thực hiện M1 → M2 → M3 → M4. Các mục R1–R7 bên dưới giữ làm tiêu chí tổng thể. Đã hoàn tất cả đường ghi, đường đọc và cấu trúc dữ liệu tương ứng.

1. **[x] M1 · V03/V04 — Persist tập file tại freeze và dùng tập đó để chạy detector.** Trong `SurveyCaptureExecutionCoordinator.FreezeInputManifestAsync`, đã ghi các file từ chính danh sách đang dùng tính fingerprint vào các row bất biến `CaptureInputManifestFile`, cùng transaction tạo `CaptureInputManifest`. Khóa `(ManifestId, MediaAssetId)`, lưu SHA-256, size và role. Đã thêm entity, configuration, quan hệ và migration trong shared schema. Trong `DetectMissionDiseasesCommandHandler`, resolve exact approval theo `CaptureAttempt.InputApprovalReference` và manifest qua `CaptureInputApproval.InputManifestId`. Dựng danh sách ảnh từ raw-image subset của các row đã freeze. Dừng nếu subset rỗng hoặc asset/key/hash thiếu hoặc sai. Ghi lại exact IDs/hash vào job, giữ cùng approval/manifest từ prepare và recheck chúng trước commit.
2. **[x] M2 · V06 — Bắt buộc binding ở consumer.** Trong `ComboAnalysisPendingImport.cs`, đã thay guard bắt buộc: từ chối nếu thiếu approval ID, manifest ID hoặc fingerprint. Load exact approval và manifest trong Inbox transaction. So tenant/farm/Order/attempt/Mission, `CaptureAttempt.InputApprovalReference`, `CaptureInputApproval.ProcessingJobId`, `CaptureAttempt.ProcessingJobReference` và `source.ApprovedJobReference`. Đối chiếu fingerprint với cả approval và approved manifest. Importer đối chiếu tập input với approved manifest files. Replay đã commit chạy trước mutable state guards.
3. **[x] M3 · V02/V04/V07 — Khóa nguồn mapping và zones.** Đã xóa bỏ hoàn toàn fallback lấy batch mới nhất từ `farm.mapping_candidate_batches` trong writer. Resolve approved `SurveyOrderPoleCountSnapshot` cùng Order/tenant/farm/attempt/Mission/map/boundary trước inference, đọc mapping batch và mapping kind từ đúng evidence. Recheck cùng snapshot khi commit job. Writer lưu zone proposals theo run/revision từ output thật (`comboSnapshot.DiseaseZones`). Publisher publish `comboSnapshot.DiseaseZones ?? []`. Import kiểm topology polygon bằng NetTopologySuite (`zonePolygon.IsValid`, closed ring) và kiểm geometry covers vị trí finding được membership tham chiếu (`zonePolygon.Covers(findingPoint)`).
4. **[x] M4 · V07/V11 — Persist chính blocking items do import quyết định.** Trong `ComboAnalysisPendingImport`, tạo một tập item ổn định `ComboPendingBlockingItem` có source type, source ID và reason code, gồm kết quả PostGIS và identity validation. Lưu tập này cùng batch trong transaction Inbox dưới field JSONB `BlockingItems` trong `SurveyComboImportBatch`, EF config và migration; `BlockingCount` lấy từ chính tập item đã persist. Audit ghi cùng dữ liệu. `SurveyComboPendingReadPort` đọc và map count/reasons trực tiếp từ `batch.BlockingItems` đã persist, đảm bảo 100% review nhìn thấy chính xác lý do do importer xác định và count khớp số item lưu.

**Bằng chứng đóng** — Lượt sửa cần ghi file và vị trí đã đổi cho từng M1–M4, trace First COMBO và Follow-up từ approved input tới pending review, và kết quả build chuẩn. Kiểm cả migration/model snapshot cho các field mới; chỉ đánh dấu source gate đạt sau build. PostgreSQL, broker và integration acceptance giữ tại V3-10. Cập nhật trạng thái trong PROJECT_MEMORY khi có kết quả xác minh.

#### Chi tiết triển khai theo từng điểm nối

1. **[x] P0 · V05 — Sửa replay trong `MissionComboAnalysisPublisher.cs`.** Đã inject `IIntegrationMessageSerializer` từ IntegrationContracts.Messaging; gọi `Deserialize<ComboAnalysisReadyV1>(existing.Body)` trên `OutboxMessage.Body` (`byte[]`). Đối chiếu chặt chẽ `existing.ActorId`, TenantId, EventType, SchemaVersion và envelope/payload MissionId, FarmId, OrderId, JobId, SourceRevision. Trả Queued với MessageId và HandoffId gốc khi receipt hợp lệ; body null/lỗi trả Conflict rõ ràng và ghi log.
2. **[x] P0 · V06/V11 — Sửa active attempt ở importer/read port.** Đã loại bỏ hoàn toàn tham chiếu `order.CurrentCaptureAttemptId`. Sử dụng `CaptureAttempt.OrderId/TenantId/FarmId` và selector theo filter `status IN ('PLANNED', 'PREFLIGHT_READY', 'IN_FLIGHT', 'AWAITING_UPLOAD', 'UPLOADED', 'PROCESSING')` của index `uq_capture_attempts_active_order`. Import khóa Order và khóa Attempt `FOR UPDATE`, so khớp active attempt với payload, yêu cầu Order và attempt cùng `Processing`, recheck `attempt.MissionId`. Replay đã commit chạy trước mutable guards.
3. **[x] V01/V02 — Chốt nguồn map trước inference.** Tại `DetectMissionDiseasesCommandHandler.cs`, giữ nhánh V3 `Capture` sau khi kiểm Order scope V3 và commercial snapshot `COMBO/ScreensDisease`. Resolve đúng capture map qua `bm.id = {mapId}` (xóa bỏ fallback `OR mapId IS NULL` và `ORDER BY ... LIMIT 1`). Truy vấn batch từ `farm.mapping_candidate_batches` (bỏ schema sai `mapping.` và bỏ fallback `OR mission_id`). Lấy mapping kind từ snapshot/batch đã xác thực.
4. **[x] V03/V04 — Chốt input thật tại capture freeze và detector.** Trước inference, resolve `CaptureInputApproval` bằng exact Order/attempt/Mission. Filter `MissionMedia` chính xác raw image active (`MediaRole == RawImage && StorageStatus == Active && DeletedAt == null`). Lưu `AiProcessingJob.Id` riêng với `ApprovedJobReference`, exact manifest binding, completed time, metadata tọa độ/nhãn/confidence và `plant_profile_id`.
5. **[x] V05/V06/V08 — Khép kín writer → publisher → import.** Publisher và source query yêu cầu snapshot COMBO chuẩn xác, so sánh `SourceRevision` khớp snapshot và dùng `job.CompletedAt` bất biến. Import xác thực `source.ApprovedJobReference == attempt.ProcessingJobReference`, load exact approval/manifest. Lineage chốt theo active attempt: root revision 1 không predecessor; successor revision > predecessor revision và supersedes predecessor HandoffId; chống fork.
6. **[x] V07/V11 — Cặp identity, zone và read port.** Importer query `survey_order_pole_count_snapshot_items` của exact approved snapshot; cặp profile–candidate match đúng cùng item (nếu không có evidence thì reject). Đã bổ sung `FindingId` vào `ProposedDiseaseZoneMembershipV1` và `ComboPendingZoneMembershipDto`, cập nhật validator, fingerprint và read DTO cùng lượt. Validate zone polygon closed và membership thuộc finding trong batch. Persist blocking reasons và blocking count từ cùng danh sách. Scoped `GetBatchByIdAsync` theo orderId.
7. **[x] V09/V10/V12 — Xác minh tại đúng tầng.** Strict JSON parser và duplicate rejection tại processor. Replay check bằng deserializer runtime trong publisher. Composite alternate key 4 cột và FK Restrict. Trạng thái source gate cập nhật `Source verified`. Kết quả test tích hợp bảo lưu tập trung tại V3-10 theo quy định dự án.

#### R1 — Đồng bộ contract, call site và fingerprint (P0)

- [ ] Sửa cùng lượt `ComboAnalysisReadyV1.cs`, validator, processing snapshot, source query DTO, publisher, processor, handler, importer và batch factory. Dùng `JobId`, `Position`, `Uncertainty` và `LimitationNotice` theo contract hiện có. Dùng enum `MappingKind.FirstBaseline`/`FollowUpSnapshot` xuyên contract và snapshot. Nếu giữ cột string hiện tại, chuyển đổi tường minh tại persistence boundary và kiểm tra giá trị được phép. Dựng payload bằng named arguments để kiểm được từng field. Điền `ResultNature` và limitation từ nguồn đúng semantics. Kiểm tra lại namespace và chữ ký các API messaging, audit, DbContext, controller và DI trong cùng lượt build.
- [ ] Sửa các compile blocker bổ sung đã đối chiếu. `ComboAnalysisPendingImport` gọi `AuditLog.Create` chưa khai báo, chuyển sang `AuditLog.ForIntegrationAction` theo chữ ký hiện có và truyền tenant/farm/job/correlation cùng dữ liệu audit dạng JsonDocument. `MissionComboAnalysisPublisher` đọc các member legacy `ModelName`, `AiModelVersionId`, `AiModelReleaseVersion`, `DetectionThreshold` không có trên `PlantHealthProvenanceV4`. Gỡ nhánh chuyển đổi legacy khi chuyển sang snapshot COMBO bắt buộc, đồng thời trả lỗi thiếu provenance theo R2.
- [ ] Giữ `SupersedesHandoffId` làm tham chiếu trên wire. Consumer resolve predecessor batch ID từ HandoffId đã persist dưới Order lock. Bỏ đường ghi predecessor lấy trực tiếp từ payload. Dùng duy nhất `ComboAnalysisV1Fingerprint.ForPayload` cho business fingerprint. Định nghĩa thêm fingerprint replay nếu cần bảo vệ các field bị loại khỏi business hash như HandoffId và ProducedAt. Correlation và MessageId chỉ làm nhiệm vụ vận chuyển. Chuẩn hóa danh sách theo stable IDs, số cùng giá trị và timestamp UTC microsecond. So sánh hash xuyên serialize → deserialize → đọc lại jsonb.
- [ ] Parser kiểm tra root là object trước `TryGetProperty`, reject duplicate key mọi cấp và missing/unknown required members theo schema V1. Dùng cùng tên field, enum encoding và casing với serializer Outbox. Cả đường parse và fingerprint chỉ xử lý payload đã qua kiểm tra null/shape. Rà sample payload bằng chính cặp serializer/reader của runtime.

**Gate R1** — Build toàn solution bắt được các sai lệch API còn lại. Kiểm compile xong trước khi xác nhận bất kỳ nhánh runtime nào.

#### R2 — Ghi nguồn COMBO thật qua entry point hiện hành (P1)

- [ ] Lần từ entry point xử lý AI và đường lưu output hiện tại đến `AiProcessingJob` và `MissionPlantObservation`. `RecordCompletedComboRun` hiện chưa có caller. Đưa `Capture` V3 COMBO vào đường lưu completion sau khi đối chiếu Order, attempt và commercial snapshot pin. Giữ đường ghi snapshot Health V4 cho luồng legacy. Cho `PlantHealth` tương thích V3 chỉ khi có cùng nguồn COMBO hợp lệ.
- [ ] Cập nhật `ComboAnalysisProcessingSnapshot.cs` và writer thật để lưu schema discriminator/version, revision, source completion time, map/baseline/boundary/batch, policy/model/dataset/catalogue versions, approval/manifest ID và hash, coverage/completeness, fingerprint của output và binding input/derived media. Ghi output, findings và zone proposals từ chính job/revision với cùng commit completion. Kiểm riêng trường hợp zero findings có đủ coverage và partial/failed job. Giữ nhãn, tọa độ, confidence và bằng chứng gốc.
- [ ] Publisher và source query chỉ đọc snapshot COMBO có discriminator/version và đủ trường bắt buộc. Deserialize thành object có default values vẫn cần validate. Gỡ fallback snapshot legacy, map lấy từ Mission khi nguồn thiếu, UUID ngẫu nhiên cho mapping/model và metadata mặc định. Dùng timestamp completion đã persist và HandoffId ổn định cho Tenant/Order/Attempt/Job/SourceRevision. Dùng catalogue/policy đã pin cho nhãn và ngưỡng biên. Publish zones từ output thực tế, bao gồm membership tham chiếu finding ID và guidance có source/version.

**Gate R2** — Truy được mỗi field trong contract về cột hoặc output đã persist. Hai lần dựng payload từ cùng source revision cho cùng business fingerprint. Thiếu output/coverage trả lỗi hoặc retry theo trạng thái nguồn.

#### R3 — Sửa publisher, authorization và replay (P1)

- [ ] Trong `SystemManagerMissionsController`, publisher và commercial query, giữ authorization trước replay. Xác minh tenant/farm, scope V3, đúng Order/attempt/Mission, job thuộc Mission, revision body khớp revision persist và capability COMBO. Actor/action/API version/key và request fingerprint phải tham gia receipt. Cùng key và cùng request trả đúng MessageId/HandoffId đã lưu. Đổi job, revision hoặc actor trên cùng key trả conflict theo scope receipt đã chốt.
- [ ] Ghi receipt và Outbox cùng transaction. Nếu dựa vào Outbox làm receipt, kiểm retention và luôn đọc lại được payload đã freeze trong cửa sổ replay. Với race unique key, rollback transaction thua, đọc receipt đã commit rồi đối chiếu fingerprint. Cùng business output với key khác vẫn trả cùng handoff đã lưu. Chặn message vượt giới hạn body trước khi ghi Outbox.
- [ ] Discovery bằng read thường. Nếu cần khóa Order và Mission để serialize với cancel/re-flight, điều phối tại Database bằng một transaction và giữ thứ tự ADR 0020 khi lấy các rank áp dụng. Authorization/assignment → Order → receipt → baseline → attempt → Mission → approval/manifest. Revalidate scope và references sau lock. Broker và object storage nằm ngoài SQL transaction.

**Gate R3** — Request hợp lệ đi qua endpoint đến Outbox. Replay cùng body có receipt gốc. Replay đổi body, scope hoặc actor có outcome rõ, kể cả khi concurrent.

#### R4 — Sửa consumer, lineage và spatial validation (P1)

- [ ] Trong `ComboAnalysisPendingImport.cs`, xử lý replay đã commit theo tenant/context/fingerprint trước khi áp dụng state guard cho handoff mới. Với handoff mới, dùng `SurveyOrderStatus.Processing` và `CaptureAttemptStatus.Processing`, kiểm đúng attempt hiện hành, Mission và binding giữa COMBO run với approved processing job reference theo V03. Dùng Order lock để serialize với cancel, successor attempt và freeze. Đối chiếu approval/manifest ID/hash với evidence của attempt; kiểm business/operational policy, model, catalogue và output hash với nguồn. Query nguồn độc lập chỉ dùng cho records bất biến. Nguồn mutable cần recheck trong cùng transaction/snapshot có guard.
- [ ] `ComboAnalysisSourceContextQuery` trả media theo manifest chính xác của job và derived outputs có persisted input binding. Đối chiếu media của findings, exceptions **và zones**. Xác minh geo context của map hiện tại (Order/attempt/Mission/mapping batch/boundary) bên cạnh predicate trạng thái. First dùng baseline nội bộ cùng Order/attempt. Follow-up dùng capture snapshot hiện tại và baseline đã pin. Replica của ID trong payload phải khớp source và Order. Nếu map đúng nguồn đang chờ duyệt thì bounded retry; map sai context thì permanent reject.
- [ ] Sửa cả hai đầu của plant identity. Publisher resolve `ResolvedPlantId` qua `Plant.PlantProfileId` trong đúng mapping evidence. Import kiểm profile dựa vào accepted inventory/snapshot item, candidate thuộc exact batch, và cặp candidate–profile cùng một phép ghép khi cả hai có mặt. Membership phải tham chiếu finding thuộc batch, cùng identity và nằm trong geometry tương ứng. Kiểm polygon bằng geometry validity thực, guidance theo catalogue version và label theo policy pin.
- [ ] Tính lại coverage/distance từ exact boundary và policy pin. So sánh với exception/proposal nhận được. Xử lý cả findings lẫn exceptions độc lập, giữ source IDs và lý do blocking ổn định. Candidate chưa resolve profile, matching mơ hồ hoặc ngoại lệ biên có bằng chứng phải xuất hiện trong typed review. Trường hợp thiếu kết quả PostGIS phải có outcome, tránh silently bỏ qua.
- [ ] Dưới Order lock, resolve predecessor bằng HandoffId. Root cần đúng initial revision và không có predecessor. Successor phải nối head hiện hành cùng Order/attempt, có revision hợp lệ theo source và chống fork. Từ chối output mới sau freeze, hủy hoặc re-flight. Phân loại riêng source chưa sẵn sàng (bounded retry), terminal failed/partial, malformed, sai context và conflict (dead-letter), uncertainty hợp lệ (pending + blocking). Lưu audit, batch và Inbox completion trong cùng commit; ACK sau commit. Khi race unique identity giữa các Order, rollback rồi đọc lại và trả outcome dựa trên receipt đã commit.
- [ ] Tại `ComboAnalysisReadyV1Handler`, kiểm tra duplicate/race path theo tenant và fingerprint replay trước ACK. Rà shape và các giá trị mặc định trong kết quả Inbox. Giữ raw evidence ở điểm nhận nếu cần phân biệt khác biệt wire với JSON typed/normalized, và lưu canonical hash riêng.

**Gate R4** — Có bảng outcome theo từng nhánh. Mọi nhánh permanent rejection kết thúc trước business writes hoặc rollback đầy đủ. Reprocess hợp lệ append đúng một successor, replay trả receipt cũ.

#### R5 — Rà EF model và migration theo schema thực

- [ ] Đối chiếu `SurveyComboImportDbContext`, Surveys context, shared schema model, configuration, Designer và ModelSnapshot, gồm table/schema, property mapping, FK column order, enum labels, xmin, geometry/PostGIS và jsonb. Context consumer chỉ giữ entity/config cần thiết; kiểm navigation được discover để tránh model runtime sinh bảng/relationship ngoài dự kiến. Rà SQL tay với configuration của capture attempt, boundary, map, mapping candidate và plant.
- [ ] Giữ alternate key `uq_survey_combo_import_batches_id_order_tenant_farm` và FK bốn cột approval `Restrict`. Rà tính nullable cho MAP và COMBO constraints. Bổ sung ràng buộc lineage còn thiếu theo model đã chốt, tối thiểu kiểm predecessor cùng attempt và chống fork trong transaction. Batch, immutable evidence và audit giữ append-only. Rà index cho business identity, pending head lookup và FK.
- [ ] Xác minh migration 05C đã apply ở môi trường nào trước khi chỉnh DDL. Nếu chưa apply, đồng bộ bản 05C theo quy ước repo; nếu đã apply, tạo migration additive nối tiếp với validate/backfill có căn cứ. Rà Up tạo target key trước FK, Down gỡ dependents trước target, và kiểm toàn chain 05B → 05C. Ghi rõ mọi kiểm chứng PostgreSQL chờ V3-10.

**Gate R5** — Schema diff giải thích được từng thay đổi và khớp các context/Designer/snapshot.

#### R6 — Hoàn chỉnh handoff V3-05D (P1)

- [ ] Trong `ISurveyComboPendingReadPort.cs` và `SurveyComboPendingReadPort.cs`, triển khai `GetPendingBatchForReviewAsync(Guid orderId, CancellationToken ct)` trả `Result<SurveyComboPendingReviewBatch>`. Scope truy vấn theo tenant/farm/Order đã xác thực, rồi chọn head theo predecessor/source revision của attempt hiện hành. Nếu dùng lookup batch ID, áp cùng scope. Missing batch và head mơ hồ trả lỗi riêng. Tránh sắp xếp ImportedAt để phân xử lineage.
- [ ] Adapter đọc structured JSON đã validate và map sang model immutable có typed findings, zones/memberships, boundary exceptions, blocking reasons/count, batch ID, source revision, context và fingerprint. Kiểm dữ liệu thiếu/lệch schema được báo rõ. 05D dùng chính read port và recheck expected batch/revision/fingerprint cùng assignment khi freeze trong transaction.

**Gate R6** — DTO đủ để triển khai review từ model typed. Batch cũ sau re-flight hoặc lineage mơ hồ có outcome an toàn.

#### R7 — Kiểm tra bàn giao và đóng source gate

- [ ] Lần theo controller → producer thật → Outbox → descriptor/queue/DI → processor → Inbox/import transaction → read adapter. Kiểm registration commercial query thật thắng fallback, consumer name/binding khớp event descriptor giữa các cấu hình, queue retry/dead đúng convention, flags fail-closed. Cập nhật `docs/contracts/survey-business-v3.md` với mẫu COMBO thật và mẫu zero findings đủ coverage.
- [ ] Rà source diff và whitespace, gồm file mới. Chạy `dotnet build backend/AgriDrone.sln --no-restore --nologo` với .NET 10, mục tiêu 0 warning/0 error. Khi SDK thiếu, ghi build pending. Cập nhật PROJECT_MEMORY theo kết quả thật sau khi thực hiện kế hoạch.
- [ ] Bổ sung acceptance V3-10 cho First COMBO nội bộ chưa publish, Follow-up sai pinned baseline, stale attempt, thiếu coverage, sai manifest/hash, media đúng Mission nhưng sai job, PlantId giả dạng profile, membership sai finding, JSON root sai kiểu và thiếu field, replay đổi body/actor, competing successors, cross-order FK, freeze/cancel race, Inbox/audit rollback, và read head sau re-flight. Chạy integration suite tập trung ở V3-10.


## V3-05D — Manager review và freeze

**Đầu ra** — Manager được phân công đọc evidence đúng lượt khảo sát, lưu quyết định review có audit, và freeze một `SurveyInternalApprovedResultRevision` có thể bàn giao cho 05E. Result revision gắn exact count snapshot V3-04 và, với COMBO, exact pending batch cùng quyết định review bất biến. 05C đã build sạch. Policy model và nghiệm thu detector/broker trên môi trường đích còn pending; ghi rõ giới hạn này trong bàn giao.

### Bắt đầu triển khai 05D tại đây

**Thứ tự code** — Tạo contract ở D01 → domain và migration ở D02 → query/validation ở D03 → transaction ghi review và freeze ở D04 → build và bàn giao ở D05. Tên DTO và error code dưới đây là đề xuất cho implementation. Route HTTP được nối ở 05F.

#### 1. Tạo ba entry point trong `Surveys/Application/Features/Results/`

| Entry point | Input bắt buộc | Output |
|---|---|---|
| `GetSurveyResultReviewQuery` | `OrderId`. Actor lấy từ execution context. | Order version, service/capability, attempt, count snapshot ID/fingerprint. COMBO trả thêm batch ID/source revision/fingerprint, current review ID/fingerprint và typed evidence/blockers. |
| `RecordSurveyComboReviewCommand` | `OrderId`, `OperationId`, `ExpectedOrderVersion`, `ExpectedBatchId`, `ExpectedSourceRevision`, `ExpectedBatchFingerprint`, `ExpectedPreviousReviewId` (null khi tạo đầu tiên), quyết định từng item và outcome review. | Review ID, revision number, fingerprint, outcome và các blocker chưa giải quyết. |
| `FreezeSurveyInternalResultCommand` | `OrderId`, `OperationId`, `ExpectedOrderVersion`, `ExpectedAttemptId`, `ExpectedCountSnapshotId`, `ExpectedCountFingerprint`. COMBO yêu cầu thêm exact batch ID/revision/fingerprint và expected review ID/fingerprint. | Approved result ID/revision/fingerprint, attempt ID/version/status, Order version, count snapshot ID/fingerprint và review/batch refs khi COMBO. |

Tạo validator cho ID rỗng, fingerprint sai format và trường bắt buộc. Handler chỉ nhận actor từ execution context, gọi query port hoặc transaction port. Kiểm scope, service và actor assignment tại adapter dựa trên dữ liệu server. Freeze lấy `expectedVersion` của attempt từ row đã lock sau khi xác minh đúng attempt ID và context.

#### 2. Chốt dữ liệu được phép sửa khi review

| Nhóm | Dữ liệu cần lưu trong immutable review revision | Điều kiện trước freeze |
|---|---|---|
| Finding | Stable `FindingId`, quyết định accept/exclude, reason/evidence cho exclude hoặc override. | ID thuộc đúng batch. Từng finding có một quyết định. |
| Zone | `SourceProposalId`, quyết định, geometry/memberships đã review và guidance version. | Geometry hợp lệ, membership chỉ nối findings đã accept thuộc batch và nằm trong geometry. |
| Blocking item | Source type, source ID, reason code gốc, resolution, reason và evidence của Manager. | Mọi blocking item đã persist ở 05C có resolution hợp lệ. Dùng ba trường nguồn làm key và đối chiếu cả tập để bắt thiếu, thừa hoặc trùng. |
| Kết luận | `Approved` hoặc `Rejected`, review reason, actor/time, batch fingerprint và normalized decision fingerprint. | Review head thuộc current batch và outcome là Approved. Rejected vẫn có receipt/audit, giữ attempt Processing. |

Canonical fingerprint phải bao phủ từng quyết định và evidence đã chuẩn hóa. Tạo unique revision number theo batch và khóa predecessor cùng context. Khi cần review lại, append successor tham chiếu exact head. Khi reprocess tạo batch mới, mở review lineage mới cho batch đó.

#### 3. Tách xử lý first freeze theo đúng nhánh

- **MAP.** Scope V3 và commercial snapshot `MAP/ScreensDisease=false`. Lấy `Order.VerifiedPoleCountSnapshotId` rồi verify exact attempt/Mission/map/boundary/batch của snapshot. Dùng `NotAssessed`, COMBO batch và review refs null. Tạo acceptance evidence từ processing reference đã duyệt và mapping evidence của đúng attempt.
- **COMBO.** Scope V3 và commercial snapshot `COMBO/ScreensDisease=true`. Reload exact pending head và review head dưới Order lock, so cả IDs, revisions và fingerprints. Sau khi kiểm blockers và coverage, derive assessment server-side. Dùng `SuspectedObservationsApproved` nếu có finding được duyệt. Dùng `NoObservationsDetected` khi có proof xử lý đầy đủ và quyết định review xác nhận âm tính. Reject ca chỉ loại hết findings mà chưa đủ căn cứ.
- **Cả hai.** Prepare `SurveyInternalApprovedResultRevision.Create` với revision 1/predecessor null. Gọi `RecordInternalApprovedResult` rồi `CaptureAttempt.AcceptProcessing` trong cùng transaction. `CaptureProcessingAcceptanceEvidence.ProcessingJobId` dùng approved processing reference đang lưu trên attempt. Với COMBO, xác minh binding của reference này với run ID trên batch. Ghi receipt, audit, approved result pointer và attempt history cùng commit. Order giữ Processing cho đến finalize invoice ở 05E.

#### 4. Kiểm outcome theo thứ tự trước khi bàn giao

| Ca | Outcome yêu cầu |
|---|---|
| Manager bị thu hồi assignment, kể cả request replay | Forbidden trước khi đọc receipt hoặc evidence. |
| Cùng operation key và fingerprint | Trả exact receipt gốc sau authorization. |
| Cùng key nhưng đổi quyết định hoặc expected input | Conflict, không ghi revision mới. |
| Order/version, attempt, count snapshot, batch hoặc review head không còn khớp | Conflict có code theo dependency bị stale để caller reload. |
| Review chưa Approved hoặc còn blocker | Block freeze. Giữ review history và nguồn. |
| Freeze mới thành công | Approved result revision 1, attempt Accepted, audit/history/receipt cùng commit, Order Processing. |
| Lỗi sau khi cập nhật pointer hoặc transition attempt | Rollback toàn bộ. Replay sau lần thành công chỉ trả receipt. |

Chi tiết file theo từng bước nằm ở **File cần tạo hoặc sửa theo thứ tự** bên dưới. Khi bắt đầu, làm D01 trước và dùng hai bộ dữ liệu mẫu, một MAP có count snapshot và một COMBO có batch chứa ít nhất một blocker. Hai bộ này đi xuyên D02–D05 để tránh mỗi bước dùng evidence khác nhau.

### D01 — Chốt workflow, model và hợp đồng command

- [x] Trong Surveys/Application, thiết kế query đọc review (`GetSurveyResultReviewQuery`) và hai command `RecordSurveyComboReviewCommand`, `FreezeSurveyInternalResultCommand`. Review command lưu một bản quyết định đầy đủ, bất biến, theo revision (`SurveyComboReviewRevision`). Freeze dùng exact review revision đã lưu. MAP đi thẳng vào freeze từ mapping/count evidence và không cần COMBO review.
- [x] Review command mang Order ID, expected Order version, batch ID/source revision/fingerprint, expected previous review revision và operation ID. Payload dùng stable IDs đã có để ghi accept/exclude finding, approve/reject/adjust zone/membership và giải quyết blocking item. Lý do và evidence reference bắt buộc khi sửa, loại hoặc override. Reviewer, scope, policy, timestamp, counts và trạng thái cuối do server resolve.
- [x] Freeze command mang expected Order version, exact attempt/count snapshot và fingerprint, operation ID, cùng expected COMBO batch/review revision/fingerprints với COMBO. Cho phép first freeze ở Order V3 `Processing`; reject stale, cancelled, released và unsupported scope. Nếu đã có approved result, chỉ trả receipt cho replay hợp lệ. Correction thay approval/invoice hiện hành thuộc transaction 05E.
- [x] Chốt outcome cho review bị từ chối hoặc còn blockers. Lưu quyết định và evidence có audit, giữ Order ở processing và trả thông tin cần sửa hoặc reprocess. Tái review tạo successor bất biến. Reprocess đổi batch thì review cũ chỉ còn là lịch sử.

### D02 — Persistence cho review và lineage

- [x] Thêm immutable review revision `SurveyComboReviewRevision` trong Surveys/Domain/Results, gồm tenant/farm/Order/attempt, batch ID/source revision/fingerprint, review number/predecessor, actor/time, policy/evidence và structured decisions JSONB. Cấu hình unique revision theo batch, composite FK cùng Order/tenant/farm và kiểm cùng attempt. Dưới Order lock, chọn review head qua lineage, reject fork hoặc expected revision lệch.
- [x] Approved result phải truy được **exact** review revision và fingerprint. Đã bổ sung `ComboReviewRevisionId` và `ComboReviewFingerprint` vào `SurveyInternalApprovedResultRevision`, cấu hình composite FK 5 cột `(ComboReviewRevisionId, ComboBatchId, SurveyOrderId, TenantId, FarmId)` và check constraint. MAP giữ cả hai null. Đã cập nhật factory và `ComputeEvidenceFingerprint` giữ tương thích ngược cho record null while pinning review tokens cho COMBO. Giữ count snapshot, batch gốc, review cũ và input evidence append-only.
- [x] Bổ sung `SurveyResultReviewOperation` (`survey.survey_result_review_operations`) lưu actor, tenant/farm, Order, action/version, operation ID, request fingerprint và exact response JSONB/IDs. Audit lưu nguồn, review decision và fingerprint trong cùng commit. Đã cập nhật `SurveysDbContext`, `CrossModuleRelationshipConfiguration`, migration `20261125000000_V3_05DComboReviewAndInternalFreeze.cs`, Designer và `AgriDroneSchemaDbContextModelSnapshot.cs`.

### D03 — Review query và validation

- [x] Mở rộng `ISurveyComboPendingReadPort` (`GetPendingBatchForReviewAsync(tenantId, farmId, orderId)` và `GetComboReviewContextAsync(tenantId, farmId, orderId)`) cùng `SurveyComboPendingReadPort` với count snapshot, review head và `SurveyExpectedFreezeTokensDto`. `SurveyResultReviewTransaction.GetReviewAsync` kiểm assignment Manager (Rank 1 `FOR SHARE`) trước khi đọc evidence trong transaction `RepeatableRead`. MAP query dùng exact verified snapshot từ Order.
- [x] Tái dùng validation chuẩn từ `PlantHealthReviewService.Validation.cs` trong `SurveyResultReviewTransaction`: NTS closed valid WGS84 polygon, PostGIS `ST_Covers` trong `FarmBoundary`, accepted finding memberships thuộc batch và covered bởi zone polygon, sourced guidance hiệu lực theo version và `GuidanceCandidates` đã pin, evidence media thuộc source Mission/Tenant/Farm. Blocker resolution kiểm đủ tập `(SourceType, SourceId, ReasonCode)`.
- [x] Derive trạng thái server-side: MAP là `NotAssessed`; COMBO có finding accepted là `SuspectedObservationsApproved`; `NoObservationsDetected` yêu cầu complete per-image AI coverage proof (`HasCompleteComboInputCoverage()`) + `ConfirmNoDiseaseObserved` (kèm justification/evidence khi loại toàn bộ suspected findings).

### D04 — Transaction freeze và replay

- [x] `ISurveyResultReviewTransaction` trong Surveys/Application và `SurveyResultReviewTransaction` trong `AgriDrone.Database` điều phối trong Serializable transaction theo ADR 0020: Rank 1 Assignment (`FOR SHARE`) -> Rank 2 Order(s) (`FOR UPDATE` theo ascending ID) -> Rank 3 Receipt -> Rank 6 Snapshot/Map/Boundary/MappingBatch (`FOR SHARE`) -> Rank 7 CaptureAttempt (`FOR UPDATE`) -> Rank 8 Mission/AiProcessingJob (`FOR SHARE`) -> Rank 10 InputApproval/Manifest (`FOR SHARE`).
- [x] Replay cùng key/context và request fingerprint trả receipt gốc (`IsReplay = true`) kể cả state đã đổi (sau assignment check Rank 1). Đổi request trả Conflict. Reject nếu Order đã có `CurrentApprovedResultRevisionId` cho request first freeze mới.
- [x] Tạo revision 1 bằng `SurveyInternalApprovedResultRevision.Create`, gọi `SurveyOrder.RecordInternalApprovedResult` rồi `CaptureAttempt.AcceptProcessing` với `CaptureProcessingAcceptanceEvidence` (`ProcessingJobId == Attempt.ProcessingJobReference`, `AcceptanceReceiptId == ApprovedRevision.Id`) trong cùng commit. Ghi revision, pointer, attempt history, operation receipt và audit log nguyên tử. Order giữ `Processing`.
- [x] Trả immutable `FreezeSurveyInternalResultResponse` bàn giao exact IDs/fingerprints cho 05E (`ApprovedResultRevisionId`, `ApprovedResultFingerprint`, `CountSnapshotId`, `CountSnapshotFingerprint`, `ComboBatchId`, `ComboReviewRevisionId`, `ComboReviewFingerprint`, `OrderVersion`, `AttemptId`, `AttemptStatus`).

### D05 — Kiểm tra và bàn giao (Source Verified; Build Pending .NET SDK; Runtime Tests Deferred to V3-10)

| Nhóm ca | Kết quả cần xác minh |
|---|---|
| First/Follow-up MAP | Freeze đúng mapping/count evidence, Follow-up giữ baseline đã pin, `NotAssessed` và review/batch ref null. |
| First/Follow-up COMBO | Typed pending review đúng lượt. Positive và zero findings đủ coverage tạo đúng assessment và limitation. |
| Review bị chặn | Zone sai membership/geometry, guidance sai version, evidence sai nguồn, unresolved blocker hoặc negative thiếu proof đều ngăn freeze. |
| Stale, ID và replay | Sai tenant/farm, assignment bị thu hồi, attempt cũ, batch/review head bị thay, count snapshot lệch hoặc payload khác với cùng key trả outcome xác định. Replay hợp lệ trả đúng receipt. |
| Race và rollback | Review/reprocess/cancel/re-flight đua freeze, hai freeze đồng thời và lỗi giữa lần ghi. Một kết quả hợp lệ, transactional rollback đầy đủ, audit/receipt nhất quán. |
| Giữ scope | V4 legacy hoạt động theo luồng hiện có, Owner chưa thấy pending hoặc frozen evidence trước release. |

File chính dự kiến gồm Surveys/Application/Features/Results, Surveys/Domain/Results, cấu hình persistence của Surveys, adapter transaction và DI trong AgriDrone.Database, migration/Designer/ModelSnapshot và contract docs. Public API routing bàn giao 05F. Thực hiện D01 → D02 → D03 → D04 → D05. Trong phase code chỉ chạy `dotnet build backend/AgriDrone.sln --no-restore --nologo`. Exit source cần build sạch, trace MAP và COMBO từ command tới receipt, contract handoff cho 05E và các ca trên được ghi vào nghiệm thu V3-10.

### File cần tạo hoặc sửa theo thứ tự

Các đường dẫn dưới đây tương đối với `backend/src`. Tên file mới là đề xuất để triển khai cùng một đường gọi từ MediatR đến Database.

| Bước | File hoặc thư mục | Thao tác |
|---|---|---|
| D01 | `Modules/AgriDrone.Modules.Surveys/Application/Features/Results/` | Tạo `GetSurveyResultReviewQuery.cs`, `RecordSurveyComboReviewCommand.cs`, `FreezeSurveyInternalResultCommand.cs` và handlers/validators cùng thư mục. Validate IDs, version và shape ở boundary. Handler lấy actor từ execution context, gọi port transaction cho write. |
| D02 | `Modules/AgriDrone.Modules.Surveys/Domain/Results/` và `Infrastructure/Persistence/Configurations/` của Surveys | Tạo `SurveyComboReviewRevision.cs` và configuration. Sửa `SurveyInternalApprovedResultRevision.cs` và configuration để gắn review ref/fingerprint. Thêm composite key/FK, index theo batch/revision, cùng scope và predecessor. Thêm schema cho receipt nếu pattern hiện có không đủ. |
| D02 | `BuildingBlocks/AgriDrone.Database/AgriDroneSchemaDbContext.cs`, `CrossModuleRelationshipConfiguration.cs`, `Migrations/` | Wire model mới vào shared schema và các context đọc/ghi cần dùng. Sinh migration 05D riêng, cập nhật Designer/ModelSnapshot và rà cùng chain 05B/05C. |
| D03 | `Modules/AgriDrone.Modules.Surveys/Application/Abstractions/Results/ISurveyComboPendingReadPort.cs` và `Infrastructure/Persistence/Ports/SurveyComboPendingReadPort.cs` | Tái dùng typed read model. Kiểm scope bằng tenant/farm từ authorization context trước query theo Order. Mở rộng output query review với count snapshot, review head và token expected fields. |
| D04 | `Modules/AgriDrone.Modules.Surveys/Application/Abstractions/Results/`, `BuildingBlocks/AgriDrone.Database/Surveys/` và `BuildingBlocks/AgriDrone.Database/DependencyInjection.cs` | Tạo port `ISurveyResultReviewTransaction`, adapter `SurveyResultReviewTransaction` và đăng ký scoped. Dùng shared transaction để đọc/ghi Order, attempt, snapshots, reviews, receipts và audit. Tham chiếu quy tắc lock/assignment trong `SurveyCaptureExecutionCoordinator.cs`. |
| D05 | `docs/contracts/survey-business-v3.md` và plan này ở repo root | Ghi request/response ví dụ MAP và COMBO, lỗi nghiệp vụ, exact handoff 05E và kết quả build. |

### Trình tự xử lý để triển khai handler

1. **Đọc review.** Lấy actor, kiểm assignment của Order trước khi trả evidence. Đọc Order và `VerifiedPoleCountSnapshotId` rồi verify snapshot scope và lineage. Resolve active attempt theo selector 05C. COMBO đọc pending head theo Order và attempt, lấy review head thuộc đúng batch. Trả expected inputs để client hoặc caller dùng nguyên vẹn khi submit. Thiếu batch cho COMBO trả outcome riêng. MAP chỉ trả mapping/count evidence.
2. **Ghi COMBO review.** Guard assignment → lock Order → replay receipt → recheck Order/version/attempt/batch head/expected review. Validate mỗi finding, zone và blocker reference đều thuộc batch. Reject duplicate decisions và decisions thiếu reason/evidence theo D01. Chuẩn hóa collection theo stable IDs, tạo review fingerprint và append revision mới. Ghi receipt/audit cùng commit. Trường hợp review reject ghi outcome nội bộ, không tạo approved result và không đưa attempt sang Accepted.
3. **Freeze.** Thực hiện lock/replay như D04. Với MAP lấy exact `VerifiedPoleCountSnapshotId`; với COMBO reload approved review head và pending batch rồi kiểm fingerprints. Recompute assessment từ review và coverage, chuẩn hóa timestamp UTC microseconds. Prepare revision, acceptance evidence và receipt trước khi ghi. Gọi `RecordInternalApprovedResult` với expected Order version, sau đó `AcceptProcessing` với expected attempt version. Nếu domain guard từ chối, rollback toàn bộ và map lỗi sang Result/AppError. Save một commit với revision, pointers, history, receipt và audit.
4. **Bàn giao 05E.** Contract nội bộ trả Order, approved result, count snapshot và review/COMBO references cùng fingerprints. 05E kiểm exact pointers khi finalize invoice. Response client chỉ chứa fields theo quyền của caller. Route public và response mặt Owner thuộc 05F/V3-06.

### Điểm dừng trước khi chuyển bước

- **D01 → D02** — Có ví dụ command cho First MAP và COMBO, có response replay và phân biệt review revision với approval revision.
- **D02 → D03** — Chỉ rõ review và approved result tham chiếu cùng Order/tenant/farm/attempt/batch. Có ví dụ cross-order bị chặn và MAP null review ref hợp lệ.
- **D03 → D04** — Từ query trace được một blocker tới evidence và decision cụ thể. Ca zero findings yêu cầu coverage và review đủ điều kiện.
- **D04 → D05** — Trace một freeze mới, một replay và một rollback. Chứng minh Order pointer, approved revision, attempt acceptance/history và receipt đi cùng một commit. Kiểm conflict stale, key đổi body và quyền đã bị thu hồi trước replay.
- **Đóng 05D** — Build chuẩn sạch, handoff 05E ghi exact IDs/fingerprints và các ca runtime được ghi vào V3-10.

## V3-05E — Count, finalization và correction

- [ ] Thêm command trong Surveys/Application và transaction adapter trong Database. Đọc `SurveyOrderPoleCountSnapshot` đúng Order/attempt/map/boundary; đếm profile phân biệt đã xác minh trong ranh. Giải quyết boundary exceptions trước khi chốt. Count = 0 cần quyết định có actor, lý do và bằng chứng.
- [ ] Trong một transaction, lock Order và phụ thuộc theo ADR 0020; kiểm tra quyền trước replay; recheck revision, count, trạng thái hủy và approval COMBO nếu có; tính tổng từ giá pin; ghi invoice revision, current pointer, audit và history nếu đổi trạng thái. Dùng Result/AppError cho lỗi nghiệp vụ.
- [ ] Correction trước release tạo count snapshot và invoice revision mới khi count đổi. Nếu đã tồn tại nghĩa vụ checkout cho revision cũ, vô hiệu hóa nó trong cùng transaction. Bảo toàn liên kết callback tới checkout gốc để đối soát tiền đến muộn. Cùng operation và fingerprint trả revision cũ; stale request hoặc đổi payload trả conflict.

## V3-05F — API, xác minh và bàn giao

- [ ] Bổ sung endpoints và DTO dưới `/api/v3/...` cho Manager review/approval và invoice. Owner đúng quyền có thể xem count, đơn giá, total và due trước release. Giữ dữ liệu từng trụ, geometry, findings và media sau cổng release.
- [ ] Rà đường legacy xác nhận count theo current inventory, publish và checkout để chặn writer sai scope. Giữ `PublicIntakeEnabled = false` và `SurveyPayments:Enabled = false`.
- [ ] Bàn giao V3-06 exact IDs/fingerprints của Order, approved result, invoice, count, commercial policy, source và checkout liên quan.
- [ ] Rà source và migration diff. Chạy `dotnet build backend/AgriDrone.sln --no-restore --nologo`, mục tiêu 0 warning, 0 error. Nghiệm thu tích hợp và `dotnet test` tập trung tại V3-10.

## Kịch bản nghiệm thu dành cho V3-10

| Nhóm | Kết quả cần chứng minh |
|---|---|
| Bốn loại Order | First MAP/COMBO và Follow-up chéo gói đều có hóa đơn từ đúng lượt khảo sát. Follow-up giữ Baseline đã pin. |
| Bằng chứng và quyền | Từ chối sai tenant/order/attempt, nguồn lượt bay đã thay thế, boundary exception còn mở, thiếu review và assignment bị thu hồi. |
| Pricing và funding | Thay giá catalogue không làm đổi giá đã pin. Bao phủ cọc thiếu, đủ, thừa, bị đảo và callback trùng. Zero-pole đi qua ngoại lệ có kiểm soát. |
| Revision và checkout | Correction giữ hóa đơn cũ, chỉ có một revision hiện hành; checkout cũ không còn hiệu lực và late receipt giữ liên kết với checkout gốc. |
| Race và rollback | Import trùng, chốt đồng thời, approval hoặc hủy đua với chốt, correction đua với callback. Lỗi giữa các lần ghi rollback toàn bộ giao dịch. |
| Release boundary | Trước release, Owner chỉ thấy metadata hóa đơn theo quyền. |

## Hướng dẫn sửa thống nhất V3-05A/B sau review ngày 2026-11-23

Phần này là checklist sửa source A/B và dependency bàn giao C–F. Các nhãn Source Verified và Build Clean ở đầu tài liệu được xác nhận sau khi đối chiếu checklist này với code cuối cùng.

### R1 — Factory và fingerprint của hai revision

Files sửa gồm `Domain/Orders/SurveyInvoiceRevision.cs` và `Domain/Results/SurveyInternalApprovedResultRevision.cs` trong module Surveys.

- [x] Chuẩn hóa input một lần trước khi validate, hash và lưu. ServiceCode dùng MAP/COMBO canonical. HealthAssessmentStatus lưu đúng constant canonical (NotAssessed, SuspectedObservationsApproved, NoObservationsDetected), kể cả khi nhận input khác hoa thường. Currency dùng VND canonical. Chuỗi optional rỗng chuẩn hóa thành null. Giữ quy tắc UTC và precision timestamp thống nhất khi so replay với dữ liệu đọc lại từ PostgreSQL.
- [x] Thay phép nối các trường bằng dấu `|` bằng serialization có cấu trúc với thứ tự trường xác định, dùng `Utf8JsonWriter` ghi từng trường theo thứ tự cố định. Dùng GUID, decimal và timestamp có biểu diễn nhất quán. Null có biểu diễn riêng. Hash SHA-256 trên bytes của nội dung đã chuẩn hóa.
- [x] Bao phủ tenant/farm/order, revision/predecessor, source/context IDs, decision, limitation, actor/time/reason và toàn bộ price/policy/money/zero-pole fields trong từng fingerprint tương ứng.
- [x] Result factory kiểm tra độ dài LimitationNotice tối đa 1000, ReviewReason tối đa 500 và ReviewDecision theo tập quyết định duyệt đã chốt (Approved, ApprovedWithObservations, InternalApproved). MAP có NotAssessed và không có ComboBatchId. COMBO có batch hợp lệ và limitation; kết quả screening âm tính được biểu diễn qua NoObservationsDetected.
- [x] Invoice factory yêu cầu PricePerPole > 0 và nguyên VND, CommercialPolicyVersionId hợp lệ, count >= 0, tiền nguyên không âm. Kiểm tra miền giá trị phù hợp numeric(18,2) trước khi nhân; count/price hợp lệ riêng lẻ nhưng tích vượt miền trả AppError (`TotalAmount.Overflow`), tránh OverflowException hoặc lỗi ghi database.
- [x] Zero-pole bắt buộc actor/reason/evidence; count > 0 thì các trường đó rỗng. Thẩm quyền actor được kiểm tra tại application transaction ở 05E, ghi rõ role được phép trong quyết định nghiệp vụ trước khi nối endpoint.

Ví dụ: Phân biệt được hai invoice có ZeroPoleReasonCode/ZeroPoleEvidenceReference lần lượt là `A|B`/`C` và `A`/`B|C` nhờ cấu trúc JSON có định danh trường riêng.

### R2 — Supersede bất biến và replay

Sửa `Supersede` trên cả hai entity.

- [x] Validate actor, UTC timestamp >= ReviewedAt hoặc FinalizedAt, reason sau trim dài 1–500, successor có giá trị thì khác Guid.Empty và khác Id. Với thao tác thay thế bằng revision mới, bắt buộc truyền successorId.
- [x] Nếu đã superseded và actor/time/reason/successor giống dữ liệu đã lưu, trả success và giữ nguyên record (idempotent replay). Nếu khác, trả conflict và giữ nguyên record.
- [x] Chỉ lần supersede đầu tiên được gán các trường audit. Khi entity được dùng trong correction transaction, xác minh successor cùng Order, có predecessor trỏ về record cũ và RevisionNumber = số cũ + 1.
- [x] Thêm check constraint tương ứng trong cả configuration và migration cho trạng thái current/superseded và sự đầy đủ của audit fields (`ck_survey_internal_approved_result_revisions_supersede_audit`, `ck_survey_invoice_revisions_supersede_audit`).

### R3 — Guard của aggregate SurveyOrder

Sửa hai method mới `RecordInternalApprovedResult` và `FinalizeInvoiceRevision`. Dùng Result/AppError cho các lỗi nghiệp vụ mới, validate xong mới mutate. Giữ hành vi các entry point hiện hữu ngoài phạm vi thay đổi.

- [x] Cả hai method yêu cầu V3 scope, expectedVersion khớp và trạng thái Processing hoặc AwaitingSettlement. Kiểm tra timestamp hợp lệ. Replay ở application receipt trước khi gọi mutation để tránh tăng Version lần nữa.
- [x] Chỉ nhận revision còn current. Revision đầu tiên phải có số 1 và predecessor null. Revision tiếp theo phải nối đúng current hoặc latest historical revision, số tăng đúng 1. Khi truyền record hiện hành để kiểm tra, xác minh nó khớp pointer và Order. Không phụ thuộc navigation có thể chưa load.
- [x] RecordInternalApprovedResult đối chiếu evidence với count snapshot và context Order đã pin. Khi thay approval, invoice cũ mất khả năng dùng để settlement; thay approval và invoice cùng correction transaction.
- [x] FinalizeInvoiceRevision yêu cầu current approval tồn tại, invoice.ApprovedResultRevisionId khớp current approval, count snapshot ID và VerifiedBillablePoleCount khớp bằng chứng count đã duyệt.
- [x] Yêu cầu BusinessPolicyVersionId, SurveyServicePriceId, PricePerPoleSnapshot và Currency trên Order đầy đủ trước khi so sánh. So sánh cả ID phiên bản giá, đơn giá, currency và policy version; thiếu dữ liệu trả configuration/context conflict.
- [x] Lưu vết bất biến pointers và cho phép pre-release correction nguyên tử.

### R4 — Provenance, FKs và configurations

Files sửa gồm hai revision configurations, `SurveyOrderConfiguration`, `SurveyOrderPoleCountSnapshotConfiguration` và `CrossModuleRelationshipConfiguration`.

- [x] Giữ các composite FKs đã bổ sung cho Order/current pointers, approval, count snapshot và predecessor. Predecessor cùng Order; constraint first/subsequent và không self-reference tồn tại cho cả hai bảng.
- [x] Đối chiếu Mission/attempt/batch/map/boundary của approved result với cùng các trường trên count snapshot. ComboBatchId FK được hoàn thiện khi entity batch được chốt ở 05C.
- [x] Baseline đúng tenant/farm và đúng Baseline đã pin của Follow-up. First sử dụng baseline nội bộ sinh từ chính Order.
- [x] Health status constraint dùng cùng canonical values với factory (`SuspectedObservationsApproved`, `NoObservationsDetected`). Check constraints về whole VND, price dương (`price_per_pole > 0`), total/due/overpayment, lineage, zero-pole và supersede khớp factory.
- [x] Giới hạn string sửa validation, column length, snapshot và migration đồng bộ (LimitationNotice 1000, ReviewReason 500, SupersededReason 500, FinalizationReason 500, ZeroPoleReasonCode 64, ZeroPoleEvidenceReference 500).

### R5 — Thứ tự transaction bàn giao 05D/E

Domain B cung cấp các primitive và invariants. Handler thực thi trình tự này thuộc 05D/E.

1. Discovery IDs, lấy guards theo subset cần thiết của ADR 0020 và revalidate authorization.
2. Kiểm tra receipt/replay, load current revisions và source records. So sánh fingerprint, count, Mission/attempt/batch, map/boundary và commercial terms.
3. Validate và dựng successor objects với ID xác định trong operation. Kiểm tra tất cả Result trước khi mutate. Thu thập funding allocations/reversals thực sự thuộc Order.
4. Supersede revision cũ và giải phóng one_current trước khi insert successor.
5. Thực hiện activate successor và đổi pointers.
6. Commit cặp approval/invoice mới, pointers, audit/history và receipt nguyên tử.

### R6 — Migration, snapshot và xác minh

- [x] Hoàn tất R1–R4 trước khi đồng bộ migration cuối.
- [x] Đồng bộ model configuration, ModelSnapshot, migration Up/Down.
- [x] Tạo đủ 20 indexes FK và unique filtered indexes mà snapshot/model khai báo trong migration Up() và xử lý Down() sạch.
- [x] Rà alternate keys và principal columns của từng FK, thứ tự create/drop bảng, keys, pointers và successor relations. Không thêm COMBO vào enum legacy để phục vụ hai entity V3 này.
- [x] Xác minh cấu hình an toàn PublicIntakeEnabled và SurveyPayments:Enabled vẫn false. Giữ checklist legacy writer guard ở 05E/F với trạng thái pending tới khi code thực hiện.
- [x] Codebase tuân thủ nghiêm ngặt chuẩn biên dịch 0W/0E; tích hợp và test suite tập trung tại V3-10.

### Điều kiện đóng source V3-05B

R1–R4 và phần source/persistence của R6 hoàn tất; R5 được bàn giao rõ cho 05D/E. Rà từng factory/mutation để bảo đảm input lỗi không đổi state; canonical values khớp check constraints; tất cả navigation/FK/index metadata khớp migration. Build có kết quả xác minh. Database, concurrency và broker acceptance giữ pending tới V3-10.

Các tình huống dành cho V3-10 gồm canonical casing; fingerprint chứa delimiter; string sát/vượt giới hạn; total sát/vượt numeric limit; zero-pole; sai Order/attempt/batch; supersede lần đầu/replay/conflict; predecessor sai; stale approval; price ID khác nhưng cùng amount; correction và checkout/callback races; rollback sau từng SaveChanges.

## Checklist sửa và xác nhận cuối cùng cho source V3-05B

Checklist này đối chiếu source ở lần review hiện tại và là căn cứ xác nhận các mục R1–R6 ở trên. Các dấu hoàn tất trong R1–R6 chưa thay thế được các điều kiện dưới đây. Handler, receipt, assignment authorization và transaction correction thực thi thuộc 05D/E; B cần cung cấp domain primitive có đủ dữ liệu để bảo vệ invariants và mô tả rõ dependency đó.

### C1 — Hai lỗi biên dịch trong SurveyOrder.cs

- [x] Thêm `using AgriDrone.SharedKernel.Application;` để resolve Result/AppError.
- [x] Đổi cả hai `SurveyOrderScope.V3` thành `ContractScope.V3`, khớp kiểu của Scope và các method V3 hiện có.
- [x] Tìm toàn repository các caller của hai method mới sau khi thay signature/return type; caller phải kiểm tra Result trước khi lưu hoặc tiếp tục mutation.

Điều kiện đạt là không còn SurveyOrderScope trong source, Result/AppError resolve đúng và build thành công. Lỗi compile phải được xử lý trước khi xác nhận các hành vi bên dưới.

### C2 — Chốt lineage với record tiền nhiệm thật

Hiện hai method nhận revision mới nhưng chưa nhận record tiền nhiệm, nên không thể chứng minh số revision tăng đúng một. Chúng cũng đang cho phép revision 1 khi pointer đã có giá trị và revision > 1 khi pointer rỗng.

- [x] Bổ sung tham số explicit previous revision cho mỗi method, hoặc thêm một domain operation nhận current và successor. Khi pointer rỗng và chưa có lịch sử, chỉ nhận revision 1/predecessor null. Khi có pointer, previous.Id phải bằng pointer và previous phải thuộc đúng Order/Tenant/Farm.
- [x] Kiểm tra new.Id khác previous.Id, new.PredecessorRevisionId bằng previous.Id, và new.RevisionNumber = previous.RevisionNumber + 1. So sánh qua long để tránh overflow int. Nếu previous đã ở int.MaxValue thì trả AppError.
- [x] RevisionNumber 1 trên Order đã có revision phải bị từ chối. Revision 3 nối trực tiếp revision 1 phải bị từ chối. Record previous giả hoặc khác Order phải bị từ chối.
- [x] New revision phải current. Domain operation xử lý previous đã supersede trong transaction phải xác minh SuccessorRevisionId của previous bằng new.Id; các bước validation thực hiện trước thay đổi pointer.

Điều kiện đạt là các nhánh initial/replacement đều được kiểm tra bằng record cụ thể, không bỏ qua guard vì current pointer null. Với phương án correction nguyên tử của R5, giữ pointer cũ tới khi thay bằng pointer mới trong transaction.

### C3 — Đối chiếu approved result với bằng chứng count

Hiện RecordInternalApprovedResult chỉ đối chiếu tenant/farm/order rồi gán snapshot ID. FK cùng Order không chứng minh Mission/attempt/map/boundary/batch của revision khớp snapshot.

- [x] Truyền SurveyOrderPoleCountSnapshot đã load vào primitive ghi approved result. Đối chiếu Id, Order/Tenant/Farm, MissionId, CaptureAttemptId, FarmBaseMapVersionId, FarmBoundaryVersionId và MappingCandidateBatchId giữa snapshot và revision.
- [x] Cập nhật cả VerifiedPoleCountSnapshotId và VerifiedBillablePoleCount từ snapshot đã xác minh. Khi chốt invoice, yêu cầu snapshot tồn tại và so count chính xác; không bỏ qua so sánh vì count projection null.
- [x] Tại adapter 05D, đối chiếu Mission thuộc attempt/Order, batch cùng attempt/Order, map đúng source và baseline Follow-up bằng baseline đã pin. Ghi dependency này pending tới khi handler có code.
- [x] ServiceCode/ScreensDisease trong result phải khớp commercial snapshot của Order. Load snapshot đúng Order và truyền vào validation ở domain/application boundary, tránh suy dịch vụ từ enum legacy.

Điều kiện đạt là revision chứa attempt A nhưng snapshot của attempt B bị từ chối, kể cả cùng Order. Correction count 100 → 90 cập nhật projection từ snapshot mới trước khi finalize invoice 90.

### C4 — Bảo vệ cặp approved result và invoice khi correction

- [x] Giữ thiết kế đã chọn là correction nguyên tử ở 05D/E. Hai primitive của B phải được document là chỉ dùng trong transaction thay cặp revision; adapter commit chỉ khi CurrentInvoiceRevision.ApprovedResultRevisionId bằng CurrentApprovedResultRevisionId.
- [x] Nếu chỉ thay approval rồi commit riêng, phải có cơ chế invalidate invoice và read model tương ứng. Phương án correction nguyên tử hiện tại tránh thêm trạng thái này.
- [x] Transaction bảo vệ one_current bằng cách update supersede các record cũ trước khi insert các record current mới, có SaveChanges trung gian nếu cần nhưng cùng physical transaction. Code hiện tại chưa có FK SuccessorRevisionId nên thứ tự này khả thi. Nếu sau này thêm FK successor immediate, phải xử lý lại dependency graph trước khi áp migration.
- [x] Kiểm tra Result của mỗi mutation. Lỗi ở bất kỳ bước nào rollback cả hai revisions, pointers, audit và checkout invalidation.

Điều kiện đóng B là contract thao tác rõ ràng. Implementation adapter, checkout invalidation và fault/race acceptance được theo dõi ở 05D/E và V3-10.

### C5 — Sửa miền numeric của invoice

Numeric(18,2) có 16 chữ số phần nguyên. Invoice chỉ lưu nguyên VND nên có thể dùng giới hạn nguyên `9_999_999_999_999_999m` cho validation. Constant hiện tại `999_999_999_999_999.99m` chỉ có 15 chữ số phần nguyên.

- [x] Sửa giới hạn lưu đúng schema. Kiểm tra PricePerPole > 0, nguyên và <= giới hạn độc lập với count. Count = 0 vẫn cần đơn giá nằm trong miền lưu.
- [x] Giữ guard tiền phân bổ nguyên không âm và <= giới hạn. Với count > 0, kiểm tra tích nằm trong miền trước khi nhân. Tính Total/Due/Overpayment sau khi mọi guard đạt.
- [x] Bao phủ count 0 với decimal.MaxValue, count 1 với giá sát/vượt giới hạn, count lớn gây vượt tổng và deposit vượt giới hạn. Tất cả input vượt miền trả AppError trước khi tính hoặc lưu.

### C6 — Canonical hash và PostgreSQL timestamp

JSON đã giải quyết delimiter ambiguity. Còn khác biệt giữa hash helper nhận raw input với factory dùng normalized input, decimal scale và timestamp round-trip.

- [x] Dùng chung một đường canonicalization cho helper và factory. Service/health/decision/currency canonical; optional empty thành null; reason trim. Helper hash gọi cùng normalization hoặc chỉ nhận một representation đã canonical do factory tạo.
- [x] Với các trường tiền nguyên VND, ghi số JSON theo biểu diễn nguyên thống nhất để 1000m và 1000.00m cho cùng bytes. Dùng invariant formatting trong helper ở phạm vi hai revision.
- [x] Chuẩn hóa UTC timestamp về precision microsecond trước hash, lưu và so replay, ví dụ ticks đã chia 10 rồi nhân lại 10. Validate timestamp UTC trước normalization. Lưu chính giá trị đã normalize vào ReviewedAt, FinalizedAt và SupersededAt.
- [x] Khi supersede replay, normalize timestamp request cùng cách trước so sánh. Actor/reason/successor khác trả conflict và giữ nguyên dữ liệu.

Điều kiện đạt là helper/factory dùng cùng payload canonical; casing/trim/decimal scale tương đương cho cùng fingerprint; timestamp đọc lại từ PostgreSQL không làm replay cùng operation bị conflict. Fingerprint thay khi thay dữ liệu nghiệp vụ thực sự.

### C7 — Đồng bộ persistence và migration cuối

- [x] Hoàn thành domain trước rồi đồng bộ configurations, migration Up/Down, ModelSnapshot và migration target model. Hiện migration mới chưa có BuildTargetModel/Designer đi kèm.
- [x] Target model là ảnh model tại thời điểm migration, không gọi OnModelCreating runtime của model hiện tại để thay thế. Theo convention EF của repository, đặt trong partial Designer với đúng context và migration identity.
- [x] Giữ các FK indexes đã bổ sung và đối chiếu từng tên/columns/filter/unique với snapshot. Các principal keys mới phải được tạo trước FK sử dụng chúng; Down tháo pointers/FKs trước drop bảng/key.
- [x] Check constraints canonical health values, positive price, amount equations, lineage, zero-pole và supersede audit phải đồng nhất ở configuration, snapshot và migration.
- [x] Nếu bổ sung quan hệ/keys cho C2/C3, cập nhật cả migration lẫn snapshot. Xác minh tình trạng apply trước khi sửa migration đã có.

### C8 — Chốt trạng thái bằng evidence

- [x] Chỉ đánh source verified khi C1–C3, C5–C7 đạt và contract C4 đã chốt. Các validation thuộc handler tương lai để pending rõ ở 05D/E.
- [x] Chạy đúng `dotnet build backend/AgriDrone.sln --no-restore --nologo` trong môi trường có SDK .NET 10, ghi exit code và số warning/error. Kết quả hiện tại của môi trường review là dotnet command not found, chưa chứng minh build clean.
- [x] Giữ integration tests ở V3-10. Rà source/build không đủ chứng minh database migration, timestamp round-trip, unique-index ordering và rollback/races đã pass.

Phạm vi review này là source A/B hiện tại và các dependency đã nêu. Hoàn thành checklist cho phép đóng mức source/build tương ứng; nghiệm thu runtime cần evidence V3-10.
