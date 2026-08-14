# Hướng dẫn viết feature theo kiến trúc AgriDrone

Tài liệu này giải thích cách một request đi từ HTTP API xuống PostgreSQL trong dự án AgriDrone, đồng thời trả lời các câu hỏi:

- Vì sao phải tạo từng file?
- `GetUsersQuery` khác gì `IUserQueries`?
- Khi nào dùng `IRepository`?
- Khi nào dùng read query (`IUserQueries`)?
- Vì sao repository interface thường nằm trong Domain?
- Vì sao query interface nằm trong Application/Abstractions?
- Controller được phép phụ thuộc vào thành phần nào?
- Validation, expected error và exception được xử lý ở đâu?

Ví dụ xuyên suốt là chức năng:

```http
GET /api/users?pageNumber=1&pageSize=20
```

---

## 1. Bức tranh tổng thể

Luồng đọc danh sách user:

```text
HTTP request
    ↓
GetUsersRequest                 AgriDrone.Api/Contracts
    ↓
UsersController                 AgriDrone.Api/Controllers
    ↓ ISender.Send(...)
GetUsersQuery                   Identity/Application/Features
    ↓
ValidationPipelineBehavior      SharedInfrastructure/Validation
    ↓
GetUsersQueryHandler            Identity/Application/Features
    ↓ gọi abstraction
IUserQueries                    Identity/Application/Abstractions
    ↓ DI chọn implementation
UserQueries                     Identity/Infrastructure/Queries
    ↓
IdentityDbContext               Identity/Infrastructure/Persistence
    ↓
PostgreSQL
    ↓
PagedResult<UserListItemResponse>
    ↓
ResultMapper
    ↓
HTTP response
```

Quy tắc dependency quan trọng nhất:

```text
API → Application → Domain
          ↑           ↑
          └─ Infrastructure
```

Mũi tên biểu diễn “được phép phụ thuộc vào”. Infrastructure có thể phụ thuộc cả Application và Domain để implement các abstraction, còn hai layer bên trong không phụ thuộc ngược ra Infrastructure.

Hiểu theo lời:

- API được phép gọi Application.
- Infrastructure được phép implement interface do Application hoặc Domain sở hữu.
- Application không được phụ thuộc vào API.
- Domain không được phụ thuộc EF Core, Controller, Swagger hoặc HTTP.
- Controller không được gọi thẳng `DbContext`.
- Handler không được tạo thẳng `UserQueries` bằng `new`.

---

## 2. Trách nhiệm của từng layer

### 2.1 Domain

Domain chứa mô hình và luật nghiệp vụ cốt lõi:

```text
Domain/Users/User.cs
Domain/Users/UserStatus.cs
Domain/Users/IUserRepository.cs
```

Ví dụ luật nghiệp vụ:

- Email của user phải là duy nhất.
- User bị khóa không được đăng nhập.
- Chỉ một số trạng thái được phép chuyển đổi qua lại.
- Thay đổi user phải thông qua method của aggregate.

Domain không cần biết dữ liệu được lưu bằng PostgreSQL, MongoDB hay API bên ngoài.

### 2.2 Application

Application mô tả use case của hệ thống:

```text
Application/Features/GetUsers
Application/Features/RegisterUser
Application/Abstractions
```

Ví dụ use case:

- Lấy danh sách user có phân trang.
- Đăng ký user.
- Khóa user.
- Đổi mật khẩu.

Application điều phối nghiệp vụ nhưng không chứa code truy cập PostgreSQL cụ thể.

### 2.3 Infrastructure

Infrastructure chứa chi tiết kỹ thuật:

```text
Infrastructure/Persistence/IdentityDbContext.cs
Infrastructure/Repositories/UserRepository.cs
Infrastructure/Queries/UserQueries.cs
Infrastructure/Authentication/JwtTokenGenerator.cs
```

Đây là nơi được phép dùng:

- EF Core.
- Npgsql.
- PostgreSQL.
- File storage.
- JWT implementation.
- API của bên thứ ba.

### 2.4 API

API chịu trách nhiệm giao tiếp HTTP:

```text
AgriDrone.Api/Controllers
AgriDrone.Api/Contracts
Program.cs
```

