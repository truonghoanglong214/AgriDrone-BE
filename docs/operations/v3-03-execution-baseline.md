# V3-03 / 03-00 — Execution baseline và source/writer inventory

Date: **2026-11-13**. Status: **03-00 source/design baseline complete; không phải runtime acceptance**. Theo yêu cầu thực hiện bước 03-00 của chủ dự án; chỉ tài liệu. [ADR 0020](../adr/0020-v3-capture-execution-and-funding-boundaries.md) chốt status/ownership/lock/contract decisions; [technical plan](../plans/v3-03-capture-funding-and-cancellation-implementation-plan.md), [checklist](../plans/v3-03-execution-checklist.md).

## 1. Baseline được bảo toàn

- Repository: `D:\Project\ProjectMonHoc\DoAn\AgriDrone`.
- Git HEAD: `216308638777bee1c2301d07ab29117c59715f9d` — `feat(plant-health): complete Step 7 review and publication`.
- Working tree đã dirty trước bước này: **41 tracked modified paths + 101 untracked files** dưới `backend/src` và `backend/tests`. Không commit/stash/reset/clean hoặc coi toàn bộ diff là thay đổi của 03-00.
- Source/contracts/test fingerprint: **2129 files**, SHA-256 aggregate `59652d51dfeeb738e7a5429edc68663a55758c41abd65412b5c11b088d80897a`. Scope: kết quả `rg --files backend/src backend/tests`, chỉ suffix `.cs`, `.csproj`, `.json`, `.props`, `.ps1`, `.py`, `.sh`, `.sql`, `.targets`, `.toml`, `.xml`, `.yaml`, `.yml`; respect ignore rules. Sort path strings, hash bytes từng file; aggregate UTF-8 `posixPath + NUL + fileSHA256 + newline`. Không hash credentials ngoài workspace hoặc ghi secret payload.
- Snapshot này là working-tree baseline, không chỉ HEAD vì phần lớn V3-02 còn uncommitted. Cần refresh evidence nếu người khác đổi source trước implementation; không ghi đè changes của họ.

Các cập nhật được phép trong bước này: baseline/ADR/checklist/plan/index tài liệu có liên quan. **Không sửa code/config/migrations/tests; không chạy build, dotnet tests, SQL, bank callback hoặc container.** Không tạo operation receipts giả để minh họa.

## 2. Read-only findings có bằng chứng source

| Finding | Source và tác động phải xử lý |
|---|---|
| V3 initial status vẫn dùng tên legacy | `S/Domain/Orders/SurveyOrder.cs:305`: CreateV3 → AwaitingBaselineAppointment. Migration/disposition chỉ cho V3 rows đủ provenance; state additions ở ADR 0020. |
| Legacy deposit/readiness phụ thuộc mapping/purpose | `S/Domain/Orders/SurveyOrder.cs`, `SurveyOrderReadinessPolicy.cs`, `S/Application/Features/Payments/InitiateSurveyPayment/`: không được reuse branch đó cho V3 Follow-up. |
| Checkout Paid chưa chứng minh mọi actual receipts đã dedup đúng | `ProcessSurveyPaymentEventCommandHandler.cs` có ALREADY_PAID_IGNORED; V3 cần receipt identity theo provider transaction, phân biệt actual payment mới với duplicate callback. |
| Existing payment lock không chứng minh total order | `DB/Surveys/SurveyPaymentProcessingRepository.cs` dùng join FOR UPDATE payment, survey_order. V3 dùng explicit Order-first trong rank được chốt, không dựa SQL planner lock order. |
| Một Mission/purpose/Order chặn Re-flight | `M/Infrastructure/Persistence/Configurations/DroneMissionConfiguration.cs`: unique uq_drone_missions_order_purpose trên mọi nonnull OrderId. Phải scope lại khi có attempt binding; xem ADR §2. |
| Recovery xóa flight timestamps | `M/Domain/Missions/DroneMission.cs:580` và `RescheduleOrderMissionCommandHandler.cs`: RecoverFailedFlight không được gọi cho V3. |
| Reset upload xóa media/GNSS | `M/Application/Features/Media/ResetMissionUpload/ResetMissionUploadCommandHandler.cs`: RemoveRange cho media links/assets/sessions/telemetry. V3 phải reject destructive path. |
| Finalize upload advance processing trước technical quality decision riêng | `M/Application/Features/Media/FinalizeMissionUpload/FinalizeMissionUploadCommandHandler.cs:107`: legacy MarkReadyForProcessing không là V3 CaptureInputApproval. |
| Start business sync qua event cũ | `M/.../TransitionMissionCommandHandler.cs`, `S/Infrastructure/Messaging/Consumers/{BaselineMissionStartedHandler,PaidMissionStartedHandler}.cs`: V3 cần synchronous cross-module commit, old consumer không advance V3. |
| Existing reservation là Mission schedule exclusion | Source migration `20260923112119_ConvertDronesToSystemOwned.cs`: ex_drone_missions_no_schedule_overlap dùng drone + [scheduled_at,scheduled_end_at), Scheduled/InFlight. Giữ cross-scope exclusion; thêm physical Drone.InUse/safety guards, không giải phóng chỉ vì hết giờ schedule. Chưa xác nhận live constraint. |
| Order.Version là DB token | `SurveyOrderConfiguration.cs:190` IsRowVersion; `OrderStateHistoryConfiguration.cs` hiện chỉ có index thường OrderId/OrderVersion. New V3 sequence dưới Order lock, không coi xmin là counter. |
| Cancellation snapshot chưa copy đủ flags | `S/Domain/Policies/CancellationPolicy.cs` có OperationalPostponementRefundable/ServiceFailureRefundable; commercial snapshot pin ID/version nhưng không copy hai flags. Đọc pinned row immutable, không current/effective fallback. |
| Release proof unavailable được giữ nguyên | `DB/Surveys/IV3BaselineDeliveryProofReader.cs` và shared eligibility reader fail closed. V3-03 không tự mở Follow-up production. |

