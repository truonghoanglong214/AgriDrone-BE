# V3 documentation rebaseline report

Date: **2026-11-10**. Authorization: chủ dự án chấp thuận toàn bộ bộ khuyến nghị và yêu cầu sửa đủ tài liệu toàn dự án, **không sửa code**.

**Historical checkpoint.** This report records the documentation-only work on 2026-11-10. A later authorized cleanup on 2026-11-13 removed superseded proposals/reports, duplicate archive copies, earlier Swagger exports and phase-7 diagrams. Counts and preservation notes below describe the original checkpoint, not the current file inventory. Current implementation status is in the [documentation index](../README.md) and phase checklists.

## 1. Approved scope

- MAP/COMBO paid services; first/follow-up price/shared-deposit snapshots at creation. Demo rates 1.000/2.000 VND/trụ, common deposit 100.000 VND, versioned config target.
- Deposit-before-capture, balance-before-release; overpayment return không block release khi Due=0.
- Cancellations/no-show/weather/quality Re-flight rules; retained cancellation deposits riêng khỏi published service value.
- ApprovedAt riêng, PublishedAt=UnlockedAt=durable NoticeCreatedAt cùng transaction; D+7 calendar claims theo UTC+7.
- Evidence-based negotiated refunds/cap/reservations/manual bank proof; substantive corrected versions/windows, no duplicate sale/fee.
- .NET business + FastAPI processing, existing SePay, online-only Manager Mobile.

## 2. Documentation changes

| Area | Treatment |
|---|---|
| .gitignore | Chỉ mở tracking các file Markdown trong Codex-Plan vốn đang bị ignore; không đổi runtime config và không git add/commit |
| Root README, docs index, Codex-Plan index | Current source-of-truth, status và navigation |
| Proposal v3 | Approved policy amendment; clarify demo values, timing/refund classifications; preserve research/product scope |
| Business Rules v3 | Canonical numbered rules, lifecycle/formulas/calendar boundaries and failure cases |
| BE1/BE2 use cases, implementation plan | Rewritten current target, explicit runtime gaps/owners/dependencies/acceptance |
| RabbitMQ guide | .NET/FastAPI boundary, strict immutable wire compatibility, pending vs release and durable-notice semantics |
| ADR 0018 | Accepted policy/architecture và detailed supersession map for prior ADRs |
| Glossary/traceability | Unified definitions and proposal→rules→owner→phase→acceptance mapping |
| Target contracts/API intent | Proposed semantics, no claim of deployed new routes/schemas |
| FE/Mobile handoff + acceptance matrix | Online-only UX, locked results, finance/claim/reflight checks; tests are planned, not executed |
| Existing proposals/ADR/domain/phase plans/reports/contracts | Contextual historical/supersession notices, preserve original evidence and released wire facts |
| Technical module/feature and integration READMEs | Scope notes linking current .NET/FastAPI/SePay business policy |
| Diagrams | New v3 overview/lifecycle/sequence PlantUML and Drawio, visible historical legends/badges for old exports |
| Original rewritten master files | Byte-preserving copies under docs/history/pre-v3-2026-11-10 |

## 3. Deliberately unchanged executable artifacts

No edits to .cs/.csproj/.sln/.py, test source, EF migrations/model snapshots, SQL/scripts/seeds, .env, runtime JSON/YAML/config, Docker/compose, golden JSON fixtures or runtime Swagger JSON snapshots. No database/gateway/broker/environment action was performed.

Historical reports' command results/counts/dates remain intact; notices do not convert old tests into v3 evidence. New route/event names are intent/working names until implementation contract review. Archived copies may retain broken relative links to files not included in the archive; resolve from original repository paths as described in the archive index.

## 4. Financial clarification

The proposal's short phrase “completed refunds” is expanded by refund classification: only return of recognized service value reduces net service revenue; every completed transfer reduces cash. Returning excess or an unpublished cancelled deposit cannot subtract service revenue that was never recognized. Example Total 60.000 / receipt 100.000 / excess return 40.000 yields revenue 60.000 and net cash 60.000, not revenue 20.000.

Complaint refund after release does not reopen balance or erase full-payment-at-release evidence. Refund reservations are liabilities/cap checks, not completed cash movements. Corrected publication creates no new original service recognition.

## 5. Verification boundary

Documentation checks cover local links/anchors, rule/acceptance references, Markdown fences, PlantUML structure, Drawio XML, archived tracked-file Git blob equality and diff file types. This report does not claim runtime build/test/migration/API acceptance or rendered diagram validation.

| Documentation verification | Result |
|---|---|
| Active/historical non-archive Markdown documents scanned | 117 |
| Local file links checked | 514; no missing/invalid targets after fixing one historical broken file link |
| Local anchor | The sole referenced section anchor still matches the preserved phase-7A heading |
| Canonical rule IDs / references | 69 IDs; no unknown rule reference in 13 active policy/plan/contract documents |
| Acceptance specification | 35 unique planned scenarios; no test source written or run |
| Markdown code fences in active policy/plan/contract documents | Balanced |
| Drawio XML and per-page cell IDs | 9 files parse; no duplicate cell IDs |
| PlantUML start/end structure | 9 files checked; no rendering claim |
| Original master snapshots | 6 copied before rewriting; 3 previously tracked originals match HEAD Git blobs, 3 were originally ignored/untracked |
| Diff file-type audit | Only Markdown/PlantUML/Drawio plus the documented .gitignore exception; no source/runtime config/SQL/JSON edits |
| git diff --check | Passed |

## 6. Remaining work

V3-01–V3-10 in the [implementation plan](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md) are future implementation/release tasks. Existing PlantHealth-only catalogue, baseline-only deposit, paid-before-flight readiness, manual publication and legacy contract assumptions still need code changes in a separately authorized implementation step. FE/Mobile source repositories are outside this workspace; their target contracts/UX are documented, not edited.
