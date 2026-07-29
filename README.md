# Finmy

> Backend quản lý ngân sách chia sẻ cho một nhóm theo mô hình **envelope budgeting**, dựng theo kiến trúc **Modular Monolith** trên **.NET 10**. Mục tiêu: mỗi kỹ thuật backend cốt lõi (Authentication, Realtime, Caching, CDN, Messaging, Concurrency) có một lát cắt chạy thật, tối giản nhưng làm đúng cách.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

---

## Mục lục

- [Finmy](#finmy)
  - [Mục lục](#mục-lục)
  - [Tổng quan](#tổng-quan)
  - [Trạng thái hiện tại](#trạng-thái-hiện-tại)
  - [Kỹ thuật được trình diễn](#kỹ-thuật-được-trình-diễn)
  - [Kiến trúc](#kiến-trúc)
  - [Luồng ghi giao dịch](#luồng-ghi-giao-dịch)
  - [Tech Stack](#tech-stack)
  - [Bắt đầu nhanh](#bắt-đầu-nhanh)
    - [Yêu cầu](#yêu-cầu)
    - [Chạy](#chạy)
  - [Cấu trúc dự án](#cấu-trúc-dự-án)
  - [Kiểm thử](#kiểm-thử)
  - [Hiệu năng cache (benchmark)](#hiệu-năng-cache-benchmark)
  - [Quyết định kiến trúc](#quyết-định-kiến-trúc)
  - [Lộ trình](#lộ-trình)
  - [License](#license)

---

## Tổng quan

Finmy để một nhóm quản lý tiền chung theo lối envelope budgeting: chia thu nhập vào các "phong bì" ngân sách (envelope) theo từng nhóm chi như ăn uống, điện nước, học phí, rồi mỗi giao dịch trừ dần vào phong bì tương ứng. Nhiều thành viên trong một Space cùng xem và cùng chi trên một bộ ngân sách, phân vai Owner / Member / Viewer.

Repo được dựng như một bài trình diễn kỹ thuật hơn là một sản phẩm đủ tính năng. Thay vì gom nhiều màn hình, mỗi khái niệm backend quan trọng có một lát cắt chạy thật và có lý do thiết kế ghi lại được.

Bài toán khó nằm ở chỗ **nhiều người cùng chi một phong bì gần cạn cùng lúc**: hai giao dịch đồng thời không được phép đẩy số dư âm quá mức. Phần này đã chạy. Cách giải là optimistic concurrency trên số dư envelope, cộng transactional outbox của Wolverine để ghi giao dịch và phát sự kiện nằm trong cùng một transaction. Concurrency token là cột `Version` kiểu `int` do domain tự tăng trong mọi method mutate, map bằng `IsConcurrencyToken()`, cố ý không dùng `xmin` của Postgres (lý do trong mục [Kiến trúc](#kiến-trúc)). Có integration test dựng Postgres thật bằng Testcontainers cho kịch bản hai giao dịch chạy song song.

Domain trước đây của repo là bán vé sự kiện; lý do đổi sang ngân sách chia sẻ ghi trong [ADR-0006](docs/adr/0006-pivot-sang-tai-chinh-chia-se.md).

---

## Trạng thái hiện tại

Đây là project cá nhân đang xây dở, không phải hệ thống production. README này tách rõ **cái đã chạy** và **cái còn nằm trong kế hoạch**, để ai đọc repo không phải đoán.

**Đã xây (chạy được, đọc được code):**

- Bộ khung Modular Monolith: host `Finmy.Api` làm composition root, `IModule` để module tự đăng ký, mỗi module một DbContext nằm trên schema riêng (`identity`, `budgeting`, `ledger`, cộng `wolverine` cho message store).
- Module **Identity** đủ 4 tầng (Domain / Application / Infrastructure / Api):
  - Đăng ký, đăng nhập, JWT access token.
  - Refresh token rotation, phát hiện tái sử dụng token đã thu hồi thì revoke toàn bộ chain của user. Token sinh bằng RNG, lưu SHA-256 hash, unique index trên `TokenHash`.
  - Seed role Admin/User và admin mặc định qua `IHostedService`, credential đọc từ config.
  - Endpoint (đều dưới prefix `/identity`): `/register`, `/login`, `/refresh`, `/logout`, `/me`, `/admin-only`.
  - Đã qua một vòng security review và siết lại các lỗ hổng tìm thấy.
- Module **Budgeting**: Envelope CRUD đầy đủ (tạo, đọc theo id, list + pagination, update, delete) và báo cáo phân bổ theo tháng; Category seed sẵn trong migration; repository + validator. Endpoint: `POST /envelopes`, `GET /envelopes/{id}`, `GET /envelopes`, `PUT /envelopes/{id}`, `DELETE /envelopes/{id}`, `GET /envelopes/summary`.
  - **Caching**: HybridCache cache-aside cho list envelope và báo cáo tháng, per-entry TTL riêng, invalidation theo tag khi mutate (`BudgetingCachePolicy` + `RemoveByTagAsync`). Thêm output caching và nén Brotli/Gzip cho hai endpoint đọc nhiều, evict qua port `IOutputCacheInvalidator`.
  - **CDN / object storage**: upload ảnh hóa đơn lên MinIO qua S3 API (AWSSDK.S3), validate bằng magic bytes, object key do server sinh, lưu con trỏ `Receipt` vào Postgres. `POST /receipts` để upload, `GET /receipts/{id}` trả 302 kèm presigned URL và `Cache-Control`.
  - **Realtime**: SignalR hub strongly-typed `Hub<IEnvelopeClient>` tại `/hubs/envelopes`, mỗi envelope một group, client nhận `EnvelopeUpdated`, `EnvelopeAlert`, `EnvelopeDeleted`. Tầng Application chỉ biết port `IEnvelopeRealtimeNotifier`, không biết SignalR.
  - **Số dư và chống chi vượt**: `Envelope` giữ `Spent`, `Remaining` tính ra từ `Allocated - Spent`, ba method mutate số dư là `Spend`, `Release` và `Fund`. Chi quá số dư trả lỗi domain thay vì cho âm.
- Module **Ledger**: aggregate `Transaction` với `TransactionState` (`Posted` / `Reversed` / `Confirmed`), endpoint `POST /transactions` trả **202 Accepted** rồi xử lý bất đồng bộ, `GET /transactions/{id}` để tra trạng thái.
  - **Messaging + outbox**: Wolverine chạy in-process, codegen Dynamic ở dev và Static ở môi trường khác, message store nằm ở schema `wolverine`, `AddDbContextWithWolverineIntegration` để ghi `Transaction` và enqueue message trong cùng một transaction. Retry có cooldown riêng cho `DbUpdateConcurrencyException` trước khi đẩy vào error queue.
- **Integration event** giữa hai module, đặt trong `Finmy.Contracts`: `TransactionPostedEvent`, `EnvelopeOverspentEvent`, `EnvelopeBalanceChangedEvent`. Chuỗi đầy đủ mô tả ở mục [Luồng ghi giao dịch](#luồng-ghi-giao-dịch).
- **Test**: unit test cho domain Envelope (create / update / spend / fund), `EnvelopeService`, cache policy, alert policy, validator ảnh hóa đơn, domain Transaction; integration test race condition hai giao dịch đồng thời chạy trên Postgres thật qua Testcontainers.
- `Result<T>` / `Error` / `ErrorType` ở SharedKernel, `GlobalExceptionHandler` trả ProblemDetails không lộ stack trace, `ValidationFilter<T>` + FluentValidation chặn input rác ngay ở endpoint.
- OpenAPI + Scalar UI bật ở môi trường Development.
- Docker Compose cho hạ tầng phụ thuộc: PostgreSQL 17, Redis 8, MinIO.
- 8 ADR ghi lại các quyết định lớn, kèm `docs/naming-conventions.md` chốt quy ước đặt tên thư mục/file/namespace.

**Chưa xây:**

Space, Account, Member và phân quyền theo Space; idempotency (header `Idempotency-Key` cho consumer, import CSV sao kê khử trùng lặp); store trạng thái request giao dịch còn nằm in-memory nên restart là mất; ADR cho chiến lược concurrency; Serilog và OpenTelemetry; architecture test bằng NetArchTest; Dockerfile cho API; CI trên GitHub Actions.

---

## Kỹ thuật được trình diễn

| Kỹ thuật                 | Module        | Cách triển khai                                                        | Trạng thái |
| ------------------------ | ------------- | --------------------------------------------------------------------- | ---------- |
| **Authentication**       | Identity      | JWT + refresh token rotation, phân quyền theo role                    | Xong       |
| **Xử lý lỗi**            | Toàn hệ thống | `Result<T>` + ProblemDetails + FluentValidation                       | Xong       |
| **CRUD + Database**      | Budgeting     | EF Core 10: Envelope CRUD + báo cáo tháng (Space / Account kế hoạch), pagination, validation | Đang làm   |
| **Caching**              | Budgeting     | HybridCache (L1 in-memory + L2 Redis), cache-aside list/report + tag invalidation, output caching + nén | Xong       |
| **CDN / Object Storage** | Budgeting     | Upload ảnh hóa đơn lên MinIO (S3 API) + serve qua cache layer bằng presigned URL | Xong       |
| **Realtime**             | Budgeting     | SignalR: đẩy số dư envelope mới và cảnh báo cho client đang theo dõi   | Xong       |
| **Messaging / Queue**    | Ledger        | Wolverine in-process: ghi giao dịch bất đồng bộ + transactional outbox | Xong       |
| **Concurrency**          | Ledger + Budgeting | Optimistic concurrency bằng cột `Version` trên Envelope, đảo giao dịch khi chi vượt | Xong       |
| **Idempotency**          | Ledger        | `Idempotency-Key` cho consumer, import CSV sao kê khử trùng lặp theo hash | Kế hoạch   |
| **Observability**        | Toàn hệ thống | Serilog structured logging + OpenTelemetry tracing                    | Kế hoạch   |
| **DevOps**               | Toàn hệ thống | Docker multi-stage, Compose, GitHub Actions CI                        | Kế hoạch   |

---

## Kiến trúc

Finmy là một **Modular Monolith**: một process duy nhất, mã nguồn chia thành các module độc lập. Mỗi module tự chứa Domain, Application, Infrastructure và API endpoints; các module giao tiếp với nhau **chỉ qua integration event** trong `Finmy.Contracts`, không reference trực tiếp nội bộ của nhau.

Cả ba module đều đã có code chạy. Space là aggregate gốc để chia sẻ: nó sẽ sở hữu Account, Category, Envelope và Transaction, và cũng là ranh giới phân quyền (một user chỉ chạm dữ liệu của Space mình). Hiện `SpaceId` mới chỉ là một cột trên Transaction, aggregate Space thì chưa viết.

```text
┌─────────────────────────────────────────────┐
│              Finmy.Api (Host)                │
│             Composition Root                 │
├───────────┬───────────────┬─────────────────┤
│  Identity │   Budgeting    │     Ledger      │
│  (xong)   │  (đang xây)    │   (đang xây)    │
│           │ Envelope/       │ Transaction     │
│           │ Category/Receipt│ (outbox)        │
└───────────┴───────────────┴─────────────────┘
        │            │              │
        └──── Wolverine message bus ┘
              (integration events)
                     │
   ┌─────────┬───────┴────────┬──────────┐
PostgreSQL   Redis           MinIO     SignalR
```

Số dư envelope chỉ được ghi bởi Budgeting. Ledger không đụng vào bảng envelope, nó gửi event rồi chờ kết quả quay lại. Quy tắc single-writer này là lý do phần chống chi vượt nằm ở Budgeting chứ không nằm ở Ledger, dù nghiệp vụ nghe như của Ledger.

Concurrency token là `int Version` tự quản, không phải `xmin` qua `IsRowVersion()`. `xmin` bắt phải sửa tay migration để bỏ lệnh `AddColumn` vô nghĩa mà EF sinh ra, và giá trị của nó không sống sót qua dump rồi restore. Đổi lại, `Version` tự quản có cái giá riêng: quên `Version++` trong một method mutate mới là mất luôn lớp bảo vệ mà build vẫn xanh, nên mỗi method mutate cần kèm một test khẳng định version có tăng.

Ranh giới module dự kiến được ép tự động bằng architecture test (NetArchTest) chạy trong CI. Cả hai phần này chưa làm.

Lý do lựa chọn xem trong các [ADR](docs/adr/).

---

## Luồng ghi giao dịch

Lát cắt này đi qua gần hết phần khó của repo nên viết ra đây cho dễ đối chiếu với code.

1. Client gọi `POST /transactions`. Endpoint sinh `Guid` v7, đánh dấu request là Pending rồi trả **202 Accepted** kèm URL tra trạng thái. Không có bước nào chạm database trong request này.
2. `RecordTransactionHandler` ghi `Transaction` ở trạng thái `Posted` và enqueue `TransactionPostedEvent` trong cùng một transaction, nhờ outbox của Wolverine. Ghi hỏng thì event cũng không đi, không có cảnh giao dịch mất tích mà event vẫn phát.
3. Budgeting nhận event ở `TransactionPostedHandler`. Chi tiêu gọi `Envelope.Spend`, thu nhập gọi `Envelope.Fund` để nạp thêm ngân sách. Hai cái này cố ý tách nhau: `Fund` cộng vào `Allocated`, còn `Release` (hoàn tiền) mới trừ ngược `Spent`.
4. Nếu số dư không đủ, Budgeting phát `EnvelopeOverspentEvent`. Ledger nhận ở `EnvelopeOverspentHandler` và đảo giao dịch về `Reversed`; phía Budgeting `EnvelopeOverspentAlertHandler` đẩy cảnh báo cho client.
5. Nếu trừ tiền thành công, Budgeting phát `EnvelopeBalanceChangedEvent`. Event này có hai handler bên Budgeting: một xoá cache theo tag, một push số dư mới qua SignalR và kèm cảnh báo khi số dư còn dưới 20% mức phân bổ (`BudgetingAlertPolicy`). Vì `MultipleHandlerBehavior.Separated`, mỗi handler là một chain riêng, hỏng cái này không kéo cái kia chết theo.
6. Ledger cũng nghe chính event đó ở `TransactionConfirmedHandler` và mới lật giao dịch sang `Confirmed`. Trạng thái `Confirmed` vì vậy có nghĩa là tiền đã trừ thật bên Budgeting, không phải chỉ là "đã ghi xong".

Khi hai giao dịch chạy song song vào một envelope gần cạn, `DbUpdateConcurrencyException` sẽ bắn ra cho cái thua. Wolverine retry ba nhịp có cooldown, đọc lại số dư mới rồi thử tiếp, hết retry thì message vào error queue.

---

## Tech Stack

Đang dùng thật:

| Lớp        | Công nghệ                                       |
| ---------- | ----------------------------------------------- |
| Runtime    | .NET 10, C# 14                                  |
| Web        | ASP.NET Core 10 (Minimal API)                   |
| ORM / DB   | EF Core 10, PostgreSQL 17 (Npgsql)              |
| Auth       | ASP.NET Core Identity + JWT Bearer              |
| Messaging  | Wolverine 6 (mediator + bus + transactional outbox) |
| Realtime   | SignalR                                         |
| Validation | FluentValidation                                |
| Caching    | HybridCache (L1 in-memory + L2 Redis), output caching |
| Object storage | MinIO qua AWSSDK.S3 (S3 API)                |
| API docs   | OpenAPI + Scalar (bật ở Development)            |
| Test       | xUnit v3 trên Microsoft Testing Platform, NSubstitute, Shouldly, Testcontainers |
| Hạ tầng    | Docker Compose (PostgreSQL, Redis, MinIO)       |

Dự kiến thêm theo lộ trình: Mapster, Serilog, OpenTelemetry, NetArchTest, GitHub Actions.

> **Lưu ý về license:** project chủ động tránh các thư viện đã chuyển sang license thương mại từ 2025 (MediatR, AutoMapper, MassTransit, Moq, FluentAssertions) và chọn thay thế tương đương. Lý do chi tiết trong [ADR-0003](docs/adr/0003-tranh-thu-vien-thuong-mai.md).

> **Tiền tệ:** số tiền lưu bằng `decimal` theo đơn vị nhỏ nhất và cẩn thận khi làm tròn. Auto-import từ ngân hàng Việt Nam chưa khả thi vì thiếu open-banking phổ biến, nên nguồn nhập là upload CSV / sao kê hoặc nhập tay.

---

## Bắt đầu nhanh

### Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/) và Docker Compose

### Chạy

API chưa được đóng gói vào Docker, nên compose chỉ dựng hạ tầng phụ thuộc, còn API chạy từ source.

```bash
# Clone repo
git clone https://github.com/tthanhtung92/finmy.git
cd finmy

# Tạo .env ở gốc từ mẫu
cp .env.example .env

# Dựng hạ tầng: PostgreSQL + Redis + MinIO
docker compose -f docker/docker-compose.yml --env-file .env up -d
```

Ba connection string (`IdentityDb`, `BudgetingDb`, `LedgerDb`) và credential MinIO để trống trong `appsettings.json`, điền qua User Secrets của host:

```bash
dotnet user-secrets set "ConnectionStrings:IdentityDb" "<chuỗi kết nối>" --project src/Bootstrap/Finmy.Api
```

Migration chưa chạy tự động lúc khởi động, phải apply tay cho từng module:

```bash
dotnet ef database update -p src/Modules/Identity/Finmy.Identity.Infrastructure -s src/Bootstrap/Finmy.Api
dotnet ef database update -p src/Modules/Budgeting/Finmy.Budgeting.Infrastructure -s src/Bootstrap/Finmy.Api
dotnet ef database update -p src/Modules/Ledger/Finmy.Ledger.Infrastructure -s src/Bootstrap/Finmy.Api

# Chạy API từ source
dotnet run --project src/Bootstrap/Finmy.Api

# Scalar API docs: http://localhost:5079/scalar
# MinIO console: http://localhost:9001
```

Bảng của Wolverine ở schema `wolverine` thì tự tạo lúc khởi động, không cần migration riêng.

Muốn kèm pgAdmin thì dùng `docker/docker-compose.local.yml`.

---

## Cấu trúc dự án

```text
finmy/
├── src/
│   ├── Bootstrap/Finmy.Api/        # Host duy nhất, composition root
│   ├── Modules/
│   │   ├── Identity/               # Auth, JWT, refresh token rotation
│   │   │   ├── Finmy.Identity.Domain/
│   │   │   ├── Finmy.Identity.Application/
│   │   │   ├── Finmy.Identity.Infrastructure/
│   │   │   └── Finmy.Identity.Api/
│   │   ├── Budgeting/              # Envelope + số dư, caching, upload ảnh, SignalR
│   │   └── Ledger/                 # Transaction, Wolverine outbox, đảo giao dịch
│   └── Shared/
│       ├── Finmy.SharedKernel/     # Result<T>, Error, ErrorType
│       ├── Finmy.Modularity/       # IModule, ResultExtensions, ValidationFilter
│       └── Finmy.Contracts/        # Integration event giữa các module
├── tests/
│   ├── Finmy.UnitTests/            # Domain, service, cache/alert policy, validator
│   └── Finmy.IntegrationTests/     # Postgres thật qua Testcontainers
├── bench/                          # Script k6 đo trước/sau cache
├── docker/                         # Compose + cấu hình
└── docs/                           # ROADMAP + naming-conventions + ADR + guides
```

Space, Account và phần còn lại sẽ thêm theo [lộ trình](docs/ROADMAP.md).

---

## Kiểm thử

```bash
dotnet test                                      # cả hai project, integration test cần Docker chạy
dotnet test --project tests/Finmy.UnitTests      # chỉ unit test, không cần Docker
```

Unit test phủ domain Envelope (create, update, spend, fund), `EnvelopeService`, cache policy, alert policy, validator ảnh hóa đơn và domain Transaction. Integration test hiện có một bài: hai giao dịch cùng chi vào một envelope gần cạn, chạy song song trên Postgres thật dựng bằng Testcontainers, khẳng định đúng một giao dịch qua được.

Test project chạy trên Microsoft Testing Platform chứ không phải VSTest, nên lọc bằng `--filter-class` / `--filter-method`, cú pháp `--filter "FullyQualifiedName~X"` kiểu cũ sẽ chạy 0 test mà không báo lỗi.

Architecture test bằng NetArchTest là khoản nợ còn lại: ranh giới module hiện vẫn dựa vào kỷ luật khi code, chưa có gì ép tự động.

---

## Hiệu năng cache (benchmark)

Đo bằng k6 trên `GET /envelopes`, so hai cảnh của cùng một endpoint: cache trượt (request đi hết xuống Postgres) và cache trúng (response bật ra ngay ở output cache). Cách dựng và cách ép miss/hit ghi trong `docs/guides/day-14/`.

Throughput và độ trễ, 50 VUs trong 30 giây mỗi cảnh, `http_req_failed` bằng 0:

| Chỉ số | Trước cache (miss) | Sau cache (hit) | Chênh |
| --- | --- | --- | --- |
| Throughput | 1238 req/s | 32722 req/s | ~26 lần |
| Độ trễ p95 | 58.5 ms | 3.0 ms | ~19 lần thấp hơn |
| Độ trễ p99 | 81.0 ms | 5.6 ms | ~14 lần thấp hơn |
| Độ trễ trung bình | 40.2 ms | 1.4 ms | ~29 lần thấp hơn |

Payload sau response compression, đo trên list `pageSize=100`:

| Encoding | Kích thước | So với không nén |
| --- | --- | --- |
| Không nén | 16545 B | 1 lần |
| Brotli (`br`) | 2517 B | nhỏ hơn 6.6 lần |
| Gzip | 3313 B | nhỏ hơn 5.0 lần |

Điều kiện đo: AMD Ryzen 7 4800H, Windows 11, host .NET 10 chạy `-c Release` trên localhost, k6 v2.1.0, 50 VUs, 30 giây mỗi cảnh, khoảng 60 envelope seed sẵn. Vì k6 và host nằm cùng máy, không qua mạng thật, các con số này chỉ để so tương đối miss với hit trên cùng cấu hình, không phải độ trễ người dùng thật gặp qua Internet.

---

## Quyết định kiến trúc

Các quyết định lớn được ghi lại dưới dạng ADR (Architecture Decision Record):

- [ADR-0001: Dùng Modular Monolith thay vì microservices](docs/adr/0001-modular-monolith.md)
- [ADR-0002: Dùng Wolverine làm mediator + message bus + transactional outbox](docs/adr/0002-wolverine.md)
- [ADR-0003: Tránh thư viện đã thương mại hóa, chọn Mapster / NSubstitute / Shouldly](docs/adr/0003-tranh-thu-vien-thuong-mai.md)
- [ADR-0004: Ranh giới module Identity theo Option A (Dependency Inversion / IIdentityService)](docs/adr/0004-identity-option-a.md)
- [ADR-0005: Phát JWT bằng short-name claim với IdentityClaimTypes làm source of truth](docs/adr/0005-jwt-short-name-claim.md)
- [ADR-0006: Chuyển domain sang ngân sách chia sẻ theo envelope budgeting](docs/adr/0006-pivot-sang-tai-chinh-chia-se.md)
- [ADR-0007: Chốt quy ước đặt tên thư mục, file và namespace cho toàn repo](docs/adr/0007-quy-uoc-dat-ten.md)
- [ADR-0008: Serve ảnh hóa đơn bằng presigned URL, đặt CDN trước object-storage origin](docs/adr/0008-cdn-truoc-object-storage.md)
- [ADR-0009: Dùng cột `Version` kiểu int do domain tự tăng làm concurrency token, không dùng `xmin`](docs/adr/0009-concurrency-token-version-tu-quan.md)

ADR cho chiến lược idempotency (bốn tầng chống trùng) viết cùng Day 21.

---

## Lộ trình

Lộ trình phát triển chi tiết 4 tuần xem trong [docs/ROADMAP.md](docs/ROADMAP.md).

- [ ] **Tuần 1**: Nền móng, solution, Identity (auth), Budgeting (CRUD)
  - [x] Nền móng, solution, bộ khung module
  - [x] Identity: auth, JWT, refresh token rotation
  - [x] Budgeting: CRUD Category / Envelope + báo cáo tháng (Space / Account kế hoạch)
- [x] **Tuần 2**: HybridCache + tag invalidation, upload ảnh MinIO, serve ảnh qua cache, output caching, và benchmark trước/sau cache (số liệu ở mục Hiệu năng cache)
- [ ] **Tuần 3**: Realtime & Messaging
  - [x] SignalR đẩy số dư envelope
  - [x] Wolverine in-process + Ledger domain Transaction
  - [x] Ghi giao dịch async trả 202 Accepted
  - [x] Transactional outbox
  - [x] Chống chi vượt envelope + test race condition
  - [x] Chuỗi event: cache invalidation, push SignalR, cảnh báo ngân sách
  - [ ] Idempotency cho consumer và import CSV sao kê, ADR chiến lược concurrency
- [ ] **Tuần 4**: DevOps & hoàn thiện, Docker, CI/CD, observability, docs

---

## License

Dự án này được phát hành dưới [MIT License](./LICENSE).
