# Phase 7E — Plant Health API and completion/output report

Date: 2026-11-09
Status: `CodeComplete — AwaitingFinalTest`

## Implemented

Runtime follow-up 2026-11-09: migrations 7A–7E are now applied and local API/consumers
are ready. See [Step 7 closure](be1-step7-implementation-closure-report.md).
Any "not applied/not exercised" statement below describes the original implementation checkpoint.

- Confirmed the six required Manager routes exist; added authenticated published
  result/snapshot read with stored comparison basis.
- Publication-time comparison resolver uses actual capture/provenance, searches
  earlier compatible Completed/Published Plant Health history, and saves IDs/policy/
  reason atomically to Order + immutable publication payload + audit/Outbox.
- Shared strict publication/notification validators and internal published-proof
  query. Optional comparison extension omits null and preserves hashes of 7D payloads.
- Mission outcome Processor/Handler + DI + queue binding. Complete only after
  persisted publication hash/source job/Mission context match, with Inbox and audit.
  Durable publication fields deduplicate across message IDs and Inbox retention.
- Notification Processor/Handler + DI + queue binding. Resolve current active owner
  and create one in-app notification per publication with Inbox/audit; no external send.
- Legacy review-state completion bypass blocked for V4. New expand migration adds
  completion/notification reference columns, FKs, unique keys/state/context guards,
  immutable completion/comparison/recipient evidence and populated Down refusal.
- No duplicate TenantOwner portal controller/read model; BE2 retains that scope.

Details: [ADR 0017](../adr/0017-plant-health-completion-output-comparison.md),
[7E handoff](../contracts/plant-health-phase7e-output-handoff.md).

## Verification and limitations

Per user instruction, **no tests were written or run**. No database migration,
HTTP business request, RabbitMQ publish/consume or external notification was executed.
Commands run from backend:

| Check | Result |
|---|---|
| `dotnet build src/AgriDrone.Api/AgriDrone.Api.csproj --no-restore --nologo -v minimal` | Succeeded, 0 warnings, 0 errors |
| `dotnet ef migrations has-pending-model-changes --project src/BuildingBlocks/AgriDrone.Database --startup-project src/BuildingBlocks/AgriDrone.Database --context AgriDroneSchemaDbContext --no-build` | No pending model changes |
| `dotnet ef migrations script Step7DPlantHealthAtomicPublication Step7EPlantHealthCompletionOutput ... --no-build --output ../docs/operations/phase7e-completion-output.sql` | SQL generated, not executed |
| `git diff --check` | Passed; existing LF/CRLF notice only |

Production build targets API/dependencies and excludes test projects. These checks
establish compilation/schema alignment; runtime acceptance remains entirely in Final Test.

## Rollout/recovery

Migration `20261109071540_Step7EPlantHealthCompletionOutput` follows 7A → 7B → 7C → 7D.
Reviewable [upgrade SQL](phase7e-completion-output.sql).
Apply all five schema steps before deploying/enabling the combined application.
No historical migration or persisted snapshot is rewritten.
Preflight rejects already Completed V4 Missions without an explicit publication
completion receipt; reconcile their real publication/state/audit before upgrade.
No synthetic completion evidence is generated.

Both new consumer bindings are in appsettings; the existing disabled RabbitMQ
default remains unchanged. Deployment operators configure/enable broker and Outbox
dispatcher. Roll out compatible consumers/contracts before the extended producer.

Down refuses durable completion/notification evidence and new comparison-bearing
publications. Prefer roll-forward recovery. Missing active owner/source/publication
references retry; after retry limits, investigate/reconcile and redrive via the
existing DLQ process. Never fabricate approval/source/publication proof.

Notifications here are stored in-app records, not delivered email or an owner portal.
Owner projection/query/notification UI remains BE2 Step 9/12; no release claim is
made for those views. Step 7 stays CodeComplete until Final Test supplies security,
contract, PostgreSQL/PostGIS, concurrency, rollback, consumer and E2E evidence.

## Main file groups

- IntegrationContracts/Health: comparison extension and published proof port;
  Messaging/Validation: strict outcome/notification validators and consumer names.
- Database/Health: comparison/read/proof and notification consumer, shared publish
  orchestration; schema/publication DbContext and DI registrations.
- Missions: dedicated publication completion, durable fields/configuration,
  outcome consumer and legacy event guard.
- Notifications: published-result notification factory/source ID/configuration.
- API: published Manager endpoint and two queue bindings.
- New 7E migration/model snapshot, ADR/contract/report and master Final Test backlog.

No commit, deployment or database update is performed.
