# Finmy: Modular Monolith Backend (.NET 10)

> Backend quản lý tài chính chung cho gia đình theo mô hình envelope budgeting, dựng để trình diễn các kỹ thuật backend cốt lõi: Authentication, Realtime, Caching, CDN, Messaging/Queue, và DevOps, mỗi phần ở mức *minimal nhưng đúng chuẩn production*.

**License:** MIT · **Kiến trúc:** Modular Monolith · **Nền tảng:** .NET 10 LTS

---

## 1. Mục tiêu & triết lý

Project này **không** cố gắng nhiều tính năng. Nó cố gắng cho mỗi khái niệm backend một *vertical slice mỏng nhưng chạy thật, có test, và giải thích được*. Một README giải thích tốt các quyết định kiến trúc có giá trị hơn 50 endpoint CRUD.

Chọn domain tài chính gia đình vì đây là thứ tác giả tự vận hành thật (self-host cho nhà mình), nên có động lực giữ nó đúng và hoàn thiện dần, chứ không phải một domain dựng lên rồi bỏ. Lý do đầy đủ ghi trong [ADR-0006](adr/0006-pivot-sang-tai-chinh-gia-dinh.md).

Ba nguyên tắc xuyên suốt:

1. **Đúng hơn nhiều.** Phần đã làm phải chạy được thật, không phải mockup.
2. **Giải thích được.** Mỗi lựa chọn kỹ thuật đều có lý do ghi trong README hoặc ADR.
3. **`docker compose up` là chạy.** Người xem CV clone về và chạy được trong 1 lệnh.

---

## 2. Tech Stack (cập nhật 06/2026)

| Lớp | Lựa chọn | License | Lý do |
|-----|----------|---------|-------|
| Runtime | **.NET 10 LTS** | MIT | LTS hỗ trợ đến 11/2028; nền tảng mới nhất cho project 2026 |
| Web | **ASP.NET Core 10** (Minimal API) | MIT | OpenAPI 3.1 built-in, auth metrics |
| Ngôn ngữ | **C# 14** | MIT | field keyword, primary constructor |
| ORM | **EF Core 10** | MIT | optimistic concurrency, named query filters |
| DB | **PostgreSQL 17** | OSS | Mạnh, miễn phí, phổ biến |
| Messaging | **Wolverine** | MIT | Gộp mediator + message bus + outbox native |
| Mapping | **Mapster** / thủ công | MIT | AutoMapper đã thương mại hóa |
| Validation | **FluentValidation** | Apache 2.0 | Vẫn miễn phí |
| Cache | **HybridCache** (L1 in-mem + L2 Redis) | MIT | Tính năng mới ổn định trong .NET 10 |
| Cache store | **Redis 8** | OSS | Distributed cache + pub/sub |
| Realtime | **SignalR** | MIT | Built-in, không vướng license |
| Object Storage | **MinIO** (S3-compatible) | AGPL | Giả lập CDN origin cục bộ cho ảnh hóa đơn |
| Auth | **ASP.NET Core Identity + JWT** | MIT | Chuẩn, có refresh token, role-based |
| Logging | **Serilog** | Apache 2.0 | Structured logging |
| Test | **xUnit + NSubstitute + Shouldly + Testcontainers** | MIT/BSD | Tránh Moq/FluentAssertions đã thương mại hóa |
| CI/CD | **GitHub Actions** | n/a | Build, test, push image |
| Container | **Docker multi-stage + Compose** | n/a | Reproducible environment |

### Lưu ý kỹ thuật quan trọng cho phỏng vấn

- **Làn sóng thương mại hóa thư viện (04/2025+):** MediatR, AutoMapper, MassTransit, FluentAssertions, Moq đều chuyển sang license thương mại. Project này chọn thay thế MIT một cách có chủ đích, đây là điểm bạn nên kể.
- **Wolverine codegen mode:** mặc định Dynamic (compile handler bằng Roslyn lúc startup), tiện khi dev. Khi build Docker image production, chuyển sang **Static codegen** để tránh recompile mỗi cold start và tiết kiệm ~100MB RAM của Roslyn. Biết chi tiết này = điểm cộng lớn.
- **Tiền tệ:** số tiền lưu bằng `decimal` theo đơn vị nhỏ nhất, cẩn thận khi làm tròn. Đây là việc dễ sai mà phỏng vấn viên hay hỏi.

---

## 3. Cấu trúc thư mục Solution (Modular Monolith)

