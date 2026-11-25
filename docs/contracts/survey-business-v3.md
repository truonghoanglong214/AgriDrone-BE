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

### 6.1 V3-05D Review & Internal Freeze Application Contracts (Handoff 05E)

- **Query**: `GetSurveyResultReviewQuery(OrderId)` returns `SurveyResultReviewResponse` (Manager assignment checked first via Rank 1 `FOR SHARE`). Exposes `VerifiedPoleCountSnapshotId`, `CountSnapshotFingerprint`, `CountSnapshotRevision`, active attempt, and for COMBO: pending batch head, current `SurveyComboReviewSummaryDto`, and `SurveyExpectedFreezeTokensDto`.
- **COMBO Review Command**: `RecordSurveyComboReviewCommand` appends an immutable `SurveyComboReviewRevision` (`RevisionNumber`, `PredecessorReviewId`, `DecisionFingerprint`, `DecisionsPayload`) under ADR 0020 locks (`Rank 1 Assignment -> Rank 2 Order(s) -> Rank 3 Receipt -> Rank 7 Attempt -> Rank 8 Mission/Job`). Requires exact 1-to-1 decisions for all findings, zones (NTS valid closed polygon covered by `FarmBoundary`, accepted finding members covered by polygon, active effective `SourcedGuidance`), and blockers `(SourceType, SourceId, ReasonCode)`. `Rejected` outcome keeps Order/Attempt in `Processing` with audit/receipt.
- **Internal Result Freeze Command**: `FreezeSurveyInternalResultCommand` performs first-freeze (`RevisionNumber = 1`) for **MAP** (`NotAssessed`, `ComboBatchId = null`, `ComboReviewRevisionId = null`) and **COMBO** (`SuspectedObservationsApproved` or `NoObservationsDetected` with complete AI coverage proof + explicit negative confirmation). Calls `SurveyOrder.RecordInternalApprovedResult(...)` + `CaptureAttempt.AcceptProcessing(...)` atomically in a single Serializable transaction while keeping Order in `Processing` until `FinalizeInvoiceRevision` in **V3-05E**.
- **Handoff Tokens to 05E** (`FreezeSurveyInternalResultResponse`): `OrderId`, `OrderVersion`, `ServiceCode`, `ApprovedResultRevisionId`, `ApprovedResultRevisionNumber`, `ApprovedResultFingerprint`, `AttemptId`, `AttemptStatus = Accepted`, `CountSnapshotId`, `VerifiedBillablePoleCount`, `CountSnapshotFingerprint`, `FarmBaseMapVersionId`, `BaselineMapVersionId`, `FarmBoundaryVersionId`, `MappingCandidateBatchId`, and when COMBO: `ComboBatchId`, `ComboBatchFingerprint`, `ComboReviewRevisionId`, `ComboReviewFingerprint`.
- **Operation Idempotency**: Keyed by `(TenantId, ActorId, Action, ApiVersion='v3', OperationId)` in `survey.survey_result_review_operations`. Revoked Manager assignment returns `Forbidden` prior to reading receipts or evidence. Same key + fingerprint returns stored response (`IsReplay = true`); different request payload returns `Conflict`.

Invoice references price snapshot already taken at Order creation, verified in-boundary count/reviewer/evidence and finalized invoice revision. Pending/estimated/current global inventory is not authoritative billable count.

Invoice change before release invalidates old review/checkout dependencies as appropriate and retries release only with current exact revision. Unit price/deposit snapshots do not silently change. Receipt money from stale checkouts is reconciled, not discarded.

### 6.2 V3-05E Invoice Finalization & Pre-Release Correction Contracts (Handoff 05F/V3-06)

