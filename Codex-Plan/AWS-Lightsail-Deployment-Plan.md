# AgriDrone — Kế hoạch chuẩn triển khai demo lên AWS Lightsail

> **Ngày rà soát:** 2026-11-18
> **Trạng thái:** PROPOSED / PLAN ONLY / NOT DEPLOYED
> **Phạm vi:** backend API và hạ tầng demo; repository hiện chưa có frontend
> **Tài liệu thực thi:** ../docs/superpowers/plans/2026-11-18-aws-lightsail-deployment-preparation.md

## 1. Mục tiêu và nguyên tắc

Mục tiêu là tạo một môi trường demo có thể lặp lại, có HTTPS, backup và rollback rõ ràng, không biến máy Lightsail thành môi trường phát triển thứ hai.

Các nguyên tắc bắt buộc:

1. Chỉ deploy image bất biến theo Git SHA/digest và một release bundle tối thiểu. Không clone toàn bộ repository lên server.
2. API không tự chạy migration khi khởi động. Migration chạy bằng một job một lần, sau backup và trước khi đổi API.
3. Chỉ Caddy được mở cổng 80/443. PostgreSQL, RabbitMQ, Redis, MinIO Console, FastAPI và NodeODM chỉ nằm trong mạng Docker nội bộ.
4. Không commit secret, dump, khóa SSH, certificate hoặc file môi trường thật.
5. Giữ SurveyPayments__Enabled=false, LegacyFeatures__EnableDeprecatedEndpoints=false và trạng thái PublicIntakeEnabled=false đến khi có change-control tại V3-10.
6. Không chạy dotnet test trong V3-01 đến V3-09. Lệnh kiểm tra C# duy nhất được phép là:

       dotnet build backend/AgriDrone.sln --no-restore --nologo

7. Không xóa migration, test project hoặc compose test để “deploy nhẹ”. Chúng không được đưa vào artifact deploy ngay từ đầu.

## 2. Hiện trạng repository đã xác minh

| Hạng mục | Hiện trạng 2026-11-18 | Hành động |
|---|---|---|
| V3-04 | Đã đóng G0–G18; build 0 Warning/0 Error; tests deferred | Không mở lại gate cũ |
| Migration V3-04 | Chuỗi canonical gồm 20261116131222, 20261118040000 và 20261118042702 | Giữ nguyên, không squash/xóa |
| EF tool | backend/.config/dotnet-tools.json đã pin dotnet-ef 10.0.10 | Chỉ kiểm tra đồng bộ với package EF trước build bundle |
| API Dockerfile | Chưa có | Tạo multi-stage image, non-root |
| Docker ignore | Chưa có ở root/backend | Tạo allowlist context hoặc ignore chặt |
| API migration startup | appsettings.json đang để ApplyMigrationsOnStartup=true | Đổi default an toàn thành false |
| Proxy/CORS | Chưa có forwarded headers; production CORS còn localhost | Sửa trước deploy |
| Local Compose | Chỉ dành local, đang publish loopback ports; MinIO/NodeODM dùng latest | Không dùng trực tiếp trên server |
| AI image | Dependency còn range mở, Dockerfile chạy root | Pin lock, CPU-only, non-root |
| Frontend | Không có trong repository | Không hứa các luồng redirect/payment hoàn chỉnh |
| Production assets | Chưa có production compose, Caddy, deploy/backup scripts, workflow | Tạo trong phase implementation |

## 3. Kiến trúc mục tiêu

### 3.1 Hai profile triển khai

Để tránh NodeODM chiếm hết RAM làm API chết, production compose có hai profile:

| Profile | Thành phần | Dùng khi nào |
|---|---|---|
| core, mặc định | Caddy, API, PostgreSQL/PostGIS, RabbitMQ, Redis, MinIO | Demo nghiệp vụ/API bằng dữ liệu đã chuẩn bị; triển khai và rollback nhanh |
| processing, bật có chủ đích | FastAPI và NodeODM, concurrency 1 | Demo xử lý ảnh thật sau khi capacity rehearsal đạt |

Nếu API readiness phụ thuộc bắt buộc vào FastAPI/NodeODM, implementation phải tách dependency hoặc đổi health policy trước khi cho phép profile core. Không được giả định core healthy khi code thực tế không cho phép.