Prefix dùng trong tài liệu: `S=backend/src/Modules/AgriDrone.Modules.Surveys`, `M=.../AgriDrone.Modules.Missions`, `I=.../AgriDrone.Modules.Identity`, `F=.../AgriDrone.Modules.Farms`, `DB=backend/src/BuildingBlocks/AgriDrone.Database`, `API=backend/src/AgriDrone.Api`. `...`/brace groups trong bảng là cách rút gọn source group, không phải tên file mới.

## 3. Caller/writer inventory và owner của thay đổi

Bảng này bao phủ các surfaces đã tìm thấy; các capabilities V3 còn thiếu được đánh target, không giả là đã có handler. Mỗi owner chịu cập nhật handler + repository/UoW + route/consumer guards cùng slice, không chỉ endpoint mới.

| ID | Entry/source hiện có | Ghi/đọc ảnh hưởng capture | V3 action / slice owner |
|---|---|---|---|
| W01 | `S/Application/Features/Approvals/ApproveSurveyRequest/` | New Order + initial snapshot/history | Future V3 factory dùng AwaitingAppointment, không đổi legacy Create; 03A-2/B/C |
| W02 | `API/Controllers/SystemManagerSurveyOrdersController.cs` → ProposeSurveyAppointment | Current appointment, schedule và Order | New Capture purpose/strategy, cùng ordered authorization/Order locks; 03C-1 |
| W03 | `TenantOwnerSurveyOrdersController.cs` → ConfirmSurveyAppointment/RequestSurveyAppointmentReschedule | Confirm/current revision, readiness invalidation | Scope-check, expected versions, owner authorization, histories; 03C-1 |
| W04 | `TenantOwnerSurveyOrdersController.cs` → InitiateSurveyPayment | Checkout/payment attempt | V3 OrderDeposit obligation, amount server-side, one active checkout; 03C-2 |
| W05 | `SurveyPaymentCallbacksController.cs` → ProcessSurveyPaymentEvent; `S/Infrastructure/Payments/SurveyPaymentCallbackVerifier.cs` | Verified callback/event/payment/Order readiness | Giữ URL/verifier; resolve persisted scope, lock Order rồi funding rows; distinct receipt dedup, late-cancel return; 03C-2 |
| W06 | `SystemSurveyPaymentsController.cs` → ReconcileSurveyPayment | Admin xác nhận tiền/disposition | Proof/actor/reason, same canonical receipts/allocations, không synthesize Paid; 03C-2 |
| W07 | `SystemManagerWorkController.cs` → PrepareMissionSet | Mission create/schedule, Drone choice | V3 prepare coordinator create Attempt+Mission+history/receipt cùng transaction; 03D-1 |
| W08 | `SystemManagerMissionsController.cs` → CompleteMissionPreflight | Checklist/preflight/mission refs | Append revision, current config/version, active reservation and assignment; 03D-3/E-1 |
| W09 | `SystemManagerMissionsController.cs` → TransitionMission start/end/fail/cancel | Mission/Drone/incident/Outbox, Order qua old async consumer | V3 route calls execution coordinator; physical flight ending/failure and Order history atomic; 03D-2/3 |
| W10 | Same controller → RescheduleOrderMission → DroneMission.RecoverFailedFlight | Schedule/drone, reset timestamps/checklist | Before-execution reschedule keeps revisions; post-failure V3 rejects reset, new attempt+Mission qua quality Re-flight; 03C/F |
| W11 | `MissionMediaController.cs` → CreateUploadSession/CompleteUploadSession/StartMultipartUpload/... | Upload operation, assets/object refs | Server-derived exact attempt ownership, resume/hash/object-version guard; 03E-1 |
| W12 | `MissionUploadController.cs` → FinalizeMissionUpload/ResetMissionUpload | Manifest readiness/status; destructive resets | V3 frozen manifest + input approval; reject destructive reset; 03E-2/F-2 |
| W13 | `MissionTelemetryController.cs` → ImportTelemetry; `M/.../SyncFieldNote/` | GNSS/import/field-note evidence | Exact attempt/Mission, late operation quarantine, preserve original hashes; 03E-1 |
| W14 | `SystemDronesController.cs` → ChangeDroneStatus/UpdateDrone; maintenance repository; flight-failure path | Shared drone availability/maintenance | Drone lock protocol cả legacy/nonversioned entry; no start after maintenance; 03D-3 |
| W15 | `SystemFarmManagersController.cs` → AssignPrimaryFarmManager/EndPrimaryFarmManagerAssignment | Farm/active assignment/profile | Shared scope guards trước Orders; revalidate affected current references; 03D-3 |
| W16 | `SystemManagersController.cs` → UpdateSystemManagerAvailability; `I/.../SystemManagers/Common/SystemManagerProfileMutationExecutor.cs` | Profile/user eligibility | Shared rank-1 row locking, no post-revoke start; 03D-3 |
| W17 | `I/Application/Features/{UpdateUser,DeactivateTenant}/` | User/Tenant hoạt động | Serialize status changes với start authorization; không khóa ngược Order→User; 03D-3 |
| W18 | `F/Application/Features/ArchiveFarm/ArchiveFarmHandler.cs` | Farm archive và active dependencies | Shared Farm guard; active V3 attempt/Order nằm trong dependency checks; 03D-3/H |
| W19 | `PreflightChecklistDefinitionsController.cs` → checklist definition/conditions commands | Operational/checklist current config | Versioned immutable revisions; config guard trước scope/Order, no stale preflight bypass; 03A-2/D-3 |
| W20 | `DB/Surveys/SurveyOrderOperationalContextV3Query.cs`, V4Query; `M/Infrastructure/Queries/SurveyOrderMissionPlanningQuery.cs` | Readiness/planning read path | V5 adapter/typed snapshot, không balance/legacy purpose/fallback radius; read không là start permission; 03D-1 |
| W21 | `S/Infrastructure/Messaging/Consumers/{BaselineMissionStartedHandler,PaidMissionStartedHandler}.cs` | Async Order transitions | Scope-reject V3 old envelopes; new events chỉ project synchronous transition; 03F-2/H |
| W22 | `S/Infrastructure/Messaging/Consumers/BaselineOrthophotoCompletedHandler.cs`; old mapping/health ingestion | Legacy processing state/profile/result writes | V3 new manifest/job receipt boundary, no stale source advancing new attempt; owned 03E/H và V3-04/05 integration |
| W23 | `SystemManagerMissionsController.cs` plant-gps/plant-health publish; `SystemManagerSurveyResultsController.cs` | Legacy review/result publication | Deny V3 old publish paths; chưa cho technical input approval làm customer release; 03F-2/H |
| W24 | `M/Application/Features/Missions/{CreateMission,ScheduleMission}/` | Generic MediatR execution entrypoints | Không thấy HTTP create/schedule callsites trong controllers qua search này; vẫn guard trong handler/repository, không dựa “route chưa expose”; 03D/F |
| W25 | `S/Application/Features/Policies/`; `DB/Surveys/IV3BaselineDeliveryProofReader.cs` | Activation/current config; future delivery proof | Pinned commercial terms không re-policy; capture flag/config guard; proof unavailable giữ deny; 03A-2/D/H |
| W26 | **Target** Re-flight approval/create, input-quality approval, cancellation/no-show/weather | Chưa có complete V3 handlers | New coordinator-backed commands, actor matrix/lineage/funding basis trong ADR, triển khai 03E/F/G; không mark existing runtime |

