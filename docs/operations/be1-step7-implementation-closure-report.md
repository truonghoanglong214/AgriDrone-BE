# Step 7 — BE1 implementation and local rollout closure

Date: 2026-11-09
Status: `CodeComplete — LocalRuntimeReady — AwaitingFinalTest`

## Delivery status

All BE1 implementation and local rollout work for 7A–7E is complete. No additional
Step 7 business feature remains in the agreed BE1 scope. Final Test and BE2's owner
portal/read-model release remain separate gates; Step 7 is not labelled acceptance Done.

| Phase | Delivered |
|---|---|
| 7A | V4 pending ingestion, provenance, dedup/Inbox and Order PendingReview |
| 7B | Assigned Manager review/correction/history, zone/guidance/boundary decisions and live blockers |
| 7C | Exact reviewed-candidate approval, immutable snapshot/hash, audit and replay receipt |
| 7D | Atomic preliminary publication/history/latest/zone/result/Completed Order/audit/Outbox |
| 7E | Verified outcome → Mission completion, owner in-app notification, frozen compatible previous result and published Manager read |

Implementation details and historical build/schema checks remain in the individual
[7A](be1-be2-phase7a-completion-report.md), [7B](be1-phase7b-completion-report.md),
[7C](be1-phase7c-completion-report.md), [7D](be1-phase7d-completion-report.md) and
[7E](be1-phase7e-completion-report.md) reports.

## Database rollout performed

Docker Desktop was initially stopped. After startup another project's container
already occupied localhost:5433. That project was left unchanged. AgriDrone's
existing PostGIS container was recreated with the same
`agridrone-local_postgres_data` volume, on localhost:5434. Local ignored `.env`
POSTGRES_PORT and appsettings.Development.json database port were updated together;
credentials and database/volume contents were preserved.

Before migration, a custom-format pg_dump backup was copied to:
`%TEMP%/agridrone-before-step7-20261109-205146.dump` (685801 bytes).
The baseline was `20261031025701_DropSurveyAuthorizationsAndManagerQualifications`.

Applied the prerequisite `20261103033709_UnlockPlantHealthConditionCodes` and:

1. `20261109040913_Step7APlantHealthPendingIngestion`
2. `20261109052104_Step7BPlantHealthHumanReview`
3. `20261109054451_Step7CPlantHealthResultApproval`
4. `20261109061911_Step7DPlantHealthAtomicPublication`
5. `20261109071540_Step7EPlantHealthCompletionOutput`

Execution used `dotnet ef database update Step7EPlantHealthCompletionOutput` with
Database project/startup project, AgriDroneSchemaDbContext, Development and --no-build.
All migrations succeeded; all five Step 7 IDs were read back from
`system.__ef_migrations_history`. Existing data passed migration preflight guards.
No database reset, volume deletion, invented approval/completion or synthetic
publication backfill was performed.

## Local runtime evidence

| Item | Observed result |
|---|---|
| PostgreSQL/PostGIS | Healthy; localhost:5434, database agridrone |
| RabbitMQ | Healthy; localhost:5672, vhost agridrone; management localhost:15672 |
| Redis | Healthy; localhost:6379 |
| API | Running at http://127.0.0.1:5080 |
| GET /health/live | 200 Healthy |
| GET /health/ready | 200 Healthy |
| Startup master-data validation | Passed; 2 active suspected classes |
| V4 ingestion queue | 1 active consumer, 0 queued messages |
| Preliminary published outcome queue | 1 active consumer, 0 queued messages |
| Publication notification queue | 1 active consumer, 0 queued messages |
| Live Swagger | Exported 15 Manager paths and 29 referenced schemas |

The three queues are:

- `agridrone.be1.plant-health-analysis-ready.v4`
- `agridrone.be2.plant-health-preliminary-result-published.v1`
- `agridrone.notifications.plant-health-publication.v1`

Outbox, broker topology and consumer hosts started successfully. No feature payload
was injected. Publication history remains empty, so consumer connectivity/readiness
does not claim a processed business workflow or acceptance result.

SMTP and SystemAdmin bootstrap were disabled for this rollout process. Object
storage was already disabled in Development. No external email or storage purge
was performed. In-app notifications remain the Phase 7E delivery mechanism.

## Reuse and handoff

New `AgriDrone.Phase7` launch profile keeps Development configuration, disables SMTP
and bootstrap, and uses HTTP port 5080 without opening a browser. The API started by
this rollout is left running; avoid starting another instance on the same port.
Its process/log paths are recorded in `%TEMP%/agridrone-phase7-runtime.json`.

To restart later, after stopping the current API instance, run from backend:

```powershell
dotnet run --project src/AgriDrone.Api/AgriDrone.Api.csproj --launch-profile AgriDrone.Phase7
```

Start the existing infrastructure from the repository root when needed:

```powershell
docker compose -f compose.yaml up -d postgres rabbitmq redis
```

Live contract snapshot: [plant-health-step7-manager.json](../openapi/plant-health-step7-manager.json).
It includes only Step 7 Manager routes and their referenced schemas/security definition,
exported from the running Swagger document. It contains no sample business records.
Publication/consumer/comparison handoff remains in
[Phase 7E contract](../contracts/plant-health-phase7e-output-handoff.md).

## Deferred gates

Per the user's standing instruction, no tests or business E2E scenarios were
written/run. The existing Final Test backlog in master section 27 remains unchecked:
authorization, strict contracts, replay, concurrent mutation/revocation, rollback,
PostGIS geometry, ordering, migrations/Down guards, broker retry/DLQ and full
ingestion → review → approve → publish → Mission completion/notification.

BE2 owns the published-only owner projection/query/portal and notification UI under
Step 9/12. Their release and Final Test evidence are required before the full
cross-system acceptance Definition of Done can be claimed. No additional Phase 7
implementation is being postponed behind those gates.

No git commit or production deployment was performed. The rollout target was only
the local AgriDrone environment.
