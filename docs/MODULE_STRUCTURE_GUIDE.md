# Hướng dẫn cấu trúc một module trong AgriDrone

Tài liệu này là bản đồ giúp quyết định một class hoặc file mới nên được đặt ở đâu trong kiến trúc hiện tại của AgriDrone.

Module `AgriDrone.Modules.Identity` được dùng làm ví dụ vì đây là module đang có đủ các phần `Domain`, `Application` và `Infrastructure`.

---

## 1. Bức tranh tổng thể

AgriDrone đang tổ chức backend theo hướng **modular monolith**, kết hợp:

- Chia hệ thống thành các module nghiệp vụ như `Identity`, `Farms`, `Missions`, `Plants`.
- Bên trong mỗi module chia thành các layer `Domain`, `Application`, `Infrastructure`.
- Bên trong `Application`, mỗi use case được gom theo feature như `GetUsers`, `RegisterUser`.

Cấu trúc tổng quát:

```text
backend/src/
├── AgriDrone.Api/
│   ├── Contracts/
│   ├── Controllers/
│   └── Program.cs
├── Modules/
│   └── AgriDrone.Modules.<ModuleName>/
│       ├── Domain/
│       ├── Application/
│       ├── Infrastructure/
│       ├── DependencyInjection.cs
│       └── AgriDrone.Modules.<ModuleName>.csproj
└── BuildingBlocks/
    ├── AgriDrone.SharedKernel/
    ├── AgriDrone.SharedInfrastructure/
    └── AgriDrone.Database/
```

Luồng dependency quan trọng nhất:

```text
AgriDrone.Api ────────────────> Application
                                      │
                                      v
                                    Domain

Infrastructure ──────────────> Application
Infrastructure ──────────────> Domain
```

Ý nghĩa:

- `Domain` không phụ thuộc `Application`, `Infrastructure` hoặc API.
- `Application` có thể phụ thuộc `Domain`, nhưng không phụ thuộc implementation trong `Infrastructure`.
- `Infrastructure` được phụ thuộc `Application` và `Domain` để implement các interface của hai layer đó.
- API gọi vào `Application`, thông thường qua MediatR `ISender`.
- Việc nối interface với implementation được thực hiện tại `DependencyInjection.cs`.

Nguyên tắc dễ nhớ:

> Code càng gần nghiệp vụ thì càng nằm vào phía trong. Code càng gắn với framework, database hoặc dịch vụ bên ngoài thì càng nằm ở Infrastructure.

---

## 2. Cấp gốc của module

Ví dụ:

```text
AgriDrone.Modules.Identity/
├── Domain/
├── Application/
├── Infrastructure/
├── DependencyInjection.cs
└── AgriDrone.Modules.Identity.csproj
```

### 2.1 File `.csproj`

File project khai báo:

- Project reference mà module được phép sử dụng.
- NuGet package của module.
- Target framework và các thiết lập build nếu cần.

Ví dụ package có thể xuất hiện ở đây:

- EF Core và Npgsql vì implementation persistence nằm trong chính module.
- MediatR và FluentValidation vì module chứa handler và validator.
- JWT hoặc thư viện hash mật khẩu vì Identity sử dụng các implementation này.

Không thêm package vào module khác nếu package chỉ phục vụ riêng module hiện tại.

### 2.2 `DependencyInjection.cs`

Đây là **composition root của module**, chịu trách nhiệm đăng ký các thành phần của module vào DI container:

```csharp
services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<IUserQueries, UserQueries>();
services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
services.AddScoped<IPasswordService, PasswordService>();
```

File này cũng có thể đăng ký:

- `DbContext`.
- MediatR handlers.
- FluentValidation validators.
- Authorization handlers.
- Options/configuration dành riêng cho module.

Không đặt nghiệp vụ hoặc câu truy vấn database trong `DependencyInjection.cs`.

---

## 3. Domain — mô hình và luật nghiệp vụ cốt lõi

`Domain` trả lời câu hỏi:

> Hệ thống đang quản lý khái niệm nghiệp vụ nào, và các khái niệm đó phải tuân theo quy tắc gì?

Ví dụ hiện tại:

```text
Domain/
├── Users/
│   ├── User.cs
│   ├── UserStatus.cs
│   └── IUserRepository.cs
├── Roles/
│   ├── Role.cs
│   └── UserRole.cs
├── Tenants/
│   ├── Tenant.cs
│   ├── TenantMembership.cs
│   └── TenantMemberRole.cs
└── FarmMemberships/
    ├── FarmMembership.cs
    ├── FarmMemberRole.cs
    └── FarmAccessScope.cs
```

Các thư mục con của `Domain` nên được đặt theo **khái niệm nghiệp vụ**, không đặt theo loại kỹ thuật chung chung.

Ví dụ tốt:

```text
Domain/Users
Domain/Tenants
Domain/FarmMemberships
```

Không nên gom tất cả thành:

```text
Domain/Entities
Domain/Enums
Domain/Interfaces
```

Việc để các file liên quan đến cùng một khái niệm ở gần nhau giúp module dễ tìm và dễ mở rộng hơn.

### 3.1 Aggregate root và entity

Đặt tại:

```text
Domain/<BusinessConcept>/<EntityName>.cs
```

Ví dụ:

```text
Domain/Users/User.cs
Domain/Tenants/Tenant.cs
```

Entity chứa:

- Trạng thái nghiệp vụ.
- Invariant và quy tắc phải luôn đúng.
- Các method thay đổi trạng thái có ý nghĩa nghiệp vụ.
- Quan hệ giữa các thành phần bên trong aggregate.

Ví dụ các hành vi phù hợp với `User`:

```csharp
user.ChangePassword(newPasswordHash);
user.Lock();
user.Activate();
user.RecordSuccessfulLogin(now);
```

Không nên để handler tự sửa property một cách tùy ý. Setter thường là `private` và thay đổi trạng thái thông qua domain method.

### 3.2 Enum và value object

Đặt gần business concept sử dụng nó:

```text
Domain/Users/UserStatus.cs
Domain/FarmMemberships/FarmMemberRole.cs
```

Value object cũng nên đặt tương tự:

```text
Domain/Users/Email.cs
Domain/Farms/FarmCode.cs
```

Một type nên là value object khi nó:

- Được nhận diện bằng giá trị thay vì ID.
- Có validation hoặc behavior riêng.
- Cần ngăn việc truyền một chuỗi hoặc số không hợp lệ khắp hệ thống.

### 3.3 Domain service

Nếu một luật nghiệp vụ:

- Thực sự thuộc domain;
- Cần phối hợp nhiều domain object;
- Không tự nhiên thuộc về một entity cụ thể;

thì có thể tạo domain service, ví dụ:

```text
Domain/Memberships/MembershipEligibilityService.cs
```

Domain service không được gọi database, HTTP, JWT, file system hoặc framework API.

### 3.4 Repository interface

Repository interface thường đặt gần aggregate mà nó phục vụ:

```text
Domain/Users/IUserRepository.cs
```

Repository phù hợp khi use case cần:

- Load aggregate để gọi domain behavior.
- Thêm aggregate mới.
- Theo dõi thay đổi rồi commit bằng Unit of Work.
- Kiểm tra dữ liệu để bảo vệ invariant của write use case.

Ví dụ:

```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    void Add(User user);
}
```

Repository interface nằm trong `Domain`, còn implementation EF Core nằm trong `Infrastructure/Repositories`.

Repository không phải lựa chọn tốt cho:

- Danh sách phân trang chỉ để hiển thị.
- Dashboard hoặc report.
- Projection sang DTO.
- Search có nhiều bộ lọc dành riêng cho màn hình.

Các trường hợp trên nên dùng read query abstraction của `Application`.

### 3.5 Domain event

Nếu dự án bổ sung domain event, event mô tả một việc có ý nghĩa nghiệp vụ đã xảy ra:

```text
Domain/Users/Events/UserRegisteredDomainEvent.cs
```

Ví dụ tên event:

- `UserRegisteredDomainEvent`.
- `PasswordChangedDomainEvent`.
- `MissionCompletedDomainEvent`.

Event không nên chứa implementation gửi email, gọi webhook hoặc publish message broker. Các side effect đó thuộc handler ở layer ngoài.

### 3.6 Những gì không được đặt trong Domain

Không đặt các thành phần sau trong `Domain`:

- `DbContext`, `DbSet`, EF Core configuration.
- Controller, HTTP request, HTTP response.
- JWT generation.
- BCrypt, Argon2 hoặc implementation hash mật khẩu.
- Truy vấn LINQ phụ thuộc EF Core.
- Gọi API bên thứ ba.
- Đọc configuration hoặc environment variable.
- DTO dành riêng cho một màn hình/API.