- **Initial Invoice Finalization Command**: `FinalizeSurveyOrderInvoiceCommand` transitions a `V3` Order from `Processing` to `AwaitingSettlement` under ADR 0020 `Serializable` locks (`Rank 1 Actor/Assignment -> Rank 2 Order(s) -> Rank 3 Receipt -> Rank 5 DepositObligation/Payments/FundingAllocations/FundingReceipts/Reversals/RefundObligations -> Rank 6 ApprovedResult/Snapshot/InvoiceRevision/MappingBatch/BaseMap/Boundary/BoundaryExceptions/ComboBatch/ComboReview -> Rank 7 CaptureAttempt -> Rank 8 Mission`).
  - Verifies exact `ApprovedResultRevision` + `SurveyOrderPoleCountSnapshot` provenance and fingerprints, recomputes distinct verified in-boundary `PlantProfileId` count, rejects if any unresolved `farm.boundary_exceptions` or COMBO blockers exist.
  - Computes deposit credit strictly from `SurveyFundingAllocation` pinned to the Order's `DepositObligation` (`ValidAllocatedDepositNet = AllocatedAmount - AllocatedReversals - AllocatedCompletedRefunds`) separately from `ConfirmedNetReceipts` (`ConfirmedReceipts - LinkedCashReversals - CompletedRefunds`), failing closed with `InvoiceFinalization.RefundReconciliationHold` if a reversal cannot be tied to its original receipt, a refund may duplicate a reversal, or the available cash would be negative. Reversal cash and its effect on allocated deposit are separate immutable amounts. The callback resolves an original deposit receipt only when it is unique for the checkout. Unresolved reversals retain their verified cash evidence for reconciliation.
  - Appends `SurveyInvoiceRevision` (`RevisionNumber = 1`), updates `CurrentInvoiceRevisionId`/`FinalPrice`/`RemainingAmount`, and appends `OrderStateHistory` (`TransitionSequence`, `PredecessorHistoryId`), `SurveyResultReviewOperation` (`Action = 'FinalizeInvoice'`), `AuditLog`, and `OutboxMessage` (`INVOICE_FINALIZED`).
- **Pre-Release Correction Command**: `CorrectSurveyOrderPreReleaseInvoiceCommand` operates on an unreleased `V3` Order in `AwaitingSettlement`.
  - Requires `VerifiedBillablePoleCount` to change after re-evaluating `ItemCorrections` (`ExpectedPlantProfileId`) against `FarmBoundary` (`ST_Within`), `plant.plant_profiles`, and versioned `MappingCandidateBatch` / `MappingReviewDecision` provenance; for `COMBO` orders, also verifies `SurveyComboReviewRevision` findings and zone memberships against the corrected pole set. If the current review no longer fits, submit `ReplacementComboReview` in the same command with complete findings, zones and blocker decisions plus negative confirmation evidence when required. The correction validates those decisions against the corrected snapshot and the original AI evidence, then appends a replacement review and returns its ID and fingerprint. Persisted rejected candidates outside the requested corrections remain excluded.
  - In a single physical transaction (R5/C4): reads and locks open `FinalBalance` `SurveyPayment` checkouts at Rank 5, then supersedes `CurrentApprovedResultRevision` and `CurrentInvoiceRevision` (releasing `one_current` partial unique indexes), persists new `SurveyOrderPoleCountSnapshot` (`RevisionNumber + 1`), `SurveyInternalApprovedResultRevision`, and `SurveyInvoiceRevision`, invalidates open `FinalBalance` checkout `SurveyPayment` obligations (`Status = Invalidated`, keeping `Provider`, `ProviderReference`, `InvoiceRevisionId`, and setting `SupersededByInvoiceRevisionId`), and atomically updates `SurveyOrder` (`AwaitingSettlement` preserved).