### Actual legacy HTTP surfaces cần guards (không đổi URL trong baseline)

| Family | Actual route hiện có / xử lý V3 khi triển khai |
|---|---|
| SePay | `POST /api/integrations/payments/sepay/ipn`; giữ route, dispatch server-side theo scope |
| Owner appointment/payment | `/api/tenant-owner/survey-orders/{orderId}/appointments/{appointmentId}/confirm`, `.../request-reschedule`, `.../payments` |
| Manager appointment | `POST /api/system-manager/survey-orders/{orderId}/appointments` |
| Prepare | `POST /api/system-manager/survey-orders/{surveyOrderId}/missions/prepare` |
| Execution | `POST /api/system-manager/farms/{farmId}/missions/{missionId}/{start,complete-flight,fail-flight,cancel,reschedule,preflight}` |
| Media | `/api/missions/{missionId}/farms/{farmId}/media/...` — direct/multipart upload/complete/download |
| Upload/reset | `POST /api/missions/{missionId}/farms/{farmId}/upload/{finalize,reset}` |
| Telemetry | `POST /api/missions/{missionId}/farms/{farmId}/telemetry/imports` |
| Reconcile | `POST /api/system-admin/survey-payments/{paymentId}/reconcile` |
| Assignment/drone/checklist | `/api/system/farms/{farmId}/primary-manager`, `/api/system/drones`, `/api/system/preflight-checklist` |