---

## 4. Application — các use case của module

`Application` trả lời câu hỏi:

> Người dùng hoặc hệ thống có thể yêu cầu module thực hiện những tác vụ nào?

Cấu trúc hiện tại:

```text
Application/
├── Features/
│   ├── GetUsers/
│   │   ├── GetUsersQuery.cs
│   │   ├── GetUsersQueryHandler.cs
│   │   ├── GetUsersQueryValidator.cs
│   │   └── UserListItemResponse.cs
│   └── RegisterUser/
│       ├── RegisterUserCommand.cs
│       ├── RegisterUserCommandHandler.cs
│       ├── RegisterUserCommandValidator.cs
│       └── RegisterUserResponse.cs
├── Abstractions/
├── Authorization/
└── Errors/
```

### 4.1 `Features` — tổ chức theo use case

Mỗi hành động của hệ thống nên có một thư mục feature riêng:

```text
Application/Features/<UseCaseName>/
```

Ví dụ:

```text
Application/Features/RegisterUser
Application/Features/GetUsers
Application/Features/ChangePassword
Application/Features/LockUser
```

Một feature thường chứa:

- Command hoặc Query message.
- Handler.
- Validator.
- Response DTO của use case.
- Mapping riêng của feature nếu thực sự cần.

Không cần bắt buộc mọi feature phải có đủ tất cả file. Chỉ tạo thành phần có vai trò thực tế.

### 4.2 Command

Command biểu diễn yêu cầu **thay đổi trạng thái**:

```text
RegisterUserCommand
ChangePasswordCommand
LockUserCommand
CreateMissionCommand
```

Đặt tại:

```text
Application/Features/<Feature>/<Feature>Command.cs
```

Command chỉ chứa dữ liệu đầu vào mà use case cần. Nó không chứa EF Core hoặc logic truy cập database.

### 4.3 Query message

Query biểu diễn yêu cầu **đọc dữ liệu mà không thay đổi trạng thái**:

```text
GetUsersQuery
GetUserDetailQuery
SearchMissionsQuery
```

Đặt tại:

```text
Application/Features/<Feature>/<Feature>Query.cs
```

Không nhầm `GetUsersQuery` với `IUserQueries`:

- `GetUsersQuery` là message đi vào use case qua MediatR.
- `IUserQueries` là interface mà handler dùng để đi ra ngoài đọc dữ liệu.

### 4.4 Handler

Handler là nơi điều phối một use case:

- Nhận command hoặc query.
- Gọi repository hoặc query abstraction.
- Gọi domain method.
- Gọi các abstraction như `IPasswordService` hoặc `IJwtTokenGenerator`.
- Commit Unit of Work cho write use case.
- Trả về `Result` hoặc response DTO.

Ví dụ write flow:

```text
RegisterUserCommandHandler
    ├── IUserRepository
    ├── IPasswordService
    └── IIdentityUnitOfWork
```

Handler không nên:

- Phụ thuộc trực tiếp `IdentityDbContext`.
- Khởi tạo `new UserRepository()` hoặc `new PasswordService()`.
- Chứa HTTP status code.
- Đọc `HttpContext` trực tiếp.
- Chứa chi tiết thuật toán JWT, hashing hoặc SQL.

### 4.5 Validator

Validator kiểm tra input của use case:

```text
Application/Features/RegisterUser/RegisterUserCommandValidator.cs
```

Phù hợp để kiểm tra:

- Trường bắt buộc.
- Độ dài chuỗi.
- Format email.
- Page size hợp lệ.
- Giá trị số nằm trong khoảng cho phép.

Không dùng validator để thực hiện thay đổi dữ liệu.

Validation cần query database, chẳng hạn kiểm tra email duy nhất, thường vẫn phải được bảo vệ trong handler/domain flow để tránh race condition. Database unique constraint vẫn là lớp bảo vệ cuối cùng.

### 4.6 Response DTO

Response DTO mô tả dữ liệu Application trả về cho caller:

```text
Application/Features/GetUsers/UserListItemResponse.cs
Application/Features/RegisterUser/RegisterUserResponse.cs
```

Nên dùng response DTO thay vì trả thẳng entity vì:

