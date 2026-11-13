# Survey Business v3 — HTTP API intent

Status: **Target design only, 2026-11-10**. Đây không phải Swagger export và không chứng nhận route/field đã live. Code, DTO và OpenAPI JSON runtime giữ nguyên. [Rules](../../Codex-Plan/Business-Rules-v3.md), [coordination contracts](../contracts/survey-business-v3.md), [runtime snapshot index](README.md).

**Route boundary locked 2026-11-11:** [ADR 0019](../adr/0019-v3-01-model-contract-and-migration-design.md) chọn additive `/api/v3/...` cho new DTOs/commands, không đổi legacy enum payload tại chỗ. Existing SePay callback path giữ nguyên. Các route families dưới đây vẫn target-only; [V3-02 plan](../plans/v3-02-catalogue-and-approval-implementation-plan.md) chốt thứ tự triển khai và public activation off.

## 1. Convention

- Public catalogue/tracking chỉ lộ thông tin tối thiểu; Tenant/Manager/Admin policies kiểm tra server-side. Không tin tenantId/status/price/amount client gửi.
- Mutating command có Idempotency-Key/OperationId và expected versions phù hợp; cùng key khác payload → conflict, replay rechecks quyền và trả stored result.
- UTC timestamps; hiển thị/calendar deadline theo UTC+7. Money VND, rate/deposit/Total/Due server-owned; service tokens target MAP/COMBO, không nhét vào legacy enum khi chưa triển khai.
- 401 unauthenticated; 403 policy denied; resource ngoài scope 404 theo security convention; 409 stale/conflict/invalid transition; 422 business validation. Field/error codes cụ thể cần controller review lúc implementation.

## 2. Route families dự kiến

Các route dưới đây là **target intent dưới version boundary đã chốt**. Các endpoint capture/release/claim thuộc phase sau vẫn cần typed schema/controller implementation review; không phát minh snapshot runtime.

| Actor | Method/path intent | Semantics |
|---|---|---|
| Public | GET /api/v3/catalog/survey-services | MAP/COMBO active rates/shared deposit/config versions/preliminary limitations |
| Public | POST /api/v3/public/survey-requests | NewCustomer service/location/contact; no provision |
| Public | POST /api/v3/public/survey-requests/track | Tracking token trong body, không ghi token vào URL/log; minimal status/decision, rate limit |
| Owner | POST /api/v3/tenant-owner/survey-requests | ExistingTenantNewFarm hoặc Follow-up own Farm/released Baseline |
| Admin | POST /api/v3/system-admin/survey-requests/{id}/approve | Atomic resources/Order snapshots; expected versions/checklist |
| Admin | GET/POST /api/v3/system-admin/survey-services/{id}/prices | Versioned per-pole rates, no historical overwrite |
| Admin | GET/POST /api/v3/system-admin/survey-deposit-policies | One shared versioned fixed deposit |
| Manager/Owner | Existing appointment propose/confirm/reschedule families | Capture appointment, not mandatory two commercial phases |
| Owner | POST /api/v3/tenant-owner/survey-orders/{id}/payments | Server resolves deposit or current finalized balance obligation |
| Integration | Existing SePay callback family | Adapter-specific verification/dedup; no client Confirm endpoint |
| Manager | GET /api/v3/system-manager/survey-orders/{id}/capture-readiness | Deposit + operational context; not full balance gate |
| Manager | POST /api/v3/system-manager/survey-orders/{id}/missions/prepare | Order-bound Missions, drone/attempt/current appointment; no client price/purpose bypass |
| Manager | Existing preflight/start/upload families | Atomic start and online upload retry/resume |
| Manager | POST /api/v3/system-manager/survey-orders/{id}/reflights | Reason/parent capture/evidence/approval; no new paid Order |
| Manager | Existing map/health review + approval families | Freeze exact reviewed result; MAP no disease approval dependency |
| Manager | POST /api/v3/system-manager/survey-orders/{id}/invoice/finalize | Reviewed count/evidence/current versions; uses creation-time price snapshot |
| Internal | Release evaluation after approval/payment | No second Manager publish required; no public set-Unlocked API |
| Owner | GET /api/v3/tenant-owner/survey-orders/{id} | Status, snapshotted price/deposit/invoice/Due/payment/refund obligations; locked content absent |
| Owner | GET /api/v3/tenant-owner/survey-orders/{id}/result | Exact released/nonwithdrawn content and claim-window metadata |
| Owner | GET /api/v3/tenant-owner/survey-orders/{id}/comparison | Stored mapping/health basis and compatibility/NotAssessed |
| Owner | Existing farm/map/plant/timeline/media families | Per-source released visibility, not merely current reviewed data |
| Owner/Admin | POST cancellation request/decision families | Before/after acceptable capture policy; no silent forfeiture |
| Owner | POST /api/v3/tenant-owner/survey-orders/{id}/claims | Released revision/window/evidence; server receipt deadline check |
| Manager | POST /api/v3/system-manager/claims/{id}/verification | Evidence-based findings/recommendation, no refund amount approval authority |
| Admin | POST /api/v3/system-admin/claims/{id}/refund-proposals | Versioned amount/currency/reason |
| Owner | POST /api/v3/tenant-owner/refund-proposals/{id}/accept | Exact proposal version; amount/status not client editable |
| Admin | POST /api/v3/system-admin/refunds/{id}/complete | Authorized reconciliation/bank transfer proof/reference, cap/reservation enforced |
| Manager | POST withdrawal/correction-review families | Immutable predecessor/replacement, substantive vs cosmetic reason |
| Admin | GET /api/v3/system-admin/revenue | month/year/service/cutoff/formulaVersion, reconciled shared query |
| Admin | GET /api/v3/system-admin/revenue/export | Same filters/query as dashboard; Excel attachment, audited export |

## 3. Response requirements

- Order creation: stable identifiers, ServiceType, price/deposit/policy snapshots; no invitation token/password leak.
- Order detail: fulfillment/review/payment/release states independently; not fabricate one `ReadyForPaidService` semantics. Old already released history still available when new Order pending.
- Finalized invoice: verified count, rate snapshot, Total, confirmed receipts, Due, Overpayment and invoice revision; zero-pole exception has clear review state.
- Released result: exact publication/revision/source/map references, ApprovedAt, PublishedAt=UnlockedAt, durable NoticeId, ClaimWindowId, deadlineExclusive and timezone. Include preliminary limitations for Combo and NotAssessed for MAP disease fields.
- Refund: reason/status/reserved/completed amounts and authorized proof reference; no broad bank details in ordinary financial exports.
- Revenue: service value, service refunds/net revenue, all receipts/refunds/net cash, held deposits/excess/retention/receivables/pending buckets, service filter and cutoff used.

## 4. Access/compatibility gates

Owner result access requires ownership, persisted release proof and nonwithdrawn policy at every surface, including cache/tiles/download/presigned media/latest health. Locked result cannot leak through per-pole/disease counts, comparison or profile aggregates; verified billable count/rate/Total/Due on the invoice remains allowed billing metadata before payment. Existing unaffected released records remain visible.

Runtime JSON snapshots giữ ngày/version gốc. Sau implementation thật, export Swagger mới, review diff với intent, có contract/security checks rồi mới chuyển status route từ Proposed sang Live. Không đổi old enum trên snapshots để giả MAP/COMBO đã hoạt động.