Guard target: old business mutation route gặp V3 Order/Mission → reject trước mutation, dùng new V3 route/coordinator; shared resource mutations như drone/assignment vẫn hỗ trợ scope chung nhưng theo lock protocol. Current membership/ownership checks vẫn chạy trước response để không lộ tồn tại record. Unversioned payment webhook là ngoại lệ transport được dispatch, không bị tắt chỉ vì public intake đang off.

## 4. Status, constraints và migration đã chốt

[ADR 0020](../adr/0020-v3-capture-execution-and-funding-boundaries.md) là nguồn quyết định chi tiết, không giữ một bộ mapping khác ở đây:

- One additive SurveyOrderStatus set cho V3, cùng OrderStateHistory; new history TransitionSequence không nhầm xmin với ordinal.
- CaptureAttempt có AwaitingUpload giữa flight end và Uploaded, Cancelled chỉ trước execution; terminal parent không reset.
- Một canonical Mission→CaptureAttempt FK/unique binding, không required reciprocal FK cycle. Replace scoped old Order/purpose uniqueness và giữ cross-scope drone exclusion.
- Source/context version **V5** mới, không đổi nghĩa legacy operational-context V3/V4.
- Domain namespace `AgriDrone.Modules.Surveys.Domain` theo convention hiện tại, đặt files trong `Domain/Captures/`.
- Migration mới append enum members; plan name là intent, chưa tạo SQL/migration. Không rewrite migrations Step8 hoặc dùng blanket backfill cho dirty historical V3/Legacy artifacts.
- Readiness/auth/funding/physical safety + current versions rechecked sau guards, không dựa timestamp cũ hoặc bool client.

Migration ordering chốt: domain/contracts → nullable expand/linkage/new facts → dual-scope guards/consumers → reviewed empty-only V3 disposition → new V3 writers → constraints tightening sau actual fixture tests. Tách PostgreSQL enum ADD VALUE và transaction sử dụng labels mới nếu provider/server yêu cầu; xác minh trên fixture thay vì đoán migration là atomic mọi bước.

## 5. Funding/cancellation ownership và invariants

Canonical identities/fields và R/F/A/D/U arithmetic nằm trong ADR §3. V3-03 tạo một deposit obligation/Order, verified receipt/allocation facts và cancellation/refund obligation IDs. V3-08 tiếp tục các obligation IDs đó để reserve/execute; V3-09 đọc facts phân loại, không tạo cash receipt/retention lần hai.

