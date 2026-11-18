---
name: dotnet-backend-workflow
description: Chuẩn mực lập trình backend .NET 10, Clean Architecture và quản lý quy trình triển khai tính năng cho dự án AgriDrone
---

# Quy trình Triển khai Backend cho AgriDrone

Skill này hướng dẫn chi tiết cách phát triển tính năng backend cho dự án AgriDrone theo mô hình Modular Monolith và Clean Architecture.

## 1. Chuẩn bị & Kế hoạch (Trước khi Code)
- Luôn đọc `PROJECT_MEMORY.md` để nắm rõ phase hiện tại và các module liên quan.
- Sử dụng Superpowers `writing-plans` để chia nhỏ công việc thành các bước tuần tự:
  1. Domain entities / Value objects / Invariants.
  2. Persistence configurations & EF Core migrations.
  3. Application commands / queries / handlers (MediatR).
  4. Integration contracts / Events / Outbox messages.
  5. API endpoints / Request-Response DTOs.

## 2. Quy định Biên dịch & Kiểm thử
- **KHÔNG CHẠY `dotnet test`**: Mọi bài test tích hợp dời về Phase V3-10.
- **CHỈ CHẠY `dotnet build`**:
  ```powershell
  dotnet build backend/AgriDrone.sln --no-restore --nologo
  ```
  Phải đạt: 0 Warning, 0 Error.

## 3. Quy chuẩn Mã nguồn
- Sử dụng C# 14 (.NET 10) với file-scoped namespaces, primary constructors, collection expressions.
- Domain Invariants: Đặt bên trong Entity methods, bảo vệ tính toàn vẹn của dữ liệu.
- Error Handling: Luôn dùng `Result` hoặc `Result<T>` kết hợp `AppError` (không throw Exception điều khiển luồng).
- Concurrency: Với PostgreSQL, sử dụng `FOR UPDATE` cho writer lock và `FOR SHARE` cho reader guard.
- Immutability: Mọi sự kiện, receipt, history phải là append-only, không update đè.
