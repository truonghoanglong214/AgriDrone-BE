# ADR-0010: Plant profile identity và owner-reported inventory change

- Status: Accepted
- Date: 2026-10-07
- Decision owners: Product, Backend 1, AI/GIS

## Context

Pricing tính theo active pole positions, trong khi longitudinal health history không được trộn giữa cây cũ và cây thay thế trên cùng vị trí. TenantOwner cần báo removal, replacement hoặc new plant nhưng không được trực tiếp sửa official inventory.

## Decision

- Digital Plant Profile đại diện cho một biological plant generation tại một pole position.
- Pole position là pricing unit. Một replacement làm profile cũ inactive và tạo PlantId mới cho generation mới; lịch sử hai generation không bị gộp.
- Removal/replacement có thể được applied sau assigned SystemManager verification. A genuinely new plant tại vị trí mới phải chờ later-survey detection evidence và manager approval trước khi có official profile.
- `PlantInventoryChangeReport` hỗ trợ `Removed`, `Replaced` và `NewPlant` với lifecycle:
  `Submitted → UnderReview → Verified → Applied`, hoặc `Rejected`;
  `NewPlant` đi qua `AwaitingSurveyEvidence` trước `Verified`.
- Owner có thể withdraw report trước khi review decision; withdrawal không xóa audit/history.
- Chỉ một open report được phép cho cùng Plant hoặc normalized pole location. Retry với cùng caller-scoped idempotency key trả report hiện hữu.
- Report không trực tiếp tạo, delete hoặc mutate official Plant. Apply chạy trong mapping/amendment transaction, giữ old/new link, evidence, reviewer, reason, map version, boundary version, audit và outbox.
- Pending/rejected reports không được đưa vào confirmed pole count hoặc TenantOwner official inventory view, ngoài màn hình trạng thái report của chính owner.

## Consequences

Plant change schema cần immutable report/evidence/history và replacement link. Map publication/amendment boundary phải hỗ trợ atomic lifecycle update mà không xóa profile cũ.