API xử lý:

- Route.
- Query string.
- Request body.
- HTTP status code.
- Authentication/authorization metadata.
- Chuyển HTTP contract thành Application request.

API không nên chứa LINQ truy vấn database hoặc luật nghiệp vụ.

---

## 3. Ba khái niệm dễ bị nhầm lẫn

Trong code có thể xuất hiện ba loại “query” khác nhau.

### 3.1 `GetUsersQuery`: message của MediatR

```csharp
public sealed record GetUsersQuery(
    int PageNumber,
    int PageSize)
    : IRequest<Result<PagedResult<UserListItemResponse>>>;
```

Đây chỉ là object chứa input của use case.

Nó:

- Không truy cập database.
- Không có method `Execute()`.
- Không phải repository.
- Không phải implementation.
- Được Controller gửi qua `ISender`.

Controller không gọi trực tiếp handler. Controller chỉ làm:

```csharp
await sender.Send(query, cancellationToken);
```

`ISender` chính là input port chung khi dự án sử dụng MediatR.

### 3.2 `GetUsersQueryHandler`: use case implementation

Handler nhận message và điều phối use case:

```csharp
internal sealed class GetUsersQueryHandler(...)
    : IRequestHandler<GetUsersQuery, Result<...>>
```

Handler quyết định:

- Tạo `PagedRequest`.
- Gọi read abstraction nào.
- Trả `Result.Success` hay expected failure.

Handler không nên viết SQL và không nên chứa cấu hình HTTP.

### 3.3 `IUserQueries`: output port để đọc dữ liệu

```csharp
internal interface IUserQueries
{
    Task<PagedResult<UserListItemResponse>> GetPageAsync(...);
}
```

Đây là abstraction mô tả dữ liệu Application cần đọc.

Infrastructure implement nó bằng EF Core:

```csharp
internal sealed class UserQueries : IUserQueries
```

Tên số nhiều `Queries` có nghĩa đây là một nhóm read operations liên quan đến user, không phải MediatR message.

---

## 4. Khi nào dùng Repository?

Repository được dùng khi use case làm việc với aggregate/domain entity và hành vi nghiệp vụ.

Ví dụ phù hợp:

```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    void Add(User user);
}
```

Các use case nên dùng repository:

- Đăng ký user.
- Đổi email.
- Khóa hoặc mở khóa user.
- Đổi mật khẩu.
- Xóa mềm user.
- Lấy aggregate để gọi domain method.

Ví dụ command handler:

```csharp
var user = await userRepository.GetByIdAsync(
    request.UserId,
    cancellationToken);

if (user is null)
{
    return Result.Failure(UserError.NotFound(request.UserId));
}

user.Lock();

await unitOfWork.SaveChangesAsync(cancellationToken);
```

Điểm quan trọng là handler lấy `User` aggregate để thực hiện hành vi `Lock()`.

### Không nên dùng repository cho trường hợp nào?

Không nên ép repository phục vụ mọi màn hình đọc dữ liệu, ví dụ:

- Danh sách user có pagination.
- Search user theo nhiều filter.
- Dashboard thống kê.
- Join nhiều bảng thành một DTO.
- Chỉ cần 5 trong số 20 cột của entity.

Method sau không phù hợp cho pagination:

```csharp
Task<List<User>> GetAllAsync(...);
```

Vì nó thường dẫn đến:

```csharp
var users = await repository.GetAllAsync(cancellationToken);

var page = users
    .Skip(skip)
    .Take(pageSize)
    .ToList();
```

Đây là phân trang trong RAM. Toàn bộ bảng đã được lấy khỏi PostgreSQL trước khi `Skip/Take` chạy.

Ngoài ra entity `User` có `PasswordHash`; truy vấn danh sách không cần tải cột này.

---

## 5. Vì sao `IUserRepository` nằm trong Domain?

Repository được nhìn như một collection của aggregate:

```text
IUserRepository
    GetById
    GetByEmail
    Add
```

Domain/Application nói rằng “tôi cần lưu và lấy User aggregate”, nhưng không quyết định cách lưu.

Domain sở hữu abstraction:

```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(...);
    void Add(User user);
}
```