Mô hình: mỗi **module** là một vertical slice tự chứa (Domain + Application + Infrastructure + API endpoints), giao tiếp với nhau **chỉ qua message bus (Wolverine) hoặc public contracts**, không reference trực tiếp internal của nhau. Đây là điều khiến nó "modular" chứ không phải monolith rối.

Aggregate gốc để chia sẻ là **Household**: nó sở hữu Account, Category, Envelope và Transaction, và là ranh giới phân quyền (một user chỉ chạm dữ liệu của hộ mình). Thành viên gắn vào hộ qua **Member** kèm role Owner / Member / Viewer.

```text
finmy/
├── .github/
│   └── workflows/
│       └── ci.yml                      # Build, test, docker push
├── docker/
│   ├── docker-compose.yml              # app + postgres + redis + minio
│   └── docker-compose.override.yml     # cấu hình dev
├── src/
│   ├── Bootstrap/
│   │   └── Finmy.Api/                  # Host duy nhất - composition root
│   │       ├── Program.cs              # Wire toàn bộ module + middleware
│   │       ├── Extensions/             # AddModules(), UseModules()
│   │       ├── Middleware/             # GlobalExceptionHandler
│   │       └── appsettings.json
│   │
│   ├── Modules/
│   │   ├── Identity/
│   │   │   ├── Finmy.Identity.Domain/          # RefreshToken (POCO thuần, UserId ref)
│   │   │   ├── Finmy.Identity.Application/      # Login/Register handlers, IIdentityService
│   │   │   ├── Finmy.Identity.Infrastructure/   # ApplicationUser/Role, DbContext, JWT + IdentityService
│   │   │   └── Finmy.Identity.Api/              # Minimal API endpoints
│   │   │
│   │   ├── Budgeting/
│   │   │   ├── Finmy.Budgeting.Domain/         # Household, Member, Account, Category, Envelope
│   │   │   ├── Finmy.Budgeting.Application/      # CRUD + cache handlers
│   │   │   ├── Finmy.Budgeting.Infrastructure/   # EF, Redis cache, MinIO
│   │   │   └── Finmy.Budgeting.Api/
│   │   │
│   │   └── Ledger/
│   │       ├── Finmy.Ledger.Domain/            # Transaction, quy tắc trừ số dư envelope
│   │       ├── Finmy.Ledger.Application/         # Ghi giao dịch qua Wolverine outbox
│   │       ├── Finmy.Ledger.Infrastructure/      # EF + concurrency handling + import CSV
│   │       └── Finmy.Ledger.Api/
│   │
│   └── Shared/
│       ├── Finmy.SharedKernel/         # Result<T>, DomainEvent base, guards
│       └── Finmy.Contracts/            # Integration events (public giữa modules)
│                                        # vd: TransactionPostedEvent, EnvelopeOverspentEvent
│
├── tests/
│   ├── Finmy.UnitTests/                # Domain logic, handlers (NSubstitute)
│   ├── Finmy.IntegrationTests/         # Testcontainers: real Postgres+Redis
│   └── Finmy.ArchitectureTests/        # NetArchTest: ép ranh giới module
│
├── docs/
│   ├── ROADMAP.md
│   ├── adr/                            # Architecture Decision Records
│   │   ├── 0001-modular-monolith.md
│   │   ├── 0002-wolverine.md
│   │   └── ...
│   └── guides/                         # Hướng dẫn tự implement theo ngày
│
├── .editorconfig
├── Directory.Build.props                # Version, nullable, treat warnings
├── Directory.Packages.props             # Central Package Management
├── Finmy.slnx                           # Solution mới (.slnx format của .NET 10)
├── LICENSE                              # MIT
└── README.md
```

### Quy tắc ranh giới module (rất quan trọng để show)

- Module **không** reference trực tiếp `Domain`/`Infrastructure` của module khác.
- Giao tiếp cross-module **chỉ qua** `Finmy.Contracts` (integration events) publish qua Wolverine.
- `ArchitectureTests` dùng **NetArchTest** để *fail CI* nếu ai đó vi phạm ranh giới. Đây là bằng chứng cứng cho phỏng vấn viên rằng bạn hiểu modular boundaries.

---

## 4. Bản đồ kỹ thuật → nơi thể hiện