Không tạo return obligation lần hai cho active reservations đã có; uncertain transfer hoặc thiếu source refund exposure → hold. Full-return/service-failure không giữ cọc vì “đã bay”. Customer retention cần exact technical input-quality evidence và Admin decision, không Owner boolean hoặc terminal Accepted. Cancellation lúc airborne chỉ request pending; actual receipt đến muộn không resurrect Order hoặc tăng retention đã chốt.

## 6. Exact file manifest theo slice

Các existing paths sửa khi tới đúng slice, không sửa trong 03-00:

| Slice | Existing files / new targets |
|---|---|
| 03A-1 **NEW only** | `S/Domain/Captures/CaptureAttempt.cs`, `CaptureAttemptStatus.cs`, `CaptureAttemptHistory.cs`, `CaptureAttemptDomainErrorCodes.cs`; `backend/tests/AgriDrone.UnitTests/Domain/Surveys/CaptureAttemptTests.cs`. Không thêm EF/API/runtime files trong slice này. |
| 03A-2 | Existing `S/Domain/Orders/{SurveyOrder.cs,SurveyOrderStatus.cs,OrderStateHistory.cs}`, `S/Domain/Appointments/SurveyAppointmentPurpose.cs`, `S/Domain/Payments/PaymentPurpose.cs`; NEW `S/Domain/Captures/{CaptureInputManifest,CaptureInputApproval}.cs`, `S/Domain/Payments/{SurveyDepositObligation,SurveyFundingReceipt,SurveyFundingAllocation}.cs`, `S/Domain/Cancellations/{SurveyCancellationDecision,SurveyRefundObligation,CancellationRetention}.cs`; typed contracts/coordinator ports và policy tests |
| 03B | Existing `DB/{AgriDroneSchemaDbContext.cs,PostgreSqlEnumMappings.cs}`, mappings trong S/M, canonical model snapshot; NEW capture/funding/cancel configurations và generated expand migrations. Mapping/register additive enums/linkage/indices và TransitionSequence. Không sửa historical migrations. |
| 03C | Existing appointment handlers/repositories, payment handlers/verifier/gateway/repositories, `API/Controllers/SurveyPaymentCallbacksController.cs`; NEW V3 commands/DTOs/controllers để không thay nghĩa old API. External SePay I/O ngoài SQL transaction. |
| 03D | NEW `DB/Surveys/SurveyCaptureExecutionDbContext.cs`, `SurveyCaptureExecutionUnitOfWork.cs`, `SurveyCaptureExecutionCoordinator.cs`, `SurveyOrderCaptureContextV5Query.cs`; existing Missions transition/planning và W14–19 writers, module/API DI. Chỉ giữ read-only V5 query như preview, start đi coordinator. |
| 03E | Existing media/telemetry/preflight source groups W08/W11–13; NEW immutable manifests/approvals configs/commands/adapters, strict processing handoff event/receipt, runtime/API tests |
| 03F | Existing DroneMission recovery/reschedule/generic create/schedule/reset paths và old consumers W21/W23; NEW quality Re-flight approval/create orchestration/receipts/history |
| 03G | NEW V3 cancellation/no-show/weather commands và coordinator operations; reuse canonical funding/refund obligation repository/IDs của A/B; don't create second refund subsystem |
| 03H | New V3 controllers/DI/exported current schema/handoff + real DB/broker/security tests, source fixture version/evidence report; giữ old fixtures/envelopes |

NEW port filenames chốt trong `backend/src/BuildingBlocks/AgriDrone.IntegrationContracts/Surveys/`: `SurveyOrderCaptureContextV5.cs`, `ISurveyOrderCaptureContextV5Query.cs`, `ISurveyCaptureExecutionCoordinator.cs`; không dùng DbContext/EF types trong public module ports. Delivery/refund-exposure DB adapters dùng active supplied connection/transaction khi là approval/start/cancel; không giữ long-running I/O trong transaction.

## 7. Handoff cụ thể để bắt đầu 03A-1

