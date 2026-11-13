# Survey Business v3 — Target coordination contracts

Status: **Approved semantic requirements; wire/API implementation pending**, 2026-11-10. [Rules](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](../adr/0018-v3-paid-map-combo-business-policy.md), [API intent](../openapi/survey-business-v3.openapi-intent.md). No DTO/schema/routing/fixtures/runtime modified in this change.

**Design lock 2026-11-11:** [V3-01](../domain/v3-01-locked-design.md) / [ADR 0019](../adr/0019-v3-01-model-contract-and-migration-design.md) chọn model, snapshot cadence, additive `/api/v3` HTTP và các target event families bên dưới. Đây là tên/semantics đã chốt cho future implementation, chưa phải registered/deployed contracts.

## 1. Compatibility boundary

- Existing Mapping V1–V3, PlantHealth V2–V4 and publication/completion V1 schemas retain their original semantics and immutable JSON/hash behavior.
- A released field/validator that expects legacy PlantHealth or customer-published FarmBaseMap cannot accept `COMBO` or internal approved map by documentation decree. Required-field/enum/meaning changes need a new wire version, consumers and cutover review.
- Target families đã chọn: internal `SurveyOrderCaptureContextV5`, pending `SurveyMappingCandidatesReadyV1` và `ComboAnalysisReadyV1`, outcomes `SurveyResultReleasedV1` và `SurveyResultWithdrawnV1`. Không tạo/sửa DTOs, queue bindings hoặc schema fixtures trong task tài liệu này.
- .NET internal ports use typed snapshots. FastAPI has authenticated/versioned processing endpoints; no shared business-table write access or Java service dependency.

## 2. Common identity and security

Envelope minimum: messageId, eventType, schemaVersion, occurredAt UTC, correlationId, causationId, tenantId and trusted actor/source where applicable. Server validates Order→Tenant/Farm, Mission/capture/source linkage and expected policy/version before writes.

Payload business context includes OrderId, FarmId, BusinessPolicyVersion, ServiceType, source IDs/revisions/fingerprints and references relevant to the capability. Money has exact decimal/integer-VND semantics and explicit currency; IDs canonical UUIDs; enums wire strings; timestamps UTC ISO-8601. Calendar-derived policy also stores zone, local business date and exclusive UTC deadline.

Clients/processing workers cannot set derived `Confirmed`, `Approved`, `Published`, `Unlocked`, beneficiary ownership or receiver roles. URL/secret/card/bank-sensitive information must not be leaked into broker event payloads.

## 3. SurveyOrderCaptureContextV5 — target synchronous internal snapshot

| Group | Required semantics |
|---|---|
| Order | Tenant/Farm/Order, active business-policy version, MAP/COMBO, First/Follow-up, expected state/version |
| Assignment/appointment | Current assigned Manager eligibility/availability, appointment identity/version/window and authorization/safety decision references |
| Funding | Shared deposit snapshot/version, actual confirmed credit/receipt; no client-controlled paid flag |
| Mapping context | Follow-up pinned released Baseline + boundary version; first capture area/source type, internal map optional until generated |
| Attempt | New capture or approved quality Re-flight; parent attempt/approval/reason, separate execution Mission; exact input approval/manifest; no additional service price/deposit |
| Start | Mission/drone/reservation/version/preflight context and operation identity for atomic recheck/consume |

Not required before capture: finalized count, full balance, customer-published first Baseline or an orthophoto/route generated from that same future capture. Operational safety/route checks appropriate to available inputs still apply.

Start does not trust an earlier readiness response. Lock/recheck/consume with Mission transition, audit and receipt; revoke/reschedule/assignment/drone race cannot leave start after invalidation.

## 4. Processing request/result boundary

.NET persists a job/source snapshot before requesting FastAPI processing. Request semantics: job/request ID, Order/Mission/capture attempt, scoped immutable input-object manifest/checksums, required pipeline MAP/COMBO, model/dataset/annotation/config versions and callback/poll correlation. API authentication and artifact/object access remain scoped.

Result semantics: same job/request identity, source run revision, status/progress, artifact keys/hashes/coordinate metadata, detector/matcher outputs, actual model/config provenance and limitations. No official Plant lifecycle or customer publication decision.

Input selection pins an uploaded checksum-validated manifest with independent `CaptureInputApproval`; processing does not wait for terminal Accepted state produced after processing/review. Each new Re-flight has a new attempt and execution Mission; no RecoverFailedFlight reset or implicit Order-wide media mixing.

Retry uses same operation identity; actual reprocess has a new revision/supersedes. .NET verifies ownership/manifest/hash/context before staging. Failed/partial upload/job cannot publish candidates as completed. Long processing does not hold a business SQL transaction.

## 5. SurveyMappingCandidatesReadyV1 / ComboAnalysisReadyV1 — target pending handoffs

Mapping family schemaVersion=1, routing intent `survey.mapping-candidates-ready.v1`; Combo family schemaVersion=1, routing intent `survey.combo-analysis-ready.v1`. Separate events allow first Combo mapping approval before disease identity matching; both carry exact attempt/source/policy identities. New consumer queues/validators/cutover evidence must be created in their future implementation phase, not inferred from these routing names.

