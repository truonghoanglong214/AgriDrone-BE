# Plan 1 — Chốt hóa đơn, phát hành và thực thi hoàn tiền V3

**Chủ trì:** LongTH. **Điểm tiếp theo:** Nối refresh token trên frontend ở RT-06 và kiểm cookie trên domain đích. A2.2 là bước tiếp theo của V3-06 và có thể làm song song. V3-05A–F đã triển khai và V3-05F build sạch với .NET 10. Nghiệm thu database/runtime và xác minh cấu hình COMBO trên môi trường đích còn pending ở V3-10.

[Master plan BE1](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md) · [Business Rules V3](../../Codex-Plan/Business-Rules-v3.md) · [Plan 2 — Owner, Claims và Financial reporting](v3-07-to-v3-10-owner-claims-reporting-plan.md)

## Identity — Refresh token và khôi phục phiên

**Hiện tại.** `AuthController` có `POST /api/auth/login` và `POST /api/auth/select-tenant`. Phiên trả về chỉ có access JWT, thời điểm hết hạn và tenant nếu có. Người dùng nhiều tenant nhận `tenantSelection` trước khi có `session`. Cấu hình mặc định của access token là 15 phút. Frontend cần API cấp lại access token để giữ đăng nhập sau reload hoặc khi JWT hết hạn.

**Đầu ra.** Người dùng đăng nhập một lần, tải lại trang vẫn vào đúng tenant và tiếp tục dùng API khi access JWT hết hạn. Sau khi triển khai, người dùng có phiên cũ sẽ nhận cookie ở lần đăng nhập kế tiếp. Hạng mục áp dụng cho frontend web và auth dùng chung của dự án. Bắt đầu từ RT-01 và RT-02.

### RT-01 — Chốt hợp đồng HTTP và thời hạn phiên

- [x] Giữ JSON của `LoginUserResponse` với `session` hoặc `tenantSelection`. Dùng lại cấu trúc phiên đó khi refresh để FE khôi phục cả thông tin người dùng và tenant. Chuẩn bị response mẫu và cập nhật OpenAPI tại RT-05.
- [x] Dùng cookie `agridrone_refresh` cho phiên web. Giá trị đề xuất ban đầu là 7 ngày không hoạt động và tối đa 30 ngày kể từ đăng nhập. Đưa hai hạn vào cấu hình `RefreshSession`, kiểm số dương và hạn không hoạt động ≤ hạn tuyệt đối. Giữ cấu hình access JWT hiện tại. Khi rotate, đặt hết hạn token và cookie bằng mốc sớm hơn giữa `now + idle lifetime` và hạn tuyệt đối của phiên. Coi `now >= expiresAt` là hết hạn.

| Route và tình huống | HTTP | Kết quả và cookie |
|---|---|---|
| `POST /api/auth/login` cho tài khoản hệ thống hoặc một tenant, theo contract web RT-05 | `200` | `session` và cookie mới. Phiên hệ thống có `session.tenant = null`. |
| `POST /api/auth/login` cho tài khoản nhiều tenant | `200` | Trả `tenantSelection` và `session = null`. Chờ chọn tenant để cấp cookie. |
| `POST /api/auth/select-tenant` hợp lệ, theo contract web RT-05 | `200` | `session` theo membership đã chọn và cookie mới. |
| `POST /api/auth/refresh` với cookie hợp lệ | `200` | `session.accessToken`, `session.expiresAt`, `session.tenant` và cookie đã rotate. |
| Refresh thiếu cookie, hết hạn, mất quyền hoặc đã thu hồi | `401` | ProblemDetails `Authentication.InvalidRefreshSession`. |
| Refresh đồng thời thua tại RT-04 | `409` | ProblemDetails `Authentication.RefreshConflict`. Giữ nguyên cookie. |
| `POST /api/auth/logout`, kể cả khi đã logout | `204` | Thu hồi phiên tìm được và gửi header xóa cookie. |
| Request dùng contract cookie sai origin hoặc thiếu header bắt buộc | `403` | Trả lỗi quyền trước khi ghi phiên hoặc đổi cookie. |

### RT-02 — Thêm model, EF mapping và migration Identity