- **Late Callback Handling for Invalidated V3 Checkouts (`R05E-03`)**: `ProcessSurveyPaymentEventCommandHandler` validates dedup (`EventAlreadyProcessed`) and `Provider`/`ProviderReference` before V3 scope gating, locks in ADR 0020 order (`Rank 2 Order -> Rank 5 DepositObligation -> Payment`) via `SurveyPaymentProcessingRepository`, preserves `SurveyPayment.Status == Invalidated`, never auto-credits replacement invoices, and records immutable `PaymentEvent` + `SurveyFundingReceipt` linked to `SurveyPaymentId` & `InvoiceRevisionId` with `RequiresReconciliation = true`. SePay requires an actual provider transaction ID. V3-06 must bind any new V3 balance checkout to the exact invoice revision under Order and funding locks.
- **Zero-Pole Authorization Decision (`R05E-05` / R1 / BR-PAY-08)**: When `VerifiedBillablePoleCount == 0`, `ZeroPoleReasonCode` (1–64 chars) and `ZeroPoleEvidenceReference` (1–500 chars) are mandatory; approver identity is derived server-side from authenticated `ActorId` and locked/verified at Rank 1 (active Manager with a current `FarmManagerAssignment` for `(TenantId, FarmId)` before Rank 2 Order and Rank 3 replay). When `count > 0`, both fields must be null.
- **Examples & Error Codes**:
  - *Initial MAP/COMBO finalization*: `FinalizeSurveyOrderInvoiceCommand(OrderId, OperationId, ExpectedOrderVersion, ExpectedApprovedResultRevisionId, ExpectedApprovedResultFingerprint, ExpectedCountSnapshotId, ExpectedCountFingerprint, FinalizationReason)` -> returns `InvoiceRevisionNumber = 1`, `OrderStatus = AwaitingSettlement`, `TotalAmount`, `ValidAllocatedDepositNet`, `ConfirmedNetReceipts`, `DueAmount`, `OverpaymentAmount`.
  - *Allocation vs Receipt separation (`R05E-02`)*: Receipt `150`, Allocation `100` to Order Deposit, Total `200` (no reversals/refunds) -> `ConfirmedNetReceipts = 150`, `ValidAllocatedDepositNet = 100`, `DueAmount = 100`.
  - *Pre-release correction (e.g., 100 -> 90 poles)*: `CorrectSurveyOrderPreReleaseInvoiceCommand(..., ItemCorrections)` using `ExpectedPlantProfileId` -> creates `CountSnapshot Rev 2`, `ApprovedResult Rev 2`, `InvoiceRevision Rev 2`, populates `InvalidatedCheckoutPaymentIds` for open `FinalBalance` checkouts (`Invalidated`), keeps `OrderStatus = AwaitingSettlement`.
  - *Late callback after correction (`R05E-03`)*: Two distinct provider transaction IDs targeting the invalidated checkout reference produce two immutable `PaymentEvent` + `SurveyFundingReceipt` records (`RequiresReconciliation = true`, linked to `SurveyPaymentId` + original `InvoiceRevisionId`); replaying either transaction ID returns idempotent replay.
  - *Replay / Stale / Errors*: Same `(TenantId, ActorId, Action, ApiVersion='v3', OperationId)` + matching payload fingerprint returns stored response (`IsReplay = true`); changed payload returns `Operation.Conflict`; revoked manager returns `Forbidden` before replay; a mismatched profile or an invalidated COMBO review returns `InvoiceCorrection.ProfileProvenanceMismatch` or `InvoiceCorrection.ComboReviewInvalidatedByCountChange`; negative net funding or missing linkage returns `InvoiceFinalization.RefundReconciliationHold`.

### 6.3 V3-05F HTTP API & V3-06 / Plan 2 Handoff Contracts

- **Routes & Authorization Policies (`AgriDrone.Api/Controllers/V3/`)**:
  - **SystemManager (`api/v3/system-manager/survey-orders/{orderId:guid}/...`, policy `SystemManager`)**:
    - `GET result-review` -> `GetSurveyResultReviewQuery` (`200 OK`; continues serving even when `CaptureAttempt.Status == Accepted` after freeze, returning `CurrentApprovedResultRevisionId`, `CurrentApprovedResultFingerprint`, `OrderVersion`, and `ExpectedFreezeTokens`).
    - `POST result-review/combo-reviews` -> `RecordSurveyComboReviewCommand` (`201 Created` on new revision, `200 OK` on `IsReplay == true`; COMBO only).
    - `POST result-review/freeze` -> `FreezeSurveyInternalResultCommand` (`201 Created` on first freeze, `200 OK` on `IsReplay == true`).
    - `GET invoice` -> `GetManagerSurveyOrderInvoiceQuery` (`200 OK`; returns exact correction/settlement tokens from `SurveyInvoiceRevision`, `SurveyInternalApprovedResultRevision`, `SurveyOrderPoleCountSnapshot`, COMBO head, funding breakdown, and open `FinalBalance` checkout IDs; returns `409 Invoice.NotFinalized` if invoice does not exist so UI reloads `GET result-review`). Current `ConfirmedNetReceipts` uses the same funding checks as finalization. `409 InvoiceFinalization.RefundReconciliationHold` means the funding evidence needs reconciliation before the API can return a confirmed net amount.
    - `POST invoice/finalize` -> `FinalizeSurveyOrderInvoiceCommand` (`201 Created` on initial revision 1, `200 OK` on `IsReplay == true`; zero-pole approver derived server-side from Rank-1 `ActorId`).
    - `POST invoice/corrections` -> `CorrectSurveyOrderPreReleaseInvoiceCommand` (`201 Created` on replacement revision, `200 OK` on `IsReplay == true`; supports `ExpectedPlantProfileId`, optional `ReplacementComboReview` for COMBO, and zero-pole reason/evidence).
  - **TenantOwner (`api/v3/tenant-owner/survey-orders/{orderId:guid}/invoice`, policy `TenantOwner`)**:
    - `GET invoice` -> `GetTenantOwnerSurveyOrderInvoiceQuery` (`200 OK`, returns minimal allowlist `V3TenantOwnerInvoiceApiResponse`: `OrderId`, `OrderNumber`, `OrderStatus`, `InvoiceRevisionNumber`, `VerifiedBillablePoleCount`, `PricePerPole`, `Currency`, `TotalAmount`, `DueAmount`, `FinalizedAt`). Missing, cross-tenant, revoked-farm, Legacy, or unfinalized orders return `404 Invoice.NotFound` (`403` for revoked Owner membership). No pole items, geometry, findings, zones, limitation notices, media URLs, internal tokens, or reconciliation details are exposed before release.