| Group | Semantic requirements |
|---|---|
| Context | Exact MAP/COMBO Order/policy/source Mission/job/attempt/revision and input manifests |
| Maps | Distinguish approved internal first-map context, pinned Baseline reference and this Order's survey snapshot; no fake published-map reference |
| Mapping | Candidate ID/original coordinate/grid/match suggestions, existing profile ID nullable, uncertainty/new/missing flags and boundary exceptions |
| Health | Combo-only suspected findings, original class/confidence/evidence, 2–3 configured labels/provenance/limitations; MAP means NotAssessed |
| Zones/guidance | Source proposals, geometry/membership/revision and sourced catalogue references; no unreferenced advice/UUIDs invented to satisfy validator |
| Import receipt | Handoff identity/fingerprint/revision, supersedes semantics and exact immutable payload |

Full validation before staging; Inbox/import/candidates/audit atomic. No official profile creation/retirement, no latest Owner-visible health or release. Same identity/different fingerprint conflict; unknown/duplicate fields follow reviewed strict-schema policy. Late output cannot mutate frozen approved/released revision.

## 6. ApprovedResult and invoice snapshot

Internal approval context freezes result/review/source revisions, accepted/corrected decisions, source hashes, evidence IDs, Manager/assignment authorization, policy/capture map/Baseline/boundary references, suspected limitation and exact approval fingerprint.

Invoice references price snapshot already taken at Order creation, verified in-boundary count/reviewer/evidence and finalized invoice revision. Pending/estimated/current global inventory is not authoritative billable count.

Invoice change before release invalidates old review/checkout dependencies as appropriate and retries release only with current exact revision. Unit price/deposit snapshots do not silently change. Receipt money from stale checkouts is reconciled, not discarded.

## 7. Release receipt and SurveyResultReleasedV1

Target schemaVersion=1, routing intent `survey.result-released.v1`; no broker declaration or released schema in this documentation change.

BE1 release coordinator commits one exact publication when approval/current invoice/settlement/authorization pass. Mandatory persisted facts:

- Order/result/source/review/invoice/policy IDs and fingerprints; MAP/COMBO, First/Follow-up, map/Baseline references.
- Paid-at-release receipt/allocation evidence, finalized count/rate/Total; optional outstanding Overpayment obligation without blocking when Due=0.
- Publication ID/version, ApprovedAt and PublishedAt=UnlockedAt=NoticeCreatedAt.
- Durable authorized in-app notification ID/recipient snapshot, ClaimWindowId, zone/local notice date/deadlineExclusive UTC.
- Original service recognition identity/amount exactly once per Order; replacement flag and predecessor publication if correction, no duplicate sale.
- Order completion, audit, idempotency/release receipt and Outbox intent.

Target released-event payload carries release/publication proof, exact references, notice/window IDs, policy/version and hash. It must not depend on consumer timestamp to start claims or on consumers to manufacture proof absent from DB. Do not expose raw pending outputs/media URLs/bank data.

Consumers for Mission outcome/portal/notification/report projections are idempotent by durable release/publication/business IDs beyond MessageId. Missing dependencies retry boundedly. Existing flight capture completion is not full paid Order completion.

## 8. Withdrawn/replacement version semantics

Target `SurveyResultWithdrawnV1`, schemaVersion=1, routing intent `survey.result-withdrawn.v1`, contains old/new publication identity where available, reason, review proof, affected Baseline/profile/survey references and visibility policy. Historical Follow-up source IDs remain unchanged. Withdrawn current content invalidates Owner cache/download permissions; retain audit and claim access.

Substantive replacement has its own durable notice/window and predecessor link; cosmetic edit does not. No second original recognition entry or extra service charge. Existing version hashes are not rewritten.

## 9. Claim/refund coordination

Claim includes Order/released revision/window, server ReceivedAt, evidence manifest/date, type/details and caller ownership. Manager verification append-only; Admin proposal version/amount/currency/reason and Owner exact acceptance precede ComplaintRefund reservation.

Refund reason distinguishes Overpayment/CancellationReturn/ServiceFailure/ComplaintRefund and whether it returns recognized service value. Reserve against all Order receipts under lock including other reservations. Manual transfer records proof/reference/actor/completed time; processing/failure do not imply completed cash outflow. Retry/uncertain transfers require reconciliation.

Post-release ComplaintRefund does not erase paid-at-release proof, reopen invoice or rewrite historical recognition. Corrective action and compensation are separate auditable decisions.

Commercial snapshot resolution pins price/deposit/claim/cancellation versions at Order creation inside the approval transaction. Claim window calculations use the original pinned policy and actual notice date; invoice/refund/replacement never re-resolve current catalogue/claim terms. V3 routes and replay fingerprints are isolated from legacy contracts, and existing SePay callbacks dispatch by persisted Payment→Order policy scope.

## 10. Rollout acceptance

- New schema fixtures and contract tests only in a future implementation change; don't modify old golden envelopes.
- Inventory legacy Orders/source jobs/queues; explicit disposition rather than automatic PlantHealth→COMBO money/data rewrite.
- Consumer-compatible deploy before producer, separate shadow queues, drain/replay/retention evidence, roll-forward for durable finance/publication facts.
- Meaningful tests: cross-tenant, schema mismatch, duplicate/conflicting fingerprints, approval/payment races, atomic release/notice/revenue rollback, capped refunds and exact historical source revisions.
