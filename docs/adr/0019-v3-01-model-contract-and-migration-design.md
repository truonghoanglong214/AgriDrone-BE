# ADR 0019 — V3-01 model, configuration snapshots và compatibility cutover

- Status: **Accepted design, 2026-11-11; implementation pending**.
- Decision authority: chủ dự án giao chốt V3-01 và lên kế hoạch phần kế tiếp, giữ phạm vi chỉ tài liệu.
- Detailed decision: [V3-01 locked design](../domain/v3-01-locked-design.md).
- Foundation: [ADR 0018](0018-v3-paid-map-combo-business-policy.md); không đổi commercial policies đã chốt.

## Context và source evidence

Code hiện có PlantHealth/Harvest enum, PlantHealth-only catalogue, fixed 5.000 baseline deposit và no price/deposit resolution trong approval. Existing approval UoW/locks/idempotency/history có thể reuse. `DroneMission.RecoverFailedFlight` đang reset timestamps/preflight, không đạt capture attempt lineage. [Source audit](../operations/v3-01-source-audit.md) liệt kê exact paths; không có live DB inventory.

Master plan được chủ dự án bổ sung zero-hardcoding, immutable history và attempt-scoped media. Cần chốt snapshot cadence và quality acceptance tránh reprice/re-policy đơn cũ hoặc vòng lặp processing.

## Decisions

1. Reuse current .NET modules/tables/approval transaction seams. New MAP/COMBO service records có versioned definitions/capabilities; legacy enum chỉ cho historical scope, không reinterpret. Nullable legacy discriminator cho new v3 rows cần explicit query/constraint compatibility audit.
2. Versioned service prices/shared deposit/claim/cancellation/policy bundle; spatial config pin theo capture/job. No money/days/tolerance/dataset policy constants trong code. Immutable values, lineage + effective windows, auditable one-time window closure.
3. Request quote indicative; Order approval atomically snapshots price/deposit/claim/cancellation + business-policy version. Later stages reuse pinned terms; release computes deadline từ actual notice date và Order's claim-policy snapshot. Không re-resolve catalogue/current claim policy ở invoice/claim/refund.
4. Một guard row shared/exclusive bảo vệ config publishing vs resolution, DB constraints enforce windows; approval resolves on same connection/transaction, failure rollback toàn resource set. Replay trả snapshot gốc sau authorization.
5. Append-only history authoritative; current status/pointer là transactional projection. Không cần full event sourcing hoặc duplicate review-history aggregate khi existing entity đủ.
6. New CaptureAttempt + new execution Mission cho Re-flight, no RecoverFailedFlight reset. Separate input-quality approval from final Accepted state; job manifest pins one approved attempt's imagery/checksums.
7. Baseline reference/version khác each Order survey snapshot. New/retired/replaced profile review preserves IDs/history; invoice/review/release có explicit revisions, paid proof và idempotent one-time recognition.
8. New HTTP DTOs dùng additive `/api/v3/...`; old Swagger/enum routes giữ compatibility. Existing SePay callback routes unchanged, policy dispatch từ persisted Payment→Order.
9. Target contract families chosen: internal CaptureContextV5; SurveyMappingCandidatesReadyV1; ComboAnalysisReadyV1; SurveyResultReleasedV1; SurveyResultWithdrawnV1. Chưa tạo deployed schemas/routing/bindings; old Mapping V3/Health V4 unchanged.
10. Legacy completed read-only, active Orders giữ original terms tới explicit disposition; no automatic service/money/notice backfill. Public v3 activation disabled tới accepted whole flow; V3-02 không tự cho nhận tiền thật.

## Consequences và phase boundaries

- [V3-02 implementation plan](../plans/v3-02-catalogue-and-approval-implementation-plan.md) đủ chi tiết để bắt đầu persistence/policy foundation khi được phép sửa code.
- Later capture/release/claims/finance entities là locked semantic seams, không phải yêu cầu tạo mọi table ngay V3-02.
- Live DB/broker inventory, generated migration/schema validation và actual test execution vẫn phải thực hiện trước cutover; design approval không giả các evidence này.
- No code/config/migration/SQL/JSON fixture change trong ADR này. Giữ nguyên mọi file ngoài doc edits được liệt kê trong handoff report.