- **Plant lifecycle**: The read requires current farm access. Tenant readers receive `404 Plant.NotFound` for V3 plant data until release proof is wired. The gate also covers the current fields of a Legacy plant affected by a V3 change or replacement. Assigned Managers retain internal access.
- **HTTP Status & Retry Flow**:
  - Missing/malformed DTO fields return `400 Bad Request`; FluentValidation / business validation errors return `422 Unprocessable Entity` via `ResultMapper`; unauthenticated returns `401`; unassigned Manager / revoked Owner returns `403`; unavailable resource returns `404`; stale versions/fingerprints/snapshots or `OperationId` payload mismatch or unfinalized invoice return `409 Conflict` with `ProblemDetails.extensions.errorCode`.
  - **UI Reload & Retry Flow on `409`**: If `POST result-review/freeze` or `POST invoice/finalize` fails with stale token conflict (`SurveyOrder.VersionConflict`, `CountSnapshot.Stale`, `ComboReview.Stale`), call `GET result-review` to re-acquire current `OrderVersion`, `ApprovedResultRevisionId`/`Fingerprint`, and `ExpectedFreezeTokens`. If `GET invoice` returns `409 Invoice.NotFinalized`, return to `GET result-review` and finalize first; if `POST invoice/corrections` conflicts, reload `GET invoice` to re-acquire `CurrentInvoiceRevisionId`/`InvoiceFingerprint`, `CurrentApprovedResultRevisionId`/`ApprovedResultFingerprint`, `VerifiedPoleCountSnapshotId`/`CountSnapshotFingerprint`, and `OrderVersion`.
- **Sample Payloads**:
  - *MAP finalize (`POST /invoice/finalize`)*: `{ "operationId": "<uuid>", "expectedOrderVersion": 5, "expectedApprovedResultRevisionId": "<uuid>", "expectedApprovedResultFingerprint": "<64-hex>", "expectedCountSnapshotId": "<uuid>", "expectedCountFingerprint": "<64-hex>", "finalizationReason": "MAP count verified" }` -> `201 Created` with `invoiceRevisionNumber = 1`, `validAllocatedDepositNet`, `confirmedNetReceipts`, `dueAmount`, `overpaymentAmount`.
  - *COMBO correction (`POST /invoice/corrections`)*: Same expected invoice/approval/count tokens + `itemCorrections` (`mappingCandidateId`, `isAccepted`, `exclusionReason`, `expectedPlantProfileId`) + optional `replacementComboReview` (required if count change excludes findings/zones referenced by current COMBO review) -> `201 Created` with Rev 2 count/approval/invoice + new `comboReviewRevisionId`/`Fingerprint` + `invalidatedCheckoutPaymentIds`.
- **Handoff Contract to V3-06 & Plan 2 (V3-07/09)**:
  - **To V3-06 (Settlement, Checkout & Release Coordinator)**: Hand off exact `OrderId`, `OrderVersion`, `Scope = V3`, `OrderStatus = AwaitingSettlement`, `CurrentApprovedResultRevisionId`, `ApprovedResultFingerprint`, `VerifiedPoleCountSnapshotId`, `CountSnapshotFingerprint`, `CurrentInvoiceRevisionId`, `InvoiceRevisionNumber`, `InvoiceFingerprint`, `CommercialPolicyVersionId`, `SurveyServicePriceId`, `PricePerPole`, `Currency`, `ValidAllocatedDepositNet`, `ConfirmedNetReceipts`, and `OpenFinalBalanceCheckoutPaymentIds`. Correction invalidates all prior tokens; new V3 `FinalBalance` checkout creation must lock Order, recheck `CurrentInvoiceRevisionId`/`InvoiceFingerprint`, and call `SurveyPayment.BindInvoiceRevision`.
  - **To Plan 2 (A1 Release Proof & Ledger Read Intent)**: Release proof binds `(OrderId, PublicationId, PublicationVersion, ApprovedResultRevisionId, ApprovedResultFingerprint, CurrentInvoiceRevisionId, InvoiceFingerprint, TotalAmount, Currency, PaidAt, PublishedAt, UnlockedAt, NoticeCreatedAt, ClaimWindowId, DeadlineExclusive, IsWithdrawn, WithdrawnAt)`. Ledger/reporting consumes immutable service-value recognition (`TotalAmount` once per Order), `SurveyFundingReceipt` (`ConfirmedNetReceipts`), `SurveyFundingAllocation` (`ValidAllocatedDepositNet`), `SurveyFundingReversal`, and `SurveyRefundObligation` separately.

