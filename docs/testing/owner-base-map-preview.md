# Owner base-map preview — 2026-11-12

## Defect and scope

Owner map detail returned a hardcoded unsigned `localhost:9000` object URL. The local private bucket rejects that URL with HTTP 403, leaving only satellite tiles, boundary and poles visible.

The existing list/detail endpoints now resolve the source Mission's existing processed JPEG through `IFarmBaseMapPreviewReader`. The Database adapter scopes both Mission and Media to the map's Tenant/Farm, excludes deleted media, signs the stored URI through the existing `IObjectStorage` for 15 minutes and reads image bounds from stored metadata. No mission processing endpoint is called.

Both handlers check Farm read access before reading map contents or signing media. Tenant users use current-tenant effective Farm access; only an assigned, operational System Manager may read drafts. Owner list requests with `includeDraft=true` still exclude drafts. An inaccessible map returns NotFound without a signed URL. Storage failure keeps the boundary/poles available with a null preview.

API routes, response shape and write permissions are unchanged. No migration, backfill, payment update, map reconstruction or bucket policy change is required. Deploy/restart BE, then reload FE. Rolling back these source changes restores the old read behavior; no data rollback is needed.

## Verification registry

| TEST-ID | Purpose / level | Input and expected result | Evidence |
| --- | --- | --- | --- |
| TEST-MAP-PREVIEW-ACCESS | regression protection / unit | Valid current-tenant Farm access allows released maps; missing actor, denied Farm, wrong tenant and mismatched Manager grants deny access. Assigned Manager can read drafts without tenant membership. | `FarmBaseMapReadAccessTests` |
| TEST-MAP-PREVIEW-DRAFT | specialized protection / unit | Owner requests draft detail or list with includeDraft=true: no detail/preview, list excludes draft; foreign-Farm detail never calls the signer. | `FarmBaseMapReadAccessTests` |
| TEST-MAP-PREVIEW-SQL | regression protection / integration | Npgsql translates the existing-output media query before an interceptor stops connection opening. No database/storage/processing I/O in this test. | `FarmBaseMapPreviewQueryTests` |
| TEST-MAP-PREVIEW-LOCAL | critical path / smoke | Existing Tam Vu Owner -> actual effective access -> MediatR map detail -> real PostgreSQL source metadata -> signed MinIO image: HTTP 206 and JPEG bytes; image bounds match metadata, 70 active poles. Wrong-tenant/Farm source lookup returns null; wrong current tenant cannot get a map URL; unsigned object still HTTP 403. | Local read-only helper and output under `D:/FPTU/P9/Project/tmp/owner-map-preview-smoke/` |

The local smoke invokes production handlers, DI, authorization services and storage signer, with an execution context populated from the actual local Owner membership. PostgreSQL connections enforce `default_transaction_read_only=on`. It does not run the HTTP authentication middleware, browser UI, background services or schema migration.

Commands:

```powershell
dotnet test backend/tests/AgriDrone.UnitTests/AgriDrone.UnitTests.csproj --filter FullyQualifiedName~FarmBaseMapReadAccessTests
dotnet test backend/tests/AgriDrone.IntegrationTests/AgriDrone.IntegrationTests.csproj --filter FullyQualifiedName~FarmBaseMapPreviewQueryTests
```

Local integration-suite build has an unrelated stale ignored `OwnerSurveyOrderQueryTranslationTests.cs` calling an obsolete constructor. The new test was run with that local file excluded using a temporary MSBuild target; the stale test was not edited. Build/test output uses an isolated artifacts directory so an existing debugger process is unaffected.

Results: API build succeeded with zero warnings/errors; the 10 access/handler unit cases and one Npgsql query-translation test passed. The read-only real Owner/MinIO smoke passed all five checks. Full unit suite: 765 passed, 3 failed out of 768. Failures are `SurveyPoliciesTests.PlantHealthReadinessAllowsOrderWhenEveryPrerequisiteIsSatisfied`, `SurveyOrderReadinessPolicyTests.EvaluateReturnsSuccessWhenPlantHealthAllRequirementsMet`, and `SurveyOrderReadinessPolicyTests.EvaluateCommercialReadinessSucceedsWhenCommercialRequirementsMetEvenIfDroneNotReady` (expected readiness true, actual false). Their readiness policies and tests were not changed by this fix; the full suite is not green.

Browser follow-up after restarting BE: sign in as Owner, open the published Tam Vu map, check the drone image aligns with poles and no image warning appears. Click Tải lại to obtain a fresh link; Owner must have no editing controls. Browser verification remains separate from the successful local read-only handler/storage smoke.