| Khái niệm | Module | Cách thể hiện (minimal) |
|-----------|--------|--------------------------|
| **Authentication** | Identity | JWT + refresh token, role-based authz |
| **CRUD + DB** | Budgeting | EF Core 10, Household/Account/Category/Envelope, pagination, validation |
| **Caching** | Budgeting | HybridCache cache-aside cho số dư/report + invalidation khi có giao dịch mới |
| **CDN** | Budgeting | Upload ảnh hóa đơn → MinIO, serve qua cache layer; README mô tả đặt Cloudflare trước origin |
| **Realtime** | Ledger | SignalR broadcast số dư envelope mới cho mọi thành viên trong hộ |
| **Queue/Messaging** | Ledger | Wolverine: ghi giao dịch async + outbox + recurring transaction |
| **Concurrency** | Ledger | Chống chi vượt envelope = optimistic concurrency (rowversion) trên số dư |
| **Idempotency** | Ledger | Import CSV sao kê, khử trùng lặp theo hash giao dịch |
| **DevOps** | toàn bộ | Docker multi-stage, Compose, GitHub Actions CI |
| **Observability** | toàn bộ | Serilog structured + OpenTelemetry tracing |

---

## 5. Roadmap 4 tuần

> Giả định quỹ thời gian: tuần này 1-2h/ngày, các tuần sau 3-4h/ngày. Mỗi tuần kết thúc bằng một commit có thể demo được.

### Tuần 1: Nền móng (1-2h/ngày, nhẹ)

Mục tiêu: solution Modular Monolith chạy được với **1 module hoàn chỉnh** (Identity) và bắt đầu module Budgeting.

| Ngày | Việc | Output |
|------|------|--------|
| 1 | Khởi tạo `Finmy.slnx`, cấu trúc thư mục, Central Package Management, `Directory.Build.props`, LICENSE (MIT), README khung | Repo public push lên GitHub |
| 2 | Docker Compose: postgres + redis + minio. EF Core 10 + first migration. Setup `AddModules()/UseModules()` pattern | `docker compose up` lên được hạ tầng |
| 3 | Module Identity: `ApplicationUser`/`Role` (Infrastructure) + `RefreshToken` POCO (Domain) + DbContext | Migration Identity chạy |
| 4 | JWT authentication + refresh token + role-based authorization. Login/Register endpoints | Đăng nhập trả JWT thật |
| 5 | Global exception handling middleware, `Result<T>` pattern, FluentValidation cho Register | Lỗi trả về chuẩn ProblemDetails |
| 6-7 | Module Budgeting: CRUD Household / Account / Category / Envelope, pagination, validation. Vài unit test domain | Budgeting API chạy + test xanh |

**Mốc cuối tuần 1:** clone về, `docker compose up`, đăng nhập, tạo hộ và phong bì ngân sách được.

### Tuần 2: Performance & Caching (3-4h/ngày)

Mục tiêu: thể hiện hiểu cache *đúng cách*, không chỉ bật cache.

| Ngày | Việc | Output |
|------|------|--------|
| 8 | Tích hợp HybridCache (L1 in-memory + L2 Redis) | GET danh sách envelope/report có cache |
| 9 | Cache-aside pattern + TTL hợp lý cho số dư envelope và report tháng | Cache hit/miss đo được |
| 10 | **Cache invalidation** khi có giao dịch hoặc sửa ngân sách (tag-based eviction) | Chi tiền → cache số dư tự mất |
| 11 | MinIO: upload ảnh hóa đơn (S3 API), validate file, sinh URL | Upload ảnh thành công |
| 12 | Serve ảnh qua cache layer; viết ADR mô tả đặt Cloudflare CDN trước origin thật | docs/adr + demo |
| 13 | Output caching + response compression cho endpoint report public | n/a |
| 14 | Benchmark đơn giản trước/sau cache (BenchmarkDotNet hoặc k6), ghi số vào README | Có số liệu cho CV |

**Mốc cuối tuần 2:** README có bảng benchmark "trước/sau cache" với số liệu thật.

### Tuần 3: Realtime & Messaging (3-4h/ngày), phần "đinh"

Đây là phần đầu tư nhiều nhất và kể thành câu chuyện khi phỏng vấn.

| Ngày | Việc | Output |
|------|------|--------|
| 15 | SignalR Hub: broadcast số dư envelope tới các thành viên đang xem cùng một hộ | Demo realtime trực quan |
| 16 | Wolverine setup (Dynamic mode dev). Module Ledger domain: Transaction + quy tắc trừ số dư | Wolverine chạy in-process |
| 17 | API ghi giao dịch → publish command → trả **202 Accepted**. Handler xử lý async | Ghi giao dịch async chạy |
| 18 | **Transactional outbox** native của Wolverine: ghi Transaction + publish event atomic | Outbox table có message |
| 19 | **Chống chi vượt envelope**: optimistic concurrency (rowversion) trên số dư. Test race condition | Test 2 giao dịch đồng thời |
| 20 | Publish `TransactionPostedEvent` → consumer cập nhật report cache + cảnh báo vượt ngân sách + push SignalR số dư mới | Event chain hoàn chỉnh |
| 21 | Idempotency cho consumer và cho import CSV sao kê (message/giao dịch trùng). Viết ADR chiến lược concurrency | docs/adr |