- `CaptureAttempt` Planned factory nhận Order/Tenant/Farm/AttemptIndex/operation identity và parent/approval context khi là successor. Không tự truy vấn DB để cấp index hoặc tự tạo money/Mission.
- Binding Mission ID được validate khi execute/start, không giả yêu cầu Mission đã insert trước Planned domain object. Physical binding do prepare transaction tạo atomic ở 03D-1.
- Logical Version + immutable history/predecessor/event identity; legal transition matrix ở ADR §2. Same event ID/exact fingerprint replay không append lần hai; changed fingerprint conflict.
- Không có transition terminal→Planned hoặc rewrite StartedAt/EndedAt; preflight invalidation chỉ trước start và giữ revision proof.
- Unit cases: legal path đến AwaitingUpload/Uploaded; invalid start without current evidence refs; cross-Order parent; missing approved reason; terminal freeze; cancellation-before-execution; duplicate event conflict; input approval không đồng nghĩa Accepted. Processing/Accepted methods chỉ nhận typed source receipts, không tự gọi worker.
- Chưa thêm schema/config/DI/HTTP handlers/migration hoặc đổi callback/recovery ở 03A-1. Runtime writer/race/DB proof tests thuộc các slices sau và không được tuyên bố đã pass bằng domain tests.

## 8. Gate và environment record

| Gate | Evidence tại 03-00 | Kết luận |
|---|---|---|
| V3-02 02A-2 DB/02D-3/02F-1 | [Checklist và report](../plans/v3-02-execution-checklist.md); historical build 791 unit/88 architecture/13 no-DB component cases không chạy lại trong bước này | Pending actual PostgreSQL upgrade/race/rollback |
| PostgreSQL/PostGIS | `compose.step1-test.yaml` khai báo postgis/postgis:17-3.5, localhost:55432; TCP check không kết nối được | Not running/reachable tại lúc kiểm tra; chưa SQL/version/schema inventory |
| RabbitMQ test | Compose khai báo AMQP localhost:55672; TCP check không kết nối được | Broker tests chưa chạy; không suy deployed version từ image tag |
| Redis test | Compose khai báo localhost:56379; TCP check không kết nối được | Cache/readiness tests chưa chạy |
| Object storage | Test compose không cung cấp storage fixture; chưa xác minh service/object/checksum access | NotVerified; không dùng production bucket để thử |
| Follow-up delivery proof | Existing unavailable adapter; không sửa | Production deny đến V3-06 |
| Operational safety/spatial params | Chưa có approved runtime evidence được audit ở bước này | NotReady nếu thiếu; không seed số demo để vượt gate |
| Activation | Policy public off, không đọc live DB values hoặc sửa config | Không go-live; không claim đã kiểm chứng production flags |

## 9. Review/verification record

Đã thực hiện read-only file/route/symbol search, source inspection, Git HEAD/status inventory, source hashes và TCP reachability của ba localhost test ports lấy từ test compose. Không gọi external services, không chạy payment hoặc create/reset database. Markdown links, ADR/checklist coverage và source fingerprint được kiểm tra khi hoàn thành tài liệu; evidence kết quả được ghi trong checklist.

Verification result: **8 tài liệu / 120 local links và anchors hợp lệ**, **26 writer groups**, **19 work items: 03-00 Accepted và 18 NotStarted**, dependency graph kể cả grouped dependencies không có cycle. Recomputed **2.129 scoped backend files** cho aggregate hash trùng baseline; **20 critical source hashes** cũng trùng. Không có backend source/contracts/tests change trong bước này; không chạy runtime build/tests.

03-00 chỉ đóng source/design deliverables: **writer inventory → status mapping → ownership → lock protocol → canonical funding/obligation IDs → exact file manifest và next-slice boundary**. Không còn quyết định nghiệp vụ cần hỏi lại để bắt đầu isolated 03A-1. Runtime adapter/funding/refund-proof completeness và fixture gates vẫn phải chứng minh ở phase tương ứng.

## 10. Critical source SHA-256

Hash tại thời điểm ghi baseline; paths tương đối repository root, không phải đề nghị sửa tất cả trong 03A-1.

