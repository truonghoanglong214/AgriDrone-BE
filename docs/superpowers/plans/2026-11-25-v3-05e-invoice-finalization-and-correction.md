# V3-05E — Count, Finalization và Pre-Release Correction Implementation Plan

**Checkpoint 2026-11-25.** Đã triển khai V3-05A–F. Build chuẩn đạt 0 warning, 0 error tại checkpoint review V3-05F.

- [ ] Xác minh cấu hình COMBO trên môi trường đích, migration PostgreSQL, broker/object storage, review/invoice/correction và scope API tại V3-10. [Acceptance matrix](../../testing/v3-business-acceptance.md)

[Backlog V3-06 đến V3-10](../../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md#4-status-và-dependency-roadmap)

<details>
<summary>Hồ sơ triển khai và evidence theo từng checkpoint</summary>

Các trạng thái, checklist và số liệu bên dưới ghi theo thời điểm lập từng phần.

> **For agentic workers:** Implementing directly in current session (`executing-plans` + `dotnet-backend-workflow`). Note: Per `AGENTS.md`, `dotnet test` is deferred to V3-10; verification in this environment uses static source/EF schema parity checks since `.NET 10 SDK` is not installed on this Linux container.

**Goal:** Implement V3-05E invoice finalization (`FinalizeSurveyOrderInvoiceCommand`) and pre-release count/invoice correction (`CorrectSurveyOrderPreReleaseInvoiceCommand`), including ADR 0020 serialization, funding net calculation, boundary exception checks, zero-pole exception authorization, immutable `OrderStateHistory`, and active unpaid checkout invalidation preserving late-receipt callback linkage.

**Architecture:** Clean Architecture / Modular Monolith across `AgriDrone.Modules.Surveys` (Domain invariants, MediatR commands/validators/handlers, persistence configurations) and `AgriDrone.Database` (`SurveyInvoiceTransaction` executing in `IsolationLevel.Serializable` with ADR 0020 lock order + `AgriDroneSchemaDbContext` migrations/snapshot).

**Spec:**
- [V3-05 Detailed Plan](../../plans/v3-05-combo-review-and-invoice-implementation-plan.md)
- [Plan 1 — Settlement & Release](../../plans/v3-05e-to-v3-10-settlement-release-plan.md#a1--v3-05ef-invoice-và-bàn-giao-api)
- [ADR 0020](../../adr/0020-v3-capture-execution-and-funding-boundaries.md)
- [Survey Business V3 Contracts](../../contracts/survey-business-v3.md)

## Global Constraints
- `Result` / `Result<T>` + `AppError` for all business errors; no exception-based control flow.
- Append-only immutability for `SurveyOrderPoleCountSnapshot`, `SurveyInternalApprovedResultRevision`, `SurveyInvoiceRevision`, `OrderStateHistory`, `SurveyResultReviewOperation`, `SurveyFundingReceipt`, `SurveyFundingReversal`.
- Zero-pole (`VerifiedBillablePoleCount == 0`) requires authorized approver (`ZeroPoleApprovedBy` verified in DB as active `SYSTEM_ADMIN` or active assigned `SystemManager` for `(TenantId, FarmId)`), `ZeroPoleReasonCode` (1..64), `ZeroPoleEvidenceReference` (1..500).
- `ValidAllocatedDepositNet = ConfirmedReceipts - FundingReversals - CompletedRefunds`; fail closed with `RefundReconciliationHold` if `NetConfirmedPaid - ActiveReservations < 0`.
- Active unpaid `FinalBalance` checkout (`Pending`, `Processing`, `AdjustmentRequired`) is transitioned to `SurveyPaymentStatus.Invalidated` in the same transaction on correction, preserving `Id`, `Provider`, `ProviderReference`, `InvoiceRevisionId`, and setting `SupersededByInvoiceRevisionId`.

## Review Focus
1. Reversed/charged-back deposit (`SurveyFundingReversal`) after capture start must reduce `ValidAllocatedDepositNet` so invoice `DueAmount` reflects net paid.
2. Unresolved `BoundaryException` or unresolved COMBO blocker must block finalization and correction.
3. Pre-release correction must supersede old `ApprovedResultRevision` and `InvoiceRevision` (flushing inside the same physical transaction) before inserting new `CURRENT` revisions so `one_current` partial unique indexes do not fail.
4. Invalidating a `Pending` checkout (where `provider_reference IS NULL`) must satisfy `ck_survey_payments_provider_session` and release `uq_survey_payments_one_active_per_order_purpose`.
5. `OrderStateHistory` must append `TransitionSequence` + `PredecessorHistoryId` when Order status changes (`Processing -> AwaitingSettlement`), and skip status history when status stays `AwaitingSettlement`.

---

### Task 1: Domain & Enum Enhancements (`Surveys.Domain`)
**Files:**
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Payments/SurveyPaymentStatus.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Payments/SurveyPayment.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Orders/SurveyOrderPoleCountSnapshot.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Domain/Results/SurveyResultReviewOperation.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Payments/Common/SurveyPaymentCommandSupport.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Payments/GetSurveyPaymentHistory/GetSurveyPaymentHistoryQueryHandler.cs`

### Task 2: Application Contracts, Validators & Handlers (`Surveys.Application`)
**Files:**
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Abstractions/Invoices/ISurveyInvoiceTransaction.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Invoices/FinalizeSurveyOrderInvoice/FinalizeSurveyOrderInvoiceCommand.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Invoices/FinalizeSurveyOrderInvoice/FinalizeSurveyOrderInvoiceCommandValidator.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Invoices/FinalizeSurveyOrderInvoice/FinalizeSurveyOrderInvoiceCommandHandler.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Invoices/CorrectSurveyOrderPreReleaseInvoice/CorrectSurveyOrderPreReleaseInvoiceCommand.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Invoices/CorrectSurveyOrderPreReleaseInvoice/CorrectSurveyOrderPreReleaseInvoiceCommandValidator.cs`
- Create: `backend/src/Modules/AgriDrone.Modules.Surveys/Application/Features/Invoices/CorrectSurveyOrderPreReleaseInvoice/CorrectSurveyOrderPreReleaseInvoiceCommandHandler.cs`

### Task 3: Persistence Configurations & EF Core Migration (`Surveys.Infrastructure` & `AgriDrone.Database`)
**Files:**
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyPaymentConfiguration.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyResultReviewOperationConfiguration.cs`
- Modify: `backend/src/Modules/AgriDrone.Modules.Surveys/Infrastructure/Persistence/Configurations/SurveyOrderPoleCountSnapshotConfiguration.cs`
- Modify: `backend/src/BuildingBlocks/AgriDrone.Database/CrossModuleRelationshipConfiguration.cs`
- Create: `backend/src/BuildingBlocks/AgriDrone.Database/Migrations/20261125120000_V3_05EInvoiceFinalizationAndCorrection.cs`
- Create: `backend/src/BuildingBlocks/AgriDrone.Database/Migrations/20261125120000_V3_05EInvoiceFinalizationAndCorrection.Designer.cs`
- Modify: `backend/src/BuildingBlocks/AgriDrone.Database/Migrations/AgriDroneSchemaDbContextModelSnapshot.cs`

### Task 4: Transaction Coordinator & DI (`AgriDrone.Database`)
**Files:**
- Create: `backend/src/BuildingBlocks/AgriDrone.Database/Surveys/SurveyInvoiceTransaction.cs`
- Modify: `backend/src/BuildingBlocks/AgriDrone.Database/DependencyInjection.cs`

### Task 5: Contracts, Plans, Memory Bank & Static Verification
**Files:**
- Modify: `docs/contracts/survey-business-v3.md`
- Modify: `docs/plans/v3-05-combo-review-and-invoice-implementation-plan.md`
- Modify: `docs/plans/v3-05e-to-v3-10-settlement-release-plan.md`
- Modify: `PROJECT_MEMORY.md`

</details>
