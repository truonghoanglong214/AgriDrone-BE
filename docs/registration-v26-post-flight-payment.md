# Registration v2.6: post-flight settlement

Source: `summary/Phieu_FA26SE218_V2.6.docx` in the local workspace (not copied into Git).
This change addresses payment sequencing in the existing Plant Health/Combo pipeline, for first and Follow-up orders. It is not a claim that the whole registration form is implemented. The separate paid Map product and its Follow-up mapping pipeline, baseline preservation, claims and revenue reporting require their own scope review.

## New orders

The three approval orchestrators freeze the effective VND per-pole price ID/value and service type, and enable `post_flight_settlement`. New orders snapshot the shared deposit policy, with zero Combo discount. Later catalogue/discount-policy edits do not change the booked rate. Ambiguous or missing effective prices reject approval.

1. First order: confirm mapping appointment, pay deposit, fly mapping and publish the reviewed map. This is not the final billing gate for Combo.
2. Schedule/confirm the disease capture appointment before final pricing. For Follow-ups, verify scope against the existing map, then schedule capture. A confirmed appointment without its deposit cannot authorize the flight.
3. Fly disease capture after deposit, process completed flight evidence, review and publish the result internally.
4. Internal reviewed publication moves the new order to `AWAITING_PRICING`, keeping Owner results locked.
5. Confirm billable in-boundary active poles. Total is count times the frozen booking rate; credit a paid deposit once, with remaining amount bounded at zero. Move directly to `AWAITING_PAYMENT`.
6. Create `FINAL_BALANCE` without a new/future appointment. Successful matching callback or Admin reconciliation completes the order. Zero balance still requires explicit settlement.
7. Published-result access also requires the existing payment gates. The Owner notification intent is staged atomically with successful final settlement, not while results are still locked.

Example: 20 poles at 1,000 VND, deposit 5,000 VND, no discount: total 20,000 VND, balance 15,000 VND.

## Existing orders and migration

Migration `20261113075731_RegistrationV26PostFlightSettlement` only adds five columns. Existing orders have `post_flight_settlement=false`, keeping their original prices, discount snapshots, payments and state machine. Do not blindly switch an old/mock order to the new flow: a valid booked-rate snapshot and verified post-flight review are required.

The supplied SQL is an EF-history-idempotent upgrade from `20261112141351_ValidateSurveyComboDiscountRange`, not an installation script for an empty database. Use EF migrations for a fresh database or when earlier migrations are pending:

```powershell
dotnet ef database update --context AgriDroneSchemaDbContext --project src/BuildingBlocks/AgriDrone.Database --startup-project src/BuildingBlocks/AgriDrone.Database
```

Run from `AgriDrone-BE/backend`. Back up the target database before applying. This change does not run migrations automatically or deploy/restart an API host.

## Verification

`PostFlightSettlementTests`: first/Follow-up review-before-billing, frozen price/no discount, deposit credit, settlement completion, unpaid-deposit flight rejection and rescheduling.
`RegistrationV26MigrationTests`: additive-only migration operations, model/snapshot agreement, and idempotent SQL without order updates/deletes.
Existing payment, pricing, readiness and state-machine tests cover legacy behavior. Live PostgreSQL upgrade/fresh-install and Swagger/payment-gateway checks remain separate from these code checks.