| Existing source file | SHA-256 |
|---|---|
| `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrder.cs` | `84bbf3f01adcfd11921439685b85cbc3e32d2bcea39f936590120b61c090adcd` |
| `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrderStatus.cs` | `aaaa5e6ab45de8ce3f0e8dbab9bbc361473f886db3dd0db742dc3cdb48fff4d9` |
| `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrderReadinessPolicy.cs` | `05725f7a14bc7fe9caed36936d257bb623c8cde2c7a935b8029d9a30ed9a2ef0` |
| `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrderCommercialSnapshot.cs` | `6099cfde160d427c37c32f3f11beeaef8d6fde37abcef0b5b1c4ead847e48c6f` |
| `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Policies/CancellationPolicy.cs` | `9ba14bd739c1a39fdfb080d9740de6952d5bfedd546ab26523df67012512cafc` |
| `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Payments/SurveyPayment.cs` | `2e8a80b709c3f7637b3154d1fd97a0d91ae3f95f996ab3b7240e69b077673e55` |
| `backend/src/BuildingBlocks/AgriDrone.Database/Surveys/SurveyPaymentProcessingRepository.cs` | `f6f975c2122c18babe14fc87eeace1ee5f590134d210c69b0d0e3f4c258fb869` |
| `backend/src/BuildingBlocks/AgriDrone.Database/Surveys/SurveyAppointmentSchedulingRepository.cs` | `6fff340b59deeb6d8dffaf25d9140e5b0d7c74c0164fb177fcda75b3c923613f` |
| `backend/src/BuildingBlocks/AgriDrone.Database/Surveys/SurveyOrderOperationalContextV4Query.cs` | `eb6bdf0d74b40776524750297dbf4969a8efff6d9c130447f5bb4841fec5d01f` |
| `backend/src/BuildingBlocks/AgriDrone.Database/Surveys/IV3BaselineDeliveryProofReader.cs` | `0fb1d9119773aef6a5c96707308b6feb1e055d07e9a40703c87d75b6795248de` |
| `backend/src/BuildingBlocks/AgriDrone.Database/Surveys/V3FollowUpBaselineEligibilityReader.cs` | `15ebf250375d51aed58f729df5922791b62be8a5bf068e9f1b3a28978c952899` |
| `backend/src/Modules/AgriDrone.Modules.Missions/Domain/Missions/DroneMission.cs` | `f5c545af4db0aa1036eec7bada662e8fd33a0f8e06b105ba38ee38ad9a4d62ee` |
| `backend/src/Modules/AgriDrone.Modules.Missions/Infrastructure/Persistence/Configurations/DroneMissionConfiguration.cs` | `14510f462255bab8cace439e7011b284ab5cc013a979bc6771e01b80e15281e6` |
| `backend/src/Modules/AgriDrone.Modules.Missions/Application/Features/Missions/TransitionMission/TransitionMissionCommandHandler.cs` | `9f73162adea57233e5059a6b094e25dfd7e1826ca88ac2caac28bbbab6a8de3f` |
| `backend/src/Modules/AgriDrone.Modules.Missions/Application/Features/Missions/PrepareMissionSet/PrepareMissionSetCommandHandler.cs` | `9362c80985e6a8bcbd0018c64e2d3b09039122d1ddb45be9d78ed54d1e86648a` |
| `backend/src/Modules/AgriDrone.Modules.Missions/Application/Features/Missions/RescheduleOrderMission/RescheduleOrderMissionCommandHandler.cs` | `f537ae55875f7d0a713d8ef4f1047bddb369da246bb264b994f6f09249490f2a` |
| `backend/src/Modules/AgriDrone.Modules.Missions/Application/Features/Media/ResetMissionUpload/ResetMissionUploadCommandHandler.cs` | `39ad0bde43a41ec9fb9f4b0154c302dff0c29a7d190d1275d73bc7247ed1e1a3` |
| `backend/src/Modules/AgriDrone.Modules.Missions/Application/Features/Media/FinalizeMissionUpload/FinalizeMissionUploadCommandHandler.cs` | `075da1463be81c1ca7b941e7ee5e7b356fa07ef5f57a6be8f9cd574b58afdd3d` |
| `backend/src/Modules/AgriDrone.Modules.Identity/Application/Features/SystemManagers/EndPrimaryFarmManagerAssignment/EndPrimaryFarmManagerAssignmentCommandHandler.cs` | `645bf5416aa6aa7bec7847079a5cfb5c0559f576280ec4ca82fd27e4a0375783` |
| `backend/src/AgriDrone.Api/Controllers/SurveyPaymentCallbacksController.cs` | `8608f7d2b8949f2f2ea963b7a1613fe3c8f1e68806442400ffb7a5ac5c27e61a` |

