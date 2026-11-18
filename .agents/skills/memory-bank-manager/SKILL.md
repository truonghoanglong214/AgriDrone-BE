---
name: memory-bank-manager
description: Quản lý và duy trì bộ nhớ dự án (Memory Bank) trong PROJECT_MEMORY.md, giúp các phiên chat và agent sau nắm bắt ngữ cảnh ngay lập tức
---

# Quản lý Bộ nhớ Dự án (Memory Bank Manager)

Mục đích: Đảm bảo tính liên tục của dự án AgriDrone giữa các phiên làm việc và giữa các AI Agent khác nhau mà không bị mất ngữ cảnh hay quên tiến độ.

## 1. Khi Bắt đầu một Phiên Chat Mới
1. Đọc ngay `PROJECT_MEMORY.md` ở thư mục gốc của repository.
2. Xác định:
   - Phase hiện tại đang làm dở hoặc chuẩn bị bắt đầu.
   - Nhánh git đang active (`LongTH`).
   - Các quyết định thiết kế quan trọng gần nhất.

## 2. Khi Hoàn thành một Công việc / Feature / Milestone
1. Mở `PROJECT_MEMORY.md`.
2. Cập nhật bảng **Master Plan Phase Tracker**:
   - Chuyển trạng thái hạng mục vừa làm thành `[x] Done` kèm commit ID tương ứng.
   - Ghi chú rõ mục tiếp theo cần làm (`[→] NEXT`).
3. Tóm tắt ngắn gọn các thành phần vừa tạo trong phần tóm tắt kết quả (entities mới, migrations, handlers).
4. Lưu file và commit cùng đợt với code tính năng.
