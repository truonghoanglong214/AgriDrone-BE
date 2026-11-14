# V3-01 — Source audit và implementation gaps

Audit date: **2026-11-11**. Scope: read-only source inspection, không DB/broker/gateway access, không build/test/migration. [Locked design](../domain/v3-01-locked-design.md), [ADR 0019](../adr/0019-v3-01-model-contract-and-migration-design.md), [V3-02 plan](../plans/v3-02-catalogue-and-approval-implementation-plan.md).

## 1. Gaps và reusable seams

All links below refer to existing inspected source. None of these files is changed in this documentation task.

| Area | Observed source behavior | Decision / future action |
|---|---|---|
| [Service enum](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Catalogue/SurveyServiceType.cs) / [MVP policy](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Catalogue/SurveyServiceMvpPolicy.cs) | PlantHealth, HarvestReadiness; sellable chỉ PlantHealth | V3 definitions/capabilities bằng versioned records; keep legacy enum meanings |
| [Service configuration](../../backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyServiceConfiguration.cs) | PostgreSQL `system.survey_service_type` required, legacy HasData rows | Scoped nullable legacy discriminator/new v3 definitions, additive migrations, no rewrite old seed IDs |
| [Price entity](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Catalogue/SurveyServicePrice.cs) / [configuration](../../backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyServicePriceConfiguration.cs) | Per-pole price FK/window/CreatedBy; CloseAt closes once; numeric(18,2), no explicit predecessor/version number | Reuse amounts/history; add v3 lineage and scoped integer-VND guards |
| [Create price handler](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Catalogue/CreateSurveyServicePrice/CreateSurveyServicePriceCommandHandler.cs) | Checks PlantHealth-only, closes previous window, references overlap constraint | New config guard + price policy publishing on v3 scope, reuse transaction/audit patterns |
| [Catalogue query](../../backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Queries/SurveyCatalogueQueries.cs) | Public filters PlantHealth and Active/Experimental, no shared-deposit response | Active v3 published config only, shared deposit/policy refs, no Experimental commercial intake |
| [Request aggregate](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Requests/SurveyRequest.cs) | ServiceId, review/checklist history, active/experimental accepted under old MVP guard | Pin indicative quote revision/policy refs, explicit v3 scope; don't provision on submit |
| [Public request controller](../../backend/src/AgriDrone.Api/Controllers/PublicSurveyRequestsController.cs) | POST submit, HttpOnly caller scope/idempotency; no tracking action in this controller | Reuse caller isolation; add v3 hashed tracking token/minimal status API, no assume feature live |
| [Approval prerequisites](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/Common/SurveyApprovalPrerequisiteResolver.cs) | Service/owner/Farm/Manager/Baseline checks, previous candidate selected same service; no price/deposit resolver | Resolve current bundle/price/deposit/claim/cancel snapshot in same approval transaction, independent mapping/health basis |
| [Compatibility helper](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/Common/SurveyApprovalPrerequisites.cs) | const policy/dataset/annotation versions, baseline current-map equality | Resolve configured provenance/policies; don't hardcode dataset IDs or use one same-service comparison for all meaning |
| [Approval DbContext](../../backend/src/BuildingBlocks/AgriDrone.Database/Surveys/SurveyApprovalDbContext.cs) | Physical transaction, row lock repositories, failure checkpoints, rollback on Result failure, audit/Outbox support | Reuse; include v3 config/version/snapshot/history sets and same-transaction resolver |
| [Reference query](../../backend/src/BuildingBlocks/AgriDrone.Database/Surveys/SurveyApprovalReferenceQueries.cs) | FOR SHARE around service/assignment/Farm; existing published map/source Order basis | Extend with new policy guard/current released Baseline receipt; no separate connection for snapshots |
| [New customer orchestrator](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/ApproveSurveyRequest/NewCustomerApprovalOrchestrator.cs) | Provision Tenant/Farm, SurveyOrder.Create, assignment/invitation | Validate full config before side effects, pass immutable commercial snapshot into Order factory |
| [New Farm orchestrator](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/ApproveSurveyRequest/ExistingTenantNewFarmApprovalOrchestrator.cs) | Reuse Tenant/Owner, create Farm/Order | Same snapshot factory and history; no duplicate Tenant |
| [Existing Farm orchestrator](../../backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Approvals/ApproveSurveyRequest/ExistingFarmSurveyApprovalOrchestrator.cs) | Reuse Tenant/Farm and old RequiresBaselineMapping/compatibility | Only new v3 Follow-up on delivered Baseline/proven adoption, create paid Order with common cọc |
| [SurveyOrder](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrder.cs) | DefaultBaselineDepositAmountVnd=5.000; mapped Order sets DepositAmount=0/deposit paid; price populated during pole confirmation | Remove runtime default for new v3, require resolved input snapshot; existing saved legacy values unchanged |
| [Order readiness](../../backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrderReadinessPolicy.cs) | PlantHealth requires published map/count/price/full payment before flight | V3-03 deposit gate and V3-06 release gate; V3-02 must not activate unfinished operational path |
| [DroneMission](../../backend/src/Modules/AgriDrone.Modules.Missions/Domain/Missions/DroneMission.cs) | RecoverFailedFlight resets StartedAt/EndedAt/preflight fields; ReplaceStalePreflight clears old checklist data | V3-03 new attempt/new Mission, retained preflight revisions/input manifests |
| [Mission reschedule handler](../../backend/src/Modules/AgriDrone.Modules.Missions/Application/Features/Missions/RescheduleOrderMission/RescheduleOrderMissionCommandHandler.cs) | Calls RecoverFailedFlight for recovery | Target recovery request creates auditable Re-flight attempt, no history reset |
| [Health publication](../../backend/src/BuildingBlocks/AgriDrone.Database/Health/PlantHealthReviewService.Publication.cs) | Exact V4 approval publication, Complete Order and Outbox; no target atomic finance/notice window contract | Reuse frozen review foundation in V3-05/06; no mark v3 release supported |

