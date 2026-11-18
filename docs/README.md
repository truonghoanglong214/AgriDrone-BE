# AgriDrone documentation

Current policy: **v3, approved 2026-11-10**. Start at the [Codex-Plan index](../Codex-Plan/README.md).

## Implementation status — 2026-11-13

- V3-01: [locked design](domain/v3-01-locked-design.md), [ADR 0019](adr/0019-v3-01-model-contract-and-migration-design.md) and dated [source audit](operations/v3-01-source-audit.md).
- V3-02: catalogue, intake and atomic approval implemented; PostgreSQL upgrade/locking/rollback acceptance remains pending. See the [execution checklist](plans/v3-02-execution-checklist.md).
- V3-03: 03A-1 CaptureAttempt and 03A-2 operational/funding/cancellation domain contracts implemented. Next: **03B-1 persistence and expand migration**. See the [plan](plans/v3-03-capture-funding-and-cancellation-implementation-plan.md), [execution checklist](plans/v3-03-execution-checklist.md), [source/design baseline](operations/v3-03-execution-baseline.md) and [ADR 0020](adr/0020-v3-capture-execution-and-funding-boundaries.md).
- Follow-up production still requires the V3-06 release-proof source; public activation remains off. Domain/model tests do not establish PostgreSQL acceptance.

## Requirements and plans

- [Proposal v3](../Codex-Plan/Be-Plan-v3.md), [approved Business Rules](../Codex-Plan/Business-Rules-v3.md), [ADR 0018](adr/0018-v3-paid-map-combo-business-policy.md).
- [Glossary](domain/be-plan-glossary.md), [traceability matrix](domain/v3-traceability-matrix.md).
- [BE1 use cases](../Codex-Plan/BE1-Use-Case-Specification.md), [BE2 use cases](../Codex-Plan/BE2-Use-Case-Specification.md), [master plan](../Codex-Plan/BE1-Core-Business-Implementation-Plan.md).
- [V3-02 implementation plan](plans/v3-02-catalogue-and-approval-implementation-plan.md).
- [Target coordination contracts](contracts/survey-business-v3.md), [HTTP API intent](openapi/survey-business-v3.openapi-intent.md), [messaging guide](../Codex-Plan/RabbitMQ-Architecture-And-Usage-Guide.md).
- [FE/online Mobile handoff](operations/v3-fe-mobile-handoff.md), [acceptance matrix](testing/v3-business-acceptance.md), [diagrams](diagrams/README.md).

## Runtime and compatibility references

- [OpenAPI index](openapi/README.md): retained Step 7 export and target V3 intent.
- [Plant Health V4 schema and fixtures](contracts/plant-health-v4/README.md), [review](contracts/plant-health-review.md), [approval](contracts/plant-health-result-approval.md), [publication](contracts/plant-health-result-publication.md), [output handoff](contracts/plant-health-phase7e-output-handoff.md).
- [Mission prepare-to-finalize guide](testing/mission-prepare-to-finalize.md).
- [SePay integration](integrations/sepay-bank-transfer.md), [FastAPI processing service](../backend/services/metashape-service/README.md).
- [Feature guide](ARCHITECTURE_FEATURE_GUIDE.md) and [module guide](MODULE_STRUCTURE_GUIDE.md).

Superseded proposals, old completion reports/phase plans, duplicate archive copies, earlier Swagger exports and phase-7 diagrams were removed during cleanup on 2026-11-13. ADRs, legacy model/disposition contracts, SQL runbooks and wire fixtures remain where they explain supported runtime or migration boundaries. The dated [documentation rebaseline report](operations/v3-documentation-rebaseline-report.md) records the earlier documentation-only checkpoint; it does not describe current implementation progress.