- [x] Thêm model dưới `AgriDrone.Modules.Identity/Domain/`, dự kiến `RefreshSession` và `RefreshSessionToken`. Session giữ `Id`, `UserId`, `TenantId`/`TenantMembershipId` tùy scope, phiên bản thông tin xác thực của user, ngày tạo, hạn tuyệt đối và lý do/thời điểm thu hồi. Token giữ `SessionId`, thế hệ, hash, ngày cấp/hết hạn/đã dùng và liên kết tiền nhiệm. Sinh 32 byte ngẫu nhiên mật mã và encode base64url cho cookie. Database chỉ nhận SHA-256 của token. Giữ chuỗi đã dùng ít nhất đến hạn tuyệt đối của phiên để phát hiện sử dụng lại. [RFC 9700, mục 4.14](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14)
- [x] Trong `Infrastructure/Persistence/Configurations/`, thêm ràng buộc hash duy nhất, thế hệ duy nhất trong phiên và tối đa một token chưa dùng trong phiên. Ràng buộc tiền nhiệm cùng session. Dùng FK cho user và membership, bảo đảm membership thuộc đúng user/tenant. Kiểm cặp tenant/membership cùng có hoặc cùng rỗng. Thêm index để thu hồi theo user và tra hạn phiên.
- [x] Thêm trường `AuthenticationVersion` vào `User.cs` và `UserConfiguration.cs`, cấp mặc định cho user hiện có. Tăng phiên bản khi đổi mật khẩu hoặc vô hiệu hóa user. Dùng chung giá trị trong phiên refresh và token chọn tenant để vô hiệu hóa chứng thực cũ sau các thay đổi này.
- [x] Đăng ký session/token trong `IdentityDbContext.cs`, thêm repository dưới `Infrastructure/Repositories/`, đưa options và dịch vụ vào `AgriDrone.Modules.Identity/DependencyInjection.cs`. Dùng `AgriDroneSchemaDbContext` để tạo một migration tiếp nối và cập nhật Designer/ModelSnapshot. Kiểm thứ tự migration hiện tại trước khi đặt tên. Migration tạo bảng rỗng cho phiên mới và giữ user, membership, dữ liệu nghiệp vụ hiện có.

**Đạt khi:** cả hai DbContext dùng chung EF mapping, có lookup theo hash và build solution qua trước khi nối route.

### RT-03 — Cấp phiên từ login và chọn tenant

- [x] Sửa `LoginUserCommandHandler.cs`, `SelectTenantCommandHandler.cs` và dùng lại `AuthenticationSessionFactory.cs`/`IJwtTokenGenerator.cs`. Branch tài khoản hệ thống hoặc một tenant cấp phiên ngay. Branch nhiều tenant trả token chọn tenant rồi tạo phiên khi `select-tenant` xác thực thành công. Controller nhận kết quả nội bộ chứa credential để ghi cookie và chủ động serialize DTO JSON hiện có.
- [x] Gắn `AuthenticationVersion` vào token chọn tenant qua `ITenantSelectionTokenService` và `TenantSelectionTokenService`. Giữ kiểm chữ ký, mục đích, audience và expiry hiện có. Khi chọn tenant, đối chiếu phiên bản với user dưới khóa trước khi cấp phiên. Token chọn tenant cũ từ trước lúc nâng cấp hoặc bị thay phiên bản yêu cầu bắt đầu lại login. Cập nhật các test/fake đang gọi port, gồm `TenantSelectionTokenServiceTests` và `SystemManagerLoginTests`.
- [x] Khi tạo phiên, đọc lại user active, `DeletedAt`, phiên bản xác thực và scope dưới khóa. Với login, đối chiếu phiên bản đã đọc lúc kiểm mật khẩu. Với tenant, dùng đúng membership active và tenant active. Commit session cùng token thế hệ đầu trước khi controller ghi cookie. Khi có yêu cầu chọn tenant, FE giữ màn hình chọn tenant tới lúc nhận phiên chính thức.

**Đạt khi:** ba nhánh login dùng đúng scope, response cũ vẫn đọc được và chỉ kết quả có phiên mới nhận cookie.

### RT-04 — Rotate token, kiểm quyền và thu hồi