- Không làm lộ toàn bộ domain model.
- Tránh vô tình trả `PasswordHash` hoặc dữ liệu nhạy cảm.
- API contract ít bị ảnh hưởng khi entity thay đổi.
- Query có thể projection đúng các cột cần thiết.

### 4.7 `Application/Abstractions`

Thư mục này chứa các **output port**: Application mô tả khả năng nó cần, Infrastructure cung cấp implementation kỹ thuật.

Ví dụ hiện tại:

```text
Application/Abstractions/
├── IUserQueries.cs
├── IPasswordService.cs
├── IJwtTokenGenerator.cs
└── IIdentityUnitOfWork.cs
```

Đặt interface tại đây khi nó mô tả nhu cầu của use case, chẳng hạn:

- Đọc read model: `IUserQueries`.
- Hash/verify password: `IPasswordService`.
- Sinh access token: `IJwtTokenGenerator`.
- Commit transaction: `IIdentityUnitOfWork`.
- Gửi email: `IEmailSender`.
- Lưu file: `IFileStorage`.
- Lấy thời gian để dễ kiểm thử: `IClock`.

Application chỉ biết interface, không biết implementation dùng BCrypt, JWT library, PostgreSQL, S3 hay SMTP.

### 4.8 `Application/Errors`

Đặt các lỗi dự kiến của use case ở đây:

```text
Application/Errors/UserError.cs
```

Ví dụ:

- User không tồn tại.
- Email đã được sử dụng.
- Mật khẩu hiện tại không đúng.
- Trạng thái hiện tại không cho phép thao tác.

Đây là lỗi nghiệp vụ/application có thể chuyển thành HTTP response phù hợp, không phải exception kỹ thuật như mất kết nối database.

### 4.9 `Application/Authorization`

Đặt tên policy hoặc metadata authorization mà API cần tham chiếu:

```text
Application/Authorization/IdentityAuthorizationPolicies.cs
```

Implementation dùng ASP.NET Core authorization handler hiện đang nằm trong:

```text
Infrastructure/Authorization/
```

### 4.10 Những gì không được đặt trong Application

Không đặt các chi tiết sau trong `Application`:

- EF Core `DbContext` hoặc `EntityTypeConfiguration`.
- Implementation BCrypt/Argon2.
- Implementation tạo JWT.
- SQL hoặc Npgsql-specific code.
- Controller và HTTP binding attributes.
- Gọi trực tiếp API bên thứ ba nếu có thể bọc sau abstraction.

---

## 5. Infrastructure — implementation kỹ thuật

`Infrastructure` trả lời câu hỏi:

> Use case và domain sẽ được kết nối với database, framework và dịch vụ bên ngoài bằng cách nào?

Cấu trúc hiện tại:

```text
Infrastructure/
├── Authentication/
├── Authorization/
├── Persistence/
│   ├── IdentityDbContext.cs
│   └── Configurations/
├── Queries/
└── Repositories/
```

### 5.1 `Infrastructure/Persistence`

Chứa các thành phần persistence nền tảng của module:

```text
Infrastructure/Persistence/IdentityDbContext.cs
Infrastructure/Persistence/Configurations/UserConfiguration.cs
```

`DbContext` chịu trách nhiệm:

- Khai báo `DbSet`.
- Chọn schema của module.
- Áp dụng EF Core configurations.
- Implement Unit of Work nếu kiến trúc chọn cách này.
- Quản lý transaction thuộc module.

Không viết endpoint hoặc application use case trong `DbContext`.

### 5.2 `Infrastructure/Persistence/Configurations`

Mỗi entity thường có một file EF Core configuration:

```text
User.cs                    -> UserConfiguration.cs
Tenant.cs                  -> TenantConfiguration.cs
FarmMembership.cs          -> FarmMembershipConfiguration.cs
```

Configuration chứa:

- Table và schema mapping.
- Primary key.
- Column type và maximum length.
- Index và unique constraint.
- Relationship và foreign key.
- Enum conversion/mapping.

Những cấu hình này là chi tiết của EF Core nên không đặt trong `Domain`.

### 5.3 `Infrastructure/Repositories`

Chứa implementation của repository interface trong Domain:

```text
Domain/Users/IUserRepository.cs
                │
                └── Infrastructure/Repositories/UserRepository.cs
```

Repository implementation:

- Dùng `DbContext` để load hoặc lưu aggregate.
- Thường trả entity/aggregate.
- Thường dùng tracking cho write flow.
- Không chứa HTTP logic.