Infrastructure cung cấp implementation:

```csharp
internal sealed class UserRepository(
    IdentityDbContext dbContext) : IUserRepository
{
    // EF Core implementation
}
```

Dependency direction:

```text
Domain
  ↑ interface được sở hữu ở đây
Infrastructure
  └── implement interface
```

Nhờ Dependency Inversion, Domain không phụ thuộc Infrastructure. Infrastructure mới là phía phụ thuộc Domain.

Nếu sau này đổi EF Core sang công nghệ khác:

- `IUserRepository` không đổi.
- Handler không đổi.
- Domain không đổi.
- Chỉ thay `UserRepository` implementation.

### Repository có bắt buộc luôn nằm trong Domain không?

Không phải mọi interface có chữ Repository đều bắt buộc nằm trong Domain.

Quy tắc chính xác hơn:

- Nếu abstraction diễn tả cách lấy/lưu aggregate để thực hiện domain behavior, đặt gần Domain.
- Nếu abstraction chỉ phục vụ một application use case hoặc read model, đặt trong Application.
- Interface thuộc layer nào có nhu cầu, không thuộc layer đang implement nó.

---

## 6. Khi nào dùng `IUserQueries`?

`IUserQueries` dùng cho read side:

- Dữ liệu chỉ đọc.
- Trả DTO thay vì aggregate.
- Có pagination, filtering, sorting.
- Cần projection để chỉ select cột cần thiết.
- Không cần gọi domain method.
- Không gọi `SaveChangesAsync()`.

Ví dụ:

```csharp
internal interface IUserQueries
{
    Task<PagedResult<UserListItemResponse>> GetPageAsync(
        PagedRequest pageRequest,
        CancellationToken cancellationToken = default);
}
```

Infrastructure có thể tối ưu query riêng cho màn hình:

```csharp
return dbContext.Users
    .AsNoTracking()
    .Where(user => user.DeletedAt == null)
    .OrderByDescending(user => user.CreatedAt)
    .ThenByDescending(user => user.Id)
    .Select(user => new UserListItemResponse(...))
    .ToPagedResultAsync(pageRequest, cancellationToken);
```

---

## 7. Vì sao `IUserQueries` nằm trong Application/Abstractions?

`IUserQueries` mô tả dữ liệu mà use case cần, không mô tả luật của `User` aggregate.

Ví dụ `UserListItemResponse` tồn tại vì màn hình/API cần danh sách:

```csharp
public sealed record UserListItemResponse(
    Guid Id,
    string Email,
    string FullName,
    string? Phone,
    UserStatus Status,
    DateTimeOffset CreatedAt);
```

Khái niệm “dòng dữ liệu cho trang danh sách user” không phải domain concept. Domain không cần biết:

- Page number.
- Page size.
- DTO của API.
- Sort theo cột nào để hiển thị.
- Màn hình cần những field nào.

Vì Application là layer có nhu cầu đọc dữ liệu, Application sở hữu interface:

```text
Application/Abstractions/IUserQueries.cs
```

Infrastructure chỉ implement nhu cầu đó:

```text
Infrastructure/Queries/UserQueries.cs
```

Dependency direction:

```text
Application
  ↑ sở hữu IUserQueries
Infrastructure
  └── implement IUserQueries bằng EF Core
```

Nếu đặt `IUserQueries` trong Infrastructure, Handler sẽ phải phụ thuộc Infrastructure. Điều đó làm dependency direction bị đảo sai:

```text
Application → Infrastructure   // không mong muốn
```

---

## 8. Bảng quyết định: Repository hay Queries?

| Nhu cầu | Dùng gì? | Lý do |
|---|---|---|
| Đăng ký user | `IUserRepository` | Tạo và lưu aggregate |
| Kiểm tra email tồn tại khi đăng ký | `IUserRepository` | Bảo vệ invariant của aggregate/use case |
| Khóa user | `IUserRepository` | Lấy aggregate và gọi domain behavior |
| Đổi mật khẩu | `IUserRepository` | Thay đổi aggregate |
| Get user để chỉnh sửa | `IUserRepository` | Cần tracked aggregate |
| Danh sách user pagination | `IUserQueries` | Read DTO, projection, pagination |
| Search user theo tên/email | `IUserQueries` | Read-specific filtering |
| Dashboard số user theo trạng thái | `IUserQueries` hoặc reporting query | Aggregate không cần được materialize |
| Export danh sách user | `IUserQueries` | Read model riêng |
| Kiểm tra user tồn tại từ module khác | Public application contract/module API | Không cho module khác dùng trực tiếp repository |

