# EventHub

> Nền tảng đặt vé sự kiện xây dựng theo kiến trúc **Modular Monolith** trên **.NET 10**. Mỗi kỹ thuật backend cốt lõi — Authentication, Realtime, Caching, CDN, Messaging, Concurrency — được trình diễn ở mức *minimal nhưng đúng chuẩn production*.

<!-- BADGES: thay <user>/<repo> bằng đường dẫn repo thật của bạn -->
[![CI](https://github.com/<user>/<repo>/actions/workflows/ci.yml/badge.svg)](https://github.com/<user>/<repo>/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

---

## Mục lục

- [Tổng quan](#tổng-quan)
- [Kỹ thuật được trình diễn](#kỹ-thuật-được-trình-diễn)
- [Kiến trúc](#kiến-trúc)
- [Tech Stack](#tech-stack)
- [Bắt đầu nhanh](#bắt-đầu-nhanh)
- [Cấu trúc dự án](#cấu-trúc-dự-án)
- [Kiểm thử](#kiểm-thử)
- [Quyết định kiến trúc](#quyết-định-kiến-trúc)
- [Lộ trình](#lộ-trình)
- [License](#license)

---

## Tổng quan

EventHub cho phép Organizer tạo sự kiện và bán vé, Attendee đặt vé theo thời gian thực. Project được thiết kế như một **bài trình diễn kỹ thuật**: thay vì nhiều tính năng, mỗi khái niệm backend quan trọng có một *vertical slice* chạy thật, có test, và được giải thích lý do thiết kế.

Điểm nhấn kỹ thuật là bài toán **chống overselling** — khi nhiều người mua vé cuối cùng cùng lúc — được giải bằng optimistic concurrency kết hợp transactional outbox.

> **Trạng thái:** 🚧 Đang phát triển. Xem [Lộ trình](#lộ-trình).

<!-- TODO: chèn GIF demo realtime đếm vé ở đây sau khi hoàn thành Tuần 3 -->
<!-- ![Demo](docs/assets/demo.gif) -->

---

## Kỹ thuật được trình diễn

| Kỹ thuật | Module | Cách triển khai |
|----------|--------|-----------------|
| **Authentication** | Identity | JWT + refresh token, phân quyền theo role |
| **CRUD + Database** | Events | EF Core 10, pagination, validation |
| **Caching** | Events | HybridCache (L1 in-memory + L2 Redis), cache-aside + invalidation |
| **CDN / Object Storage** | Events | Upload poster lên MinIO (S3-compatible) |
| **Realtime** | Ticketing | SignalR — cập nhật số vé còn lại trực tiếp |
| **Messaging / Queue** | Ticketing | Wolverine — đặt vé bất đồng bộ + transactional outbox |
| **Concurrency** | Ticketing | Optimistic concurrency (rowversion) chống overselling |
| **Observability** | Toàn hệ thống | Serilog structured logging + OpenTelemetry tracing |
| **DevOps** | Toàn hệ thống | Docker multi-stage, Compose, GitHub Actions CI |

---

## Kiến trúc

EventHub là một **Modular Monolith**: một process duy nhất, nhưng mã nguồn chia thành các module độc lập (Identity, Events, Ticketing). Mỗi module tự chứa Domain, Application, Infrastructure và API endpoints; các module giao tiếp với nhau **chỉ qua integration events** (publish qua Wolverine) hoặc public contracts — không reference trực tiếp nội bộ của nhau.

Ranh giới này được **kiểm soát tự động** bằng architecture tests (NetArchTest): nếu một module vi phạm ranh giới, CI sẽ fail.

<!-- TODO: chèn sơ đồ kiến trúc ở đây -->
<!-- ![Architecture](docs/assets/architecture.png) -->

```
┌─────────────────────────────────────────────┐
│            EventHub.Api (Host)               │
│           Composition Root                   │
├───────────┬───────────────┬─────────────────┤
│  Identity │     Events     │   Ticketing     │
│  module   │     module     │    module       │
└───────────┴───────────────┴─────────────────┘
        │            │              │
        └──── Wolverine message bus ┘
              (integration events)
                     │
   ┌─────────┬───────┴────────┬──────────┐
PostgreSQL   Redis           MinIO     SignalR
```

Chi tiết và lý do lựa chọn xem trong [docs/architecture.md](docs/architecture.md) và các [ADR](docs/adr/).

---

## Tech Stack

| Lớp | Công nghệ |
|-----|-----------|
| Runtime | .NET 10 LTS, C# 14 |
| Web | ASP.NET Core 10 (Minimal API), OpenAPI 3.1 |
| ORM / DB | EF Core 10, PostgreSQL 16 |
| Messaging | Wolverine (mediator + message bus + outbox) |
| Cache | HybridCache, Redis 7 |
| Realtime | SignalR |
| Object Storage | MinIO (S3-compatible) |
| Auth | ASP.NET Core Identity + JWT |
| Validation / Mapping | FluentValidation, Mapster |
| Logging | Serilog, OpenTelemetry |
| Testing | xUnit, NSubstitute, Shouldly, Testcontainers, NetArchTest |
| DevOps | Docker, Docker Compose, GitHub Actions |

> **Lưu ý về license:** tất cả thư viện sử dụng đều là open-source MIT/Apache/BSD. Project chủ động tránh các thư viện đã chuyển sang license thương mại từ 2025 (MediatR, AutoMapper, MassTransit, Moq, FluentAssertions) và dùng thay thế tương đương. Lý do chi tiết trong [ADR-0002](docs/adr/0002-why-wolverine.md).

---

## Bắt đầu nhanh

### Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/) và Docker Compose

### Chạy toàn bộ hệ thống

```bash
# Clone repo
git clone https://github.com/<user>/<repo>.git
cd <repo>

# Khởi động toàn bộ: API + PostgreSQL + Redis + MinIO
docker compose -f docker/docker-compose.yml up -d

# API sẵn sàng tại:
#   - API:        http://localhost:8080
#   - OpenAPI UI: http://localhost:8080/scalar
#   - MinIO UI:   http://localhost:9001
```

### Chạy ở chế độ phát triển

```bash
# Chỉ khởi động hạ tầng phụ thuộc
docker compose -f docker/docker-compose.yml up -d postgres redis minio

# Chạy API từ source
dotnet run --project src/Bootstrap/EventHub.Api
```

<!-- TODO: bổ sung thông tin tài khoản seed (admin/organizer) sau khi làm seed data -->

---

## Cấu trúc dự án

```
EventHub/
├── src/
│   ├── Bootstrap/EventHub.Api/      # Host duy nhất — composition root
│   ├── Modules/
│   │   ├── Identity/                # Auth, JWT, refresh token
│   │   ├── Events/                  # CRUD sự kiện, cache, upload poster
│   │   └── Ticketing/               # Đặt vé, outbox, chống overselling
│   └── Shared/
│       ├── EventHub.SharedKernel/   # Result<T>, base types
│       └── EventHub.Contracts/      # Integration events giữa các module
├── tests/                           # Unit, Integration, Architecture tests
├── docker/                          # Compose + cấu hình
├── docs/                            # Kiến trúc + ADR
└── .github/workflows/               # CI/CD
```

Cấu trúc chi tiết từng module xem trong [ROADMAP.md](ROADMAP.md).

---

## Kiểm thử

```bash
# Chạy toàn bộ test (integration test dùng Testcontainers, cần Docker)
dotnet test
```

Project có ba tầng test:

- **Unit tests** — logic domain và handler, dùng NSubstitute để mock.
- **Integration tests** — chạy với PostgreSQL và Redis thật qua Testcontainers.
- **Architecture tests** — NetArchTest ép ranh giới giữa các module; vi phạm sẽ fail CI.

---

## Quyết định kiến trúc

Các quyết định lớn được ghi lại dưới dạng ADR (Architecture Decision Record):

- [ADR-0001 — Vì sao chọn Modular Monolith](docs/adr/0001-why-modular-monolith.md)
- [ADR-0002 — Vì sao chọn Wolverine](docs/adr/0002-why-wolverine.md)
- [ADR-0003 — Chiến lược chống overselling](docs/adr/0003-overselling-strategy.md)

---

## Lộ trình

Lộ trình phát triển chi tiết 4 tuần xem trong [ROADMAP.md](ROADMAP.md).

- [ ] **Tuần 1** — Nền móng: solution, Identity (auth), Events (CRUD)
- [ ] **Tuần 2** — Caching & CDN: HybridCache, invalidation, MinIO
- [ ] **Tuần 3** — Realtime & Messaging: SignalR, Wolverine outbox, chống overselling
- [ ] **Tuần 4** — DevOps & hoàn thiện: Docker, CI/CD, observability, docs

---

## License

Dự án này được phát hành dưới [MIT License](./LICENSE).
