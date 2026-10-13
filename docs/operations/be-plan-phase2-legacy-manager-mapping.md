# Be-Plan Phase 2 — Legacy manager mapping

Ngày lập: 2026-09-23

## Quyết định dữ liệu

Không tự động chuyển `FarmMembership` role `MANAGER`, `TenantAdmin`, `Member`,
`Worker` hoặc `ZoneAssignment` thành `SYSTEM_MANAGER` hay
`FarmManagerAssignment`.

Lý do: một primary SystemManager là nhân sự vận hành của AgriDrone và phải được
SystemAdmin xác nhận riêng về tài khoản, trạng thái, availability và flight
qualification. Tenant/Farm role cũ không chứng minh các điều kiện này.

Các bảng legacy vẫn được giữ nguyên để đọc lịch sử và phục vụ migration ở
Phase 11. Phase 2 không drop, truncate, overwrite hoặc repurpose dữ liệu cũ.

## Preflight read-only

Chạy truy vấn sau trước khi tạo profile hoặc assignment từ dữ liệu hiện hữu:

```sql
select
    fm.tenant_id,
    fm.farm_id,
    fm.user_id,
    fm.role,
    fm.status,
    u.email,
    u.status as user_status
from identity.farm_memberships fm
join identity.users u on u.id = fm.user_id
where fm.role = 'MANAGER'::system.farm_member_role
order by fm.tenant_id, fm.farm_id, fm.user_id;
```

Truy vấn kiểm tra coverage của mô hình mới:

```sql
select
    f.tenant_id,
    f.id as farm_id,
    f.code,
    a.id as active_assignment_id,
    p.user_id as manager_user_id,
    p.status as profile_status,
    p.availability,
    p.qualification_status,
    p.qualification_expires_at
from farm.farms f
left join identity.farm_manager_assignments a
    on a.farm_id = f.id and a.ended_at is null
left join identity.system_manager_profiles p
    on p.id = a.system_manager_profile_id
where f.deleted_at is null
order by f.tenant_id, f.code;
```

Một Farm chỉ được đưa vào flow Survey mới khi truy vấn coverage cho thấy đúng
một assignment active và profile còn active, flight-qualified, chưa hết hạn.
Availability là điều kiện chọn manager khi assign/reassign; manager đã được gán
vẫn có thể xem công việc khi tạm thời unavailable nhưng không được chọn cho Farm
mới.

## Quy trình chuyển thủ công

1. SystemAdmin xác nhận user là nhân sự AgriDrone và user đang active.
2. Tạo `SystemManagerProfile`; thao tác này cấp role `SYSTEM_MANAGER`.
3. Cập nhật flight qualification và expiry.
4. Activate profile, sau đó đặt availability thành `Available`.
5. Assign Farm với reason cụ thể.
6. Kiểm tra audit old/new và coverage query.

Mọi reassignment kết thúc record cũ rồi tạo record mới trong cùng transaction;
không overwrite lịch sử.

## Recovery

- Nếu profile chưa đủ điều kiện, không tạo assignment.
- Nếu hai request cạnh tranh, filtered unique index
  `uq_farm_manager_assignments_active_farm` từ chối request thua cuộc.
- Nếu reassign lỗi giữa chừng, transaction rollback cả thao tác end cũ và insert
  mới.
- Có thể kết thúc assignment mới bằng API `end` với reason; không xóa record.