### 6.4 V3-06 A2.1 Transaction Boundaries, Retry & Handshake Ports

- **Three-step `FinalBalance` checkout boundary (`IV3BalanceCheckoutTransaction`)**
  1. `BeginCheckoutIntentAsync` authorizes the active Owner at Rank 1, locks the Order at Rank 2, and checks the actor/action/version/operation key at Rank 3. New intents lock funding at Rank 5 and verify current invoice, approval and count snapshot IDs and fingerprints at Rank 6. A positive due creates a `Pending` payment bound to the invoice, with a SePay reference and an immutable operation receipt in the same commit. Reusing a key with a different fingerprint returns `Checkout.OperationConflict`.
  2. The A2.3 adapter calls `ISurveyPaymentGateway.CreateSessionAsync` outside SQL, only for `Created`, and passes the stored `BalanceCheckoutReference`. SePay returns `Payment.ProviderOutcomeUncertain` when the request may have created a session. A repeat request returns the saved outcome if available. An unresolved intent is reconciled using the same payment and reference.
  3. `CompleteCheckoutOutcomeAsync` verifies the original operation, payment/reference and current invoice context. Success records `Processing`, the provider outcome and the payment event outbox entry in the same commit. A definitive rejection records `Failed`. An uncertain outcome records a separate receipt and keeps the payment `Pending` for reconciliation. Correction returns `InvalidatedByCorrection` and retains the reference for late receipts. Replay serves checkout credentials only while its invoice remains current and its payment is `Processing`.
- **Release evaluation trigger and bounded retry (`SurveyReleaseEvaluationRequestedV1`)**
  - Routing key `survey.release-evaluation-requested.v1` and consumer `Be1SurveyReleaseEvaluationRequestedV1`. A2.1 writes the trigger in the same transaction as invoice finalization/correction and verified deposit receipts or linked reversals on an Order in `AwaitingSettlement`. The current-balance callback and Admin reconciliation producers are part of A2.3.
  - The coordinator owns its SQL transaction. Transient DB conflicts (`40001`, `40P01`, `55P03`, `SettlementRelease.ConcurrencyConflict`) use bounded RabbitMQ retry. `ReleasePersistence.PendingA2_4` follows the same retry policy without filing a permanent inbox failure. `InsufficientFunding`, `StaleContext` and `ReconciliationHold` complete the inbox message. `Forbidden`, `NotFound` and other `Blocked` outcomes go to the dead-letter queue.
- **Release Coordinator (`ISurveySettlementReleaseCoordinator`) & Outcome Taxonomy**:
  - `EvaluateAndReleaseAsync` opens its own isolated `Serializable` transaction (throws if nested in an active SQL transaction), enforces ADR 0020 **Rank 1 authorization BEFORE reading receipts**, locks related Orders at Rank 2 (`Current` `FOR UPDATE`, `Baseline` `FOR SHARE` sorted by ID), verifies Rank 5 funding (`SurveyInvoiceFunding`) and Rank 6 exact V3-05F tokens + `IV3BaselineDeliveryProofReader`.
  - The consumer requires a declared trigger source and checks the message's tenant and farm against the Order. Admin reconciliation requires an active user holding the `SYSTEM_ADMIN` role.
  - Discriminated `SurveySettlementReleaseOutcome`: `Released`, `Replay`, `InsufficientFunding`, `StaleContext`, `ReconciliationHold`, `Forbidden`, `NotFound`, `Blocked`. Returns `V3ResultReleaseProof` ONLY when `Released` or `Replay` after Rank 1 authorization succeeds.
- **Plan 2 Handshake Ports (`AgriDrone.IntegrationContracts.Surveys`)**:
  - `IV3ResultReleaseProofReader` -> `V3ResultReleaseProof` (28-field contract matching `docs/plans/v3-07-to-v3-10-owner-claims-reporting-plan.md`).
  - `IV3FinancialLedgerFactsReader` -> `V3OrderLedgerFacts` (`V3OriginalServiceRecognitionFact`, `V3LedgerFundingReceiptFact`, `V3LedgerFundingAllocationFact`, `V3LedgerFundingReversalFact`, `V3LedgerRefundObligationFact`), wired fail-closed via `UnavailableV3ResultReleaseProofReader` and `UnavailableV3FinancialLedgerFactsReader` until A2.4/A2.5.

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
