# AgriDrone

Hệ thống quản lý vườn thanh long bằng drone và AI, với hai dịch vụ **Map** và **Combo** theo proposal v3.

- [Bắt đầu đọc tài liệu](Codex-Plan/README.md).
- [Business Rules đã chốt](Codex-Plan/Business-Rules-v3.md).
- [Kế hoạch triển khai](Codex-Plan/BE1-Core-Business-Implementation-Plan.md).
- [Danh mục tài liệu](docs/README.md).

ASP.NET Core modular monolith sở hữu nghiệp vụ; PostgreSQL/PostGIS, FastAPI xử lý ảnh/AI, object storage, RabbitMQ Outbox/Inbox và SePay. Manager Mobile online-only.

**Trạng thái 2026-11-13:** V3-02 đã có implementation; V3-03 đã có domain CaptureAttempt và operational/funding/cancellation contracts (03A-1/03A-2). Bước tiếp theo là **03B-1 persistence và expand migration**. DB acceptance của V3-02 còn pending; Follow-up production chờ release proof V3-06, public activation vẫn tắt. Xem [checklist V3-03](docs/plans/v3-03-execution-checklist.md) để theo dõi evidence và giới hạn nghiệm thu.