### 5.4 `Infrastructure/Queries`

Chứa implementation read-side:

```text
Application/Abstractions/IUserQueries.cs
                │
                └── Infrastructure/Queries/UserQueries.cs
```

Query implementation phù hợp để:

- Dùng `AsNoTracking()`.
- Filter, sort và pagination tại database.
- Projection thẳng sang response DTO.
- Tối ưu riêng cho màn hình/report.
- Chỉ lấy những cột cần thiết.

Không load toàn bộ entity về memory rồi mới phân trang.

### 5.5 `Infrastructure/Authentication`

Chứa implementation kỹ thuật liên quan đăng nhập/xác thực:

```text
Infrastructure/Authentication/JwtTokenGenerator.cs
Infrastructure/Authentication/PasswordService.cs
```

Ví dụ:

- Tạo và ký JWT.
- Hash mật khẩu bằng BCrypt/Argon2/PBKDF2.
- Verify password hash.
- Adapter tới identity provider bên ngoài nếu sau này có.

Interface tương ứng vẫn đặt tại `Application/Abstractions` để handler không phụ thuộc thư viện cụ thể.

### 5.6 `Infrastructure/Authorization`

Chứa implementation kiểm tra quyền dựa trên framework hoặc nguồn dữ liệu:

```text
Infrastructure/Authorization/TenantRoleAuthorizationHandler.cs
Infrastructure/Authorization/FarmRoleAuthorizationHandler.cs
Infrastructure/Authorization/TenantRoleRequirement.cs
```

Phân biệt:

- Authentication: xác định người gọi là ai.
- Authorization: xác định người đó có được làm hành động hay không.

### 5.7 Tích hợp bên ngoài

Khi module cần gọi một external service, có thể tạo thư mục theo vai trò kỹ thuật:

```text
Infrastructure/Email/
Infrastructure/Storage/
Infrastructure/ExternalApis/
Infrastructure/Messaging/
```

Ví dụ:

```text
Application/Abstractions/IEmailSender.cs
Infrastructure/Email/SmtpEmailSender.cs
```

Nếu integration đủ lớn và được nhiều module dùng, cân nhắc project riêng dưới `backend/src/Integrations` thay vì nhét vào một module.

### 5.8 Những gì không nên đặt trong Infrastructure

Không nên đặt tại đây:

- Luật cốt lõi của `User`, `Tenant` hoặc aggregate khác.
- Command/query message của use case.
- Handler điều phối use case.
- HTTP contract và Controller.

Infrastructure thực thi chi tiết kỹ thuật, không sở hữu nghiệp vụ của hệ thống.

---

## 6. API nằm ngoài module

Trong cấu trúc hiện tại, HTTP API là project riêng:

```text
AgriDrone.Api/
├── Contracts/
│   └── Users/
│       ├── GetUserRequest.cs
│       └── RegisterUserRequest.cs
└── Controllers/
    └── UserController.cs
```

### 6.1 API contract

Đặt HTTP input/output dành riêng cho transport tại:

```text
AgriDrone.Api/Contracts/<Resource>/
```

Contract xử lý hình dạng của:

- JSON body.
- Route parameter.
- Query string.
- Header nếu cần.

API contract có thể khác Application command/query. Controller chịu trách nhiệm mapping giữa hai loại.

### 6.2 Controller

Controller chịu trách nhiệm:

- Route và HTTP verb.
- Model binding.
- Authentication/authorization attribute.
- Chuyển API request thành Application command/query.
- Gửi message qua `ISender`.
- Chuyển `Result` thành HTTP response.

Controller không được:

- Inject `DbContext`.
- Gọi repository trực tiếp.
- Chứa business rule.
- Tự hash password.
- Khởi tạo handler bằng `new`.

Luồng đúng:

```text
HTTP Request
    -> Controller
    -> MediatR Command/Query
    -> Handler
    -> Domain/Abstraction
    -> Infrastructure implementation
```

---

## 7. SharedKernel, SharedInfrastructure và Database

Không đưa một class vào `BuildingBlocks` chỉ vì có khả năng một ngày nào đó class sẽ được dùng lại.

Chỉ chia sẻ khi khái niệm thực sự ổn định và có nhiều module cùng cần.

### 7.1 `AgriDrone.SharedKernel`

Chứa building block ít phụ thuộc kỹ thuật và có ý nghĩa chung:

- `Entity`.
- `AggregateRoot`.
- `Result` và `AppError`.
- Pagination model chung.
- Abstraction thật sự dùng xuyên module như `ICurrentUser` hoặc `IUnitOfWork`.

Không đặt `User`, `Farm`, `Mission` hoặc interface riêng của Identity vào SharedKernel.

### 7.2 `AgriDrone.SharedInfrastructure`

Chứa implementation kỹ thuật dùng chung giữa nhiều module/API:

- Global exception handling.
- Authentication plumbing dùng chung.
- Validation pipeline behavior.
- HTTP result mapping.
- Persistence helper dùng chung.

`PasswordService` hiện là trách nhiệm của Identity nên chưa cần đưa vào đây.

### 7.3 `AgriDrone.Database`

Chứa phần phối hợp database ở cấp toàn hệ thống:

- Migrations.
- Schema context/factory phục vụ migrations.
- Mapping hoặc configuration cần kết nối nhiều module.

Entity configuration riêng của module vẫn nên ở `Module/Infrastructure/Persistence/Configurations`.

---

## 8. Cách chọn giữa Repository và Queries

| Nhu cầu | Chọn | Lý do |
|---|---|---|
| Tạo user | `IUserRepository` | Thêm aggregate mới |
| Đổi mật khẩu | `IUserRepository` | Load và thay đổi `User` aggregate |
| Khóa user | `IUserRepository` | Gọi domain behavior |
| Lấy user để chỉnh sửa | `IUserRepository` | Cần tracked entity |
| Danh sách user phân trang | `IUserQueries` | Read DTO và projection |
| Search theo email/tên | `IUserQueries` | Read-specific filtering |
| Dashboard thống kê | Query abstraction | Không cần materialize aggregate |
| Export báo cáo | Query abstraction | Cần read model riêng |

Quy tắc ngắn:

```text
Có thay đổi aggregate hoặc gọi domain method?
├── Có  -> Repository + Unit of Work
└── Không, chỉ đọc dữ liệu
    └── Query abstraction + Infrastructure query
```

---

## 9. Read flow và write flow

### 9.1 Read flow

Ví dụ lấy danh sách user:

```text
GetUserRequest                       API contract
    -> UserController                HTTP adapter
    -> GetUsersQuery                 Application input message
    -> GetUsersQueryHandler          Use case
    -> IUserQueries                  Application abstraction
    -> UserQueries                   Infrastructure implementation
    -> IdentityDbContext             EF Core
    -> UserListItemResponse          Projection DTO
```

Đặc điểm:

- Không thay đổi dữ liệu.
- Dùng `AsNoTracking()`.
- Projection trực tiếp sang DTO.
- Filter/sort/pagination chạy tại database.

### 9.2 Write flow

Ví dụ đăng ký user:

```text
RegisterUserRequest                  API contract
    -> UserController                HTTP adapter
    -> RegisterUserCommand           Application input message
    -> RegisterUserCommandHandler    Use case
       -> IUserRepository            Load/add aggregate
       -> IPasswordService           Hash password
       -> User                       Domain behavior
       -> IIdentityUnitOfWork        Commit
    -> UserRepository                EF Core repository
    -> PasswordService               Hash implementation
    -> IdentityDbContext             Persistence/transaction
```

Đặc điểm:

- Làm việc với aggregate.
- Domain giữ invariant.
- Repository thường dùng EF tracking.
- Commit một lần ở cuối use case hoặc trong một transaction rõ ràng.

---

## 10. Ví dụ cụ thể: `PasswordService`

Cấu trúc chuẩn:

```text
AgriDrone.Modules.Identity/
├── Application/
│   └── Abstractions/
│       └── IPasswordService.cs
├── Infrastructure/
│   └── Authentication/
│       └── PasswordService.cs
└── DependencyInjection.cs
```

Interface mô tả điều Application cần:

```csharp
namespace AgriDrone.Modules.Identity.Application.Abstractions;

internal interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
```

Implementation chứa chi tiết thư viện:

```csharp
namespace AgriDrone.Modules.Identity.Infrastructure.Authentication;

internal sealed class PasswordService : IPasswordService
{
    public string Hash(string password)
    {
        // BCrypt, Argon2 hoặc PasswordHasher implementation.
    }

    public bool Verify(string password, string passwordHash)
    {
        // Library-specific implementation.
    }
}
```

