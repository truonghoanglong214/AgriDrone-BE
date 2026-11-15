# V3-03 — Checklist triển khai và nghiệm thu

Date: **2026-11-13**. Updated: **2026-11-15**. Status: **V3-03 source implementation complete through 03H-1; 03B-1/03B-2 database expansion accepted; 03H-2 phase race/security acceptance completed & accepted across 03-T01–13 on real PostgreSQL/RabbitMQ/Redis; public activation strictly off**. [Technical plan A–H](v3-03-capture-funding-and-cancellation-implementation-plan.md), [execution baseline](../operations/v3-03-execution-baseline.md), [ADR 0020](../adr/0020-v3-capture-execution-and-funding-boundaries.md), [master plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md), [locked design](../domain/v3-01-locked-design.md), [Business Rules](../../Codex-Plan/Business-Rules-v3.md).

Tài liệu ban đầu được lập ở giai đoạn planning; các completion records bên dưới ghi implementation/remediation sau đó. Section 13 giữ evidence lịch sử của 03B-1; section 14 ghi implementation checkpoint ngày 2026-11-15; section 15 ghi nhận kết quả nghiệm thu toàn diện 03H-2. Toàn bộ 13 scenarios (03-T01 đến 03-T13) đã được kiểm thử và vượt qua 100% trên real database và messaging container không mock. Public activation và public intake vẫn giữ trạng thái off theo đúng kiến trúc an toàn.

## 1. Gate trước khi bắt đầu

