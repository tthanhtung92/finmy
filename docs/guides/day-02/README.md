# Day 2 — Hạ tầng (Docker Compose) + nền tảng module

> **Mentor mode.** Tài liệu giải thích *vì sao* và *làm gì*, **không kèm code C#/cấu hình** — bạn tự gõ. Mọi lệnh CLI (`dotnet`, `docker`, `git`) thì cứ chạy theo. Mỗi file dưới đây là **một bước**, làm tuần tự từ trên xuống.
>
> Viết cho người **mới**: nếu một câu khiến bạn phải đoán, đó là lỗi của tài liệu — nhắn mentor để bổ sung.

---

## Mục tiêu Day 2

Theo [ROADMAP](../../ROADMAP.md) (mục 5, Tuần 1 — Ngày 2): *Docker Compose: postgres + redis + minio. EF Core 10 + first migration. Setup `AddModules()/UseModules()` pattern → `docker compose up` lên được hạ tầng.*

Kết thúc Day 2 bạn có: *một lệnh `docker compose up` dựng được **Postgres + Redis + MinIO**; EF Core 10 đã nối vào solution và sinh được **migration đầu tiên** (áp lên Postgres thật); host Bootstrap nạp module qua pattern `AddModules()` / `UseModules()` thay vì wire tay từng cái.*

Quỹ thời gian: ~1–2h. Đây là ngày "đặt đường ray" cho hạ tầng — làm chắc, vì mọi ngày sau (Identity, Events, Ticketing) đều chạy trên đường ray này.

> **Lưu ý phạm vi (đọc kỹ để không làm thừa):** Day 2 **chưa** mô hình hóa entity Identity thật (`User`/`Role`/`RefreshToken`) — việc đó là [Day 3](../README.md). Hôm nay "first migration" chỉ nhằm **chứng minh pipeline EF Core chạy thông** (kết nối được Postgres → tạo migration → áp được vào DB). Đừng cố model nghiệp vụ ở đây.

## Bạn cần có sẵn trước khi bắt đầu

- **Đã hoàn thành [Day 1](../day-01/README.md)** — solution build sạch, có CPM + `Directory.Build.props`.
- **Docker Desktop** đã cài và đang chạy. Kiểm tra: `docker --version` và `docker compose version` đều ra số. Trên Windows cần bật WSL2 backend.
- **.NET 10 SDK**: `dotnet --version` ra số bắt đầu bằng `10.`.
- **Công cụ `dotnet-ef`** (cài ở [Bước 2](02-ef-core-packages.md)) — CLI để tạo/áp migration.
- Terminal mở tại **thư mục gốc repo** (nơi có `EventHub.slnx`).

## Các bước (làm theo thứ tự)

| Bước | File | Việc |
|------|------|------|
| A | [00-tong-quan.md](00-tong-quan.md) | **Hiểu** hạ tầng + pipeline EF + pattern module (đọc, chưa gõ gì) |
| 1 | [01-docker-compose.md](01-docker-compose.md) | Tạo `docker/docker-compose.yml`: Postgres + Redis + MinIO, volume + healthcheck |
| 2 | [02-ef-core-packages.md](02-ef-core-packages.md) | Thêm package EF Core 10 + Npgsql vào CPM; cài `dotnet-ef` |
| 3 | [03-dbcontext-migration.md](03-dbcontext-migration.md) | DbContext tối thiểu + connection string → migration đầu tiên → áp vào DB |
| 4 | [04-module-pattern.md](04-module-pattern.md) | Pattern `IModule` + `AddModules()` / `UseModules()` cho host |
| 5 | [05-verify-commit.md](05-verify-commit.md) | Verify end-to-end → commit → push |
| 📝 | [notes.md](notes.md) | Ghi chú & đính chính sau review (đọc sau khi làm xong) |

## Quy tắc kiểm chứng xuyên suốt

Sau **mỗi** bước, chạy lại lệnh kiểm chứng ghi ở cuối file bước đó. Hai mỏ neo chính của Day 2:

```bash
dotnet build EventHub.slnx
docker compose -f docker/docker-compose.yml ps
```

Build phải xanh; các service hạ tầng phải ở trạng thái `healthy`. Đừng sang bước mới khi bước hiện tại còn đỏ.

## Định nghĩa "hoàn thành" Day 2

- [ ] `docker compose -f docker/docker-compose.yml up -d` dựng **3 service** Postgres + Redis + MinIO, tất cả `healthy`.
- [ ] Dữ liệu **bền qua restart**: `down` rồi `up` lại không mất dữ liệu (nhờ named volume).
- [ ] EF Core 10 + provider Npgsql đã khai trong CPM; project Infrastructure tham chiếu (không kèm version).
- [ ] `dotnet ef migrations add` tạo được **migration đầu tiên**; `dotnet ef database update` áp được vào Postgres (kiểm tra bảng `__EFMigrationsHistory` tồn tại).
- [ ] Host Bootstrap dùng `AddModules()` / `UseModules()` để nạp module; module Identity tự đăng ký service/endpoint của mình qua một `IModule`.
- [ ] `dotnet build EventHub.slnx` xanh, không warning; host chạy được.
- [ ] **Bạn tự nói thành lời được:** vì sao chạy hạ tầng bằng Docker thay vì cài tay; ba service đảm nhận vai trò gì; migration & `__EFMigrationsHistory` là gì; pattern `IModule` giải quyết vấn đề gì so với wire tay trong `Program.cs`.

Xong Day 2, nhắn mentor **"review Day 2"** trước khi sang [Day 3](../README.md).
