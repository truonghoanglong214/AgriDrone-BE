# BE1 R2 legacy artifact inventory

> **V3 scope notice — 2026-11-10.**
> Historical locked contract/inventory: target business semantics are now governed by Business Rules v3 and ADR 0018. Previously released wire formats and recorded audit facts remain immutable; this notice does not rewrite them.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](../adr/0018-v3-paid-map-combo-business-policy.md), [target contracts](../contracts/survey-business-v3.md). Documentation-only update; no runtime change.

Ngày khóa inventory: 2026-10-15, theo proposal v2 tại checkpoint đó. Proposal v2 đã được dọn; giữ inventory này để giải thích legacy disposition và upgrade compatibility. Nghiệp vụ mới theo [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md).

| Artifact | Quyết định | Target write/read path |
|---|---|---|
| `SurveyServiceType.HarvestReadiness`, service seed và dữ liệu order/result cũ | `Stretch-disabled` + `compatibility-read-only` | Không xóa enum/migration/data; activation, public catalogue, ba request flow, approval và paid-readiness API đều bị khóa |
| Harvest Readiness assessment/criterion tables, repository/query và V2 contract | `compatibility-read-only` | Không producer/consumer hoặc public option mới trong MVP; chỉ phục vụ retention/upgrade |
| V2/V3 Plant Health payload có `SeverityPercent`, `HealthLevelCode` | `compatibility-read-only` | Payload đã phát hành bất biến; target producer/consumer dùng V4 không severity |
| `PlantCondition.Severity*`, `HealthLevel` và index lịch sử | `compatibility-read-only` | Không dùng trong V4/preliminary target; contract/drop chỉ thực hiện sau retention evidence |
| `TreatmentRecommendation` aggregate/table/contract V3 | `compatibility-read-only` | Target mới là `SourcedGuidance`, resolve theo suspected class + source/version, không theo severity |
| Wording diagnosis/official/expert-validated trong surface cũ | `remove` khỏi target | V4 dùng `ResultNature=Preliminary`, suspected/team-labelled limitation, uncertainty và provenance |
| Một legal/flight-feasibility checkbox cũ | `rename/supersede` | Checklist v2 tách `IsWrittenConsentFeasible` và `IsFlightPermissionFeasible`; feasibility không mở flight gate |
| Mutable order price/count snapshot | `supersede` | Giữ `Original*` bất biến; current effective view thay đổi qua adjustment ledger |
| FieldTask legacy | `remove` khỏi target | Không khôi phục; R2 chỉ phát hành `ManagerMobileWorkItemV1`/sync contract, full sync thuộc Step 10 |

## Runtime enforcement

- `SurveyServiceMvpPolicy` là guard dùng chung cho create/activate.
- Public catalogue filter ngay trong database query.
- Cả ba submit handlers và approval đều re-check Plant Health server-side.
- Harvest Readiness purpose trả failure `SERVICE_DISABLED`; API paid-purpose không expose lựa chọn này.
- Approval tạo hai requirement pending; chỉ verify có evidence mới mở consent/permission gate.
- Readiness query đánh giá status, verification time, expiry và revoke trực tiếp từ authoritative database state.

## Migration policy

Migration `R2ProposalV2Remediation` chỉ expand/backfill: tạo bảng mới, backfill
`Original*`, hạ service Harvest Readiness đang Active về Experimental và tạo hai
authorization requirement Pending cho order lịch sử. Migration không drop hoặc
rewrite bảng/cột/event lịch sử.
