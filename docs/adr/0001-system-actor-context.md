# ADR-0001: System actor context không phụ thuộc Tenant

- Status: Accepted
- Date: 2026-09-22
- Decision owners: Backend 1, Security

## Context

SystemAdmin và SystemManager là nhân sự của AgriDrone, không phải thành viên của Tenant khách hàng. Context hiện tại ưu tiên `TenantMembership` và tenant selection, nên nếu tái sử dụng cho operational flow sẽ tạo quyền giả hoặc tin vào `TenantId` do client cung cấp.

## Decision

- JWT của system actor mang `UserId` và system role; không bắt buộc có selected `TenantId`.
- `SYSTEM_ADMIN` có quyền quản trị toàn hệ thống theo policy riêng.
- `SYSTEM_MANAGER` truy cập dữ liệu qua active primary `FarmManagerAssignment`; server resolve `Farm → Tenant → assignment`, không tin `TenantId` do client tự khẳng định.
- Customer request vẫn cần tenant context và active `TenantMembership` với role `OWNER`.
- Background/AI action dùng execution context có `ActorType`, correlation/causation và source; không giả mạo user/tenant context.
- Mọi authorization quyết định từ system actor phải auditable và deny-by-default khi profile/assignment không active hoặc qualification hết hạn.

## Consequences

System actors đăng nhập không cần bước select Tenant. Các policy cũ `FarmManage`/`ZoneManage` không được dùng cho operational flow mới; Phase 2 phải thêm profile, assignment history và access service chuyên biệt.
