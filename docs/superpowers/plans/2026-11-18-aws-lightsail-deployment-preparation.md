# AgriDrone — Kế hoạch thực thi chuẩn bị triển khai AWS Lightsail

> **Ngày cập nhật:** 2026-11-18
> **Trạng thái:** READY FOR IMPLEMENTATION / NOT DEPLOYED
> **Blueprint chuẩn:** ../../../Codex-Plan/AWS-Lightsail-Deployment-Plan.md
> **Nguyên tắc thực thi:** làm tuần tự, mỗi task có exit gate; không tự đánh dấu đạt khi chưa có evidence

## 0. Ràng buộc toàn cục

- Không chạy dotnet test trong V3-01 đến V3-09.
- Khi có thay đổi C#, chỉ chạy:

      dotnet build backend/AgriDrone.sln --no-restore --nologo

- Giữ SurveyPayments__Enabled=false, LegacyFeatures__EnableDeprecatedEndpoints=false và PublicIntakeEnabled=false.
- Không apply migration vào database demo có dữ liệu nếu chưa backup và chưa rehearsal trên fresh PostGIS.
- Không sửa/squash/xóa chuỗi migration canonical V3-04.
- Không dùng tag latest cho image production; release phải dùng Git SHA hoặc digest.
- Không đưa secret thật vào source, Docker layer, release bundle, CI log hoặc tài liệu.
- Task dọn repository phải tách khỏi task deployment; không xóa test/migration/fixture còn dùng.

## 1. Definition of Done

Kế hoạch chỉ hoàn thành khi có đủ:

1. release artifact tối thiểu, không chứa source/test/docs/local secret;
2. API, migrator và AI image reproducible, non-root, pin bằng digest;
3. production compose validate thành công và chỉ Caddy publish 80/443;
4. migration bundle chạy đạt từ fresh PostGIS, API startup migration đã tắt;
5. HTTPS, health, CORS, forwarded headers và presigned object URL hoạt động;
6. backup nằm ngoài instance và restore drill đạt;
7. rollback về previous image đã được diễn tập;
8. processing profile có capacity evidence hoặc được giữ tắt;
9. mọi safety flag vẫn fail-closed;
10. PROJECT_MEMORY.md chỉ cập nhật theo evidence thực tế.

## 2. Thứ tự thực thi bắt buộc

    P0 inventory/cleanup
      -> P1 API proxy/config
      -> P2 images/migrator
      -> P3 production compose/Caddy
      -> P4 scripts/secrets/backup
      -> P5 CI/CD
      -> P6 host/DNS/TLS
      -> P7 rehearsal/first deploy
      -> P8 processing capacity gate
      -> P9 handoff

Không làm P6–P8 trước khi P0–P5 được review.

## P0 — Chốt inventory và dọn đúng phạm vi

### Task P0.1 — Ghi manifest hiện trạng

**Đọc/kiểm tra:**

- PROJECT_MEMORY.md
- backend/.config/dotnet-tools.json
- backend/src/BuildingBlocks/AgriDrone.Database/Migrations
- compose.yaml
- compose.step1-test.yaml
- backend/services/metashape-service

**Checklist:**

- [ ] Xác nhận V3-04 đã đóng G0–G18, không dùng nội dung cũ nói M0/G14 còn mở.
- [ ] Ghi đúng ba migration cuối của V3-04: 20261116131222, 20261118040000, 20261118042702.
- [ ] Xác nhận dotnet-ef 10.0.10 khớp EF Core package version.
- [ ] Ghi Git SHA sạch dùng làm release candidate.
- [ ] Ghi digest của mọi third-party image sau khi chọn version.

**Exit gate:** manifest không còn status/version suy đoán.

### Task P0.2 — Tạo deploy allowlist

**Tạo:**

- backend/.dockerignore
- .dockerignore nếu build context ở repository root
- infrastructure/release/README.md hoặc manifest tương đương

**Phải loại khỏi build/release context:**

- .git, .github, .agents, .codacy, .qodo, .idea, .tmp, .tools;
- docs, Codex-Plan, scratch;
- backend/tests, bin, obj, TestResults, coverage, publish, artifacts;
- .env và mọi .env.* thật; appsettings.Development.json;
- compose.yaml, compose.step1-test.yaml;
- dump, archive, local database, IDE/cache files.

Dockerfile chỉ COPY project/source cần thiết. Không dùng COPY toàn repository rồi dựa hoàn toàn vào ignore.

**Exit gate:** kiểm tra build context và image history không có file bị cấm.