Đăng ký trong `DependencyInjection.cs`:

```csharp
services.AddScoped<IPasswordService, PasswordService>();
```

Handler chỉ phụ thuộc abstraction:

```csharp
internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordService passwordService,
    IIdentityUnitOfWork unitOfWork)
{
    // Không new PasswordService() tại đây.
}
```

`PasswordHash` là trạng thái của `User`, nhưng thuật toán tạo/kiểm tra hash là chi tiết kỹ thuật. Vì vậy:

- `User.PasswordHash` thuộc Domain.
- `IPasswordService` thuộc Application abstraction.
- `PasswordService` thuộc Infrastructure/Authentication.

---

## 11. `public`, `internal` và `sealed`

### Dùng `public` khi assembly khác cần nhìn thấy

Ví dụ API project phải nhìn thấy:

- Application command/query mà Controller khởi tạo.
- Response type nằm trong public contract giữa API và module.
- `DependencyInjection` extension để `Program.cs` gọi.
- Authorization policy name nếu API dùng.

### Dùng `internal` cho implementation detail

Ví dụ:

- Handler được MediatR scan tự động.
- Validator được FluentValidation scan tự động.
- `IUserQueries` chỉ dùng bên trong module.
- `UserQueries`, `UserRepository`, `PasswordService`.
- `IdentityDbContext` nếu không cần expose ra module khác.

### Dùng `sealed` khi không thiết kế cho kế thừa

Phần lớn command/query record, handler, validator và infrastructure implementation có thể là `sealed`.

---

## 12. Bảng quyết định nhanh: file mới đặt ở đâu?

| File hoặc trách nhiệm mới | Vị trí đề xuất |
|---|---|
| Entity/aggregate | `Domain/<Concept>/` |
| Enum nghiệp vụ | `Domain/<Concept>/` |
| Value object | `Domain/<Concept>/` |
| Domain event | `Domain/<Concept>/Events/` |
| Repository interface cho aggregate | `Domain/<Concept>/I...Repository.cs` |
| Command thay đổi dữ liệu | `Application/Features/<UseCase>/` |
| Query message đọc dữ liệu | `Application/Features/<UseCase>/` |
| Handler | Cùng thư mục feature với command/query |
| FluentValidation validator | Cùng thư mục feature |
| Response DTO của use case | Cùng thư mục feature |
| Read query interface | `Application/Abstractions/` |
| Interface của external/technical service | `Application/Abstractions/` |
| Application error | `Application/Errors/` |
| Policy name | `Application/Authorization/` |
| `DbContext` | `Infrastructure/Persistence/` |
| EF entity configuration | `Infrastructure/Persistence/Configurations/` |
| Repository implementation | `Infrastructure/Repositories/` |
| Read query implementation | `Infrastructure/Queries/` |
| JWT/password implementation | `Infrastructure/Authentication/` |
| ASP.NET authorization handler | `Infrastructure/Authorization/` |
| Email/storage/API adapter riêng module | `Infrastructure/<TechnicalArea>/` |
| HTTP request contract | `AgriDrone.Api/Contracts/<Resource>/` |
| Controller/action | `AgriDrone.Api/Controllers/` |
| Component dùng chung ổn định giữa nhiều module | Cân nhắc `BuildingBlocks/` |
| Database migration | `AgriDrone.Database/Migrations/` theo cấu hình hiện tại |

---

## 13. Cây câu hỏi để tự quyết định

Khi tạo một file mới, hỏi lần lượt:

```text
1. Đây có phải HTTP concern không?
   ├── Có -> AgriDrone.Api/Contracts hoặc Controllers
   └── Không

2. Đây có phải khái niệm hoặc luật nghiệp vụ cốt lõi không?
   ├── Có -> Domain/<BusinessConcept>
   └── Không

3. Đây có phải một use case mà hệ thống cung cấp không?
   ├── Có -> Application/Features/<UseCase>
   └── Không

4. Đây có phải khả năng mà Application cần từ bên ngoài không?
   ├── Có, là interface -> Application/Abstractions
   └── Không

5. Đây có gắn với EF Core, database, JWT, hashing, file, email,
   framework hoặc external API không?
   ├── Có -> Infrastructure/<TechnicalArea>
   └── Không

6. Nó có thực sự dùng chung và ổn định ở nhiều module không?
   ├── Có -> Cân nhắc BuildingBlocks hoặc Integrations
   └── Không -> Giữ trong module sở hữu nghiệp vụ
```

