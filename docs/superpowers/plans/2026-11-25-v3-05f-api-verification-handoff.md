# V3-05F — API, xác minh và bàn giao Implementation Plan

**Checkpoint 2026-11-25.** Đã triển khai V3-05A–F. Build chuẩn đạt 0 warning, 0 error tại checkpoint review V3-05F.

- [ ] Xác minh cấu hình COMBO trên môi trường đích, migration PostgreSQL, broker/object storage, review/invoice/correction và scope API tại V3-10. [Acceptance matrix](../../testing/v3-business-acceptance.md)

[Backlog V3-06 đến V3-10](../../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md#4-status-và-dependency-roadmap)

<details>
<summary>Hồ sơ triển khai và evidence theo từng checkpoint</summary>

Các trạng thái, checklist và số liệu bên dưới ghi theo thời điểm lập từng phần.

> **For agentic implementation:** Execute sequentially, keeping `PublicIntakeEnabled = false` and `SurveyPayments:Enabled = false`. Only run `/home/hoanglong/.dotnet/dotnet build backend/AgriDrone.sln --no-restore --nologo` (never `dotnet test` per `AGENTS.md`).

**Goal:** Expose complete V3 HTTP endpoints for assigned Manager review-to-invoice flows (`GET /result-review`, `POST /result-review/combo-reviews`, `POST /result-review/freeze`, `GET /invoice`, `POST /invoice/finalize`, `POST /invoice/corrections`) and Owner minimal pre-release invoice metadata (`GET /invoice`), harden Owner read paths and legacy writers against unreleased/V3 bypass, and document handoff contracts for V3-06 and Plan 2.

---

## Task 1: Application Queries & Result Review Reload Fix (`F02`, `F03`)

1. **`GetSurveyResultReviewQuery` & `SurveyResultReviewTransaction`**:
   - Add `string? CurrentApprovedResultFingerprint = null` to `SurveyResultReviewResponse` (`GetSurveyResultReviewQuery.cs`).
   - Update `SurveyResultReviewTransaction.GetReviewAsync` so that after `FreezeSurveyInternalResultCommand` transitions the attempt to `CaptureAttemptStatus.Accepted` (`order.CurrentApprovedResultRevisionId.HasValue`), reloading `GET /result-review` still succeeds: load the current `SurveyInternalApprovedResultRevision` (`IsCurrent`, matching Order/Tenant/Farm) to populate `CurrentApprovedResultFingerprint`, and resolve the attempt by `approved.CaptureAttemptId` even in `Accepted` status (keeping `forUpdate: true` mutations strictly on non-terminal `Processing` attempts).
   - Return `ExpectedFreezeTokens` + `CurrentApprovedResultRevisionId` + `CurrentApprovedResultFingerprint` + `OrderVersion` + `VerifiedPoleCountSnapshotId` + `CountSnapshotFingerprint` consistently.

2. **Port `ISurveyInvoiceQueries` (`Surveys/Application/Abstractions/Invoices/ISurveyInvoiceQueries.cs`)**:
   - `Task<Result<ManagerSurveyOrderInvoiceResponse>> GetManagerInvoiceAsync(Guid orderId, Guid actorId, CancellationToken cancellationToken = default);`
   - `Task<Result<TenantOwnerSurveyOrderInvoiceResponse>> GetTenantOwnerInvoiceAsync(Guid orderId, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);`

3. **Manager Invoice Read Query (`Surveys/Application/Features/Invoices/GetManagerSurveyOrderInvoice/`)**:
   - `GetManagerSurveyOrderInvoiceQuery.cs`: `GetManagerSurveyOrderInvoiceQuery(Guid OrderId)` + `ManagerSurveyOrderInvoiceResponse` exposing exact correction/settlement tokens (`OrderId`, `TenantId`, `FarmId`, `OrderNumber`, `OrderVersion`, `OrderStatus`, `Scope`, `ServiceCode`, `ScreensDisease`, `CurrentInvoiceRevisionId`, `InvoiceRevisionNumber`, `InvoiceFingerprint`, `CurrentApprovedResultRevisionId`, `ApprovedResultRevisionNumber`, `ApprovedResultFingerprint`, `VerifiedPoleCountSnapshotId`, `CountSnapshotRevisionNumber`, `CountSnapshotFingerprint`, `SurveyServicePriceId`, `PricePerPole`, `Currency`, `CommercialPolicyVersionId`, `VerifiedBillablePoleCount`, `TotalAmount`, `ValidAllocatedDepositNet`, `ConfirmedNetReceipts`, `DueAmount`, `OverpaymentAmount`, `ZeroPoleApprovedBy`, `ZeroPoleReasonCode`, `ZeroPoleEvidenceReference`, `ComboBatchId`, `ComboReviewRevisionId`, `ComboReviewFingerprint`, `OpenFinalBalanceCheckoutPaymentIds`, `FinalizedAt`).
   - `GetManagerSurveyOrderInvoiceQueryValidator.cs` & `GetManagerSurveyOrderInvoiceQueryHandler.cs`: Validates `OrderId`, ensures authenticated `ActorId` and `CorrelationId`, delegates to `ISurveyInvoiceQueries`.

4. **Owner Invoice Metadata Read Query (`Surveys/Application/Features/Invoices/GetTenantOwnerSurveyOrderInvoice/`)**:
   - `GetTenantOwnerSurveyOrderInvoiceQuery.cs`: `GetTenantOwnerSurveyOrderInvoiceQuery(Guid OrderId)` + minimal allowlist `TenantOwnerSurveyOrderInvoiceResponse` (`OrderId`, `OrderNumber`, `OrderStatus`, `InvoiceRevisionNumber`, `VerifiedBillablePoleCount`, `PricePerPole`, `Currency`, `TotalAmount`, `DueAmount`, `FinalizedAt`).
   - `GetTenantOwnerSurveyOrderInvoiceQueryValidator.cs` & `GetTenantOwnerSurveyOrderInvoiceQueryHandler.cs`: Validates `OrderId`, checks `ActorId` and `TenantId`, delegates to `ISurveyInvoiceQueries`.

5. **Database Adapter `SurveyInvoiceQueries` (`AgriDrone.Database/Surveys/SurveyInvoiceQueries.cs` + DI in `DependencyInjection.cs`)**:
   - Run under `IsolationLevel.RepeatableRead`:
     - **Manager (`GetManagerInvoiceAsync`)**: Rank 1 assignment check (`FOR SHARE`) before evidence reads (`Order.NotFound` -> `404`, revoked/unassigned -> `403`, Legacy scope -> `409 SurveyOrder.ScopeInvalid`). If `!order.CurrentInvoiceRevisionId.HasValue`, return `409 Invoice.NotFinalized` (prompting UI to reload `GET /result-review`). Cross-verify `CurrentInvoiceRevision` (`IsCurrent`), `CurrentApprovedResultRevision` (`IsCurrent`, matching `invoice.ApprovedResultRevisionId`), `CountSnapshot` (matching `invoice.PoleCountSnapshotId` & `approved.PoleCountSnapshotId`), `CommercialSnapshot`, active funding totals (`ValidAllocatedDepositNet` vs `ConfirmedNetReceipts`), COMBO head review, and open `FinalBalance` checkouts.
     - **Owner (`GetTenantOwnerInvoiceAsync`)**: Verify active `TenantMembership` (`OWNER` + `ACTIVE` user) + matching `(TenantId, FarmId)` + non-deleted Farm + `ContractScope.V3` + valid current `SurveyInvoiceRevision` (`IsCurrent`, matching current approval & snapshot). If hidden, wrong tenant, not Owner, Legacy, or no valid finalized current invoice exists -> return `AppError.NotFound("Invoice.NotFound", ...)` (`404`) per F03 without leaking existence!

---

## Task 2: HTTP DTOs & Controllers (`F01`, `F02`, `F03`)

1. **`AgriDrone.Api/Contracts/Surveys/V3/V3SurveyResultAndInvoiceContracts.cs`**:
   - Request records with DataAnnotations (`[Required]`, `[Range]`, `[StringLength]`, `[RegularExpression]` for 64-hex SHA-256 fingerprints):
     - `RecordV3ComboReviewRequest` (+ finding/zone/blocker input DTOs matching `RecordSurveyComboReviewCommand`)
     - `FreezeV3InternalResultRequest`
     - `FinalizeV3InvoiceRequest` (no `ZeroPoleApprovedBy`)
     - `CorrectV3PreReleaseInvoiceRequest` (+ `PreReleaseV3PoleCountItemCorrectionRequest` with `ExpectedPlantProfileId`, optional `PreReleaseV3ComboReviewRequest` mapped to `ReplacementComboReview`)
   - Response DTOs:
     - `V3SurveyResultReviewApiResponse`
     - `V3ComboReviewApiResponse`
     - `V3FreezeInternalResultApiResponse`
     - `V3ManagerInvoiceApiResponse`
     - `V3FinalizeInvoiceApiResponse`
     - `V3CorrectPreReleaseInvoiceApiResponse`
     - `V3TenantOwnerInvoiceApiResponse` (minimal Owner allowlist only)
   - Explicit mapper (`V3SurveyResultAndInvoiceMapper.cs` in `AgriDrone.Api/Mapping/`).

2. **`V3SystemManagerSurveyOrdersController.cs` (`api/v3/system-manager/survey-orders`)**:
   - `GET {orderId:guid}/result-review` -> `200 OK`
   - `POST {orderId:guid}/result-review/combo-reviews` -> `201 Created` (`200 OK` on `IsReplay`)
   - `POST {orderId:guid}/result-review/freeze` -> `201 Created` (`200 OK` on `IsReplay`)
   - `GET {orderId:guid}/invoice` -> `200 OK`
   - `POST {orderId:guid}/invoice/finalize` -> `201 Created` (`200 OK` on `IsReplay`)
   - `POST {orderId:guid}/invoice/corrections` -> `201 Created` (`200 OK` on `IsReplay`)
   - OpenAPI `[ProducesResponseType]` covering `200`, `201`, `400`, `401`, `403`, `404`, `409`, `422`.

3. **`V3TenantOwnerSurveyOrdersController.cs` (`api/v3/tenant-owner/survey-orders`)**:
   - `GET {orderId:guid}/invoice` -> `200 OK` / `401` / `403` / `404`.

---

## Task 3: Pre-Release Owner Read Security & Legacy Writer Scope Audit (`F03`, `F04`)

1. **Close Owner Pre-Release Read Leaks Found During Audit (`F03`)**:
   - `CompareFarmBaseMapsQueryHandler` (`Farms`): Add `FarmBaseMapReadAccess` and `IFarmBaseMapPreviewReader.CanOwnerReadAsync` gating (mirroring `GetFarmBaseMapsQueryHandler` / `GetFarmBaseMapDetailQueryHandler`), denying V3 unreleased/internal maps and cross-tenant reads (`404`/`409`).
   - `GetFarmBaseMapAnalyticsQueryHandler` (`Farms`): Add `FarmBaseMapReadAccess` and `IFarmBaseMapPreviewReader.CanOwnerReadAsync` filtering for non-manager callers (`404` for unauthorized/cross-tenant, exclude unreleased/V3 versions).
   - `GetFarmPlantHealthMapQueryHandler` (`Missions`): Guard query so `farm.farm_base_map_versions` never falls back to unreleased `InternalApproved` or `Draft` or `Scope = 'V3'` base maps, and fail closed (`FarmPlantHealthMap.PaymentRequired`) if the farm has an unreleased/unpaid `ContractScope.V3` order on active/latest `farm_base_map_versions`.
2. **Legacy Writer Scope Guard Verification (`F04`)**:
   - Add explicit `if (order.Scope != ContractScope.Legacy) throw new ReviewFault(AppError.Conflict("SurveyOrder.ScopeInvalid", ...));` in `PlantHealthReviewService.AuthorizeAsync` so legacy `ApprovePlantHealthSurveyResult`, `PublishPlantHealthSurveyResult`, and all legacy PlantHealth review commands reject `ContractScope.V3` orders cleanly.
   - Verify existing guards in `ConfirmSurveyOrderPoleCountCommandHandler` (`order.Scope != ContractScope.Legacy`), `InitiateSurveyPaymentCommandHandler`, `ReconcileSurveyPaymentCommandHandler`, and `SurveyPaymentProcessingRepository`.

---

## Task 4: Handoff Contracts, Plan 2 Inventory & Acceptance Matrix (`F05`)

1. Update [docs/contracts/survey-business-v3.md](../../contracts/survey-business-v3.md):
   - Add **6.3 V3-05F HTTP API & V3-06/Plan 2 Handoff Contracts**: exact routes, status codes (200/201/400/401/403/404/409/422), ProblemDetails error codes + UI reload-token retry flow, MAP vs COMBO sample payloads, Owner allowlist, exact V3-06 handoff token fields, and distinction between `ValidAllocatedDepositNet` vs `ConfirmedNetReceipts`.
2. Update [docs/plans/v3-07-to-v3-10-owner-claims-reporting-plan.md](../../plans/v3-07-to-v3-10-owner-claims-reporting-plan.md) & [docs/plans/v3-05e-to-v3-10-settlement-release-plan.md](../../plans/v3-05e-to-v3-10-settlement-release-plan.md):
   - Record the complete **V3-07 Pre-Release Read Surface Inventory** (Farms base-maps/compare/analytics, Plants lifecycle, Missions plant-health-map/media, Surveys orders/payments/invoice metadata) + agreed A1 release-proof and ledger read field/scope contracts with Plan 2.
3. Update [docs/testing/v3-business-acceptance.md](../../testing/v3-business-acceptance.md):
   - Add explicit **V3-05F API & Handoff Fixtures / Test Matrix** (`V3-T36`+) covering unauthorized/revoked Manager, wrong-tenant Owner, unfinalized invoice (`409` on Manager / `404` on Owner), stale tokens, operation replay (`200` + `IsReplay`), correction refreshing Owner metadata & invalidating old tokens, pre-release result isolation across all surfaces, and legacy writer scope rejection (to execute in V3-10).
4. Update [docs/plans/v3-05-combo-review-and-invoice-implementation-plan.md](../../plans/v3-05-combo-review-and-invoice-implementation-plan.md) (`V3-05F` checkboxes) and [PROJECT_MEMORY.md](../../../PROJECT_MEMORY.md).

---

## Task 5: Build & Static Verification (`F05`)

- Verify `PublicIntakeEnabled` / `SurveyPayments:Enabled` remain `false`.
- Run `git diff --check` and `/home/hoanglong/.dotnet/dotnet build backend/AgriDrone.sln --no-restore --nologo` targeting **0 Warning, 0 Error**.

</details>