**Mốc cuối tuần 3:** demo được kịch bản 2 thành viên cùng chi vào phong bì gần cạn cùng lúc → đúng 1 giao dịch qua, không âm quá mức.

### Tuần 4: DevOps & Hoàn thiện (3-4h/ngày)

| Ngày | Việc | Output |
|------|------|--------|
| 22 | Dockerfile multi-stage tối ưu. **Wolverine Static codegen** cho production image | Image nhỏ, cold start nhanh |
| 23 | Docker Compose đầy đủ + health checks cho mọi service | `up` là chạy toàn bộ |
| 24 | GitHub Actions CI: restore → build → test (Testcontainers) → build image | CI xanh trên PR |
| 25 | Serilog structured logging + OpenTelemetry tracing (span cho cache/messaging) | Trace nhìn được |
| 26 | ArchitectureTests (NetArchTest) ép ranh giới module → chạy trong CI | Vi phạm ranh giới = fail CI |
| 27 | OpenAPI 3.1 docs (Scalar/Swagger UI), seed data, Postman/.http collection | API docs đẹp |
| 28 | **README chất lượng cao**: sơ đồ kiến trúc, quyết định kỹ thuật + lý do, hướng dẫn chạy, ảnh/GIF demo | Thứ nhà tuyển dụng đọc đầu tiên |

**Ngày dư (buffer):** xử lý phần trễ. Luôn có phần trễ.

---

## 6. Định nghĩa "Hoàn thành" (Definition of Done)

Project coi là xong khi:

- [ ] `git clone` + `docker compose up` → toàn bộ hệ thống chạy, không cần bước thủ công.
- [ ] 3 module (Identity, Budgeting, Ledger) hoạt động end-to-end.
- [ ] Cả 8 khái niệm trong bảng mục 4 đều có slice chạy thật.
- [ ] Unit + integration + architecture test xanh trong CI.
- [ ] Kịch bản chống chi vượt envelope demo được.
- [ ] README có sơ đồ kiến trúc, bảng benchmark, GIF demo realtime.
- [ ] Tối thiểu 3 ADR giải thích quyết định lớn.
- [ ] LICENSE MIT, repo public.

---

## 7. Bẫy cần tránh

- **Đừng làm mọi phần mỏng như nhau.** Dồn chiều sâu vào Tuần 3 (messaging/concurrency), đó là thứ phân biệt Middle với Junior.
- **Đừng để app finance trượt thành CRUD thuần.** Phần lớn khối lượng một app tài chính là CRUD và báo cáo; nếu không cố ý đặt hai slice envelope-concurrency và import-idempotency làm trung tâm, project sẽ mất chiều sâu.
- **Đừng ôm 20 tính năng dở dang.** 3 module hoàn chỉnh > 30 module rối.
- **Đừng bỏ README đến phút cuối.** Nó là thứ được đọc đầu tiên, không phải code.
- **Đừng dùng thư viện đã thương mại hóa** rồi không giải thích được vì sao, phỏng vấn viên 2026 sẽ hỏi.

---

## 8. Câu chuyện kể khi phỏng vấn (chuẩn bị sẵn)

> "Tôi gặp bài toán khi hai thành viên trong nhà cùng chi vào một phong bì ngân sách gần cạn cùng lúc. Nếu mỗi request đọc số dư rồi ghi đè, cả hai đều thấy còn đủ và cùng trừ, phong bì âm quá mức. Tôi thử pessimistic lock, đơn giản nhưng giết throughput. Tôi chuyển sang optimistic concurrency với rowversion của EF Core: ai commit trước thắng, người sau nhận `DbUpdateConcurrencyException` rồi retry trên số dư mới. Tôi kết hợp transactional outbox của Wolverine để ghi giao dịch và publish event cập nhật số dư là atomic, và làm consumer idempotent để message trùng không trừ hai lần. Kết quả demo được: 2 request đồng thời vào phong bì còn đúng một suất → đúng 1 thành công."

Một câu chuyện như vậy giá trị hơn cả danh sách công nghệ.