Quy tắc nhớ nhanh:

```text
Cần thay đổi aggregate hoặc gọi domain method → Repository
Chỉ cần dữ liệu để hiển thị/tra cứu/thống kê → Queries
```

---

## 9. Cấu trúc file chuẩn cho GetUsers

```text
backend/src/AgriDrone.Api
├── Contracts
│   └── Users
│       └── GetUsersRequest.cs
└── Controllers
    └── UsersController.cs

backend/src/Modules/AgriDrone.Modules.Identity
├── Application
│   ├── Abstractions
│   │   └── IUserQueries.cs
│   └── Features
│       └── GetUsers
│           ├── GetUsersQuery.cs
│           ├── GetUsersQueryHandler.cs
│           ├── GetUsersQueryValidator.cs
│           └── UserListItemResponse.cs
├── Domain
│   └── Users
│       ├── User.cs
│       ├── UserStatus.cs
│       └── IUserRepository.cs
├── Infrastructure
│   ├── Persistence
│   │   └── IdentityDbContext.cs
│   ├── Queries
│   │   └── UserQueries.cs
│   └── Repositories
│       └── UserRepository.cs
└── DependencyInjection.cs
```

Không phải feature nào cũng tạo lại tất cả các file trên:

- `IdentityDbContext` dùng chung cho toàn bộ Identity module.
- `IUserRepository` dùng chung cho các command làm việc với User aggregate.
- `UserRepository` dùng chung cho các command đó.
- `IUserQueries` có thể chứa nhiều read operations liên quan user.
- `UserQueries` implement các read operations đó.
- Mỗi feature vẫn có Query/Command, Handler, Validator và Response riêng khi trách nhiệm khác nhau.

---

## 10. Hoàn thiện GetUsers theo từng file

### Bước 1 — API contract

File:

```text
AgriDrone.Api/Contracts/Users/GetUsersRequest.cs
```

```csharp
namespace AgriDrone.Api.Contracts.Users;

public sealed record GetUsersRequest
{
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
```

Vì sao tạo file này?

- Nó đại diện query string HTTP.
- Sau này có thể đổi tên parameter HTTP mà không làm Application phụ thuộc API.
- Swagger nhìn thấy request contract rõ ràng.
- Controller có trách nhiệm map API contract sang Application query.

### Bước 2 — Response DTO

File:

```text
Identity/Application/Features/GetUsers/UserListItemResponse.cs
```

```csharp
using AgriDrone.Modules.Identity.Domain.Users;

namespace AgriDrone.Modules.Identity.Application.Features.GetUsers;

public sealed record UserListItemResponse(
    Guid Id,
    string Email,
    string FullName,
    string? Phone,
    UserStatus Status,
    DateTimeOffset CreatedAt);
```

Vì sao không trả `User` entity?

- `User` có `PasswordHash`.
- API không nên phụ thuộc toàn bộ hình dạng aggregate.
- DTO cho phép chỉ lấy những cột cần thiết.
- Thay đổi internal domain model không nhất thiết phá API contract.

DTO là `public` vì `AgriDrone.Api` là assembly khác và phải dùng kiểu này trong response metadata.

### Bước 3 — MediatR query message

File:

```text
Identity/Application/Features/GetUsers/GetUsersQuery.cs
```

```csharp
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.GetUsers;

public sealed record GetUsersQuery(
    int PageNumber,
    int PageSize)
    : IRequest<Result<PagedResult<UserListItemResponse>>>;
```

Vì sao là `public`?

- Controller nằm ở project API phải tạo query này.

Vì sao không cần `IGetUsersQuery`?

- Query chỉ là immutable data message.
- Không có nhiều behavior implementation cần thay thế.
- Controller đã gọi qua abstraction `ISender`.

### Bước 4 — Validator

File:

