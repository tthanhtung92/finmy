# Bước 3 — DbContext tối thiểu + migration đầu tiên

> Mục tiêu: nối ứng dụng vào Postgres (đang chạy ở [Bước 1](01-docker-compose.md)) bằng một DbContext **tối thiểu**, sinh **migration đầu tiên**, và áp được vào DB. Đây là lúc chứng minh pipeline EF chạy thông end-to-end.
>
> Lưu ý mentor: DbContext, entity, connection string đều là code/cấu hình — **mình không viết hộ**. Mình mô tả cần dựng gì; bạn tự gõ.

---

## 3.1. Cái gì

Tạo một `DbContext` cho module Identity trong project Infrastructure, cấu hình nó dùng provider Npgsql với connection string trỏ tới Postgres trong Docker, rồi chạy `dotnet ef migrations add` + `dotnet ef database update`.

## 3.2. Vì sao

> TODO mentor: nhấn lại — migration giúp schema **version hóa cùng code**, áp lại được trên mọi môi trường, review qua PR. Giải thích vì sao mỗi module có **DbContext riêng** (ranh giới dữ liệu theo module — nối với quy tắc ranh giới ROADMAP mục 3). Giải thích **design-time DbContext factory**: vì sao `dotnet ef` (chạy lúc design-time, không có DI runtime của host) đôi khi cần một factory để khởi tạo DbContext.

## 3.3. Các bước làm — và ranh giới với Day 3

> **QUAN TRỌNG — đọc trước khi gõ:** Day 3 mới model `User`/`Role`/`RefreshToken` thật. Hôm nay **chỉ cần đủ để migration chạy thông**, đừng thiết kế entity nghiệp vụ ở đây.

Có hai hướng làm "tối thiểu", cả hai hợp lệ trên EF Core 10 (đã đối chiếu tài liệu — xem Link):

- **Hướng B — migration rỗng (khuyến nghị, sạch hơn):** nếu DbContext **chưa có `DbSet` nào**, EF không thấy thay đổi schema và `migrations add` sinh một **migration rỗng** (`Up`/`Down` không thao tác). Migration rỗng vẫn hợp lệ; khi `database update`, EF **vẫn tạo bảng `__EFMigrationsHistory`** và ghi nhận migration đã áp — đúng mục tiêu "chứng minh pipeline" mà **không** đẻ entity rác. Day 3 thêm entity thật rồi `migrations add` lần nữa.
- **Hướng A — entity placeholder:** khai một entity tạm đơn giản + `DbSet` để migration tạo một bảng thật; "nhìn thấy bảng" rõ hơn nhưng phải dọn bảng rác ở Day 3. Chỉ chọn nếu bạn muốn quan sát một bảng được tạo.

> Khuyến nghị đi **Hướng B** cho gọn. Phần "vì sao migration rỗng đủ chứng minh pipeline" để bạn tự đúc kết khi làm — đó là điểm hiểu sâu.

Khung các bước (theo Hướng B):

1. Trong `src/Modules/Identity/EventHub.Identity.Infrastructure/`, tạo một class kế thừa `DbContext` (đặt tên theo module, vd `IdentityDbContext`) — chưa cần khai `DbSet` nào.
2. Cấu hình DbContext dùng Npgsql với **connection string** trỏ Postgres trong Docker. Hai chỗ cần connection string, hai cơ chế khác nhau:
   - **Lúc chạy host (runtime):** đăng ký DbContext trong DI bằng `AddNpgsql<IdentityDbContext>(connectionString)` (hoặc `AddDbContext` + `options.UseNpgsql(...)`), với connection string đọc từ `appsettings.json` / **User Secrets** của `src/Bootstrap/EventHub.Api` (đừng commit mật khẩu thật).
   - **Lúc design-time (`dotnet ef`):** `dotnet ef` chạy *ngoài* host, không có DI runtime — nó cần tự dựng được DbContext. Hiện thực một class `IDesignTimeDbContextFactory<IdentityDbContext>` trong project Infrastructure: trong method `CreateDbContext`, dựng `DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(<connection string design-time>)` rồi trả về context. (Cách thay thế: trỏ `--startup-project` vào host để EF mượn cấu hình DI của host — chọn một, đừng làm cả hai.)
