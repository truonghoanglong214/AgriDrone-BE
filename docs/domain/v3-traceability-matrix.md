# V3 traceability — proposal, approved rules và acceptance

Date: **2026-11-10**. Sources: [proposal](../../Codex-Plan/Be-Plan-v3.md), [rules](../../Codex-Plan/Business-Rules-v3.md), [BE1](../../Codex-Plan/BE1-Use-Case-Specification.md), [BE2](../../Codex-Plan/BE2-Use-Case-Specification.md). Runtime status của mọi delta là **Planned/RebaselineRequired**, không suy từ documentation approval.

Design checkpoint 2026-11-11: [V3-01](v3-01-locked-design.md) và [source audit](../operations/v3-01-source-audit.md) đã chốt. BR-CFG-01/02, BR-AUD-01/02 và BR-CAP-03 cụ thể hóa dynamic config/lineage/attempt requirements. [V3-02A–F](../plans/v3-02-catalogue-and-approval-implementation-plan.md) là implementation plan kế tiếp; V3-01 DesignLocked không chuyển runtime status thành Done.

| Proposal / accepted policy | Rules | UC / implementation owner | Phase | Acceptance IDs |
|---|---|---|---|---|
| S1 two paid services/public catalogue/tracking | BR-SCP-01, BR-ORD-01–03 | BE1-UC03/04/05 | V3-02 | V3-T01–04 |
| NewCustomer and existing Tenant/Farm no duplicates | BR-ACT, BR-ORD-01–04 | BE1-UC00/01/02/04/05 | V3-01/02 | V3-T02–04, T28 |
| S2 price/deposit snapshots at creation | BR-ORD-04–06, BR-PRC | BE1-UC03/05/06 | V3-02 | V3-T01/03/05 |
| Deposit before every first/follow-up capture | BR-PAY-01–03 | BE1-UC06/07; BE2-UC01/10 | V3-03 | V3-T06/07/08 |
| Balance after survey before release | BR-PAY-04–06, BR-REL | BE1-UC06/13; BE2-UC08/10/11 | V3-06/07 | V3-T09–12/28 |
| First approved paid Baseline, no free flight | BR-MAP-01/02 | BE1-UC08/09; BE2-UC04/06 | V3-04/05 | V3-T13/14 |
| Follow-up paid Order, no Baseline overwrite | BR-ORD-03/08, BR-MAP-03 | BE1-UC05/08/11; BE2-UC04/05 | V3-02/04/07 | V3-T04/15/16 |
| Free quality Re-flight same Order | BR-CAP, BR-CAN | BE1-UC06; BE2-UC01/02 | V3-03 | V3-T08/17 |
| S3 GNSS+grid matching, boundary uncertainty | BR-PLT-01/02 | BE1-UC07/08; BE2-UC04/05 | V3-04 | V3-T13/15/18 |
| Combo 2–3 suspected classes/zones/cited guidance | BR-HLT-01/02 | BE1-UC09; BE2-UC06 | V3-05 | V3-T14/19/20 |
| S4 persistent profiles and later-survey lifecycle | BR-PLT-01–04 | BE1-UC08/11; BE2-UC05 | V3-04/07 | V3-T15/18/20 |
| MAP does not clear disease history | BR-HLT-02, BR-CMP | BE1-UC09/11; BE2-UC06/11 | V3-05/07 | V3-T16/19/20 |
| S5 online-only mobile, upload retry/resume | BR-SCP-02, BR-REL-05 | BE1-UC12; BE2-UC02 | V3-03/07 | V3-T21/28 |
| S6 evidenced claim 7 calendar days after notice | BR-CLM-01–05 | BE1-UC14; BE2-UC12 | V3-06/08 | V3-T22/23 |
| Negotiated manual refund, cap actual receipts | BR-RFD-01–05 | BE1-UC14; BE2-UC10/12 | V3-08 | V3-T24–26 |
| Withdrawn/corrected report/Baseline versions | BR-MAP-04, BR-COR | BE1-UC15; BE2-UC08/11 | V3-08 | V3-T23/27 |
| Monthly revenue/cash/dashboard/Excel UTC+7 | BR-FIN-01–05 | BE1-UC16; BE2-UC12 | V3-09 | V3-T29–32 |
| Accepted demo rates 1.000/2.000/shared 100.000 | BR-PRC-01/02 | BE1-UC03/05 | V3-02 | V3-T01/05 |
| Accepted overpayment refund without release block | BR-PAY-04/05/07 | BE1-UC06/13/14 | V3-06/08 | V3-T09/25/29 |
| Accepted cancellation/no-show/weather rules | BR-CAN-01–06 | BE1-UC06; BE2-UC01 | V3-03/08/09 | V3-T17/26/30 |
| ApprovedAt separate, release/notice one commit | BR-REL-01–06 | BE1-UC13; BE2-UC08/11/12 | V3-06/07 | V3-T10–12/28 |
| Substantive correction new window, cosmetic no | BR-CLM-05, BR-COR-01 | BE1-UC14/15 | V3-08 | V3-T23/27 |
| .NET business/FastAPI processing/SePay retained | BR-ACT-03/04 | BE2-UC03/10 | V3-01/03/05 | V3-T07/14/33 |
| Immutable old contracts; explicit legacy migration | BR-ORD-04/05, ADR 0018 | BE1-UC00; BE2-UC03/09 | V3-01/10 | V3-T33/34 |
| NFR1/2, research RQ1/RQ2 | Rules §10, proposal NFR/RQ | BE1/BE2 with research team | V3-10 | V3-T35 |

Acceptance definitions: [V3 business acceptance](../testing/v3-business-acceptance.md). API/event target semantics: [API intent](../openapi/survey-business-v3.openapi-intent.md), [coordination contracts](../contracts/survey-business-v3.md). Historical evidence: [rebaseline inventory](../operations/v3-documentation-rebaseline-report.md), không phải automated proof cho v3.
