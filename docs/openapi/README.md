# OpenAPI — Runtime export and V3 API intent

Updated **2026-11-13**. Target policy: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](../adr/0018-v3-paid-map-combo-business-policy.md).

| Artifact | Scope |
|---|---|
| [survey-business-v3.openapi-intent.md](survey-business-v3.openapi-intent.md) | Target MAP/COMBO booking, capture/deposit, settlement/release, claims and finance API design |
| [plant-health-step7-manager.json](plant-health-step7-manager.json) | Retained runtime export from 2026-11-09, before the V3-02 routes were implemented |

Earlier phase-0/1/4 and surveys exports, plus the superseded Step0R intent, were removed during documentation cleanup. The retained JSON is a dated compatibility reference and does not describe the complete current API. V3-02 runtime evidence is tracked in the [execution checklist](../plans/v3-02-execution-checklist.md); V3-03 domain work is tracked in its [checklist](../plans/v3-03-execution-checklist.md).

For a current client contract, export Swagger from the built API, record source commit/date/environment and review it against authorization, version/idempotency and release rules. Do not edit the dated JSON by hand or label an intent document as generated Swagger. Operational/release routes still awaiting implementation remain target design.