## 2. Existing projects and boundaries to preserve

- [SurveysDbContext](../../backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/SurveysDbContext.cs): runtime policy/catalogue persistence.
- [Schema DbContext](../../backend/src/BuildingBlocks/AgriDrone.Database/AgriDroneSchemaDbContext.cs): canonical migration model; only generate new migration in future implementation.
- [Mapping publication context](../../backend/src/BuildingBlocks/AgriDrone.Database/MappingPublicationDbContext.cs) and [result publication context](../../backend/src/BuildingBlocks/AgriDrone.Database/SurveyResultPublicationDbContext.cs): cross-module transaction adapters, no new business DB.
- [Existing price tests](../../backend/tests/AgriDrone.UnitTests/Application/Surveys/CreateSurveyServicePriceCommandHandlerTests.cs), [approval tests](../../backend/tests/AgriDrone.UnitTests/Application/Surveys/SurveyApprovalStep3Tests.cs), [approval architecture tests](../../backend/tests/AgriDrone.ArchitectureTests/SurveyApprovalApiContractTests.cs) are reusable seams; no test execution is claimed.

## 3. What remains unverified

No count/identity of deployed Orders, active policy windows, real paid/refunded balances, running broker queues, migration history or object-store permissions was queried. Source check does not establish that a schema migration is deployed or production rows satisfy target constraints. Live read-only inventory and explicit artifact-level disposition are mandatory before future migration/activation.

The previous documentation rebaseline and user-added master-plan v3.1/v3.2 edits remain in the working tree. This task adds V3-01 decisions/V3-02 plan on top; it does not revert those edits or modify executable files.

## 4. Documentation handoff checks — 2026-11-11

- 121 non-archive Markdown documents scanned; 623 local links resolve, including inspected-source links in the new design/plan.
- 74 unique canonical business rule definitions; no duplicate definitions or unknown rule references in 14 active policy/design/plan/contract documents. Markdown fences balanced.
- Target API intent aligned with additive `/api/v3`; no remaining VNext placeholder, stale unversioned target route or terminal Accepted-before-processing wording in the checked current design documents.
- `git diff --check` passed. Working-tree file-type audit found no executable source, runtime config, SQL or JSON changes; existing documentation/.gitignore tracking edits from the prior task remain intact.
- No build, automated runtime tests, schema migration or database/broker/gateway request was executed. These checks validate the documentation handoff, not implementation acceptance.
