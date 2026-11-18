# Main / V3 merge resolution — 2026-11-13

Merged source: `fda57ec5b0d228611027296f54659d0f0549b6c1` into V3 head `692d6e3c`.

## Resolution

- Preserve V3 commercial snapshot/history and release-proof guards in all three approval orchestrators. Apply main's configurable deposit/Combo discount only to the legacy factory branch.
- Keep V3 `SurveyDepositPolicy` with its version/predecessor/reason/evidence fields. Main's simpler, zero-deposit-capable policy becomes `LegacySurveyDepositPolicy`; its repository/Admin routes continue using the legacy table. V3 approval does not query those legacy policies.
- Merge both DI registrations, retain `DepositObligationId` / `TransitionSequence` EF Ignore bridge until 03B, and scope the legacy Owner query before projecting its required service enum.
- Generate the EF snapshot from the combined model, including main's refund/discount/map-history changes. Temporary scaffold files are outside the repository and are not migrations to apply.

## Deposit schema merge

Both branches originally used `survey.survey_deposit_policies` for incompatible schemas. The merged model uses:

| Model | Table | Amount / history |
|---|---|---|
| LegacySurveyDepositPolicy | survey.survey_deposit_policies | amount_vnd; existing main policy history |
| SurveyDepositPolicy | survey.v3_survey_deposit_policies | amount, currency, predecessor, reason/evidence; V3 bundle/snapshot references |

Two ordered bridge migrations preserve existing IDs, rows and foreign keys:

1. `20261111040000_StageLegacyDepositPoliciesForMerge`, before Step8: temporarily rename an existing main-style deposit table and its colliding indexes.
2. Step8 creates its original V3 schema and snapshot/bundle FKs.
3. `20261111150000_SeparateLegacyAndV3DepositPolicies`, after Step8D and before main's policy migrations: rename V3 storage/indexes and restore staged legacy storage. PostgreSQL follows table renames for the existing FKs. Conflicting target tables cause an explicit failure instead of copying/overwriting histories.
4. Main's policy migrations retain their original legacy table name.

Step8D's deposit consistency check was aligned with main's Follow-up deposits so a pending Step8D upgrade does not reject a valid legacy Follow-up with a positive deposit. Already-applied Step8D databases receive main's later `AllowFollowUpSurveyDeposit` check replacement. The separate positive whole-VND constraint for V3 remains intact.

This merge did **not** update a database. Before deployment, test fresh migration and populated upgrades from each branch, validate policy/bundle/snapshot FKs and existing Follow-up deposits, and exercise rollback against dedicated PostgreSQL fixtures. SQL generation/model checks below do not establish that acceptance. Existing V3 public-activation and release-proof gates remain pending.

The bridge IDs deliberately surround Step8. Use the migrator's actual applied-history set or a complete idempotent script from `0`; a script starting only at main's newest migration timestamp would omit older pending bridge/V3 migrations.

## Verification

| Check | Actual result |
|---|---|
| Solution build, `--no-restore` | PASS, 0 warnings/errors |
| UnitTests | PASS, 938/938 |
| ArchitectureTests | PASS, 88/88 |
| Filtered IntegrationTests without database connection | PASS, 18/18: V3 proof, enum/model compatibility, merge policy/migration SQL and preview query |
| EF `migrations has-pending-model-changes --no-build` | No model changes |

New regression cases verify that legacy terms reach a legacy approval, V3 approvals retain the pinned bundle without consulting legacy policy repositories, both policy types map to different tables, and the bridge migrations precede main's policy SQL in the generated migration order/script.