Metashape service trong repository thực chất là FastAPI điều phối imagery/AI và NodeODM; đây không phải phần mềm Agisoft Metashape có license. Tài liệu và tên container phải tránh gây hiểu nhầm về license.

### 3.2 Luồng truy cập

    Internet
       |
       +-- TCP 80/443 --> Caddy
                              +--> api:8080
                              +--> minio:9000 (S3 API, bucket private)

    Internal Docker network
       api --> postgres / rabbitmq / redis / minio / fastapi
       fastapi --> nodeodm / minio

    GitHub Actions --> GHCR --> Lightsail deploy script
    Backup job --> external object storage

### 3.3 Public/private exposure

| Dịch vụ | Public | Ghi chú |
|---|---:|---|
| Caddy | Có | Chỉ 80/443 |
| ASP.NET API | Qua Caddy | api.domain |
| MinIO S3 API | Qua Caddy khi cần presigned URL | media.domain; bucket vẫn private |
| SSH | Có giới hạn | Chỉ IP quản trị hoặc browser SSH |
| PostgreSQL/RabbitMQ/Redis | Không | Không có ports trong production compose |
| MinIO Console | Không | Chỉ SSH tunnel khi thật sự cần |
| FastAPI/NodeODM | Không | Docker internal network |

## 4. Quyết định về máy và chi phí

Baseline đã chọn là Lightsail Linux Xlarge 16 GB RAM, 4 vCPU, 320 GB SSD, public IPv4 tại Singapore. Tài liệu AWS hiện niêm yết bundle này ở mức 84 USD/tháng; snapshot, object storage, domain và email tính riêng. Giá và availability phải được xác nhận lại trên console ngay trước lúc tạo máy, không hardcode con số vào automation.

Máy 16 GB chưa phải bằng chứng đủ cho 400 ảnh/1 GB. Profile processing chỉ được bật sau một capacity rehearsal có số liệu peak RAM, swap, disk, thời gian và OOM. Nếu không đạt, ưu tiên:

1. demo bằng output đã xử lý trước;
2. chạy processing trên máy riêng/on-demand;
3. tăng bundle tạm thời;
4. không tăng swap vô hạn hoặc hy sinh database/API stability.

## 5. Release artifact tối thiểu

Server chỉ nhận các thành phần sau:

| Artifact | Nội dung |
|---|---|
| API image | ASP.NET runtime + published API; non-root; không có SDK/source/test |
| Migrator image/artifact | EF bundle Linux x64 + appsettings cần thiết; one-shot, không restart |
| AI image | FastAPI/CPU dependencies/model checksum; non-root |
| Release bundle | compose.production.yaml, Caddyfile, scripts, manifest/checksums |
| Runtime secret file | Chỉ tạo trên server tại /opt/agridrone/secrets/.env.production, mode 0600 |

Không copy lên server: .git, .github, .agents, .codacy, .qodo, .idea, .tmp, .tools, docs, Codex-Plan, scratch, backend/tests, bin, obj, TestResults, local .env, compose.yaml và compose.step1-test.yaml.

## 6. Ma trận dọn dẹp repository

### 6.1 Giữ trong Git nhưng loại khỏi artifact deploy

| Thành phần | Lý do phải giữ |
|---|---|
| backend/tests và compose.step1-test.yaml | V3-10 còn cần integration/acceptance tests |
| toàn bộ EF migrations và model snapshot | Fresh database cần chạy đủ chain; audit schema |
| docs, plans, PROJECT_MEMORY.md, AGENTS.md | Quản trị dự án; không phải runtime payload |
| compose.yaml và .env.example | Local development |
| scratch fixtures đang được README/service tham chiếu | Chỉ xóa sau khi chuyển fixture hoặc sửa tài liệu |

### 6.2 Candidate cho cleanup PR riêng

Các file root dưới đây không được runtime/build tham chiếu trong lần rà soát này và có vẻ là artifact thủ công:

- mock_baseline_finalized.sql
- review-batch-payload.json
- review-batch-payload-latest.json
- seed_baseline_before_prepare.sql
- test_drawio.xml

Chỉ xóa chúng trong một commit cleanup riêng sau khi:

1. chạy tìm kiếm tham chiếu trên toàn repository;
2. xác nhận không còn dùng cho tài liệu/demo thủ công;
3. nếu vẫn cần, chuyển vào docs/operations/samples hoặc test fixtures với tên/mục đích rõ ràng;
4. ghi danh sách đã xóa trong commit/PR.