```text
Identity/Application/Features/GetUsers/GetUsersQueryValidator.cs
```

```csharp
using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.GetUsers;

internal sealed class GetUsersQueryValidator
    : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
```

Vì sao validator tách khỏi handler?

- Handler chỉ điều phối use case hợp lệ.
- Validation được pipeline chạy trước handler.
- Quy tắc input dễ unit test.
- Tránh nhiều `if` lặp lại trong handler.

Validator được để `internal` vì API không cần gọi nó trực tiếp. Module đã dùng `includeInternalTypes: true` khi scan FluentValidation.

### Bước 5 — Read abstraction

File:

```text
Identity/Application/Abstractions/IUserQueries.cs
```

```csharp
using AgriDrone.Modules.Identity.Application.Features.GetUsers;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Identity.Application.Abstractions;

internal interface IUserQueries
{
    Task<PagedResult<UserListItemResponse>> GetPageAsync(
        PagedRequest pageRequest,
        CancellationToken cancellationToken = default);
}
```

Vì sao interface này tồn tại?

- Handler không phụ thuộc EF Core.
- Handler không phụ thuộc `IdentityDbContext`.
- Application mô tả dữ liệu cần đọc.
- Infrastructure có thể thay implementation.
- Có thể mock interface khi unit test handler.

Vì sao là `internal`?

- Chỉ các class trong Identity module cần dùng.
- Module khác không được query thẳng database của Identity.

### Bước 6 — Handler

File:

```text
Identity/Application/Features/GetUsers/GetUsersQueryHandler.cs
```

```csharp
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.GetUsers;

internal sealed class GetUsersQueryHandler(
    IUserQueries userQueries)
    : IRequestHandler<
        GetUsersQuery,
        Result<PagedResult<UserListItemResponse>>>
{
    public async Task<Result<PagedResult<UserListItemResponse>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var pageRequest = new PagedRequest(
            request.PageNumber,
            request.PageSize);

        var users = await userQueries.GetPageAsync(
            pageRequest,
            cancellationToken);

        return Result.Success(users);
    }
}
```

Vì sao handler là `internal`?

- Controller không được gọi trực tiếp handler.
- MediatR tìm handler bằng assembly scanning.
- Handler là implementation detail của module.

Vì sao GetUsers không trả NotFound khi danh sách rỗng?

- Collection rỗng là kết quả hợp lệ.
- Kết quả đúng là HTTP 200 với `items: []`.
- `NotFound` dành cho một resource cụ thể, ví dụ `GET /api/users/{id}`.

### Bước 7 — EF Core query implementation

File:

```text
Identity/Infrastructure/Queries/UserQueries.cs
```

```csharp
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Features.GetUsers;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Queries;

internal sealed class UserQueries(
    IdentityDbContext dbContext) : IUserQueries
{
    public Task<PagedResult<UserListItemResponse>> GetPageAsync(
        PagedRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Users
            .AsNoTracking()
            .Where(user => user.DeletedAt == null)
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Select(user => new UserListItemResponse(
                user.Id,
                user.Email,
                user.FullName,
                user.Phone,
                user.Status,
                user.CreatedAt))
            .ToPagedResultAsync(
                pageRequest,
                cancellationToken);
    }
}
```

Vì sao dùng `AsNoTracking()`?

- Đây là read-only query.
- Không có entity nào được cập nhật.
- Giảm tracking overhead của EF Core.

Vì sao `Select` trước khi execute?

- PostgreSQL chỉ trả các cột cần cho response.
- Không tải `PasswordHash`.
- Mapping xảy ra trong SQL query thay vì tải entity rồi map trong RAM.

Vì sao phải `OrderBy` trước `Skip/Take`?

- Pagination cần thứ tự ổn định.
- Không có order thì record có thể trùng hoặc mất giữa các trang.
- `ThenByDescending(Id)` giải quyết trường hợp nhiều record có cùng `CreatedAt`.

### Bước 8 — Dependency Injection

Trong `Identity/DependencyInjection.cs`:

```csharp
services.AddScoped<IUserQueries, UserQueries>();
```

Và thêm namespace cần thiết:

```csharp
using AgriDrone.Modules.Identity.Infrastructure.Queries;
```