### Task P0.3 — Cleanup PR riêng

**Candidate cần review/xóa hoặc chuyển chỗ:**

- mock_baseline_finalized.sql
- review-batch-payload.json
- review-batch-payload-latest.json
- seed_baseline_before_prepare.sql
- test_drawio.xml

**Quy trình:**

1. Tìm tham chiếu trên toàn repository.
2. Nếu còn giá trị vận hành, chuyển vào docs/operations/samples hoặc test fixtures và thêm README.
3. Nếu không có consumer, xóa bằng patch trong một commit chỉ chứa cleanup.
4. Không xóa scratch/test_orthophoto.tif khi README metashape-service còn tham chiếu.
5. Phân loại toàn bộ scratch: KEEP/MOVE/DELETE; chuyển fixture cần giữ về module tương ứng trước.
6. Không xóa backend/tests, compose.step1-test.yaml, migrations hoặc model snapshot.

**Không thực hiện:** git clean -xfd, docker system prune -a, docker volume prune hoặc xóa volume bằng wildcard.

**Exit gate:** cleanup diff độc lập, có danh sách file đã xóa/chuyển và không làm hỏng reference.

## P1 — An toàn API sau reverse proxy

### Task P1.1 — Tắt migration tự động

**Sửa:** backend/src/AgriDrone.Api/appsettings.json

- [ ] Đặt DatabaseInitialization:ApplyMigrationsOnStartup=false làm safe default.
- [ ] Production env vẫn khai báo rõ false.
- [ ] Giữ validation master data theo policy hiện tại; không biến validation thành schema migration.
- [ ] Không gọi database migration từ Docker entrypoint của API.

### Task P1.2 — Forwarded headers đúng trust boundary

**Sửa:** backend/src/AgriDrone.Api/Program.cs

- [ ] Đăng ký ForwardedHeadersOptions cho X-Forwarded-For và X-Forwarded-Proto.
- [ ] Chỉ trust Caddy proxy/network đã biết. Không xóa toàn bộ KnownNetworks/KnownProxies để tin mọi nguồn Internet.
- [ ] Giới hạn ForwardLimit phù hợp một proxy hop.
- [ ] Gọi UseForwardedHeaders trước exception handler, HTTPS redirection, routing, rate limiter và authentication.
- [ ] Xác minh rate limiter nhận client IP thật, không gom toàn bộ người dùng vào IP của Caddy.

### Task P1.3 — Production CORS fail-closed

**Sửa:**

- backend/src/AgriDrone.Api/Program.cs
- backend/src/AgriDrone.Api/appsettings.json

- [ ] Thêm Cors:AllowedOrigins mặc định là mảng rỗng.
- [ ] Development policy có thể giữ local behavior.
- [ ] Production chỉ dùng origins được cấu hình; bỏ localhost hardcode.
- [ ] Không dùng wildcard cùng AllowCredentials.
- [ ] Khi chưa có frontend thật, để danh sách rỗng và smoke API bằng non-browser client.

**Verification P1:**

- [ ] Chạy build được phép, đạt 0 Warning/0 Error.
- [ ] Review thứ tự middleware.
- [ ] Không chạy dotnet test.

## P2 — Image API, migrator và AI

### Task P2.1 — API image

**Tạo:** backend/src/AgriDrone.Api/Dockerfile

Yêu cầu:

- multi-stage .NET SDK 10 -> ASP.NET Runtime 10;
- restore/publish Release theo central package management;
- runtime chỉ chứa publish output;
- user/group cố định, không root;
- listen 0.0.0.0:8080;
- healthcheck dùng công cụ thực sự có trong runtime image hoặc viết probe không phụ thuộc curl bị thiếu;
- read-only filesystem nếu code cho phép; temp path/volume ghi riêng;
- không chứa SDK, source, test, local config hay migration credentials.

### Task P2.2 — Migrator one-shot

**Tạo một trong hai dạng, ưu tiên image riêng:**

- backend/src/AgriDrone.Api/Dockerfile.migrator; hoặc
- target migrator trong Dockerfile và service migrate trong production compose.

Yêu cầu:

- build EF bundle self-contained cho linux-x64 từ đúng commit;
- project: backend/src/BuildingBlocks/AgriDrone.Database/AgriDrone.Database.csproj;
- startup project: backend/src/AgriDrone.Api/AgriDrone.Api.csproj;
- ASPNETCORE_ENVIRONMENT=Production khi build/chạy bundle;
- kèm appsettings cần thiết nhưng không kèm secret;
- service restart=no và chỉ chạy sau postgres healthy;
- deployment identity có DDL; runtime API identity không có DDL;
- ghi checksum bundle và migration list vào release manifest.