Thư mục scratch có ảnh/TIFF/SQL fixture và backend/services/metashape-service/README.md đang tham chiếu scratch/test_orthophoto.tif. Không xóa toàn bộ scratch trong cùng thay đổi deployment. Trước tiên chuyển fixture cần giữ về đúng module; phần không dùng mới được xóa sau.

### 6.3 Generated local files

bin, obj, TestResults, publish, artifacts, .tmp và cache IDE có thể dọn ở máy local, nhưng không dùng lệnh broad như git clean -xfd hoặc xóa Docker volumes hàng loạt. Việc này không phải điều kiện deploy vì chúng đã bị loại khỏi build context.

Docker cleanup chỉ được dùng với project name cụ thể:

    docker compose -p agridrone-step1-test -f compose.step1-test.yaml down --remove-orphans
    docker compose -p agridrone-local -f compose.yaml down --remove-orphans

Hai lệnh trên không thêm --volumes. Chỉ xóa named volume sau khi đã xác minh đúng project, có backup và Owner chấp thuận dữ liệu không còn cần.

## 7. Database và migration

### 7.1 Chuỗi canonical phải giữ

V3-04 kết thúc bằng:

1. 20261116131222_V3_04MappingAndPlantIdentity
2. 20261118040000_V3_04AddInternalApprovedMapStatus
3. 20261118042702_V3_04CanonicalMappingRemediation

Không tạo migration “v304 deploy”, không đổi timestamp, không squash và không đưa migration cũ đã loại trở lại.

### 7.2 Quy trình migration production

1. Build bundle từ đúng commit và dotnet-ef 10.0.10.
2. Sinh thêm idempotent SQL để review/evidence khi cần; checksum cả bundle và SQL.
3. Rehearse từ database PostGIS trống trên môi trường disposable.
4. Trước deploy: tạo pg_dump và xác minh file/checksum.
5. Dừng write traffic hoặc chưa mở public traffic.
6. Chạy migrator one-shot bằng deployment identity có quyền DDL.
7. Kiểm tra exit code, migration history và health.
8. Khởi động API bằng runtime identity không có quyền DDL.

Không tự động chạy Down migration. Nếu migration đã làm thay đổi dữ liệu, rollback ưu tiên roll-forward; restore phải diễn tập trên database mới trước khi cutover.

## 8. Security và cấu hình

### 8.1 API sau reverse proxy

- Cấu hình X-Forwarded-For và X-Forwarded-Proto.
- Chỉ trust proxy/network Docker đã biết; không Clear KnownNetworks/KnownProxies rồi tin mọi nguồn.
- UseForwardedHeaders chạy trước HTTPS redirection, rate limiting và authentication.
- Production CORS chỉ nhận danh sách origin thật từ cấu hình; không localhost, không wildcard với credentials.
- Swagger vẫn tắt ở Production; dùng health/API client cho smoke test.

### 8.2 Secrets

- Runtime secrets nằm tại /opt/agridrone/secrets/.env.production, owner deploy, mode 0600.
- Secret độc lập cho PostgreSQL, RabbitMQ, Redis, MinIO, JWT, SMTP và SePay IPN.
- Không bật shell tracing; không in connection string/presigned URL đầy đủ.
- Rotate mọi secret từng xuất hiện trong source, log, screenshot hoặc chat.
- Bootstrap admin chỉ bật cho lần đầu, xác nhận tạo thành công rồi tắt và redeploy.

### 8.3 Firewall và host

- Lightsail IPv4 firewall: 80/443 công khai; 22 giới hạn nguồn.
- Nếu bật IPv6 phải cấu hình firewall tương đương; không tạo AAAA record trước khi kiểm tra.
- Bật automatic snapshots, budget alerts, disk/RAM/swap/OOM alerts.
- Docker log rotation 10 MB x 5 file mỗi service.

## 9. DNS, TLS và object storage

DNS tối thiểu:

    api.domain   A   LIGHTSAIL_STATIC_IP
    media.domain A  LIGHTSAIL_STATIC_IP

Caddy cấp TLS sau khi DNS resolve đúng. MinIO S3 API có thể public qua Caddy để browser dùng presigned URL, nhưng:

- bucket không anonymous;
- console 9001 không public;
- CORS chỉ cho frontend origin thật;
- presigned URL có TTL ngắn phù hợp;
- internal endpoint và public endpoint phải tách cấu hình nếu SDK hiện tại không thể presign đúng hostname công khai.

## 10. Backup, restore và rollback

### Backup bắt buộc

- PostgreSQL pg_dump custom format mỗi ngày, kèm SHA-256 và manifest.
- MinIO mirror/versioned backup ra storage ngoài instance.
- Lightsail automatic snapshot; manual snapshot trước migration lớn.
- Retention xóa theo prefix/project cụ thể, không dùng wildcard chưa kiểm chứng.

Backup chỉ được đánh dấu VERIFIED sau khi restore vào target disposable và kiểm tra migration history, row count chính và checksum object mẫu.

### Rollback

| Tình huống | Cách xử lý |
|---|---|
| Chỉ lỗi image, schema tương thích | Đổi về previous SHA/digest và smoke lại |
| Migration đã chạy, dữ liệu đúng | Roll-forward bằng migration sửa lỗi |
| Fresh demo DB hỏng và chưa có dữ liệu quan trọng | Restore pre-deploy dump sau phê duyệt |
| Có dữ liệu quan trọng | Dừng write, restore sang DB mới, kiểm tra rồi cutover |
| Instance hỏng | Tạo instance từ snapshot, gắn lại static IP, restore logical backup nếu cần |

## 11. CI/CD và thứ tự triển khai

CI trước V3-10 chỉ làm restore/build/compose validation/image build/secret scan; không chạy dotnet test.

CD chỉ chạy bằng workflow_dispatch hoặc release tag, có concurrency lock và immutable SHA:

1. build và push API/migrator/AI images;
2. xuất digest, SBOM/provenance nếu khả dụng và release manifest;
3. upload release bundle theo allowlist;
4. trên server: lock deployment, backup, pull theo digest;
5. start/check data services;
6. chạy migration one-shot;
7. update core services;
8. smoke test và quan sát;
9. chỉ bật processing profile khi capacity gate cho phép.

## 12. Go-live gates

- [ ] Repository cleanup và deploy allowlist đã review; artifact không chứa source/test/secret.
- [ ] Build C# đạt 0 Warning/0 Error; không chạy tests trái quy định.
- [ ] API/AI/migrator images chạy non-root và pin bằng digest.
- [ ] Fresh PostGIS migration rehearsal đạt; chain V3-04 đúng canonical.
- [ ] ApplyMigrationsOnStartup=false ở default và production.
- [ ] Chỉ 22 giới hạn, 80 và 443 public.
- [ ] HTTPS, forwarded headers, CORS và rate-limit client IP đúng.
- [ ] PostgreSQL/RabbitMQ/Redis/MinIO Console/FastAPI/NodeODM không public.
- [ ] SurveyPayments disabled và PublicIntakeEnabled=false.
- [ ] Backup upload ngoài instance và restore drill đạt.
- [ ] Core profile smoke đạt; processing profile chỉ bật sau capacity rehearsal.
- [ ] Frontend redirect routes tồn tại trước khi bật invitation/reset/payment end-to-end.
- [ ] Có previous release manifest và quy trình rollback đã diễn tập.

## 13. Nguồn chuẩn cần đối chiếu khi thực thi

- AWS Lightsail instance bundles: https://docs.aws.amazon.com/lightsail/latest/userguide/amazon-lightsail-bundles.html
- AWS Lightsail firewall: https://docs.aws.amazon.com/lightsail/latest/userguide/understanding-firewall-and-port-mappings-in-amazon-lightsail.html
- AWS Lightsail snapshots: https://docs.aws.amazon.com/lightsail/latest/userguide/amazon-lightsail-faq-snapshots.html
- EF Core applying migrations: https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying
- Docker Engine Ubuntu: https://docs.docker.com/engine/install/ubuntu/
- Caddy automatic HTTPS: https://caddyserver.com/docs/automatic-https

Mọi trạng thái DEPLOYED, BACKUP VERIFIED, RESTORE VERIFIED, PAYMENT ENABLED hoặc PRODUCTION READY chỉ được cập nhật khi có evidence thực tế. Đây là kiến trúc demo có kiểm soát, không phải tuyên bố production-ready.
