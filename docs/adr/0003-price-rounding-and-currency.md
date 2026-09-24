# ADR-0003: Currency và quy tắc làm tròn giá

- Status: Accepted
- Date: 2026-09-22
- Decision owners: Backend 1, Product

## Context

SurveyOrder cần snapshot giá để catalogue price thay đổi không làm thay đổi order đã xác nhận. Phép nhân diện tích thập phân và giá phải cho kết quả xác định giữa .NET, PostgreSQL và payment provider.

## Decision

- Currency MVP là `VND`, lưu bằng mã ISO 4217 ba ký tự; không suy ra currency từ locale.
- `ConfirmedSurveyAreaHa` dùng decimal precision `(12,4)` và phải lớn hơn 0.
- `PricePerHaSnapshot` và `FinalPrice` dùng decimal precision `(18,2)`, không dùng `double/float`.
- Công thức duy nhất: `FinalPrice = Round(ConfirmedSurveyAreaHa × PricePerHaSnapshot, 2, MidpointRounding.AwayFromZero)`.
- Giá, currency và area được khóa cùng lúc khi SystemManager confirm scope. Catalogue price thay đổi không cập nhật snapshot.
- Sau payment confirmation, thay đổi area/price chỉ qua auditable `PriceAdjustment`; không silent recalculation.
- API truyền monetary values dưới dạng JSON number có decimal semantics; event payload kèm currency và snapshot values.

## Consequences

VND thực tế không dùng đơn vị lẻ nhưng schema vẫn giữ scale 2 để tương thích provider/audit. Mọi implementation và test phải dùng cùng rounding rule ở application và database boundary.
