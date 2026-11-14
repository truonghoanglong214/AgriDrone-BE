# Registration v2.6: post-flight settlement

Source: `summary/Phieu_FA26SE218_V2.6.docx` in the local workspace (not copied into Git).
This change addresses payment sequencing in the existing Plant Health/Combo pipeline, for first and Follow-up orders. It is not a claim that the whole registration form is implemented. The separate paid Map product and its Follow-up mapping pipeline, baseline preservation, claims and revenue reporting require their own scope review.

## New orders

The three approval orchestrators freeze the effective VND per-pole price ID/value and service type, and enable `post_flight_settlement`. New orders snapshot the shared deposit policy and the current Admin-configured Combo discount for both first and Follow-up Combo orders. Later catalogue/discount-policy edits do not change the booked rate or saved discount. Ambiguous or missing effective prices reject approval.

1. First order: confirm mapping appointment, pay deposit, fly mapping and publish the reviewed map. This is not the final billing gate for Combo.
2. Schedule/confirm the disease capture appointment before final pricing. For Follow-ups, verify scope against the existing map, then schedule capture. A confirmed appointment without its deposit cannot authorize the flight.
3. Fly disease capture after deposit, process completed flight evidence, review and publish the result internally.
4. Internal reviewed publication moves the new order to `AWAITING_PRICING`, keeping Owner results locked.
5. Confirm billable in-boundary active poles. Total is count times the frozen booking rate after the saved discount (per-pole VND rounding); credit a paid deposit once, with remaining amount bounded at zero. Move directly to `AWAITING_PAYMENT`.
6. Create `FINAL_BALANCE` without a new/future appointment. Successful matching callback or Admin reconciliation completes the order. Zero balance still requires explicit settlement.
7. Published-result access also requires the existing payment gates. The Owner notification intent is staged atomically with successful final settlement, not while results are still locked.

Example: 20 poles at 1,000 VND, saved discount 20%, deposit 5,000 VND: effective rate 800 VND, total 16,000 VND, balance 11,000 VND.

## Existing orders and migration

Migration `20261113075731_RegistrationV26PostFlightSettlement` only adds five columns. Existing orders have `post_flight_settlement=false`, keeping their original prices, discount snapshots, payments and state machine. Do not blindly switch an old/mock order to the new flow: a valid booked-rate snapshot and verified post-flight review are required.

The supplied SQL is an EF-history-idempotent upgrade from `20261112141351_ValidateSurveyComboDiscountRange`, not an installation script for an empty database. Use EF migrations for a fresh database or when earlier migrations are pending:

```powershell
dotnet ef database update --context AgriDroneSchemaDbContext --project src/BuildingBlocks/AgriDrone.Database --startup-project src/BuildingBlocks/AgriDrone.Database
```

Run from `AgriDrone-BE/backend`. Back up the target database before applying. This change does not run migrations automatically or deploy/restart an API host.

## Verification

`PostFlightSettlementTests`: first/Follow-up review-before-billing, frozen booking price/discount, deposit credit, settlement completion, unpaid-deposit flight rejection and rescheduling.
`RegistrationV26MigrationTests`: additive-only migration operations, model/snapshot agreement, and idempotent SQL without order updates/deletes.
Existing payment, pricing, readiness and state-machine tests cover legacy behavior. Live PostgreSQL upgrade/fresh-install and Swagger/payment-gateway checks remain separate from these code checks.

## Main/V3 merge integration 2026-11-14

- Keep V3 approval branches and their pinned commercial snapshots, initial state history and durable Baseline-release eligibility. Legacy approval freezes its booking price and enables the existing post-flight settlement flow with a positive shared deposit and a pinned Combo discount. Existing saved legacy orders retain their original terms.
- V3 service references do not depend on Legacy booking-price fields. Existing checkout/callback/Admin reconciliation cannot route V3 through the Legacy state machine; the unfinished V3 capture/funding/release runtime remains gated.
- Final checkout and settlement require reviewed post-flight results and a finalized invoice. Neither final payment nor final pricing is required for the existing post-flight capture readiness path.
- Owner base-map detail, list and preview signing check the map's source Order before exposing geometry/media. Manager review access is preserved. Unsettled map versions are omitted from the Owner list.
- Internal post-flight review does not advance latest reviewed health or emit customer-delivery publication events. Settlement validates the exact frozen result/batch/review fingerprint, then advances the released health projection and emits delivery events in the payment transaction. Missing or invalidated reviewed output prevents settlement.

