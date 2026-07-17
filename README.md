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
  - [Tech Stack](#tech-stack)
  - [Bắt đầu nhanh](#bắt-đầu-nhanh)
    - [Yêu cầu](#yêu-cầu)
    - [Chạy](#chạy)
  - [Cấu trúc dự án](#cấu-trúc-dự-án)
  - [Kiểm thử](#kiểm-thử)
  - [Quyết định kiến trúc](#quyết-định-kiến-trúc)
  - [Lộ trình](#lộ-trình)
  - [License](#license)

---

## Tổng quan

Finmy để một nhóm quản lý tiền chung theo lối envelope budgeting: chia thu nhập vào các "phong bì" ngân sách (envelope) theo từng nhóm chi như ăn uống, điện nước, học phí, rồi mỗi giao dịch trừ dần vào phong bì tương ứng. Nhiều thành viên trong một Space cùng xem và cùng chi trên một bộ ngân sách, phân vai Owner / Member / Viewer.

Repo được dựng như một bài trình diễn kỹ thuật hơn là một sản phẩm đủ tính năng. Thay vì gom nhiều màn hình, mỗi khái niệm backend quan trọng có một lát cắt chạy thật và có lý do thiết kế ghi lại được.

Bài toán khó nằm ở chỗ **nhiều người cùng chi một phong bì gần cạn cùng lúc**: hai giao dịch đồng thời không được phép đẩy số dư âm quá mức. Đây là bài toán tranh chấp thật, giải bằng optimistic concurrency (rowversion) trên số dư envelope, cộng transactional outbox để ghi giao dịch và phát sự kiện là một khối atomic. Phần này chưa xây, xem [Lộ trình](#lộ-trình).

Domain trước đây của repo là bán vé sự kiện; lý do đổi sang ngân sách chia sẻ ghi trong [ADR-0006](docs/adr/0006-pivot-sang-tai-chinh-chia-se.md).

---

## Trạng thái hiện tại

Đây là project cá nhân đang xây dở, không phải hệ thống production. README này tách rõ **cái đã chạy** và **cái còn nằm trong kế hoạch**, để ai đọc repo không phải đoán.

**Đã xây (chạy được, đọc được code):**

- Bộ khung Modular Monolith: host `Finmy.Api` làm composition root, `IModule` để module tự đăng ký, DbContext tách riêng theo module.
- Module **Identity** đủ 4 tầng (Domain / Application / Infrastructure / Api):
  - Đăng ký, đăng nhập, JWT access token.
  - Refresh token rotation, phát hiện tái sử dụng token đã thu hồi thì revoke toàn bộ chain của user. Token sinh bằng RNG, lưu SHA-256 hash, unique index trên `TokenHash`.
  - Seed role Admin/User và admin mặc định qua `IHostedService`, credential đọc từ config.
  - Endpoint: `/identity/register`, `/login`, `/refresh`, `/logout`, `/me`, `/admin-only`.
  - Đã qua một vòng security review và siết lại các lỗ hổng tìm thấy.
- `Result<T>` / `Error` / `ErrorType` ở SharedKernel, `GlobalExceptionHandler` trả ProblemDetails không lộ stack trace, `ValidationFilter<T>` + FluentValidation chặn input rác ngay ở endpoint.
- Docker Compose cho hạ tầng phụ thuộc: PostgreSQL 17, Redis 8, MinIO.
- 6 ADR ghi lại các quyết định lớn.

**Chưa xây:**

Module Budgeting (Space / Account / Category / Envelope), module Ledger (Transaction), Wolverine, SignalR, HybridCache (Redis mới có container, chưa có code dùng), tích hợp MinIO, chống chi vượt envelope, import CSV sao kê idempotent, OpenAPI/Scalar, Serilog, OpenTelemetry, test (unit / integration / architecture), Dockerfile cho API, CI trên GitHub Actions.

---

## Kỹ thuật được trình diễn

| Kỹ thuật                 | Module        | Cách triển khai                                                        | Trạng thái |
| ------------------------ | ------------- | --------------------------------------------------------------------- | ---------- |
| **Authentication**       | Identity      | JWT + refresh token rotation, phân quyền theo role                    | Xong       |
| **Xử lý lỗi**            | Toàn hệ thống | `Result<T>` + ProblemDetails + FluentValidation                       | Xong       |
| **CRUD + Database**      | Budgeting     | EF Core 10: Space / Account / Category / Envelope, pagination, validation | Kế hoạch   |
| **Caching**              | Budgeting     | HybridCache (L1 in-memory + L2 Redis), cache-aside số dư/report + invalidation | Kế hoạch   |
| **CDN / Object Storage** | Budgeting     | Upload ảnh hóa đơn lên MinIO (S3-compatible)                          | Kế hoạch   |
| **Realtime**             | Ledger        | SignalR: đẩy số dư envelope mới cho mọi thành viên trong cùng Space   | Kế hoạch   |
| **Messaging / Queue**    | Ledger        | Wolverine: ghi giao dịch bất đồng bộ + transactional outbox + recurring | Kế hoạch   |
| **Concurrency**          | Ledger        | Optimistic concurrency (rowversion) chống chi vượt envelope           | Kế hoạch   |
| **Idempotency**          | Ledger        | Import CSV sao kê, khử trùng lặp theo hash giao dịch                  | Kế hoạch   |
| **Observability**        | Toàn hệ thống | Serilog structured logging + OpenTelemetry tracing                    | Kế hoạch   |
| **DevOps**               | Toàn hệ thống | Docker multi-stage, Compose, GitHub Actions CI                        | Kế hoạch   |

---

## Kiến trúc

Finmy là một **Modular Monolith**: một process duy nhất, mã nguồn chia thành các module độc lập. Mỗi module tự chứa Domain, Application, Infrastructure và API endpoints; các module giao tiếp với nhau **chỉ qua integration events** hoặc public contracts, không reference trực tiếp nội bộ của nhau.

Hiện mới có module **Identity**. Budgeting và Ledger trong sơ đồ dưới là phần dự kiến, chưa có code. Space là aggregate gốc để chia sẻ: nó sở hữu Account, Category, Envelope và Transaction, và cũng là ranh giới phân quyền (một user chỉ chạm dữ liệu của Space mình).

```text
┌─────────────────────────────────────────────┐
│              Finmy.Api (Host)                │
│             Composition Root                 │
├───────────┬───────────────┬─────────────────┤
│  Identity │   Budgeting    │     Ledger      │
│  (xong)   │  (kế hoạch)    │   (kế hoạch)    │
│           │ Space/          │ Transaction     │
│           │ Account/Envelope│ (outbox+realtime)│
└───────────┴───────────────┴─────────────────┘
        │            │              │
        └──── message bus (kế hoạch) ┘
              (integration events)
                     │
   ┌─────────┬───────┴────────┬──────────┐
PostgreSQL   Redis           MinIO     SignalR
 (đang dùng) (kế hoạch)   (kế hoạch)  (kế hoạch)
```

Ranh giới module dự kiến được ép tự động bằng architecture test (NetArchTest) chạy trong CI. Cả hai phần này chưa làm.

Lý do lựa chọn xem trong các [ADR](docs/adr/).

---

## Tech Stack

Đang dùng thật:

| Lớp        | Công nghệ                                       |
| ---------- | ----------------------------------------------- |
| Runtime    | .NET 10, C# 14                                  |
| Web        | ASP.NET Core 10 (Minimal API)                   |
| ORM / DB   | EF Core 10, PostgreSQL 17 (Npgsql)              |
| Auth       | ASP.NET Core Identity + JWT Bearer              |
| Validation | FluentValidation                                |
| Hạ tầng    | Docker Compose (PostgreSQL, Redis, MinIO)       |

Dự kiến thêm theo lộ trình: Wolverine, HybridCache + Redis, SignalR, MinIO client, Mapster, Serilog, OpenTelemetry, OpenAPI/Scalar, xUnit + NSubstitute + Shouldly + Testcontainers + NetArchTest, GitHub Actions.

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

# Chạy API từ source
dotnet run --project src/Bootstrap/Finmy.Api

# MinIO console: http://localhost:9001
```

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
│   │   ├── Budgeting/              # (kế hoạch) Space, Account, Category, Envelope
│   │   └── Ledger/                 # (kế hoạch) Transaction, outbox, realtime
│   └── Shared/
│       ├── Finmy.SharedKernel/     # Result<T>, Error, ErrorType
│       ├── Finmy.Modularity/       # IModule, ResultExtensions
│       └── Finmy.Contracts/        # Integration events giữa các module
├── docker/                          # Compose + cấu hình
└── docs/                            # ROADMAP + ADR + guides
```

Module Budgeting, Ledger và thư mục `tests/` sẽ thêm theo [lộ trình](docs/ROADMAP.md).

---

## Kiểm thử

Chưa có test. Đây là khoản nợ lớn nhất của repo hiện tại.

Kế hoạch ba tầng test khi làm tới:

- **Unit test**: logic domain và handler, dùng NSubstitute để mock.
- **Integration test**: chạy với PostgreSQL và Redis thật qua Testcontainers.
- **Architecture test**: NetArchTest ép ranh giới giữa các module, vi phạm thì fail CI.

---

## Quyết định kiến trúc

Các quyết định lớn được ghi lại dưới dạng ADR (Architecture Decision Record):

- [ADR-0001: Dùng Modular Monolith thay vì microservices](docs/adr/0001-modular-monolith.md)
- [ADR-0002: Dùng Wolverine làm mediator + message bus + transactional outbox](docs/adr/0002-wolverine.md)
- [ADR-0003: Tránh thư viện đã thương mại hóa, chọn Mapster / NSubstitute / Shouldly](docs/adr/0003-tranh-thu-vien-thuong-mai.md)
- [ADR-0004: Ranh giới module Identity theo Option A (Dependency Inversion / IIdentityService)](docs/adr/0004-identity-option-a.md)
- [ADR-0005: Phát JWT bằng short-name claim với IdentityClaimTypes làm source of truth](docs/adr/0005-jwt-short-name-claim.md)
- [ADR-0006: Chuyển domain sang ngân sách chia sẻ theo envelope budgeting](docs/adr/0006-pivot-sang-tai-chinh-chia-se.md)

ADR-0002 ghi quyết định chọn Wolverine, nhưng phần tích hợp thật vẫn chưa làm.

---

## Lộ trình

Lộ trình phát triển chi tiết 4 tuần xem trong [docs/ROADMAP.md](docs/ROADMAP.md).

- [ ] **Tuần 1**: Nền móng, solution, Identity (auth), Budgeting (CRUD)
  - [x] Nền móng, solution, bộ khung module
  - [x] Identity: auth, JWT, refresh token rotation
  - [ ] Budgeting: CRUD Space / Account / Category / Envelope
- [ ] **Tuần 2**: Caching & CDN, HybridCache cho số dư/report, invalidation, MinIO ảnh hóa đơn
- [ ] **Tuần 3**: Realtime & Messaging, SignalR số dư, Wolverine outbox, chống chi vượt envelope, import CSV idempotent
- [ ] **Tuần 4**: DevOps & hoàn thiện, Docker, CI/CD, observability, docs

---

## License

Dự án này được phát hành dưới [MIT License](./LICENSE).