Không nhét dotnet-ef/SDK vào API runtime image và không chạy migrator mỗi lần API restart.

### Task P2.3 — AI image CPU-only

**Sửa:**

- backend/services/metashape-service/Dockerfile
- backend/services/metashape-service/requirements.txt
- thêm lock/constraints file nếu cần

Yêu cầu:

- pin exact version cho Python packages; không giữ range mở trong production lock;
- cài Torch/Torchvision CPU từ official CPU index và pin cặp tương thích;
- pin Ultralytics và model checksum;
- non-root, temp workspace có giới hạn/dọn dẹp;
- health endpoint nhẹ, không dùng /docs làm readiness;
- không copy scratch/sample ảnh vào image;
- NodeODM concurrency=1 và image pin bằng digest đã smoke-test.

### Verification P2

- [ ] Build image bằng context allowlist.
- [ ] Inspect image user, labels, layers và size.
- [ ] API healthcheck không phụ thuộc binary không tồn tại.
- [ ] Migrator chạy một lần rồi exit 0.
- [ ] AI import/startup không tải CUDA runtime.
- [ ] Không chạy test suite.

## P3 — Production Compose và Caddy

### Task P3.1 — Production compose

**Tạo:** compose.production.yaml

Services core:

- caddy;
- api;
- migrate one-shot;
- postgres PostGIS;
- rabbitmq;
- redis;
- minio;
- minio-init one-shot.

Services profile processing:

- metashape-service/FastAPI;
- nodeodm.

Yêu cầu:

- [ ] Chỉ Caddy có ports 80:80 và 443:443.
- [ ] Không có ports cho database/broker/cache/storage console/AI.
- [ ] Tách edge và internal network; internal network đặt internal=true nếu không làm hỏng pull/runtime cần thiết.
- [ ] Named volumes rõ tên cho postgres, rabbitmq, redis, minio và Caddy.
- [ ] Healthchecks thật; depends_on dùng service_healthy hoặc service_completed_successfully đúng chỗ.
- [ ] restart=unless-stopped cho daemon; restart=no cho migrate/minio-init.
- [ ] Resource limits dùng key được Docker Compose hiện tại hỗ trợ và được docker compose config xác nhận.
- [ ] Logging json-file, max-size 10m, max-file 5.
- [ ] Image application theo SHA/digest; third-party image pin tag/digest, không latest.
- [ ] Secret file dùng đường dẫn tuyệt đối/được kiểm soát trên server.
- [ ] Core profile không phụ thuộc cứng vào processing profile.

### Task P3.2 — Caddy

**Tạo:** infrastructure/caddy/Caddyfile

- api.domain -> api:8080;
- media.domain -> minio:9000;
- encode gzip/zstd cho API khi phù hợp;
- upstream health probe /health/live;
- không expose Caddy admin API;
- không route MinIO Console/port 9001;
- giữ Host/scheme đúng để presigned signature hoạt động;
- security headers ở mức không phá Swagger/client; Swagger vẫn tắt production.

### Task P3.3 — Object storage endpoint split

**Inspect trước khi sửa code:** cách MinIO SDK hiện tạo presigned URL.

- Internal endpoint: http://minio:9000 cho server-to-server.
- Public endpoint: https://media.domain cho URL trình duyệt.
- Nếu option hiện tại không hỗ trợ tách, thêm options rõ ràng và validation; không hardcode domain.
- Bucket private, anonymous access off, CORS chỉ cho frontend thật.

### Verification P3

- [ ] docker compose --env-file template -f compose.production.yaml config không lỗi.
- [ ] Rendered config không publish 5432, 5672, 15672, 6379, 9000, 9001, 3000 hoặc 8000.
- [ ] Không có secret thật trong rendered config lưu làm artifact.
- [ ] Core profile render độc lập; processing profile render khi bật.

## P4 — Secrets, bootstrap, deploy, backup và restore

### Task P4.1 — Env template

**Tạo:** infrastructure/env/.env.production.example

Template chỉ có placeholder và safe defaults. Nhóm biến tối thiểu:

- domains và ASP.NET environment;
- PostgreSQL runtime và migration identities riêng;
- JWT;
- RabbitMQ/Redis;
- MinIO internal/public endpoint và credentials;
- SMTP;
- AI endpoint/profile toggle;
- SurveyPayments disabled và SePay Sandbox placeholders;
- admin bootstrap one-shot.