Nếu vẫn chưa rõ, xác định **ai là người sở hữu nhu cầu**:

- Domain sở hữu luật nghiệp vụ.
- Application sở hữu use case và abstraction mà use case cần.
- Infrastructure sở hữu implementation kỹ thuật.
- API sở hữu giao thức HTTP.

---

## 14. Checklist tạo read feature mới

Ví dụ `GetUserDetail` hoặc `SearchUsers`:

1. Tạo API request contract nếu có route/query/body input.
2. Tạo `<Feature>Query.cs` trong `Application/Features/<Feature>`.
3. Tạo response DTO trong cùng feature.
4. Tạo validator nếu request có rule cần kiểm tra.
5. Tạo query handler.
6. Kiểm tra read abstraction hiện có đã đáp ứng chưa.
7. Nếu chưa, thêm interface/method trong `Application/Abstractions`.
8. Implement query bằng EF Core trong `Infrastructure/Queries`.
9. Đăng ký interface/implementation trong `DependencyInjection.cs` nếu là service mới.
10. Tạo Controller action và gửi query qua `ISender`.
11. Dùng `AsNoTracking`, projection và pagination tại database.
12. Viết unit/integration test phù hợp.

---

## 15. Checklist tạo write feature mới

Ví dụ `RegisterUser`, `ChangePassword` hoặc `LockUser`:

1. Tạo API request contract.
2. Tạo `<Feature>Command.cs` trong `Application/Features/<Feature>`.
3. Tạo response nếu use case cần trả dữ liệu.
4. Tạo validator.
5. Tạo command handler.
6. Dùng repository để load/thêm aggregate.
7. Đưa invariant và state transition vào domain method.
8. Nếu cần technical service, định nghĩa interface trong `Application/Abstractions`.
9. Implement technical service trong `Infrastructure`.
10. Thêm hoặc cập nhật EF configuration nếu domain model thay đổi.
11. Commit qua Unit of Work một lần ở cuối use case.
12. Đăng ký dependency trong `DependencyInjection.cs`.
13. Tạo Controller action và gửi command qua `ISender`.
14. Tạo migration nếu database schema thay đổi.
15. Viết unit/integration test phù hợp.

---

## 16. Anti-pattern cần tránh

### Controller gọi thẳng `DbContext`

Sai dependency direction và làm HTTP layer gắn chặt với EF Core.

### Handler phụ thuộc implementation

Không inject `UserQueries`, `UserRepository` hoặc `PasswordService` trực tiếp nếu đã có abstraction. Inject `IUserQueries`, `IUserRepository`, `IPasswordService`.

### Đặt tất cả interface vào một thư mục chung

Interface thuộc về layer sở hữu abstraction:

- Aggregate repository thường thuộc Domain.
- Technical/output port của use case thuộc Application.
- Interface thuần kỹ thuật chỉ phục vụ Infrastructure có thể ở gần implementation, nhưng không expose nó vào Application.

### Trả entity trực tiếp ra API

Có thể làm lộ trường nhạy cảm và khiến API contract phụ thuộc domain model.

### Dùng repository cho mọi truy vấn

Repository phục vụ aggregate/write behavior. Danh sách, dashboard, search và report nên dùng read query abstraction.

### Đặt code vào Shared quá sớm

Giữ code trong module sở hữu nó cho đến khi có ít nhất nhiều consumer thực tế và abstraction đã ổn định.

### Để nghiệp vụ trong Infrastructure

EF configuration và database query thuộc Infrastructure; quyết định có được khóa user, chuyển trạng thái mission hay đổi mật khẩu hay không thuộc Domain/Application.

---

## 17. Quy tắc cuối cùng để ghi nhớ

```text
Domain
    = Hệ thống là gì và luật nghiệp vụ là gì?

Application
    = Hệ thống cho phép thực hiện use case nào?

Infrastructure
    = Database/framework/external service thực hiện việc đó bằng cách nào?

API
    = Client giao tiếp với hệ thống qua HTTP như thế nào?
```

Một câu tóm tắt:

> Controller đi vào Application qua command/query; Application điều phối Domain và đi ra ngoài qua abstraction; Infrastructure implement các abstraction đó; Domain không biết database, HTTP hay framework tồn tại.