3. Sinh migration đầu tiên và áp vào DB (lệnh ở mục Kiểm chứng).

**Đặt migration ở đâu:** file migration EF sinh ra nằm ở project chứa DbContext, tức **project Infrastructure** của module (`EventHub.Identity.Infrastructure`). Vì DbContext (Infrastructure) khác project với host (Bootstrap), khi gọi `dotnet ef` phải chỉ **cả hai** cờ `--project` (nơi chứa DbContext/migration) và `--startup-project` (host để EF lấy cấu hình). Lệnh đầy đủ ở mục 3.4.

> Nguồn `IDesignTimeDbContextFactory` và lý do cần nó: xem [Design-time DbContext creation — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation). Cú pháp `AddNpgsql<TContext>` / `UseNpgsql` theo [docs provider Npgsql](https://www.npgsql.org/efcore/).

## 3.4. Kiểm chứng

Đảm bảo hạ tầng đang chạy (`docker compose ... up -d` ở Bước 1), rồi:

```bash
dotnet ef migrations add InitialCreate --project src/Modules/Identity/EventHub.Identity.Infrastructure --startup-project src/Bootstrap/EventHub.Api
dotnet ef database update --project src/Modules/Identity/EventHub.Identity.Infrastructure --startup-project src/Bootstrap/EventHub.Api
```

Hai cờ giải thích: `--project` trỏ project chứa DbContext (nơi migration được ghi vào); `--startup-project` trỏ host để EF lấy cấu hình lúc design-time. Nếu bạn đã hiện thực `IDesignTimeDbContextFactory`, factory đó được EF ưu tiên dùng để dựng context.

Xác nhận đã áp vào Postgres — vào container Postgres kiểm tra bảng hệ thống:

```bash
docker compose -f docker/docker-compose.yml exec postgres psql -U <user> -d <db> -c "\dt"
```

Phải thấy bảng **`__EFMigrationsHistory`** (và bảng placeholder nếu dùng Hướng A). Có nó nghĩa là migration đã áp thành công.

## 3.5. Cạm bẫy thường gặp

- **Thiếu design-time factory:** `dotnet ef` không khởi tạo được DbContext vì không có DI runtime → lỗi "Unable to create an object of type ...". Thường cần `IDesignTimeDbContextFactory` hoặc cấu hình DbContext qua host startup-project.
- **Sai `--project` / `--startup-project`:** DbContext nằm ở Infrastructure nhưng connection string ở host → phải chỉ cả hai cờ.
- **Connection string sai host/port:** trong Docker, app chạy *ngoài* compose nối tới Postgres qua `localhost:<port đã map>`; nếu app cũng chạy *trong* compose thì host là **tên service** (`postgres`), không phải `localhost`.
- **Commit mật khẩu thật:** dùng User Secrets cho dev, không nhét password vào `appsettings.json` rồi push.

## 3.6. Góc kể khi phỏng vấn

> TODO mentor: điền — gợi ý "DbContext per module = ranh giới dữ liệu theo module", "migration version hóa schema cùng code", "phân biệt connection string runtime vs design-time".

## 3.7. Link tài liệu chính thức

- [EF Core Migrations — tổng quan](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Design-time DbContext creation](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation)
- [Connection strings trong EF Core](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-strings)
- [Safe storage of secrets — User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)

## 3.8. Xong bước này khi

- [x] DbContext tối thiểu của Identity tồn tại trong project Infrastructure.
- [x] `dotnet ef migrations add` tạo được migration đầu tiên.
- [x] `dotnet ef database update` áp được; bảng `__EFMigrationsHistory` xuất hiện trong Postgres.
- [x] Connection string không bị commit kèm secret thật.

→ Sang [Bước 4 — Pattern AddModules/UseModules](04-module-pattern.md).