Không biểu diễn PublicIntakeEnabled như env nếu nó là policy state trong database; smoke script phải đọc trạng thái thật qua query/endpoint được phép.

### Task P4.2 — Host bootstrap

**Tạo:** infrastructure/scripts/bootstrap-lightsail.sh

- idempotent; Ubuntu LTS được Lightsail hỗ trợ tại thời điểm tạo máy;
- cài Docker từ official repository, Compose plugin, jq/curl/fail2ban;
- tạo user deploy và /opt/agridrone/{releases,secrets,backups,current};
- secrets mode 0700 directory/0600 file;
- cấu hình timezone, log rotation và swap hợp lý;
- không tự sửa SSH password policy trước khi xác minh key login;
- không hardcode 16 GB swap nếu disk/headroom không đủ; đo rồi cấu hình có giới hạn.

### Task P4.3 — Deploy script

**Tạo:** infrastructure/scripts/deploy.sh

Luồng bắt buộc:

1. set -euo pipefail, không set -x;
2. nhận release SHA/digest và kiểm tra format;
3. acquire deployment lock để chặn hai deploy đồng thời;
4. xác minh manifest/checksum và dung lượng disk;
5. tạo/verify pre-deploy backup;
6. pull image theo digest;
7. start/check data services;
8. chạy migrator one-shot và dừng ngay nếu exit khác 0;
9. update core services;
10. chạy smoke;
11. lưu current/previous release manifest;
12. nếu smoke fail và schema tương thích, rollback image; không tự restore DB.

### Task P4.4 — Backup/restore

**Tạo:**

- infrastructure/scripts/backup.sh
- infrastructure/scripts/restore.sh

Backup:

- pg_dump custom format, checksum SHA-256 và manifest;
- MinIO mirror/versioned copy sang storage ngoài instance;
- temp file trong target cố định, permissions chặt;
- retention chỉ xóa đúng project prefix sau dry-run/validation;
- cảnh báo backup age/size bất thường.

Restore:

- bắt buộc target database/bucket mới;
- xác nhận target rõ ràng;
- verify checksum trước restore;
- không overwrite live target mặc định;
- sau restore kiểm tra migration history, row counts chính và object mẫu.

### Task P4.5 — Smoke script

**Tạo:** infrastructure/scripts/smoke-test.sh

Kiểm tra không ghi dữ liệu phá hoại:

- compose services expected/healthy, không restart loop;
- HTTPS certificate và redirect;
- /health/live, /health/ready, /health/minio;
- PostGIS extension và migration head;
- data ports không public;
- bucket private và presigned PUT/GET bằng object tạm có prefix smoke;
- SurveyPayments disabled và PublicIntakeEnabled=false;
- không có secret trong recent logs.

Object smoke tạo ra phải xóa đúng key sau khi xác minh; không dùng recursive wildcard.

## P5 — CI/CD

### Task P5.1 — CI

**Tạo:** .github/workflows/ci.yml

- checkout exact commit;
- setup .NET 10 và restore tool manifest;
- restore/build theo lệnh dự án;
- không chạy dotnet test trước V3-10;
- validate production compose;
- build API/migrator/AI images;
- secret scan và image vulnerability scan theo policy;
- không push image từ pull request không tin cậy.

### Task P5.2 — CD

**Tạo:** .github/workflows/deploy-lightsail.yml

- trigger workflow_dispatch hoặc release tag demo-v*;
- GitHub Environment lightsail-demo, approval nếu khả dụng;
- concurrency group deploy-lightsail, không cancel deployment đang chạy;
- push GHCR image theo full SHA, record digest;
- pin SSH host key; không tắt strict host checking;
- runtime business secrets ở server, không truyền toàn bộ qua workflow;
- gọi deploy script với release manifest;
- upload deployment evidence không chứa secret.

**Exit gate P5:** rerun cùng release không tạo trạng thái khác ngoài idempotent no-op/redeploy an toàn.

## P6 — AWS host, DNS và TLS

### Task P6.1 — Tạo tài nguyên

- [ ] Xác nhận lại giá/availability bundle trên official Lightsail console.
- [ ] Region Singapore, Ubuntu LTS được hỗ trợ.
- [ ] Baseline 16 GB/4 vCPU/320 GB chỉ là giả định capacity.
- [ ] Attach static IPv4 trước khi tạo DNS.
- [ ] Firewall IPv4: 80/443 all; 22 giới hạn IP quản trị.
- [ ] Nếu IPv6 bật, cấu hình firewall độc lập; chưa tạo AAAA khi chưa verify.
- [ ] Bật automatic snapshots và AWS Budget alerts.
- [ ] Tạo external backup bucket với lifecycle phù hợp.

