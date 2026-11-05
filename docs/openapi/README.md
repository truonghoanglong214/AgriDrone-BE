# OpenAPI compatibility snapshots

`be-plan-phase0.openapi.json` lÃ  snapshot cá»§a API legacy trÆ°á»›c khi Phase 1 Ä‘Ã³ng cÃ¡c entry point sai nghiá»‡p vá»¥. Snapshot Ä‘Æ°á»£c láº¥y tá»« `GET /swagger/v1/swagger.json` sau khi toÃ n bá»™ migration hiá»‡n táº¡i Ã¡p dá»¥ng thÃ nh cÃ´ng trÃªn PostgreSQL test.

`be-plan-phase1.openapi.json` lÃ  contract sau cutover Phase 1. CÃ¡c route legacy váº«n hiá»‡n diá»‡n Ä‘á»ƒ client nháº­n há»£p Ä‘á»“ng chuyá»ƒn tiáº¿p á»•n Ä‘á»‹nh, Ä‘Æ°á»£c Ä‘Ã¡nh dáº¥u `deprecated: true` vÃ  khai bÃ¡o response `410`. Khi `LegacyFeatures:EnableDeprecatedEndpoints=false` (máº·c Ä‘á»‹nh), middleware tráº£ `LegacyFlow.Disabled` trÆ°á»›c khi handler cháº¡y.

KhÃ´ng chá»‰nh tay snapshot. Khi contract chá»§ Ã½ thay Ä‘á»•i, táº¡o snapshot/review diff má»›i vÃ  giá»¯ cÃ¡c báº£n trÆ°á»›c Ä‘á»ƒ kiá»ƒm tra compatibility hoáº·c thÃ´ng bÃ¡o deprecation/`410 Gone`.

Step 0R khÃ´ng má»Ÿ thÃªm HTTP endpoint nÃªn khÃ´ng táº¡o snapshot Swagger giáº£. API intent
Ä‘Ã£ khÃ³a cho cÃ¡c step triá»ƒn khai sau Ä‘Æ°á»£c mÃ´ táº£ táº¡i
[`be1-step0r.openapi-intent.md`](be1-step0r.openapi-intent.md). Khi controller tháº­t
Ä‘Æ°á»£c má»Ÿ, snapshot má»›i pháº£i Ä‘Æ°á»£c sinh tá»« `GET /swagger/v1/swagger.json` vÃ  Ä‘á»‘i chiáº¿u
vá»›i intent nÃ y.

`be1-surveys.openapi.json` lÃ  snapshot runtime má»›i nháº¥t sau Phase 4E. Snapshot khÃ³a
tÃ¡m route Survey Request cho public, TenantOwner vÃ  SystemAdmin; public submit lÃ 
anonymous, cÃ¡c command retry-safe yÃªu cáº§u `Idempotency-Key`, cÃ¡c enum dÃ¹ng wire
token á»•n Ä‘á»‹nh. Route approve chá»‰ dÃ nh cho SystemAdmin, nháº­n body cÃ³ checklist/
reason/expected version vÃ  tráº£ identifiers/tráº¡ng thÃ¡i, khÃ´ng lá»™ invitation token,
password hoáº·c applicant PII. Hai route SystemManager boundary-scope cung cáº¥p
assigned-manager work context vÃ  atomic boundary verification vá»›i GeoJSON,
evidence cÃ¹ng optimistic version guards. Bá»‘n route Phase 4B bá»• sung manager
propose/re-propose Baseline appointment, owner confirm/request-reschedule vÃ  manager
Ä‘á»c authoritative Baseline readiness theo `missionId`; appointment dÃ¹ng optimistic
version guards vÃ  readiness khÃ´ng nháº­n payment state tá»« client. Route Phase 4C
`POST /api/system-manager/survey-orders/{orderId}/pole-count/confirm` chá»‰ nháº­n
expected Order/base-map version vÃ  evidence; count, price version, unit price,
currency vÃ  final price Ä‘á»u lÃ  server-owned output. Phase 4D má»Ÿ thÃªm
`POST /api/tenant-owner/survey-orders/{orderId}/payments`, chá»‰ nháº­n
expected Order version vÃ  tráº£ session Ä‘Æ°á»£c táº¡o tá»« amount/currency snapshot phÃ­a
server. `POST /api/integrations/payments/{provider}/callbacks` lÃ  anonymous provider
route cÃ³ hai header timestamp/signature, payload identity/event/amount/currency vÃ 
khÃ´ng cÃ³ Bearer security requirement; chá»¯ kÃ½ váº«n Ä‘Æ°á»£c kiá»ƒm tra trÆ°á»›c application
handler. Phase 4E bá»• sung request/review/apply PriceAdjustment append-only,
SystemAdmin payment reconciliation vÃ  paid-service readiness theo MissionPurpose.
SystemAdmin survey-service catalogue chá»‰ nhÃºng `currentPrice` Ä‘ang hiá»‡u lá»±c; lá»‹ch
sá»­ giÃ¡ Ä‘áº§y Ä‘á»§ cÃ³ response riÃªng táº¡i `/{serviceId}/prices`.
`POST /api/system-admin/survey-services/{serviceId}/prices` chá»‰ nháº­n amount,
currency vÃ  expected service version; backend tá»± Ä‘áº·t thá»i Ä‘iá»ƒm hiá»‡u lá»±c UTC hiá»‡n
táº¡i vÃ  Ä‘á»ƒ version má»Ÿ cho tá»›i láº§n thay giÃ¡ tiáº¿p theo.
Callback nháº­n thÃªm refund/chargeback nhÆ°ng váº«n giá»¯ PaymentEvent báº¥t biáº¿n; owner
khÃ´ng cÃ³ contract xÃ¡c nháº­n payment trá»±c tiáº¿p. Snapshot Ä‘Æ°á»£c sinh tá»«
`GET /swagger/v1/swagger.json`