- [x] Thêm `RefreshSessionCommand`, `LogoutSessionCommand` và MediatR handlers trong Identity, trả `Result`/`AppError`. Dịch vụ kiểm định dạng của token từ cookie rồi băm để repo tìm session và đọc thế hệ quan sát được trước khóa. Dùng transaction với thứ tự khóa nhất quán **tenant nếu có → user → membership/quyền liên quan → session → token**. Đọc lại các hàng dưới khóa, dùng thời gian server sau khi lấy khóa và kiểm phiên bản xác thực, trạng thái, hạn của phiên và thế hệ token. Dùng truy vấn cùng transaction hoặc reload để tránh dữ liệu EF đã track trước khóa. [PostgreSQL 17, Read Committed](https://www.postgresql.org/docs/17/transaction-iso.html#XACT-READ-COMMITTED)
- [x] Đọc role hiện tại khi cấp JWT. Phiên tenant giữ `UserId`, `TenantId` và `TenantMembershipId` ban đầu, lấy tenant role còn hiệu lực. Phiên hệ thống yêu cầu role hệ thống hợp lệ hiện hành. Request refresh lấy scope từ phiên lưu server. Áp chính sách quyền hiện có của API nghiệp vụ với access JWT đã phát.
- [x] Nếu token còn hiện hành, tạo token kế tiếp, đánh dấu token cũ đã dùng và lưu cùng một transaction. Với unique index của token chưa dùng, lưu việc đánh dấu đã dùng trước khi chèn token mới trong cùng transaction. Chỉ trả JWT/cookie sau commit. Khi request đã quan sát token còn dùng được nhưng request khác đã rotate trước khi lấy khóa, trả `409` nếu phiên vẫn hợp lệ. Request mới nhìn thấy token đã dùng là sử dụng lại, ghi thu hồi phiên và commit trước khi trả `401`. Lỗi lưu giữa hai lần ghi rollback cả việc dùng token cũ lẫn token mới.
- [x] Logout khóa và thu hồi theo session tìm được, kể cả cookie vừa bị rotate khi logout đang chờ. Cookie thiếu/không nhận diện vẫn trả `204` sau khi qua guard HTTP. Trong `UpdateUserPasswordCommandHandler.cs` và `ResetPasswordCommandHandler.cs`, khóa user trước khi đổi mật khẩu và thu hồi các phiên cùng transaction. Giữ việc tiêu thụ reset token trong transaction reset. Thực hiện kiểm tra tương tự khi vô hiệu hóa user qua writer Identity. Các phiên khác của user vẫn hoạt động khi logout một phiên. JWT đã phát hết hạn theo cơ chế hiện có.

**Đạt khi:** chỉ có một successor, logout/đổi mật khẩu thắng thì refresh sau đó bị từ chối, và từ chối do replay vẫn lưu được việc thu hồi.

#### Checklist sửa sau review RT-03 và RT-04

- [x] Trong `RefreshSessionRepository.cs`, lấy bản ghi mới dưới khóa khi cấp phiên. Đối chiếu `AuthenticationVersion` đã quan sát lúc kiểm mật khẩu với giá trị vừa đọc.
- [x] Trong `ForgotPasswordCommandHandler.cs` và `ResetPasswordCommandHandler.cs`, lấy khóa user trước khi cập nhật reset token. Giữ gửi email sau commit và lấy thời gian kiểm expiry reset token sau khóa.
- [x] Truyền hạn JWT chọn tenant từ `TenantSelectionTokenService.cs` sang `SelectTenantCommandHandler.cs`. Kiểm quyền, phiên bản xác thực và hạn token dưới khóa trước khi cấp JWT ở cả hai chế độ, có hoặc không có cookie.
- [x] Build solution đạt 0 cảnh báo, 0 lỗi. Đã bổ sung ca tranh chấp vào V3-T46. [Ca nghiệm thu](../testing/v3-business-acceptance.md#ca-tranh-chấp-bổ-sung-cho-v3-t46)
- [ ] Chạy các ca tranh chấp Identity trên PostgreSQL ở V3-10.

### RT-05 — Nối AuthController và bảo vệ cookie

- [x] Thêm routes `refresh` và `logout` vào `AuthController.cs`. Dùng cookie ngay cả khi access JWT đã hết hạn, map lỗi qua ProblemDetails hiện có. Phản hồi phiên dùng `Cache-Control: no-store`. Cookie host-only dùng `HttpOnly`, `Secure` trên HTTPS, `SameSite=Lax`, `Path=/api/auth` và expiry từ server. Logout xóa cùng tên/path. Loại giá trị JWT và refresh token khỏi log, response JSON chỉ chứa access JWT.
- [x] Cho FE chọn dùng phiên cookie bằng header `X-AgriDrone-Session: 1` khi login/chọn tenant, giữ response access JWT hiện có cho client chưa gửi header. Refresh/logout yêu cầu header này và `Origin` thuộc allowlist auth. Kiểm Origin trước khi đổi cookie hoặc state. Cấu hình auth origins chính xác trong `Program.cs`/settings cho từng môi trường, kể cả Development. Với auth routes, CORS credentials dùng origin cụ thể và cho phép header trên. Kiểm cả HTTPS/forwarded headers qua proxy. [Fetch Standard](https://fetch.spec.whatwg.org/#cors-protocol-and-credentials)
- [ ] Xác nhận origin FE và API thực tế từ môi trường deploy. Nếu FE và API thuộc hai site khác nhau, dùng domain cùng site hoặc proxy auth cùng origin với frontend. Xác minh cả login, refresh và logout qua đường triển khai. [WebKit Tracking Prevention](https://webkit.org/tracking-prevention/)

### RT-06 — Nối frontend và bàn giao

- [ ] Bàn giao header, cookie, ví dụ response và mã lỗi qua OpenAPI và [handoff frontend](../operations/v3-fe-mobile-handoff.md#9-khôi-phục-phiên-đăng-nhập). FE gửi `credentials: 'include'` ở login, chọn tenant, refresh và logout. Giữ access JWT trong bộ nhớ. Khi mở lại ứng dụng, bootstrap phiên qua refresh trước khi tải màn hình cần đăng nhập.
- [ ] Gom các request cùng chờ vào một lần refresh mỗi tab. Điều phối giữa các tab cùng origin để ở một thời điểm một tab gửi refresh. Có thể dùng Web Locks và BroadcastChannel sau khi xác minh trình duyệt đích. Tab chờ đến lượt phải kiểm lại kết quả refresh trước khi gửi request mới. Đồng bộ logout giữa các tab và bỏ kết quả đến muộn của lần refresh đã chạy trước logout hoặc đổi phiên. [W3C Web Locks](https://www.w3.org/TR/web-locks/) · [HTML Standard, BroadcastChannel](https://html.spec.whatwg.org/multipage/web-messaging.html#broadcasting-to-other-browsing-contexts)
- [ ] Sau refresh thành công, thử lại tối đa một lần với request an toàn. Mutation có idempotency giữ operation ID. Upload dùng luồng retry/resume hiện có. Với `409 RefreshConflict`, chờ lượt refresh thắng và thử lại có giới hạn. Với `401 InvalidRefreshSession`, xóa state đăng nhập rồi chuyển về login. Với lỗi mạng/`5xx`, giữ state chờ và cho thử lại có giới hạn. `403` hiển thị lỗi quyền.

### RT-07 — Xác minh build và chuẩn bị nghiệm thu V3-10

- [ ] Sau RT-02 và sau khi nối xong backend, chạy `dotnet build backend/AgriDrone.sln --no-restore --nologo` và ghi kết quả thật. Rà migration tiếp nối, JSON login/select-tenant cũ, đường thu hồi và schema constraints. Chuẩn bị fixture hai tenant, user hệ thống, hai phiên cùng user và thời điểm hết hạn điều khiển được.
- [ ] Tại V3-10, chạy **T44** cho cả ba nhánh cấp phiên, reload và JWT hết hạn. **T45** dùng hai transaction và điểm đồng bộ để ép cùng thế hệ, thêm lỗi giữa đánh dấu cũ và chèn mới. **T46** cho logout lặp, logout/đổi mật khẩu đua với refresh, biên hết hạn, scope bị thu hồi và token chọn tenant có phiên bản cũ. **T47–T48** kiểm origin/domain đích, cookie/header, nhiều tab, lỗi mạng và retry mutation. Ghi status, nội dung cookie đã che giá trị và trạng thái DB trong bằng chứng. [Ma trận V3-T44–T48](../testing/v3-business-acceptance.md#acceptance-scenarios)

**Điều kiện bàn giao.** FE có thể tích hợp sau khi backend build sạch và công bố hợp đồng RT-05. Nghiệm thu hoàn tất khi demo reload và JWT hết hạn vẫn giữ đúng scope, logout có hiệu lực, rotation đồng thời chỉ ghi một token kế tiếp và có kết quả V3-T44–T48.

## A1 — V3-05E/F: invoice và bàn giao API

**Đã có source và build sạch tại checkpoint review.** Dùng invoice, revision tokens, API review/correction, funding cọc và late-receipt reconciliation đã bàn giao. Runtime và migration acceptance nằm ở A4. [Checkpoint V3-05](v3-05-combo-review-and-invoice-implementation-plan.md) · [Contract bàn giao](../contracts/survey-business-v3.md#63-v3-05f-http-api--v3-06--plan-2-handoff-contracts)

## A2 — V3-06 — Settlement, release, notice và ledger facts

**Đầu ra.** First/Follow-up MAP và COMBO phát hành đúng revision được duyệt khi số dư đã được đối soát. Checkout, callback, release receipt và bút toán truy về cùng Order cùng revision. Thực hiện A2.1 → A2.6. Đối chiếu [ADR 0020](../adr/0020-v3-capture-execution-and-funding-boundaries.md#4-transaction-boundary-và-total-lock-order), [master plan V3-06](../../Codex-Plan/BE1-Core-Business-Implementation-Plan.md#9-v3-06--atomic-settlementrelease) và [release contract](../contracts/survey-business-v3.md#7-release-receipt-and-surveyresultreleasedv1).

### A2.1 — Chốt phần nối checkout, callback và release

- [x] Lưu intent, fingerprint và kết quả checkout theo operation ID cùng giao dịch với payment. Tạo reference SePay ở bước ghi intent. Khi provider trả kết quả chưa xác định, giữ `Pending` để đối soát reference đã có. Retry dùng lại dữ liệu đã ghi và ẩn URL/token khi invoice bị thay thế.
- [x] Ghi yêu cầu đánh giá release cùng commit chốt/sửa invoice và cùng commit callback cọc có receipt hoặc reversal đã liên kết khi Order đang `AwaitingSettlement`. Tiền đến từ checkout bị vô hiệu tiếp tục vào đối soát.
- [x] Coordinator dùng giao dịch riêng, xác minh tenant/farm và quyền theo nguồn trigger trước khi đọc receipt. Giữ các kiểu kết quả và handshake với Plan 2 theo contract V3-05F. [Contract bàn giao](../contracts/survey-business-v3.md#64-v3-06-a21-transaction-boundaries-retry--handshake-ports)
- [x] Build `dotnet build backend/AgriDrone.sln --no-restore --nologo` đạt 0 cảnh báo, 0 lỗi.
- [ ] Xác minh runtime tại V3-10 cho replay cùng key, xung đột fingerprint, timeout khi provider đã nhận yêu cầu, correction trong lúc tạo checkout và trigger sai scope/quyền.

### A2.2 — Settlement và cap hoàn sau reversal

- [ ] Mở rộng `SurveyInvoiceFunding.cs` theo dữ kiện balance checkout được xác minh và exact invoice hiện hành. Tính lại due và excess từ receipts/reversal/refunds hợp lệ, chống credit hai lần. Giữ đúng provenance của cọc đã phân bổ, rồi nối API hiển thị due hiện hành sau callback hoặc reversal.
- [ ] Sửa cap của đường hủy có sẵn trong `SurveyCaptureExecutionCoordinator.cs`. Đưa reversal đã xác minh vào số tiền khả dụng dưới Order lock. Reversal thiếu linkage vào reconciliation hold. Giữ `CancellationReturn` và `ServiceFailure` đã tạo từ quyết định hủy, cùng luồng tiền cọc đến muộn.
- [ ] Khi đủ điều kiện release, giữ chỗ `Overpayment` đúng receipt một lần trên `SurveyRefundObligation`. Trừ completed refunds và active reservations khi tính phần còn có thể hoàn. Khi zero-pole, phân biệt đơn có kết quả hợp lệ để giao với đơn dừng và cần hoàn tiền. Dùng thứ tự khóa ADR 0020 cho checkout/funding, evidence và proof của Order Baseline nguồn.

### A2.3 — Checkout số dư, callback và đối soát

- [ ] Thêm route V3 `FinalBalance` trong `V3TenantOwnerSurveyOrdersController.cs` với request gồm token Order/invoice và idempotency key theo contract V3. Trong handler/adapter xác minh Owner, trạng thái `AwaitingSettlement`, exact current invoice và due dương trên server. Gọi `SurveyPayment.BindInvoiceRevision` khi lưu intent. Bảo vệ invariant một checkout số dư còn hiệu lực bằng khóa Order và ràng buộc persistence phù hợp.
- [ ] Nối SePay vào checkout ba bước. Chỉ `Created` được gọi tạo session bằng `BalanceCheckoutReference` đã lưu. Với `Replay`, trả outcome đã lưu hoặc đối soát theo payment/reference nếu outcome chưa xác định. Ánh xạ `Payment.ProviderOutcomeUncertain` về `ProviderOutcomeUncertain` khi lưu kết quả. Callback cần nhận được tiền đã xác minh cho checkout `Pending` nếu provider gửi trước khi giao dịch thứ hai hoàn tất.
- [ ] Trong callback và Admin reconciliation đã xác minh, ghi payment event, immutable funding receipt/adjustment và yêu cầu đánh giá release đồng bộ với commit ghi tiền. Dedup theo provider transaction ngoài dedup delivery. Phân biệt retry callback với hai khoản tiền thực khác nhau về cùng checkout. Receipt của invoice cũ, sai currency hoặc thiếu linkage đi vào đối soát theo rule, tránh tự động credit invoice hiện hành. Giữ contract Legacy và V3 OrderDeposit tại các entry point dùng chung.

### A2.4 — Phát hành nguyên tử, thông báo và hạn khiếu nại

- [ ] Chốt thiết kế domain/persistence cho release receipt bất biến, publication và visibility, durable in-app notice, claim window và original service-value fact. Rà `Notification.cs` cùng cấu hình Notifications để chọn cách ghi trong cùng physical transaction do Database điều phối. Xác định business unique keys cho release exact revision, notice/window và original sale theo Order. Đưa EF configurations, migration, Designer, ModelSnapshot và DI vào cùng thay đổi.
- [ ] Coordinator recheck assignment/context của approval, current approval/count/invoice/source fingerprints, service capability, baseline proof, trạng thái cancel/withdrawal, finalized Total, evidence thanh toán và `Due = 0` dưới khóa. Ghi publication, visibility, notice, claim window, original service value, completion/history, audit, receipt và Outbox trong cùng giao dịch. Dùng một timestamp server cho `PublishedAt = UnlockedAt = NoticeCreatedAt`. First thiết lập Baseline khi release. Follow-up giữ Baseline đã pin và công bố revision của riêng đơn đó.
- [ ] Đọc `ClaimPolicyId`, version, `ClaimWindowDays` và `ClaimTimeZoneId` từ `SurveyOrderCommercialSnapshot`. Tính hạn bằng nửa đêm địa phương ngày D + N + 1, rồi đổi sang UTC. D là ngày của durable notice, N là số ngày trong snapshot. Ví dụ N = 7 và notice ngày 10/11 theo giờ VN thì hạn exclusive là 00:00 ngày 18/11 theo giờ VN. Replay giữ notice và window đã ghi.
- [ ] Nối các yêu cầu đánh giá từ A2.1 và A2.3 vào ghi release thật. Mọi trigger dùng cùng coordinator, hỗ trợ retry bền vững và recheck phiên bản trong giao dịch. Consumer email/push chỉ gửi bổ sung. Review + đủ tiền tự phát hành cho đúng revision.

### A2.5 — Release proof, dữ kiện báo cáo và bàn giao

- [ ] Thêm read adapter đọc bằng chứng release đã commit theo exact order/tenant/farm/publication và lineage. Release proof mang trạng thái withdrawal cùng notice/window IDs, source/MAP/COMBO và financial references theo Plan 2. Bàn giao fixture released, pending và withdrawn. Fixture withdrawn chuẩn bị cho consumer trước khi writer withdrawal V3-08 có sẵn.
- [ ] Khai báo `SurveyResultReleasedV1` trong `AgriDrone.IntegrationContracts` theo contract đã chốt. Ghi Outbox trong release transaction và dedup consumer theo publication/business ID. Payload mang exact refs và timestamps. Giữ raw media URLs, thông tin ngân hàng và pending evidence ở nguồn có kiểm quyền.
- [ ] Rà migration `20261121090000_AddFinancialReportingHistory.cs` và `FinancialReportCalculator.cs`. Trigger hiện có ghi `original_service_publication` khi map/result được publish. Chốt một nguồn ghi nhận cho V3 từ release transaction và giữ hành vi Legacy bằng migration tiếp nối nếu phải sửa trigger. Rà allowlist để read model lấy được khóa invoice/payment/receipt, cash reversal, allocation, liability, refund reservation và effective/recorded time. Một sale/Order và báo cáo as-of là tiêu chí bàn giao. Query dashboard và Excel thuộc Plan 2.

### A2.6 — Xác minh build và chuẩn bị nghiệm thu V3-10

- [ ] Rà diff và migration chain, gồm data cũ, unique keys, rollback và EF snapshot. Cập nhật `docs/contracts/survey-business-v3.md` với payload mẫu checkout, release receipt và lỗi nghiệp vụ. Chạy `dotnet build backend/AgriDrone.sln --no-restore --nologo`, ghi kết quả thực tế. Giữ `PublicIntakeEnabled = false` và `SurveyPayments:Enabled = false` đến nghiệm thu.
- [ ] Dùng [acceptance matrix](../testing/v3-business-acceptance.md) V3-T05, T07 và T09–T13 làm nền. Bổ sung First/Follow-up MAP/COMBO, approve trước hoặc tiền trước, due = 0 từ cọc, excess 40.000 trên total 60.000, reversal/late receipt, correction đua với checkout/callback, assignment revoke, claim deadline đúng biên, replay và lỗi giữa từng lần ghi. Tập trung integration suite, race và fault injection tại V3-10.

**Điều kiện bàn giao A2.** Có contract và fixture truy được từ checkout/receipt đến đúng publication. Release, notice, claim window và original service-value fact được ghi theo cùng giao dịch và cùng business key tương ứng. Plan 2 nhận release-proof adapter và dữ kiện ledger có lineage. Build sạch là điều kiện bàn giao source, còn bằng chứng runtime ghi tại V3-10.

## A3 — V3-08: thực thi refund và phát hành correction

- [ ] Nhận claim/proposal đã được chấp nhận theo exact version từ Plan 2. Recheck proposal, scope, cap toàn Order và availability trong transaction trước khi reserve. Giữ idempotency theo operation và bank reference khi retry.
- [ ] Nối nghĩa vụ hoàn V3 đã có (hủy/service failure) và excess được giữ chỗ ở A2 với workflow chuyển khoản. Bổ sung reservation cho accepted complaint, proof/actor/bank reference, completion/reconciliation và ledger facts. Rà tái dùng upload/chứng từ từ `QualityRefundCase`. Pending/failed giữ nghĩa vụ; completion cần bằng chứng đã xác minh.
- [ ] LongTH sở hữu mutation phát hành bản correction và withdrawal. Plan 2 ghi yêu cầu/đề xuất correction và đọc lineage. Substantive correction tạo revision/window mới, cosmetic theo rule không reset window. Giữ original invoice/revenue và append withdrawal/correction/ledger facts.
- [ ] Bàn giao final refund facts, phân loại hoàn service value/cash, thời điểm effective và correction/withdrawal proofs cho report reconciliation của Plan 2.

## A4 — V3-10: nghiệm thu phần ghi và cutover

- [ ] Chủ trì migration/preflight và các gate PostgreSQL còn mở của V3-02/03/04/05, COMBO policy/coverage, DB/locks/races/rollback của settlement, release, refund, bank proof và correction. Cùng Plan 2 nối demo First → Follow-up → complaint/refund/correction → báo cáo tháng/Excel.
- [ ] Ghi bằng chứng và quyết định cutover theo [acceptance matrix](../testing/v3-business-acceptance.md). Giữ flags fail-closed tới nghiệm thu. Trong phase code dùng build chuẩn theo quy định repo. Tập trung integration suite ở V3-10.

## Nhịp bàn giao

| Khi nào | LongTH giao | Plan 2 trả lại |
|---|---|---|
| Ngay khi bắt đầu | Contract intent cho release proof và ledger read, ví dụ dữ liệu | Danh sách đường đọc, bộ lọc báo cáo và yêu cầu trường |
| Khi API refresh sẵn sàng | Contract Identity, cookie và mã lỗi | Kết quả nối khôi phục phiên và retry trên frontend |
| Sau A2 | Adapter release thật, deadline, sale/payment/liability facts | Owner integration và đối soát fixture tháng |
| Sau A3 | Refund completed/reserved facts, correction và withdrawal lineage | Claims-to-refund trace và dashboard/Excel reconciliation |
| V3-10 | Migration chain và kết quả nghiệm thu phần ghi | Kết quả nghiệm thu quyền đọc, claims và reporting |
