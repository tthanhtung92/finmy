# Day 3. Module Identity: ApplicationUser/Role (Infra) + RefreshToken POCO (Domain) + DbContext

> **Mentor mode.** Tài liệu giải thích *vì sao* và *làm gì*, **không kèm code C#/cấu hình**, bạn tự gõ. Mọi lệnh CLI (`dotnet`, `docker`, `git`) thì cứ chạy theo. Mỗi file dưới đây là **một bước**, làm tuần tự từ trên xuống.
>
> Viết cho người **mới**: nếu một câu khiến bạn phải đoán, đó là lỗi của tài liệu. Nhắn mentor để bổ sung.

---

## Mục tiêu Day 3

Theo [ROADMAP](../../ROADMAP.md) (mục 5, Tuần 1, Ngày 3): *Module Identity: `ApplicationUser`/`Role` (Infrastructure) + `RefreshToken` POCO (Domain) + DbContext → Migration Identity chạy.*

Kết thúc Day 3 bạn có: *module Identity đã **mô hình hóa** `User`, `Role`, `RefreshToken` thật (không còn DbContext rỗng như Day 2); `IdentityModuleDbContext` sinh được **schema Identity đầy đủ** (7 bảng `AspNet*` + bảng `RefreshTokens`); migration mới áp được vào Postgres.*

Quỹ thời gian: ~1–2h. Đây là ngày đầu tiên chạm **domain thật**, nên đi chậm mà chắc: mọi thứ auth (Day 4: JWT, refresh token) đứng trên mô hình dữ liệu bạn dựng hôm nay.

> **Lưu ý phạm vi (đọc kỹ để không làm thừa):** Day 3 **chỉ** dựng *mô hình dữ liệu* (entity + DbContext + migration). Login/Register, sinh JWT, refresh token rotation là [Day 4](../README.md). Hôm nay chưa viết endpoint, chưa cấu hình authentication. Đừng làm sớm.

## Bạn cần có sẵn trước khi bắt đầu

- **Đã hoàn thành [Day 2](../day-02/README.md)**: solution build sạch; `docker compose … up -d` dựng được Postgres; migration rỗng `InitialCreate` đã áp (bảng `__EFMigrationsHistory` tồn tại).
- **Hạ tầng đang chạy**: `docker compose --env-file .env -f docker/docker-compose.yml ps` thấy Postgres `healthy`.
- **Công cụ `dotnet-ef`** đã cài (từ Day 2). Kiểm tra `dotnet ef --version` ra số.
- **Connection string `IdentityDb`** đã có trong User Secrets của host (từ Day 2, đừng set lại).
- Terminal mở tại **thư mục gốc repo** (nơi có `EventHub.slnx`).

## Các bước (làm theo thứ tự)

| Bước | File | Việc |
|------|------|------|
| A | [00-tong-quan.md](00-tong-quan.md) | **Hiểu** ASP.NET Core Identity vs domain thuần, 7 bảng `AspNet*`, các quyết định lớn (đọc, chưa gõ) |
| 1 | [01-package.md](01-package.md) | Thêm package `Microsoft.AspNetCore.Identity.EntityFrameworkCore` vào CPM + reference đúng project |
| 2 | [02-entities.md](02-entities.md) | Mô hình hóa `ApplicationUser`, `ApplicationRole`, `RefreshToken` |
| 3 | [03-dbcontext.md](03-dbcontext.md) | Đổi context → `IdentityModuleDbContext` kế thừa base Identity + cấu hình `RefreshToken` + `AddIdentityCore` |
| 4 | [04-migration.md](04-migration.md) | Sinh migration schema Identity thật + áp vào DB + verify bảng |
| 5 | [05-verify-commit.md](05-verify-commit.md) | Verify end-to-end → commit → push |
| 📝 | [notes.md](notes.md) | Ghi chú & đính chính sau review (đọc sau khi làm xong) |

## Quy tắc kiểm chứng xuyên suốt

Sau **mỗi** bước, chạy lại lệnh kiểm chứng ghi ở cuối file bước đó. Hai mỏ neo chính của Day 3:

```bash
dotnet build EventHub.slnx
dotnet ef migrations list --project src/Modules/Identity/EventHub.Identity.Infrastructure --startup-project src/Bootstrap/EventHub.Api
```

Build phải xanh; migration mới phải liệt kê ra (và áp được vào DB). Đừng sang bước mới khi bước hiện tại còn đỏ.

## Định nghĩa "hoàn thành" Day 3

- [ ] Package `Microsoft.AspNetCore.Identity.EntityFrameworkCore` khai trong CPM (không kèm version ở `.csproj`), version căn khớp EF Core.
- [ ] `ApplicationUser`, `ApplicationRole`, `RefreshToken` đã mô hình hóa; quan hệ 1-n User↔RefreshToken cấu hình rõ.
- [ ] `IdentityModuleDbContext` kế thừa base Identity (`IdentityDbContext<ApplicationUser, ApplicationRole, …>`); `base.OnModelCreating(builder)` gọi **trước** cấu hình custom.
- [ ] `dotnet ef migrations add` sinh được migration có schema Identity thật; `dotnet ef database update` áp được.
- [ ] Trong Postgres thấy đủ **7 bảng `AspNet*`** + bảng **`RefreshTokens`**.
- [ ] `dotnet build EventHub.slnx` xanh, không warning (kể cả warning downgrade version EF Core).
- [ ] **Bạn tự nói thành lời được:** vì sao dùng ASP.NET Core Identity thay vì domain thuần; 7 bảng `AspNet*` để làm gì; vì sao chọn kiểu khóa (Guid/`string`); vì sao `ApplicationUser` ở **Infrastructure** còn `RefreshToken` ở **Domain**, và abstraction `IIdentityService` giải *cả hai* luật sạch (DDD + phân lớp) bằng Dependency Inversion ra sao; `RefreshToken` trỏ User bằng `UserId` (id-reference) thế nào.

Xong Day 3, nhắn mentor **"review Day 3"** trước khi sang [Day 4](../README.md).