R2 (2026-10-15) khÃ³a public catalogue vÃ  paid-readiness purpose chá»‰ cÃ²n
`PLANT_HEALTH`. `HARVEST_READINESS` váº«n tá»“n táº¡i trong database/contract cÅ© Ä‘á»ƒ
Ä‘á»c lá»‹ch sá»­ nhÆ°ng khÃ´ng cÃ²n lÃ  public request option. Capture authorization
Ä‘Æ°á»£c quáº£n trá»‹ qua:

- `PUT /api/system-admin/survey-orders/{orderId}/capture-authorizations/{type}/verify`
- `PUT /api/system-admin/survey-orders/{orderId}/capture-authorizations/{type}/revoke`

`type` chá»‰ nháº­n `WRITTEN_DATA_USE_CONSENT` hoáº·c `FLIGHT_PERMISSION`.
cá»§a API Development Ä‘ang cháº¡y vá»›i startup migration/core-master-data validation
táº¡m táº¯t báº±ng environment override; giÃ¡ trá»‹ máº·c Ä‘á»‹nh cá»§a á»©ng dá»¥ng váº«n báº­t cÃ¡c
startup gate nÃ y.

R2 Step 1E (2026-10-17) tÃ¡i sinh snapshot tá»« runtime sau Plant Health master-data
cutover. `GET /api/catalog/plant-health` tráº£ táº­p team-labelled class vÃ  sourced
guidance khÃ´ng cÃ³ severity; cÃ¡c route quáº£n trá»‹ guidance náº±m dÆ°á»›i
`/api/system-admin/plant-health/guidance`. Route `/api/catalog/health-levels`
khÃ´ng cÃ²n trong snapshot. Legacy health-level/treatment/Harvest Readiness schema
chá»‰ cÃ²n phá»¥c vá»¥ compatibility vÃ  khÃ´ng Ä‘Æ°á»£c Ä‘Äƒng kÃ½ lÃ m target HTTP runtime.

R2 Step 2 (2026-10-18) nÃ¢ng Survey Request checklist lÃªn
`survey-request-review.v5`. Contract tÃ¡ch `consent-feasibility` vÃ 
`flight-permission-feasibility`, Ä‘á»“ng thá»i kháº³ng Ä‘á»‹nh Ä‘Ã¢y khÃ´ng pháº£i consent/
permission Ä‘Ã£ verified. Admin inbox khÃ´ng cÃ²n `applicantName`; contact PII chá»‰
Ä‘Æ°á»£c tráº£ táº¡i SystemAdmin detail. Snapshot hiá»‡n hÃ nh lÃ  contract tá»•ng há»£p nÃªn cÃ³
thá»ƒ chá»©a route approve cá»§a Step 3 dÃ¹ route Ä‘Ã³ khÃ´ng thuá»™c Step 2.

R2 Step 3 (2026-10-18) tÃ¡i sinh snapshot sau khi approval response bá»• sung
`previousOrderCompatibilityPolicyVersion` vÃ 
`previousOrderCompatibilityReason`. Hai field nÃ y giáº£i thÃ­ch vÃ¬ sao
`previousCompatibleOrderId` Ä‘Æ°á»£c chá»n hoáº·c bá»‹ Ä‘á»ƒ null theo policy kiá»ƒm tra cÃ¹ng
base-map/Plant identity vÃ  cÃ¹ng label-dataset/annotation-rule semantics.