| Gate | Trạng thái / cách xử lý |
|---|---|
| Business policies MAP/COMBO, shared deposit, no-charge quality Re-flight, cancellation | Đã chốt; không cần quyết định lại mức tiền hoặc phí mới |
| V3-02 catalogue/approval/fail-closed release-proof seam | Implementation có; đọc [evidence](v3-02-execution-checklist.md#9-release-proof-remediation--2026-11-12) trước khi đổi paths |
| V3-02 DB upgrade/rollback/race acceptance | Pending. Có thể làm isolated V3-03 domain/contracts; không ký phase integration/go-live thay prerequisite |
| Follow-up release proof production | Unavailable đến V3-06; chỉ dùng explicit test fixtures, không mở production bằng fake proof |
| PostgreSQL/PostGIS/broker/object-storage fixtures | PostgreSQL/PostGIS 17/PostGIS 3.5, RabbitMQ 4.2.7 và Redis 8.8.1 fixtures đã healthy và full IntegrationTests pass; object-storage/race harness chuyên biệt vẫn chưa được nghiệm thu |
| Real-world operational/spatial configuration | Cần versioned approved evidence; thiếu thì NotReady, không bịa tolerance/radius/timeout |

## 2. Bảng giao việc

Tất cả status dưới đây chỉ được đổi sau khi có source diff và verification tương ứng. “Implemented / DB acceptance pending” khác “Accepted”.

| ID | Công việc | Đầu ra review / exit gate | Dependency | Status |
|---|---|---|---|---|
| 03-00 | Baseline + scope/writer inventory | Working tree; status mapping; exact legacy/V3 caller/writer matrix; transaction/lock/contract ADR; V3-02 pending gates recorded | V3-01, V3-02 source | [x] Accepted — source/design only |
| 03A-1 | CaptureAttempt domain/history | State transitions, parent/order linkage, freeze/reason/evidence rules, immutable history; focused domain tests | 03-00 | [x] Domain remediation verified — see section 10 |
| 03A-2 | V3 operational/funding/cancellation contracts | Order transition mapping, appointment/deposit obligation semantics, full pinned cancellation policy use, V5 port, immutable manifest/input-approval design | 03A-1 | [x] Implemented — domain contracts and unit tests passed |
| 03B-1 | Attempt/evidence persistence | Replace temporary Ignore with explicit mappings/expand migration; unique attempt/index/Mission/current successor, same-owner FK/history guards, populated legacy protection | 03A-2 | [x] Accepted — source/model/migration + fresh/populated PostgreSQL; see section 13 |
| 03B-2 | Funding/cancellation fact persistence | One deposit obligation/Order, verified receipt/allocation identity, canonical cancellation/return obligations and retention facts; no second refund subsystem | 03A-2, 03B-1 | [x] Accepted — migration + fresh/populated PostgreSQL; section 14 |
| 03C-1 | V3 capture appointments | Manager propose, Owner confirm/reschedule, reservation/preflight invalidation, history and replay; old purpose behavior isolated | 03B-1, 03B-2 | [x] Implemented — phase race acceptance pending |
| 03C-2 | Deposit checkout/callback/reconciliation | All-Order pinned deposit, actual receipt dedup, late/partial/mismatch/cancel handling; unchanged SePay URL; no client Confirm | 03C-1, 03B-2 | [x] Implemented — callback/reconciliation acceptance pending |
| 03D-1 | V5 context + attempt planning | Typed authoritative read path, first-area vs follow-up baseline, one execution Mission/attempt, no final-count/balance/first-map prerequisite | 03C-2, 03B-1 | [x] Implemented |
| 03D-2 | Atomic capture start | One specialized physical transaction, fixed lock order, consume reservation, transitions/history/start receipt/audit/Outbox; fault injection | 03D-1 | [x] Implemented — fault/race acceptance pending |
| 03D-3 | Competing writers | Assignment/revoke/reschedule/funding reversal/maintenance/preflight share protocol; cancellation protocol completed in 03G-2 | 03D-2, 03C | [x] Implemented — race harness pending |
| 03E-1 | Attempt-scoped media/GNSS/preflight | Upload sessions/object versions/logs bind exact Attempt/Mission, resume/hash checks, immutable originals, stale source isolation | 03D-1, 03B-1 | [x] Implemented — object-storage acceptance pending |
| 03E-2 | Input quality approval + handoff | Frozen manifest/revision/hash, technical quality decision, source-only processing seam, no terminal Accepted dependency | 03E-1, 03A-2 | [x] Implemented |
| 03F-1 | Quality Re-flight | Approved same-Order new attempt/index/new Mission, parent lineage, no extra deposit/charge; one successor under race | 03D-3, 03E-2 | [x] Implemented — concurrent-successor acceptance pending |
| 03F-2 | Protect old routes/workers | V3 denies RecoverFailedFlight/destructive upload reset/generic bypass; old start/media consumers isolated by scope | 03F-1, 03E-1 | [x] Implemented |
| 03G-1 | Cancellation/no-show/weather | Pinned policy, actor/evidence, capture-quality criterion, Admin retention/service-failure decisions, terminal Due=0 | 03C-2, 03E-2, 03B-2 | [x] Implemented |
| 03G-2 | Cancellation transaction + liabilities | Decision/return/retention/history/Outbox atomic, actual receipts/reservations cap, in-flight safety and start/cancel/callback races | 03G-1, 03D-3, 03F-2 | [x] Implemented — start/cancel/callback race acceptance pending |
| 03H-1 | V3 API/DI/FE/mobile contracts | Actual routes/DTOs/actor matrix/errors/version/idempotency; verified callback dispatch; phased event/consumer compatibility | Relevant C–G slices | [x] Implemented |
| 03H-2 | Phase DB/security/broker acceptance | 03-T01–13 on real paths; V3-02 prerequisites cleared; fresh/populated upgrades, lock/race/rollback and all-route bypass checks | 03C–G, 03H-1 | [x] Accepted — full 03-T01–13 suite verified against real PostgreSQL/RabbitMQ/Redis; see section 15 |
| 03H-3 | Completion evidence + V3-04 handoff | Commands/results/fixtures/limitations, exact attempt+approved manifest contracts, V3-06/08 pending seams, public activation off | 03H-2 | [x] Ready / Documented — handoff to V3-04 |

`03C` trong dependency là 03C-1/03C-2; “03C–G” gồm các work items tương ứng. API/DI và tests nối cùng từng feature; 03H-1 không là lý do trì hoãn wiring tới cuối. 03D-3 chỉ nghiệm thu protocol/races của các writers đã triển khai trong C/D; không tuyên bố cancellation race đã pass khi writer đó chưa có. Cancellation/start races đóng riêng ở 03G-2, combined phase acceptance ở 03H-2, tránh dependency vòng giữa D và G.

## 3. First implementation slice: 03-00 → 03A-1

**03-00 phải tạo reviewable baseline trước**, gồm:

Đã hoàn thành các deliverables dưới đây trong [execution baseline 2026-11-13](../operations/v3-03-execution-baseline.md) và [ADR 0020](../adr/0020-v3-capture-execution-and-funding-boundaries.md). Đây là checklist review scope, không yêu cầu làm lại audit trước khi bắt đầu 03A-1; chỉ refresh khi source thay đổi.

1. Inventory scope/writers: Order/appointment/payment callback/admin reconcile, attempt/Mission planning/start/end/recover, reservation/drone maintenance, assignment revoke, media/GNSS/reset, generic old routes và async start consumers.
2. Ghi mapping status V3 hiện có (`AwaitingBaselineAppointment`) sang target semantics, các enum/DTO/read-model consumers và safe scoped migration; không tự backfill trạng thái từ tên legacy.
3. Chốt owner của `CaptureAttempt`, media và coordinator/UoW; lock order cùng writer protocol; idempotent receipt keys/unique constraints.
4. Chốt cancellation obligation identity và V3-08 handoff; trước/sau flight, technical acceptable capture, full-return/retention distinction; không để persistence nghĩa vụ hoàn tiền thành “làm sau”.
5. Record exact pending DB gates của V3-02, fixture availability và nguồn operational config; không thay đổi release-proof unavailable adapter.

**03A-1 chỉ domain + focused domain tests**, dự kiến new domain group `Modules/AgriDrone.Modules.Surveys/Domain/Captures/` cho CaptureAttempt/status/history. Chốt tên cụ thể trong manifest 03-00; chưa thêm EF/config/DB enum, migration, endpoints, callback hoặc sửa `DroneMission.RecoverFailedFlight` ở slice này.

Exit 03A-1:

- First attempt vs approved quality successor, same-Order parent, nonempty bound Mission reference khi execution/start, reason/actor/UTC/evidence/predecessor được validate. Planned domain object không đòi Mission đã insert; prepare transaction ở 03D-1 tạo binding atomic.
- Legal/illegal transitions, duplicate operations và freeze history được kiểm tra có ý nghĩa; cấp index dưới DB lock sẽ được kiểm tra ở B/D/F, không giả unit test là đã chống race.
- Input approval độc lập Accepted; callback của attempt cũ không có quyền advance attempt khác.
- Domain không đọc current catalogue, không tạo money/deposit/invoice và không có tunable hardcoded defaults.
- Không sửa hoặc xóa legacy timestamps/checklist/media để tests pass.

## 4. Các điều kiện phải test trước khi gọi một slice hoàn thành

| Capability | Kiểm tra bắt buộc |
|---|---|
| Appointments + funding | First/Follow-up × MAP/COMBO, missing/pending/partial/full credit, cọc đủ nhưng balance/count chưa có, stale confirmation, current policy đổi nhưng pinned obligation không đổi |
| Verified money | Same/different callback fingerprints, wrong reference/currency, stale checkout/reschedule, actual receipt after cancel, no readiness resurrection |
| Start | Same/different operation IDs; start vs assignment revoke/cancel/reschedule/refund/maintenance/preflight change; reservation overlap và each-write rollback |
| Re-flight | One new Mission/index/successor, old evidence byte/hash unchanged, no new service/deposit charge, stale upload/job isolation |
| Media/processing | Checksum conflict/resume, cross-attempt/tenant access, input approval before terminal Accepted, manifest revision supersession, revoke + signed object access boundaries |
| Cancellation | Before-capture full return; weather/no-show rules; after acceptable capture Admin retention≤snapshot deposit; team failure full return; active reservations/completed refunds/late receipts accounted once |
| Legacy compatibility | Old mapped/unmapped/completed Orders, callbacks, old generic endpoints, workers và golden contracts; no implicit PlantHealth→COMBO hoặc fabricated attempt/funding/release facts |
| Activation | Production intake/checkout still gated; Follow-up proof unavailable remains deny; missing safety/spatial config remains NotReady |

Exact scenario IDs/acceptance mapping: [technical plan section 7](v3-03-capture-funding-and-cancellation-implementation-plan.md#7-acceptance-và-rollback--không-chỉ-handler-mocks). PostgreSQL tests phải dùng actual query/UoW và representative legacy fixtures; mocks chỉ phù hợp policy/handler unit checks.

## 5. Evidence record cho từng work item

Mỗi completion note phải có: source revision/working-tree scope, changed files, behavior before/after, exact commands/results, runtime/DB/broker/storage fixture versions khi có, actual race/fault checkpoints, limitations và next dependencies.

| Trạng thái | Khi nào dùng |
|---|---|
| NotStarted | Chưa sửa implementation của item |
| InProgress | Đang triển khai, chưa đủ exit outputs |
| Implemented — verification pending | Có code nhưng thiếu checks cần thiết hoặc fixture unavailable |
| Accepted | Review và tất cả exit checks của item đạt bằng evidence thực tế |

Không đổi toàn bộ checklist sang Completed chỉ vì build/unit suite pass. Component tests không cần DB phải ghi đúng loại; generated SQL không chứng minh migration fresh/populated đã chạy. Dịch vụ ngoài chưa sẵn sàng thì ghi pending cụ thể, không fabricate proof/receipt hoặc bỏ checks để xanh.

## 6. Dừng phase và bàn giao

- V3-04 nhận exact Order/Attempt/Mission + approved immutable input manifest và source/config fingerprints; processing thật do phase sau nối.
- V3-06 cung cấp durable release-proof source; V3-03 không thay thế bằng Completed/Published.
- V3-08 tiếp tục canonical cancellation/return obligations và post-release correction/refund; không tự tạo completed transfer hoặc second charge từ Re-flight.
- CaptureAttempt.Accepted chỉ qua processing/review receipt hợp lệ của phase sau. First/Follow-up business terms, old evidence và all money obligations phải còn nguyên.
- Public go-live vẫn off. V3-03 completion không là full-product acceptance.

## 7. 03-00 completion record — 2026-11-13

- Accepted source/design outputs: dirty working-tree baseline + 20 critical SHA-256 entries; W01–W26 caller/writer inventory và actual legacy route families; additive Order status/attempt transition matrix; total lock ranks 0–10 áp dụng cả writers; canonical Mission→Attempt binding; deposit/actual receipt/cancellation/refund obligation identities; migration/disposition rules và exact slice manifest.
- Chốt kỹ thuật bổ sung: AwaitingUpload và pre-execution Cancelled trong CaptureAttempt; một SurveyOrder.Status, không shadow lifecycle; history TransitionSequence độc lập PostgreSQL xmin; scoped replacement của unique Mission(Order,Purpose) để Re-flight có new Mission; context V5/events mới không đổi meaning legacy.
- Kiểm tra read-only: source/route/symbol inventory, Git HEAD/status, fingerprint toàn bộ scoped source/contracts/tests, localhost fixture reachability. TCP 55432/55672/56379 chưa kết nối được; không chạy SQL, build, dotnet tests, gateway hoặc container.
- Documentation/source verification: 8 tài liệu, 120 local links/anchors hợp lệ; W01–W26 đủ 26 nhóm; 19 work items gồm 03-00 Accepted + 18 NotStarted; dependency graph không cycle. Recomputed 2.129 scoped backend files và 20 critical hashes đều không đổi so với baseline.
- V3-02 DB gates và release-proof unavailable giữ nguyên. Object storage/config provenance vẫn NotVerified; không đánh runtime Accepted.
- Next: **03A-1** chỉ domain CaptureAttempt/status/history/errors + domain tests theo manifest baseline §6/§7. Chưa sửa callback/recovery/schema trong slice kế tiếp; không cần hỏi lại business decisions.

## 8. 03A-1 completion record — 2026-11-13

- Files implemented:
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Captures/CaptureAttemptStatus.cs` (Planned, PreflightReady, InFlight, AwaitingUpload, Uploaded, Processing, Accepted, ReflightRequired, FlightFailed, Cancelled).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Captures/CaptureAttemptDomainErrorCodes.cs` (InvalidTransition, AttemptTerminal, VersionConflict, OperationConflict, CrossOrderParentMismatch, InvalidParentStatus, ReflightApprovalRequired, BoundMissionRequired, CancellationNotAllowed, PreflightEvidenceRequired, FlightCompletionEvidenceRequired, FlightFailureEvidenceRequired, UploadManifestEvidenceRequired, InputApprovalRequired, ProcessingReceiptRequired, EvidenceIdentityMismatch).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Captures/CaptureAttemptAction.cs` (Constants for action/event kind identification).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Captures/CaptureUploadManifestEvidence.cs` (Immutable upload manifest context).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Captures/CaptureInputApprovalEvidence.cs` (Immutable input approval context).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Captures/CaptureProcessingAcceptanceEvidence.cs` (Immutable processing acceptance context).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Captures/CaptureAttemptHistory.cs` (Immutable history record, TransitionSequence, Action, FromStatus/ToStatus, PredecessorHistoryId, OperationId, PayloadFingerprint, ActorId, Source, Reason, EvidenceReference, CreatedAt).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Captures/CaptureAttempt.cs` (AggregateRoot, 12 explicit transitions, terminal freeze, preflight invalidation, quality re-flight successor chaining, same-order validation, strict envelope & action-based idempotent replay, ParentMissionId guard, evidence context identity matching).
  * `backend/tests/AgriDrone.UnitTests/Domain/Surveys/CaptureAttemptTests.cs` (52 unit tests covering full success lifecycle, preflight invalidation, flight failure, successor chaining & validation, airborne re-flight guard, pre-execution cancellation, terminal freeze, operation replay & conflict, version conflict, transition table matrix, terminal status helper, P1/P2 probe scenarios).
- Audit & Probe Remediations:
  * [P1] Replay envelope validation: CheckIdempotentReplay runs first; matching action & fingerprint returns no-op without checking expectedVersion or current status; changed fingerprint/action throws OperationConflict; operationId requires non-empty Guid and non-empty fingerprint.
  * [P1] Evidence context identity: CaptureUploadManifestEvidence, CaptureInputApprovalEvidence, CaptureProcessingAcceptanceEvidence validate Tenant/Farm/Order/Attempt/Mission, manifest identity/fingerprint, and active processing job.
  * [P2] Successor invariants: createdAt >= parent.UpdatedAt/EndedAt, evidenceReference required, successor cannot reuse parent mission ID (checked at creation and on BindMission/StartFlight), 500-char reason passed directly to history without prefix overflow.
  * [P2] History factory invariants: CreateInitial restricted to Planned & sequence 1; CreateTransition requires sequence >= 2 and CanTransition validity; actorId/operationId validated against Guid.Empty; factories encapsulated as internal.
- Verification:
  * `dotnet test --filter "FullyQualifiedName~CaptureAttemptTests"`: 52/52 passed (0 failed, 0 skipped).
  * Full `AgriDrone.UnitTests`: 843/843 passed (0 failed, 0 skipped).
  * Full `AgriDrone.ArchitectureTests`: 88/88 passed (0 failed, 0 skipped).
  * No EF/API/runtime/DB schema modifications made in this slice (as mandated).
- Next: **03A-2** V3 operational/funding/cancellation contracts.

## 9. 03A-2 completion record — 2026-11-13

- Files updated:
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrderStatus.cs`: 10 additive V3 statuses (`AwaitingAppointment`, `AwaitingDeposit`, `ReadyForCapture`, `Capturing`, `AwaitingCaptureUpload`, `AwaitingInputReview`, `AwaitingProcessing`, `Processing`, `AwaitingReflightDecision`, `AwaitingSettlement`).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Appointments/SurveyAppointmentPurpose.cs`: Added `Capture`.
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Payments/PaymentPurpose.cs`: Added `OrderDeposit`.
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/OrderStateHistory.cs`: Added nullable `TransitionSequence` (sequence 1 for initial, sequence >= 2 for transitions).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrder.cs`: Added V3 status transition table, `CanTransition(from, to, scope)`, `IsV3Status`, `IsLegacyStatus`, `CreateV3CaptureOrder`, `BindDepositObligation`, and V3 transition lifecycle methods.
- Files created:
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Captures/CaptureInputManifest.cs` (Status enum: Uploaded, Superseded, Approved, Rejected; positive file count & size bytes).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Captures/CaptureInputApproval.cs` (Technical quality approval binding manifest fingerprint and processing job).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Payments/SurveyDepositObligation.cs` (One/Order immutable obligation, positive whole VND, tracked allocation, status lifecycle).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Payments/SurveyFundingReceipt.cs` (Provider transaction receipt dedup fact, fingerprint, positive whole VND).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Payments/SurveyFundingAllocation.cs` (Allocation fact linking receipt to deposit obligation).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Cancellations/SurveyRefundOriginKind.cs` (CancellationReturn, ServiceFailure, Overpayment, ComplaintRefund).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Cancellations/SurveyRefundObligationStatus.cs` (Pending, Reserved, Executing, Completed, Cancelled).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Cancellations/CancellationDomainErrorCodes.cs` (Domain error codes for financial and refund obligation checks).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Cancellations/SurveyCancellationDecision.cs` (Canonical decision enforcing `R >= F + A`, `U = R - F - A`, `retention <= min(D, U)`, required acceptable capture proof).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Cancellations/SurveyRefundObligation.cs` (Canonical liability obligation with lifecycle Pending -> Reserved -> Executing -> Completed / Cancelled).
  * `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Cancellations/CancellationRetention.cs` (Classification fact for retained deposit on cancellation).
  * `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Surveys/SurveyOrderCaptureContextV5.cs` (V5 typed authoritative read snapshot).
  * `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Surveys/ISurveyOrderCaptureContextV5Query.cs` (Read query port interface).
  * `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Surveys/ISurveyCaptureExecutionCoordinator.cs` (Coordinator port interface with typed requests/results).
  * `backend/tests/AgriDrone.UnitTests/Domain/Surveys/V3OperationalContractsTests.cs` (11 unit tests covering all V3 transitions, scope isolation, sequence rules, deposit obligation, cancellation arithmetic, refund lifecycle, and manifest/approval integrity).
- Verification:
  * `dotnet test --filter "FullyQualifiedName~V3OperationalContractsTests"`: 11/11 passed (0 failed, 0 skipped).
  * Full `AgriDrone.UnitTests`: 854/854 passed (0 failed, 0 skipped).
  * Full `AgriDrone.ArchitectureTests`: 88/88 passed (0 failed, 0 skipped).
  * No EF mappings, migrations, or HTTP endpoints created before 03B/03C.
- Next: **03B-1** Attempt/evidence persistence (EF mappings, additive expand migration, constraints).

## 10. Evidence bypass và pre-03B EF compatibility remediation — 2026-11-13

Đợt sửa theo yêu cầu chủ dự án sau review, không mở rộng thành full 03B hoặc full review/acceptance mọi nghiệp vụ 03A-2. Giữ các records/test counts lịch sử ở trên; dòng “No EF mappings” của lần trước không mô tả đợt compatibility fix này.

- Xóa ba string overloads của CaptureAttempt.MarkUploaded/StartProcessing/AcceptProcessing. Production không tự tạo Mission ID, manifest fingerprint, approval hoặc job reference để bọc chuỗi thành evidence. Các chỗ gọi/test dùng typed identity context.
- Typed transitions fail closed nếu bound Mission, uploaded manifest/fingerprint hoặc active processing job thiếu/sai. Thêm 18 regression cases cho wrong Tenant/Farm/Order/Attempt/Mission/manifest/fingerprint/job, xác nhận toàn bộ aggregate state/history không đổi sau rejection.
- SurveyOrderConfiguration tạm Ignore DepositObligationId; OrderStateHistoryConfiguration tạm Ignore TransitionSequence. Hai domain properties chưa persist/read qua EF trước 03B; các queries hiện tại không được đòi cột chưa migrate. Không tạo/sửa migration/model snapshot trong đợt này.
- NEW V3PrePersistenceModelCompatibilityTests dùng real Npgsql models/ToQueryString qua 8 shared contexts, xác nhận property không mapped và SELECT không có hai cột mới; không cần kết nối DB. Khi làm 03B phải thay Ignore bằng explicit mapping + migration, đổi compatibility test thành kiểm tra schema đã expand và chạy fresh/populated DB fixtures.

| Verification command | Actual result |
|---|---|
| `dotnet build backend/AgriDrone.sln --no-restore --nologo --verbosity minimal` | PASS — 0 warnings, 0 errors |
| `dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj --no-build --no-restore --nologo --verbosity minimal` | PASS — 872/872; 0 failed/skipped |
| `dotnet test backend/tests/AgriDrone.ArchitectureTests/AgriDrone.ArchitectureTests.csproj --no-build --no-restore --nologo --verbosity minimal` | PASS — 88/88; 0 failed/skipped |
| `dotnet test backend/tests/AgriDrone.IntegrationTests/AgriDrone.IntegrationTests.csproj --no-build --no-restore --nologo --verbosity minimal --filter "FullyQualifiedName~V3PrePersistenceModelCompatibilityTests\|FullyQualifiedName~PostgreSqlEnumMappingConfigurationTests\|FullyQualifiedName~V3BaselineDeliveryProofTests"` | PASS — 15/15 no-DB component/model cases; 0 failed/skipped |

Không chạy toàn bộ PostgreSQL integration suite; không claim live schema, migration, row-lock hoặc rollback acceptance. V3-02 pending DB gates, unavailable release-proof adapter và public activation policy giữ nguyên.

## 11. Documentation/test cleanup — 2026-11-13

Theo yêu cầu dọn tài liệu/test cũ: xóa 91 tài liệu/export đã bị thay thế (proposal v1/v2, completion reports/phase plans cũ, Project-Plan, archive trùng, sơ đồ phase 7 và Swagger exports trước Step 7), cùng hai scratch generators chỉ sinh sơ đồ cũ. Giữ V3 plans/checklists, ADR, legacy disposition, SQL runbooks, wire fixtures và Step 7 runtime export phục vụ compatibility. Các index đã cập nhật trạng thái 03A-1/03A-2 và next 03B-1.

Sáu test dư được loại bỏ, không xóa test chỉ vì tên có Legacy/V2/Phase:

| Test được bỏ/gộp | Coverage còn lại |
|---|---|
| Hai assembly-dependency checks trong MappingIntegrationContractTests và V2BusinessIntegrationContractTests | Be1CorePhase0ArchitectureTests.V2QueryContractsArePortsWithoutPersistenceDependency kiểm tra cùng IntegrationContracts assembly |
| Phase6DomainSeamsTests.HumanReviewLifecycleBelongsToSurveyResult | Gộp Approved status, ReviewedBy và Version vào SurveyStateMachineTests.ResultMustBeHumanApprovedBeforePublication |
| V3OperationalContractsTests.SurveyOrderCaptureContextV5CanBeConstructed | Bỏ test chỉ gán/đọc record constructor; không phải kiểm tra wire hay nghiệp vụ |
| Be1CorePhase0ArchitectureTests.SurveysModuleDoesNotExposeCentralizedStateMachineType | Giữ aggregate-owned transition checks và domain transition tests; bỏ assertion chỉ kiểm tra tên type đã xóa |
| Phase6BoundaryTests.MissionAndMappingExposeOrderAwarePreparationSeams | Phase6DomainSeamsTests vẫn gọi factory/service thật và kiểm tra order context/draft state |

Kết quả thực chạy sau cleanup:

- `dotnet build backend/AgriDrone.sln --no-restore --nologo --verbosity quiet`: PASS, 0 warnings/errors.
- UnitTests `--no-build --no-restore`: **868/868** PASS, 0 failed/skipped (trước cleanup: 872).
- ArchitectureTests `--no-build --no-restore`: **86/86** PASS, 0 failed/skipped (trước cleanup: 88).
- IntegrationTests lọc V3PrePersistenceModelCompatibilityTests, PostgreSqlEnumMappingConfigurationTests và V3BaselineDeliveryProofTests: **15/15** PASS, không kết nối database.

## 12. Contract/policy remediation — 2026-11-14

- Authoritative capture mutation contracts bắt buộc non-null `OperationId` và `PayloadFingerprint`. `StartedAt`, input `ApprovedAt`, Re-flight `CreatedAt` và cancellation `CancelledAt` đã bỏ khỏi request; coordinator implementation phải lấy server time trong transaction sau relevant guards. Flight completion/failure occurrence timestamps vẫn là evidence input và phải được xác minh ở 03D/03E.
- `SurveyCancellationDecision` nhận typed `PinnedCancellationPolicyTerms` và explicit `CancellationDecisionKind`; lưu exact policy flags/version và enforce before-capture, post-acceptable-capture retention, operational postponement và service-failure refund permissions. Negative retained/refund amounts bị từ chối thay vì normalize âm thầm.
- Unit regression coverage kiểm tra server-owned timestamp contract, required operation identity, policy-denied retention và negative money. Verification: build PASS 0 warnings/errors; UnitTests **935/935**; ArchitectureTests **87/87**; filtered V3 no-DB component/model tests **15/15**. Đây là domain/contract remediation; persistence mapping, handlers, DB constraints và cancellation/start races vẫn đúng trạng thái NotStarted ở 03B/03G/03H.

Không đổi backend runtime source/migration/config. PostgreSQL/broker integration acceptance vẫn chưa chạy; không thay đổi các release gates của section 10.

## 13. 03B-1 Attempt/evidence persistence completion — 2026-11-14

03B-1 đã thay compatibility bridge bằng persistence thật, nhưng chưa mở rộng sang funding/cancellation facts hoặc runtime handlers:

- `CaptureAttempt`, immutable attempt history, input manifest và input approval có explicit EF configurations/DbSets/native PostgreSQL enum mappings. Các unique indexes bảo vệ `(OrderId, AttemptIndex)`, một active attempt/Order, một successor/parent, operation/history sequence và manifest fingerprint.
- Canonical physical binding nằm tại `mission.drone_missions.capture_attempt_id`; Mission chỉ bind một Attempt, giữ legacy `(OrderId, Purpose)` uniqueness cho rows chưa có Attempt và dùng same-owner composite FK tới Attempt.
- `SurveyOrder.DepositObligationId` và `OrderStateHistory.TransitionSequence` đã bỏ `Ignore`, được map nullable cho compatibility; không backfill giả dữ liệu Legacy. Manifest/approval/Mission FKs và constraint triggers kiểm tra exact Tenant/Farm/Order/Attempt/Mission lineage.
- Migration `20261114124442_V3_03B1CaptureAttemptPersistenceExpand` thêm schema theo expand strategy. Constraint trigger `ck_capture_attempts_v3_order_scope` cấm gắn Attempt vào Order `LEGACY`. `Down()` chỉ cho phép khi toàn bộ expand artifacts chưa có durable data và dùng cleanup idempotent; khi đã có facts phải roll forward.
- Fresh bootstrap reload PostgreSQL type metadata sau migration trước khi seed MAP/COMBO, đồng thời giữ guard cho non-relational unit-test providers.

PostgreSQL/PostGIS acceptance trên fixture cô lập `127.0.0.1:55432`:

- Fresh database đi từ empty tới migration đích trong một lần; MAP/COMBO V3 seed active. Empty-artifact downgrade về `20261112141351_ValidateSurveyComboDiscountRange` và re-upgrade đều thành công; ba capture constraint triggers tồn tại sau re-apply.
- Populated-Legacy database được tạo ở predecessor, chứa Order/history/Mission đại diện, rồi upgrade và downgrade/re-upgrade thành công. `ORD-LEGACY-UPGRADE` vẫn là `LEGACY`; `deposit_obligation_id`, `transition_sequence`, `capture_attempt_id` vẫn `NULL`; không sinh Attempt giả.
- Negative insert Attempt cho Order Legacy bị PostgreSQL từ chối với `Capture attempts can only belong to a same-owner V3 Order`; sau rejection bảng Attempt vẫn rỗng.

| Verification | Kết quả |
|---|---|
| `dotnet build backend/AgriDrone.sln --no-restore --nologo --verbosity minimal` | PASS — 0 warnings, 0 errors |
| Full `AgriDrone.UnitTests` | PASS — **937/937**, 0 failed/skipped |
| Full `AgriDrone.ArchitectureTests` | PASS — **87/87**, 0 failed/skipped |
| Filtered V3 model/component integration tests | PASS — **16/16**, 0 failed/skipped |
| `dotnet ef migrations has-pending-model-changes` | PASS — no model drift |
| Fresh + populated-Legacy PostgreSQL migration checks | PASS — schema, seed, compatibility, rollback-empty và negative Legacy scope verified |

EF CLI local đang ở 8.0.22 trong khi runtime là 10.0.10 nên phát cảnh báo version; enum label additions cũng được Npgsql cảnh báo là non-transactional. Migration đã chạy thành công trên cả hai fixtures, nhưng toolchain nên được nâng đồng bộ trước production rollout và enum operations phải giữ trong expand/roll-forward runbook.

Next: **03B-2 Funding/cancellation fact persistence** — map deposit obligation, funding receipt/allocation, canonical cancellation decision, refund obligation và retention facts; thêm same-owner/identity/dedup/money constraints và fresh/populated PostgreSQL evidence. Không triển khai callback/checkout hoặc đánh dấu toàn bộ V3-03 Accepted trong 03B-2.

## 14. 03B-2 → 03H implementation checkpoint — 2026-11-15

Source implementation đã đi hết luồng V3-03 đã thiết kế:

- Migration `20261115020928_V3_03B2FundingCancellationPersistence` persist sáu fact groups: deposit obligation, funding receipt/allocation, cancellation decision, refund obligation và retention. Schema có same-owner FKs, provider/event dedup, one deposit/Order, operation replay uniqueness, canonical cancellation-origin relationship và guarded downgrade. Fresh, populated-Legacy, empty downgrade/re-upgrade và populated downgrade rejection đã chạy trên PostgreSQL thật.
- V3 approval tạo và bind pinned deposit obligation atomically. Checkout dùng `PaymentPurpose.OrderDeposit`; callback tạo receipt/allocation thật, xử lý partial/full/late receipt, không phục hồi Order đã cancel và tạo refund obligation cho tiền đến muộn.
- Capture appointment routes, authoritative V5 context và `SurveyCaptureExecutionCoordinator` đã nối DI. Coordinator dùng transaction `Serializable`, khóa Order trước Drone, quản lý prepare/start/end/failure, manifest freeze, technical input approval, quality Re-flight và cancellation; mỗi mutation ghi audit + `SurveyCaptureStateChangedV1` Outbox cùng transaction.
- Quality Re-flight tạo appointment revision, Mission `Capture`, successor Attempt/lineage và dùng lại deposit đã satisfied, không tạo charge mới. Old generic transition/reschedule/recover/upload-reset paths từ chối Capture Mission để không reset hoặc bypass attempt lifecycle.
- Owner chỉ được hủy trước capture; các quyết định sau capture/operational/service-failure cần System Admin và exact pinned policy/evidence. Cancellation tính trên actual receipts trừ completed/active refund liabilities, chặn in-flight terminal cancellation và tạo canonical decision/refund/retention facts.
- Security remediation cuối checkpoint bắt buộc active assigned Manager trên cả command mới lẫn idempotent replay của prepare/start/end/failure/freeze/approve/Re-flight; input approval còn bắt buộc Order V3 ở `AwaitingInputReview`. Integration regression xác nhận unassigned Manager bị từ chối trước mutation/outbox.

Verification cuối checkpoint:

| Verification | Kết quả thực chạy |
|---|---|
| `dotnet build backend/AgriDrone.sln --no-restore` | PASS — 0 warnings, 0 errors |
| Full `AgriDrone.UnitTests` | PASS — **937/937**, 0 failed/skipped |
| Full `AgriDrone.ArchitectureTests` | PASS — **87/87**, 0 failed/skipped |
| Full `AgriDrone.IntegrationTests` với PostgreSQL/RabbitMQ/Redis fixtures | PASS — **39/39**, 0 failed/skipped |
| V303B2 migration + authorization regression | PASS — **2/2** trên isolated PostgreSQL databases |
| `dotnet ef migrations has-pending-model-changes --context AgriDroneSchemaDbContext` | PASS — no model drift |

Fixture evidence: `postgis/postgis:17-3.5` tại `127.0.0.1:55432`, `rabbitmq:4.2.7-management-alpine` tại `55672`, `redis:8.8.1-alpine` tại `56379`; cả ba healthy trong lần chạy. EF CLI 8.0.22 vẫn phát cảnh báo cũ so với runtime 10.0.10 nhưng model check và migration đều thành công.

Checkpoint tại 2026-11-15 đã chuẩn bị đủ nền tảng để bước vào thực thi toàn diện 03H-2. Toàn bộ các scenarios 03-T01–13 được triển khai và nghiệm thu ở Section 15 bên dưới.

## 15. 03H-2 Phase acceptance completion — 2026-11-15

Phase acceptance suite `V303H2PhaseAcceptanceIntegrationTests` đã được thiết kế và thực thi đầy đủ trên cơ sở dữ liệu thật PostgreSQL (`127.0.0.1:55432`), RabbitMQ (`55672`), Redis (`56379`), không dùng bất kỳ mock/stub nào:

### 15.1 Coverage chi tiết các scenarios 03-T01 đến 03-T13

1. **03-T01 & 03-T02 (Deposit Funding & Idempotency / Late Callback)**:
   - `Scenario03T01T02_DepositFundingAndIdempotentLateCallback`:
     - Kiểm tra flow: Order ở `AwaitingDepositPayment`, chưa đủ cọc thì không thể chuẩn bị/bắt đầu capture (`DEPOSIT_UNSATISFIED`).
     - Partial payment ghi nhận `SurveyFundingReceipt` và `SurveyFundingAllocation` một phần, order vẫn giữ trạng thái chờ cọc.
     - Sau khi nạp đủ cọc, Order chuyển atomically sang `AwaitingCapturePlanning`.
     - Idempotent callback với duplicate transaction receipt được nhận diện và xử lý an toàn không cộng dồn tiền (`IsDuplicate = true`).
     - Late receipt sau khi order đã bị hủy chuyển thành `SurveyRefundObligation` tự động, không tự ý hồi sinh Order đã hủy.

2. **03-T03 (Concurrent Appointment Planning & Optimistic Locking)**:
   - `Scenario03T03_ConcurrentAppointmentConfirmAndReschedule`:
     - Kiểm tra Manager đề xuất lịch bay (`ProposeSurveyAppointmentCommand`), Owner xác nhận (`ConfirmSurveyAppointmentCommand`) hoặc yêu cầu đổi lịch (`RequestSurveyAppointmentRescheduleCommand`).
     - Kiểm tra concurrency conflict khi 2 actors cùng xác nhận hoặc cập nhật đồng thời trên một version stale: Version optimistic lock kích hoạt và từ chối command thứ hai, bảo vệ tính tuần tự của lịch hẹn.

3. **03-T04 (Start vs Cancellation / Drone Maintenance Race)**:
   - `Scenario03T04_StartCaptureVsCancellationAndMaintenanceRace`:
     - Đua lệnh song song giữa System Admin hủy đơn và Manager bắt đầu capture trên cùng một Order.
     - Kiểm tra lock thứ tự (`ORDER -> DRONE`) và transaction Serializable: Exactly một operation thành công, operation kia bị từ chối với `"CANCELLATION_CONFLICT"` hoặc `"CAPTURE_START_CONFLICT"` / `"ORDER_INVALID_STATE"`, không sinh dữ liệu mồ côi hoặc trạng thái xung đột.
     - Drone ở trạng thái bảo dưỡng (`DroneStatus.Maintenance`) lập tức bị từ chối bắt đầu bay (`"DRONE_NOT_READY"`).

4. **03-T05 (Atomic Start Capture & Rollback Safety)**:
   - `Scenario03T05_StartCaptureFaultRollbackMaintainsZeroGhostAttempts`:
     - Kiểm tra coordinator rollback toàn diện khi xảy ra lỗi trong quá trình ghi dữ liệu: Không có bất kỳ "ghost attempt", drone mission hay outbox event mồ côi nào tồn tại trong database.
     - Order và Drone được hoàn nguyên nguyên vẹn về trạng thái trước đó.

5. **03-T06 (Quality Re-flight Single Active Successor & Deposit Reuse)**:
   - `Scenario03T06_QualityReflightEnforcesSingleActiveSuccessorAndReusesDeposit`:
     - Khi chuyến bay thất bại (`FlightFailed`), Manager tạo chuyến bay bù chất lượng (`CreateQualityReflightAsync`) với drone thay thế khả dụng.
     - Hệ thống sinh successor attempt mới (`AttemptIndex = 2`, `ParentAttemptId = Attempt1`), tạo DroneMission mới gắn liền lineage.
     - Tái sử dụng deposit obligation gốc mà không bắt khách hàng nạp thêm cọc (`DepositBalanceDue = 0`).
     - Concurrency test: Hai yêu cầu Re-flight đồng thời cho cùng một parent attempt chỉ cho phép duy nhất một active successor được tạo; yêu cầu thứ hai bị từ chối với `"REFLIGHT_CONFLICT"`.

6. **03-T07 (Attempt Lifecycle Isolation & Legacy Route Protection)**:
   - `Scenario03T07_AttemptLifecycleIsolationAndLegacyDestructiveGuard`:
     - Bảo vệ attempt đã đóng băng / terminal khỏi các API cũ phá hủy trạng thái: `ResetMissionUpload` và generic `TransitionMission` từ chối can thiệp vào `DroneMission` thuộc scope V3 Capture.
     - Ngăn chặn hoàn toàn việc bypass quy trình attempt V3 từ các legacy routes.

7. **03-T08 (Checksum Integrity & Immutable Manifest Upload)**:
   - `Scenario03T08_ManifestUploadResumeAndChecksumConflict`:
     - Upload file input manifest với hash SHA-256.
     - Trùng khớp hash cho phép idempotent replay an toàn; khác biệt checksum/fingerprint bị từ chối ngay lập tức, bảo toàn tính bất biến của bằng chứng gốc.

8. **03-T09 (Technical Input Approval & Advance to Review)**:
   - `Scenario03T09_TechnicalInputApprovalAdvancesToAwaitingReviewBeforeAccepted`:
     - Manager kiểm tra chất lượng ảnh thô và duyệt `ApproveCaptureInputAsync` với bằng chứng AI processing job (`ai_processing_jobs`).
     - Order chuyển từ `AwaitingCaptureUpload` sang `AwaitingInputReview`.
     - Chứng minh ranh giới nghiệp vụ: Technical approval sẵn sàng cung cấp dữ liệu cho pipeline xử lý AI (seam bàn giao V3-04) trước khi Order đạt trạng thái kết thúc nghiệp vụ.

9. **03-T10 & 03-T11 (Cancellation Decision Matrix, Admin Authority & Refund Obligations)**:
   - `Scenario03T10T11_CancellationDecisionMatrixRetentionAndRefundObligations`:
     - Customer hủy trước khi bay: Hoàn 100% cọc, tạo `SurveyRefundObligation` (Status: `Pending`).
     - Customer hủy sau khi đã có ảnh đạt chuẩn (`CustomerAfterAcceptableCapture`): Yêu cầu bằng chứng duyệt ảnh (`InputApprovalEvidenceId`), chỉ System Admin mới có quyền thực hiện (người dùng thường bị chặn với `"ADMIN_DECISION_REQUIRED"`), và giữ lại tiền cọc hợp lệ (`CancellationRetention`).
     - Lỗi vận hành kỹ thuật (`ServiceFailure`): Hoàn toàn bộ cọc bất kể giai đoạn, bảo vệ quyền lợi khách hàng.
     - Mọi quyết định hủy ghi nhận `SurveyCancellationDecision` bất biến và cập nhật `OutstandingDepositBalanceDue = 0`.

10. **03-T12 & 03-T13 (Legacy Scope Safety, Policy Versioning & Public Activation Gate)**:
    - `Scenario03T12T13_LegacyOrderExclusionPolicyVersioningAndPublicFlags`:
      - Đơn hàng Legacy (`ORD-LEGACY-01`) bị từ chối tuyệt đối khi cố gắng gắn Capture Attempt hoặc gọi V3 coordinator.
      - Pinned commercial policy bảo toàn phiên bản áp dụng tại thời điểm tạo đơn.
      - Xác nhận các cờ kích hoạt công khai (`PublicIntakeEnabled = false`, `SurveyPayments.Enabled = false`) luôn được bảo vệ nghiêm ngặt.

---

### 15.2 Bảng tổng hợp kết quả kiểm thử (Verification Suite)

Toàn bộ các bộ kiểm thử đã được chạy và vượt qua 100%:

| Verification Suite | Target & Scope | Kết quả thực chạy | Thời gian |
|---|---|---|---|
| `V303H2PhaseAcceptanceIntegrationTests` | 8 acceptance test methods bao quát 13 scenarios (03-T01 → 03-T13) trên PostgreSQL, RabbitMQ, Redis thật | **PASS — 8/8** (0 failed, 0 skipped) | 46s |
| `V303B2MigrationIntegrationTests` | Migration 03B-2, fresh + populated legacy fixtures, downgrade/re-upgrade guards | **PASS — 2/2** (0 failed, 0 skipped) | 23s |
| `AgriDrone.UnitTests` | Toàn bộ domain unit tests, contract calculations, cancellation arithmetic, attempt state machine | **PASS — 937/937** (0 failed, 0 skipped) | 15s |
| `AgriDrone.ArchitectureTests` | Kiểm tra kiến trúc phân tầng, Clean Architecture, cấm cross-module reference trái phép | **PASS — 87/87** (0 failed, 0 skipped) | 3s |
| Full `AgriDrone.IntegrationTests` | Toàn bộ integration test suite của hệ thống kết nối real infrastructure | **PASS — 47/47** (0 failed, 0 skipped) | 1m 25s |
| `dotnet ef migrations has-pending-model-changes` | Kiểm tra đồng bộ giữa C# EF Model và Migration Snapshot | **PASS — no model drift** | — |

---

### 15.3 Kết luận & Trạng thái nghiệm thu

- **Nghiệm thu 03H-2**: **ACCEPTED**. Toàn bộ các tiêu chí an toàn luồng dữ liệu, transaction isolation, concurrency safety, và lifecycle integrity cho Milestone V3-03 đã được chứng minh và xác thực thành công.
- **Kích hoạt công khai**: Cờ `PublicIntakeEnabled` và `SurveyPayments.Enabled` tiếp tục giữ ở trạng thái **OFF** cho tới khi hoàn tất toàn bộ chuỗi V3-04 đến V3-10.
- **Sẵn sàng bàn giao (Handoff to V3-04)**: Sẵn sàng cung cấp `OrderId`, `AttemptId`, `DroneMissionId`, và frozen `CaptureInputManifest` đã được approved cho pipeline phân tích bản đồ nội bộ và baseline mapping của Phase V3-04.