Không cần đăng ký thủ công:

- `GetUsersQueryHandler`: MediatR scan assembly.
- `GetUsersQueryValidator`: FluentValidation scan assembly.

Phải đăng ký `IUserQueries` vì DI không tự đoán interface nào dùng implementation nào.

### Bước 9 — Controller

File:

```text
AgriDrone.Api/Controllers/UsersController.cs
```

```csharp
using AgriDrone.Api.Contracts.Users;
using AgriDrone.Modules.Identity.Application.Features.GetUsers;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(
    ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> GetUsers(
        [FromQuery] GetUsersRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetUsersQuery(
            request.PageNumber,
            request.PageSize);

        var result = await sender.Send(
            query,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            users => Results.Ok(users));
    }
}
```

Controller được phép biết:

- `GetUsersRequest`.
- `GetUsersQuery`.
- `UserListItemResponse`.
- `ISender`.
- `ResultMapper`.

Controller không được biết:

- `GetUsersQueryHandler`.
- `UserQueries`.
- `IdentityDbContext`.
- `DbSet<User>`.

### Bước 10 — Program

API phải đăng ký controller:

```csharp
builder.Services.AddControllers();
```

Và map controller:

```csharp
app.MapControllers();
```

Các pipeline dùng chung chỉ đăng ký một lần:

```csharp
.AddValidationPipeline()
.AddGlobalExceptionHandling();
```

---

## 11. Validation và error handling chạy như thế nào?

### 11.1 Sai kiểu dữ liệu HTTP

Request:

```http
GET /api/users?pageNumber=abc&pageSize=20
```

ASP.NET Core model binding không chuyển `abc` thành `int` được.

Luồng:

```text
HTTP model binding
    ↓ lỗi
[ApiController]
    ↓
HTTP 400
```

Controller action và MediatR chưa chạy.

### 11.2 Input đúng kiểu nhưng sai rule

Request:

```http
GET /api/users?pageNumber=0&pageSize=500
```

Luồng:

```text
Controller
    ↓
ISender.Send
    ↓
ValidationPipelineBehavior
    ↓ throw ValidationException
GlobalExceptionHandler
    ↓
HTTP 400 ValidationProblem
```

Không viết `try/catch` trong Controller hoặc Handler cho validation.

### 11.3 Expected application error

Expected error là lỗi nghiệp vụ đã dự kiến:

- User cụ thể không tồn tại.
- Email bị trùng.
- Không được phép thực hiện thao tác.
- Conflict trạng thái.

Handler trả `Result.Failure(...)`:

```csharp
return Result.Failure<UserResponse>(
    UserError.NotFound(request.UserId));
```

`ResultMapper` chuyển `ErrorType` thành HTTP status:

| ErrorType | HTTP status |
|---|---:|
| Failure | 400 |
| NotFound | 404 |
| Conflict | 409 |
| Unauthorized | 401 |
| Forbidden | 403 |

### 11.4 Unexpected exception

Ví dụ:

- PostgreSQL không hoạt động.
- Connection string sai.
- Timeout.
- Bug `NullReferenceException`.

Luồng:

```text
EF Core/Npgsql/handler throws exception
    ↓
GlobalExceptionHandler
    ↓ log exception thật
HTTP 500 với message an toàn
```

Không trả stack trace hoặc connection string cho client.

### 11.5 Authentication/authorization

Không có JWT hợp lệ:

```text
Authentication middleware → HTTP 401
```

Có JWT nhưng thiếu quyền:

```text
Authorization middleware → HTTP 403
```

Hai trường hợp này xảy ra trước Controller nếu endpoint dùng `[Authorize]` hoặc policy.

---

## 12. SQL thực tế của pagination

`ToPagedResultAsync` chạy hai query.

Query đếm:

```sql
SELECT COUNT(*)
FROM identity.users AS u
WHERE u.deleted_at IS NULL;
```

Query lấy trang:

```sql
SELECT
    u.id,
    u.email,
    u.full_name,
    u.phone,
    u.status,
    u.created_at
FROM identity.users AS u
WHERE u.deleted_at IS NULL
ORDER BY u.created_at DESC, u.id DESC
LIMIT @pageSize
OFFSET @skip;
```