This merge does not implement the still-pending V3 MAP/COMBO capture and result-release phases. In particular, a separate paid MAP product must not be represented as legacy PlantHealth, and the unavailable V3 release-proof adapter must not be replaced with fabricated Completed/Published evidence. No database update, producer activation or deployment is performed by this change.

### Merge verification

- Solution build with `--no-restore -m:1`: passed, zero warnings/errors.
- Executed 121 targeted UnitTests cases, 20 no-database component/model/query/migration cases, and 86 architecture cases directly through reflection against the built test assemblies: passed. Existing xUnit assertions and InlineData/MemberData are executed, without VSTest transport. The temporary runner is outside the repository.
- Standard `dotnet test` was attempted with 90-second and 240-second testhost connection timeouts; both runs aborted before executing tests because the host could not connect. These direct-execution results are not a successful standard VSTest run or live PostgreSQL/gateway acceptance.
- No conflict markers/unmerged index entries remain. The merge is not committed or pushed. Existing pending migrations have not been applied to a database.

### Combo discount correction

The project owner's latest instruction supersedes the earlier no-discount assumption for COMBO and Follow-up COMBO. Approval uses the existing Admin discount policy (and the existing Legacy default if no policy exists), and the post-flight switch no longer erases that snapshot. Both final-pricing entry points apply the saved discount to the frozen booking rate before crediting the deposit. Passing an updated catalogue rate or an already-discounted rate cannot apply the discount twice. This uses the existing ComboDiscountPercentage column and does not require a new migration or rewrite existing Orders.

V3 MAP/COMBO capture/funding/release integration remains pending as noted above; this correction restores discount in the existing first/Follow-up Combo runtime, without routing V3 through Legacy checkout.

Discount correction verification: solution build passed with zero warnings/errors. Direct reflection execution passed 116 targeted unit cases and 5 no-database model/migration cases. Coverage includes first/Follow-up Combo, both final-pricing entry points, Admin discount snapshots, frozen booking rates, VND rounding, no double discount, deposit credit, callback/reconciliation and zero-balance settlement. Standard VSTest and live PostgreSQL/SePay acceptance retain the limitations documented above.

## Two-component Combo pricing

New Combo approvals require both component rates on the effective Legacy service price. Admin uses the existing `POST /api/system-admin/survey-services/{serviceId}/prices` with `amountPerPole` equal to `mappingPricePerPole + plantHealthPricePerPole`. Both rates must be positive whole VND. Missing component configuration fails approval before provisioning; it is never inferred from an old combined rate.

The approval transaction pins both rates on the Order. Pole-count confirmation reads mapping count across the current approved/published map and Health count within the Order's confirmed FarmWide/Zone scope. Health count includes healthy and suspected poles; disease-finding counts do not affect money. The same approved farm boundary applies to both inventory checks. Health scope must be within the map. This extends the current inventory-backed Legacy pipeline; it does not implement the pending per-survey V3 mapping provenance flow.

The invoice is `round((mappingCount * mappingRate + healthCount * healthRate) * (1 - savedDiscount / 100), 0)` using AwayFromZero VND rounding. The paid deposit is credited once. Both first and Follow-up Combo use this one-invoice/one-final-payment flow after review. Owner detail and Manager confirmation expose both counts/rates/subtotals, saved discount and final due. The old single-count price-adjustment and pricing methods cannot overwrite a component invoice.

Existing Orders without component snapshots keep their historical single-rate behavior. The new `SurveyOrderPricingConfirmedV2` contract represents the two-count invoice; V1 is unchanged and is still emitted for old single-rate Orders. No published wire payload is rewritten.

Migration `20261114090343_AddComboComponentPricing` adds nine nullable columns, scoped component constraints and the model snapshot. It does not update existing prices, Orders or receipts. Apply it before starting the new BE, then configure both rates before approving a new Combo. This code change does not apply migrations, enable unfinished V3 routes or deploy an API.

Example: map 1,000 poles at 1,500 VND plus Health 300 poles at 1,000 VND = 1,800,000 VND gross; saved 20% discount gives 1,440,000 VND; paid deposit 500,000 VND leaves one final payment of 940,000 VND.

Component pricing verification: solution build passed with zero warnings/errors. Direct reflection execution passed 129 targeted unit cases, 21 no-database component/model/query/migration cases, and 86 architecture cases. Cases include different map/Health counts and rates, both first/Follow-up, immutable booking values, rejection before review or without component configuration, V2 pricing event output, single-payment callback/reconciliation/zero final, nullable-column migration and model/snapshot agreement. Standard VSTest retains the previously documented testhost limitation; these are direct executions of the compiled xUnit tests, not a live PostgreSQL/SePay acceptance run.
