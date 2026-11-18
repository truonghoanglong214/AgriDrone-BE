# Plant Health V4 golden envelopes

> **V3 scope notice — 2026-11-10.**
> Compatibility fixtures: all existing V4 JSON envelopes remain unchanged. Proposal v3 does not rename this wire version or authorize COMBO/internal-map semantics under validators that only accept legacy PlantHealth/published-map context.
> Current: [Business Rules v3](../../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](../../adr/0018-v3-paid-map-combo-business-policy.md), [target contracts](../survey-business-v3.md). Documentation-only update; no runtime change.

Các JSON này là synthetic contract fixtures, không phải production seed hoặc kết
quả test đã chạy. Schema/version/routing và field names khớp V4 DTO/validator.

| Fixture | Arrange/expected use trong Final Test |
|---|---|
| `normal.json` | Tạo matching Order InProgress, Mission/job source version 1, capture map/boundary, Plant và catalogue; import thành pending và Order PendingReview |
| `candidate-only.json` | Arrange độc lập; candidate thuộc mapping batch của capture map; PlantId null và NeedsReview |
| `boundary-exception.json` | Arrange độc lập; boundary đặt point ở cách mép 0.5 m; lưu blocking exception |
| `duplicate-handoff.json` | Sau normal; MessageId mới, cùng HandoffId/job/content; cùng outcome, không tạo data/audit mới |
| `reprocess-v2.json` | Sau normal; arrange source job revision 2 cùng provenance; original source ID giữ nguyên, thêm revision/Supersedes |
| `invalid-severity.json` | Phải DLQ ở raw JSON boundary, trước business import |

Các reference IDs đều cần arrange đúng context trong test database. Guidance trong
normal là reference giả: nếu chưa arrange entry active đúng class/version/source,
consumer vẫn giữ suggestion nhưng zone có blocking flag. Không import tuần tự các
fixture ghi “arrange độc lập” vào cùng job vì khác nội dung phải trả conflict.

SourceVersion lấy từ typed Mission/job context, không thêm field bắt buộc vào wire
V4. `normal`, duplicate và reprocess cần cùng dataset/annotation catalogue. Giá trị
dataset/annotation trong fixture theo baseline seed; model/provenance runtime phải
được kiểm chứng và cấu hình từ pipeline thực tế.