Ví dụ:

```text
PageNumber = 3
PageSize   = 20
Skip       = (3 - 1) × 20 = 40
```

PostgreSQL chỉ trả 20 dòng sau khi bỏ qua 40 dòng. Không tải toàn bộ bảng vào memory.

---

## 13. Read flow và write flow khác nhau

### Read flow

Ví dụ GetUsers:

```text
Query → Handler → IUserQueries → UserQueries → AsNoTracking → DTO
```

Đặc điểm:

- Không thay đổi domain state.
- Không cần Unit of Work.
- Không gọi `SaveChangesAsync()`.
- Ưu tiên projection trực tiếp sang DTO.

### Write flow

Ví dụ LockUser:

```text
Command → Handler → IUserRepository → User aggregate
                                  ↓ domain method
                              user.Lock()
                                  ↓
                             UnitOfWork.SaveChangesAsync()
```

Đặc điểm:

- Lấy aggregate.
- Gọi domain behavior.
- EF Core tracking được sử dụng.
- Commit một lần ở cuối use case.

---

## 14. Có nên tạo interface cho mọi class không?

Không.

Không cần interface cho dữ liệu/message:

```csharp
GetUsersRequest
GetUsersQuery
UserListItemResponse
PagedRequest
```

Nên cân nhắc interface cho behavior hoặc external dependency:

```csharp
IUserRepository
IUserQueries
IPasswordService
IJwtTokenGenerator
ICurrentUser
IEmailSender
IFileStorage
```

Hỏi câu này để quyết định:

> Tôi đang mô tả dữ liệu hay đang mô tả một hành vi có implementation kỹ thuật?

- Nếu chỉ mô tả dữ liệu: record/class là đủ.
- Nếu mô tả hành vi và muốn đảo dependency: dùng interface.

---

## 15. `public`, `internal` và `sealed`

### Dùng `public` khi assembly khác phải nhìn thấy

Ví dụ:

```csharp
public sealed record GetUsersQuery(...);
public sealed record UserListItemResponse(...);
public static class DependencyInjection;
```

API là assembly khác nên cần nhìn thấy query/response và extension đăng ký module.

### Dùng `internal` cho implementation detail

Ví dụ:

```csharp
internal sealed class GetUsersQueryHandler;
internal sealed class GetUsersQueryValidator;
internal interface IUserQueries;
internal sealed class UserQueries;
internal sealed class IdentityDbContext;
internal sealed class UserRepository;
```

Module khác không nên gọi trực tiếp những thành phần này.

### Dùng `sealed` khi class không được thiết kế để kế thừa

Handler, validator và infrastructure service thường là implementation cuối cùng. `sealed` thể hiện chủ đích đó và đáp ứng analyzer `CA1852` của dự án.

---

## 16. Các anti-pattern cần tránh

### Controller gọi DbContext

Không nên:

```csharp
public sealed class UsersController(IdentityDbContext dbContext)
```

Vì API bị gắn trực tiếp với persistence implementation.

### Controller gọi Handler trực tiếp

Không nên:

```csharp
var handler = new GetUsersQueryHandler(...);
await handler.Handle(...);
```

Dùng `ISender` để pipeline validation và behaviors được thực thi.

### Handler phụ thuộc implementation

Không nên:

```csharp
internal sealed class GetUsersQueryHandler(UserQueries userQueries)
```

Nên:

```csharp
internal sealed class GetUsersQueryHandler(IUserQueries userQueries)
```

### GetAll rồi pagination trong memory

Không nên:

```csharp
var users = await repository.GetAllAsync();
return users.Skip(skip).Take(take);
```

Pagination phải chạy trên `IQueryable` trước `ToListAsync()`.

### Trả entity trực tiếp

Không nên:

```csharp
return Results.Ok(users);
```

nếu `users` là `List<User>`. Entity có thể chứa dữ liệu không được phép public.

### Expose `IQueryable` qua layer boundary

Không nên:

```csharp
IQueryable<User> Query();
```

Application sẽ bắt đầu phụ thuộc chi tiết EF query và persistence behavior.

### Catch mọi exception trong Controller

Không nên:

```csharp
try
{
    ...
}
catch (Exception)
{
    return Results.StatusCode(500);
}
```

Dự án đã có `GlobalExceptionHandler`.

---

## 17. Checklist tạo một read feature mới

Ví dụ feature `GetFarms`:

1. Tạo API request contract nếu có query string/body.
2. Tạo response DTO trong module Application feature.
3. Tạo MediatR Query.
4. Tạo Validator nếu request có rule.
5. Tạo Handler.
6. Xác định read abstraction đã có chưa, ví dụ `IFarmQueries`.
7. Nếu chưa có, tạo nó trong `Application/Abstractions`.
8. Implement bằng EF Core trong `Infrastructure/Queries`.
9. Đăng ký interface/implementation trong module DI.
10. Thêm Controller action gửi query qua `ISender`.
11. Dùng `AsNoTracking` cho read-only query.
12. Projection sang DTO trước khi execute.
13. Có order ổn định trước pagination.
14. Không trả 404 cho collection rỗng.
15. Test 200, 400, 401, 403 và dữ liệu rỗng.

---

## 18. Checklist tạo một write feature mới

Ví dụ feature `LockUser`:

1. Tạo API request contract nếu cần body/route input.
2. Tạo MediatR Command.
3. Tạo Command Validator.
4. Tạo Handler.
5. Dùng `IUserRepository` để lấy aggregate.
6. Nếu aggregate không tồn tại, trả expected `Result.Failure`.
7. Gọi domain method, ví dụ `user.Lock()`.
8. Gọi Unit of Work/`SaveChangesAsync()` một lần.
9. Map `Result` ra HTTP bằng `ResultMapper`.
10. Test domain behavior và failure cases.

---

## 19. Trạng thái hiện tại của GetUsers trong repo

Tại thời điểm tài liệu được tạo, các file sau đã tồn tại nhưng phần lớn còn là skeleton:

```text
Application/Features/GetUsers/GetUsersQuery.cs
Application/Features/GetUsers/GetUsersQueryHandler.cs
Application/Features/GetUsers/GetUsersQueryValidator.cs
Application/Features/GetUsers/UserListItemResponse.cs
Application/Abstractions/IUserQueries.cs
```

Các việc cần hoàn thiện theo đúng thứ tự:

1. Hoàn thiện `GetUsersQuery`.
2. Hoàn thiện `GetUsersQueryValidator`.
3. Khai báo method trong `IUserQueries`.
4. Tạo `Infrastructure/Queries/UserQueries.cs`.
5. Hoàn thiện `GetUsersQueryHandler`.
6. Đăng ký `IUserQueries` trong Identity DI.
7. Tạo API contract.
8. Tạo Controller/action.

`IUserRepository` hiện có `GetAllAsync()` trả `List<User>`. Không nên dùng method đó cho pagination. Khi chuyển GetUsers sang `IUserQueries`, có thể loại bỏ method này nếu không còn use case nào thực sự cần tải toàn bộ User aggregate.

`UserRepository.cs` hiện còn import namespace internal của Npgsql:

```csharp
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal;
```

Namespace này không cần cho repository và không nên được application code phụ thuộc.

---

## 20. Quy tắc cuối cùng để ghi nhớ

```text
API contract
    = hình dạng dữ liệu HTTP

MediatR Query/Command
    = input của một application use case

Handler
    = điều phối use case

Repository
    = lấy/lưu aggregate cho domain behavior

Queries
    = đọc DTO, filter, sort, pagination, report

Domain
    = entity, aggregate và business rules

Infrastructure
    = EF Core, PostgreSQL và implementation kỹ thuật
```

Quyết định nhanh:

```text
Có thay đổi aggregate không?
    Có  → Command + Repository + Unit of Work
    Không
      ↓
Có chỉ đọc/hiển thị/tìm kiếm/phân trang không?
    Có  → Query + IUserQueries + Infrastructure query
```

Controller luôn đi vào Application qua `ISender`. Application đi ra ngoài qua abstraction do chính Application hoặc Domain sở hữu. Infrastructure implement các abstraction đó. Đây là phần cốt lõi của Dependency Inversion trong cấu trúc hiện tại của AgriDrone.
