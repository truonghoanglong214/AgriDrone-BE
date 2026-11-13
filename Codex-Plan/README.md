# AgriDrone — Tài liệu hiện hành theo proposal v3

Nghiệp vụ được chốt ngày **2026-11-10**. Các đặc tả dưới đây xác định phạm vi mục tiêu; trạng thái triển khai và bằng chứng nghiệm thu nằm trong checklist từng phase.

## Thứ tự đọc

1. [Proposal v3](Be-Plan-v3.md): phạm vi sản phẩm S1–S6 và nghiên cứu.
2. [Business Rules v3](Business-Rules-v3.md): policy, công thức, lifecycle và các trường hợp biên đã chốt.
3. [ADR 0018](../docs/adr/0018-v3-paid-map-combo-business-policy.md), [glossary](../docs/domain/be-plan-glossary.md), [ADR 0019](../docs/adr/0019-v3-01-model-contract-and-migration-design.md) và [locked design](../docs/domain/v3-01-locked-design.md).
4. [BE1 use cases](BE1-Use-Case-Specification.md), [BE2 use cases](BE2-Use-Case-Specification.md).
5. [Master implementation plan](BE1-Core-Business-Implementation-Plan.md).
6. [Target contracts](../docs/contracts/survey-business-v3.md), [API intent](../docs/openapi/survey-business-v3.openapi-intent.md), [FE/Mobile handoff](../docs/operations/v3-fe-mobile-handoff.md), [acceptance matrix](../docs/testing/v3-business-acceptance.md).

Nếu một policy chi tiết khác câu mô tả ngắn trong proposal, áp dụng amendment đã chốt trong Business Rules và đồng bộ hai tài liệu. Proposal v3 khác version wire contract Mapping V3/Health V4; giữ nguyên ý nghĩa payload đã phát hành.

## Trạng thái triển khai — 2026-11-13

| Phạm vi | Trạng thái |
|---|---|
| V3-01 | Design locked, source audit hoàn thành; live DB inventory còn là release gate |
| V3-02 | Có implementation catalogue/intake/approval; [checklist](../docs/plans/v3-02-execution-checklist.md) còn DB upgrade/locking/rollback acceptance |
| V3-03 / 03-00 | [Source/design baseline](../docs/operations/v3-03-execution-baseline.md) và [ADR 0020](../docs/adr/0020-v3-capture-execution-and-funding-boundaries.md) đã chốt |
| V3-03 / 03A-1, 03A-2 | Đã có CaptureAttempt và operational/funding/cancellation domain contracts; xem [checklist](../docs/plans/v3-03-execution-checklist.md) |
| Bước tiếp theo | **03B-1 persistence và expand migration** theo [technical plan](../docs/plans/v3-03-capture-funding-and-cancellation-implementation-plan.md) |
| Production | Public activation tắt; Follow-up chờ release proof V3-06; chưa nghiệm thu PostgreSQL |

## Phạm vi nghiệp vụ

- Hai dịch vụ bán: `MAP`, `COMBO`; First survey và Follow-up đều là paid Order.
- Giá/cọc/claim/cancellation terms có version và được snapshot khi tạo Order. Cấu hình operational/spatial/model pin theo cadence của V3-01.
- Lịch sử và lineage bất biến; quality Re-flight tạo CaptureAttempt/Mission mới trong Order gốc, không phí dịch vụ thêm.
- Cọc trước capture; balance sau review và trước publication/unlock. Baseline duyệt nội bộ chưa mở quyền Owner.
- Manager Mobile online-only. .NET sở hữu nghiệp vụ, FastAPI xử lý ảnh/AI; giữ SePay cho MVP.
- Claims theo 7 ngày lịch; refund có thỏa thuận/chứng từ, corrected revisions và đối soát tài chính.

Demo rates MAP=1.000, COMBO=2.000 VND/trụ, cọc chung 100.000 VND là cấu hình khởi tạo có version; không thay snapshot của giao dịch cũ.

## Tra cứu

- [Ma trận yêu cầu → use case → owner → acceptance](../docs/domain/v3-traceability-matrix.md).
- [Danh mục tài liệu và compatibility references](../docs/README.md).
- [RabbitMQ guide](RabbitMQ-Architecture-And-Usage-Guide.md).

Proposal v1/v2, báo cáo/phase plan cũ, archive trùng và sơ đồ cũ đã được dọn. ADR và compatibility artifacts còn dùng được giữ kèm phạm vi áp dụng.
