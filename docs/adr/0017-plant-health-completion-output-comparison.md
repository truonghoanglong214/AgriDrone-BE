# ADR 0017 — Plant Health completion, notification and comparison

Date: 2026-11-09
Status: Accepted; runtime acceptance deferred to Final Test

## Published reference and API

The six manager routes in master plan 15.4 already exist in 7B–7D. Add manager-only
GET `health-results/publication` on the same Order route. Reauthorize the current
active/available primary Manager; only Completed Order + Published result is
readable. Return immutable published payload/fingerprint, including its stored
comparison decision. Do not rebuild this view from current mutable catalogue or
expose internal pending/rejected AI payloads. No TenantOwner controller is created.

`IPlantHealthPublicationQuery` is an internal published-only proof port in
IntegrationContracts. The Database implementation checks tenant/Farm, Completed
Order, Published result, approved batch and immutable payload hash. It returns the
typed payload/hash and number of reviewed source findings, without exporting an
internal repository/domain entity. Consumers use this port to verify claimed events.

## Comparison decision

Resolve comparison at publication, under the existing Farm advisory lock and
publication transaction, using actual captured provenance rather than the pilot
constants used by the earlier request-approval compatibility policy. Store the
final decision in Order.PreviousCompatibleOrderId/policy/reason and the immutable
publication payload. The request-approval decision remains historically audited;
it is provisional until actual Plant Health capture/analysis exists.

Policy `plant-health-publication-comparison.v1` requires same Tenant/Farm,
SurveyServiceId and PlantHealth service, Completed previous Order, Published V4
result with immutable publication, same capture base-map and boundary, and exact
dataset ID/version, annotation rule, model ID/version, threshold profile and boundary
policy provenance. This first policy deliberately rejects cross-map/cross-model
comparisons rather than assuming comparability. A future broader policy needs its
own version and Plant identity/semantics evidence.

Previous result's last observed time must be strictly before the earliest current
source finding observation. Sort candidates by observed time descending, then Farm
publication version and result ID descending, and choose the first compatible
candidate; an incompatible most-recent candidate does not hide an older compatible
one. Use PostgreSQL microsecond precision. No earlier candidate, incompatible
provenance/maps, unavailable current observation time, or no accepted current
findings yields null previous IDs and an explicit reason. No zero-finding result
is labelled HEALTHY. The comparison reference carries no automatic health trend,
diagnosis or efficacy inference.

The optional `comparison` property extends the existing publication V1 additively.
It is omitted when null in both wire/internal fingerprint serialization. Thus an
existing 7D payload without comparison still deserializes/recomputes its original
hash; new 7E publications include a frozen decision. Old payloads are not backfilled
and GET does not invent a comparison for them. Consumers should deploy compatible
contracts before the extended producer. Persisted PascalCase JSON and camelCase
wire conventions from ADR 0016 remain in force.

## Mission completion

Missions registers `be2-plant-health-preliminary-result-published-v1` on the existing
broker topology. The shared structural validator rejects malformed/non-published
or non-preliminary payloads, invalid review/membership/guidance/time bindings and
unknown DTO properties. The producer also validates before its business commit.

The handler verifies envelope actor/causation/time, locks Mission, checks its
Tenant/Farm/Order/capture context, verifies the complete event fingerprint through
the published proof port and checks the completed source job's version/provenance.
Only then does the dedicated domain method move AwaitingReview/ReviewRequired to
Completed/Completed. Completed review counters include all source findings, not
only accepted findings. Empty source findings are valid only because 7C/7D already
required actual positive processed imagery.

Inbox completion, Mission change and integration audit commit together. Permanent
Mission publication ID/result/version/hash provide semantic idempotency across
different message IDs and after Inbox retention; conflicting publications cannot
replace completion. Unavailable publication/source references retry, malformed or
mismatched outcomes dead-letter. Legacy review-state V1 cannot complete/reopen V4
Missions, and database guards enforce this independently of handler routing.

## Owner notification

Database registers `plant-health-publication-notification-v1`. It consumes the 7D
notification intent after publication, verifies all values/actor against the
immutable published proof and resolves the current active tenant OWNER, active
user/tenant/Farm inside its Inbox transaction with shared authorization-row locks.
It creates an **in-app notification**, linked to PublicationId with preliminary
wording/limitation, and an integration audit. No email, recipient-supplied address,
external send or pending owner read route is introduced.

A transaction advisory lock and unique nullable SourcePublicationId give one
notification per publication. Duplicate messages cannot notify again after Inbox
retention or ownership transfer. Recipient identity is fixed at first successful
consumption; future replay does not retarget it. Missing/inactive owner context
retries and can be redriven after reconciliation. Consumer arrival order is
independent of the Mission consumer and the owner portal projection: the reference
must already be Published, but Mission completion is not required to create notice.

Notification source/recipient context cannot be changed/deleted; normal read-state
updates remain allowed. Notification/profile UI must enforce current ownership
when serving data, as owned by BE2; notification persistence is not a portal release.

## Schema and rollout

New expand migration `20261109071540_Step7EPlantHealthCompletionOutput` adds nullable
Mission publication ID/result/version/hash and notification SourcePublicationId,
unique keys, FKs, state/context checks and durable evidence guards. It protects
published comparison fields from later edits. Existing null-source legacy rows
remain compatible. No historical migration is edited and no result is backfilled.

Apply 7A → 7B → 7C → 7D → 7E before this combined API build. Keep RabbitMq.Enabled
unchanged; operators enable/configure it in the deployment environment. Both new
queue bindings are listed in appsettings. Stop old producer/legacy completion paths
before rollout, deploy consumers/contracts before enabling the extended producer,
and redrive pending publication/notification events after schema readiness.

Down refuses populated Mission/notification receipts or publications containing
comparison; use roll-forward recovery. Actual database/HTTP/broker/consumer behavior
requires Final Test evidence. No tests are written/run in this phase.