### Task P6.2 — DNS/TLS

- [ ] api.domain và media.domain trỏ static IP.
- [ ] DNS resolve đúng trước khi start Caddy ACME.
- [ ] Certificate hợp lệ và renew được.
- [ ] Không public MinIO Console hoặc Caddy admin.

### Task P6.3 — SMTP/SePay

- SMTP SES chỉ cấu hình sau khi domain/DKIM/SPF/DMARC sẵn sàng.
- SePay chỉ dùng Sandbox placeholder/credentials trên server.
- SurveyPayments vẫn false.
- Không bật payment khi success/error/cancel frontend routes chưa tồn tại.

## P7 — Migration rehearsal và deploy lần đầu

### Task P7.1 — Fresh database rehearsal

Đây là kiểm tra migration/deployment, không phải dotnet test suite.

1. Tạo disposable PostGIS bằng project name riêng.
2. Chạy bundle từ InitialCreate đến head.
3. Kiểm tra PostGIS extension, __EFMigrationsHistory và ba migration V3-04 canonical.
4. Khởi động API với ApplyMigrationsOnStartup=false.
5. Gọi readiness và inspect logs.
6. Lưu command/output/checksum đã redaction.
7. Chỉ xóa disposable database sau khi xác minh đúng project name; không chạm agridrone-local volumes.

### Task P7.2 — First deploy

1. Upload release bundle/checksum.
2. Tạo secret file 0600 trực tiếp trên server.
3. Start postgres/rabbitmq/redis/minio.
4. Chạy minio-init.
5. Chạy migrator one-shot.
6. Start API/Caddy.
7. Bootstrap admin một lần; xác nhận rồi tắt flag và redeploy.
8. Chạy smoke; quan sát tối thiểu 15 phút.
9. Chạy backup và restore drill vào target disposable.
10. Ghi release evidence; chưa bật processing nếu P8 chưa đạt.

## P8 — Processing capacity gate

Chỉ thực hiện khi demo cần xử lý ảnh thật.

- [ ] Bật processing profile với NodeODM concurrency 1.
- [ ] Chạy dataset đại diện, bắt đầu nhỏ rồi mới đến 400 ảnh/1 GB.
- [ ] Ghi peak RAM, swap, CPU, temp disk, thời gian, restart/OOM và API latency.
- [ ] Xác minh temp files được dọn cả khi job thành công và thất bại.
- [ ] API/DB vẫn healthy trong suốt job.
- [ ] Còn ít nhất 25–30% disk sau cleanup.

Nếu fail: giữ profile tắt và chọn preprocessed output, worker riêng hoặc bundle lớn hơn. Không sửa healthcheck để che OOM và không tăng swap vô hạn.

## P9 — Handoff và vận hành

### Evidence phải lưu

- release SHA và image digests;
- compose config redacted;
- migration bundle/SQL checksums và migration head;
- build result 0 Warning/0 Error;
- firewall/DNS/TLS evidence;
- smoke result;
- backup/restore result và RPO/RTO thực tế;
- capacity result hoặc quyết định giữ processing disabled;
- rollback rehearsal result;
- xác nhận safety flags.

### Runbook tối thiểu

- deploy release mới;
- rollback application image;
- backup on-demand;
- restore vào disposable target;
- SSH tunnel cho console nội bộ;
- rotate secrets;
- xử lý disk full, OOM, unhealthy container và certificate failure.

### Cập nhật tài liệu

Chỉ sau khi evidence tồn tại:

- [ ] Cập nhật PROJECT_MEMORY.md với trạng thái thật.
- [ ] Đánh dấu checkbox tương ứng trong plan này.
- [ ] Không ghi production-ready; đây là demo deployment.
- [ ] Giữ test status là NOT RUN đến V3-10.

## Review cuối trước khi bắt đầu implementation

- [x] Blueprint và execution plan đã tách vai trò, không lặp nguyên văn.
- [x] Status V3-04 đã sửa thành Done/Remediated.
- [x] Tool manifest được ghi đúng là đã tồn tại và pin 10.0.10.
- [x] Cleanup dùng deploy allowlist; không xóa test/migration để giảm artifact.
- [x] NodeODM/AI chuyển thành processing profile có capacity gate.
- [x] Migration tách khỏi API startup và có one-shot migrator.
- [x] Backup, restore, rollback và secret boundary có exit gate rõ ràng.
- [x] Không yêu cầu chạy dotnet test.
