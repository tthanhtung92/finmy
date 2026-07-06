# Bước 4. Sinh migration schema Identity thật + áp vào DB

> Mục tiêu: từ mô hình mới (7 entity Identity + `RefreshToken`), sinh migration tạo **8 bảng** rồi áp vào Postgres. Đây là lúc "nhìn thấy" schema auth thật trong DB.
>
> Lưu ý: chỉ có lệnh CLI ở đây, cứ chạy theo.

---

## 4.1. Cái gì

Chạy `dotnet ef migrations` để EF so sánh mô hình mới với snapshot cũ (migration rỗng Day 2) và sinh migration tạo các bảng `AspNet*` + `RefreshTokens`, rồi `dotnet ef database update` áp vào Postgres.

## 4.2. Vì sao

Migration = **version hóa schema cùng code** (đã học Day 2): mỗi thay đổi mô hình được dịch thành một file migration có `Up`/`Down`, review qua PR, áp lại được trên mọi môi trường.

Điểm mới hôm nay: migration này **không rỗng**, nó chứa `CreateTable` thật cho 8 bảng. EF tính ra nội dung migration bằng cách **diff** mô hình hiện tại với **snapshot** (`IdentityModuleDbContextModelSnapshot.cs`), file mô tả "trạng thái mô hình lần migration gần nhất". Day 2 snapshot rỗng (DbContext trơn); giờ mô hình có 8 entity → diff ra 8 `CreateTable`.

## 4.3. Xử lý migration rỗng của Day 2: đi **Hướng B** (vì đã chọn khóa `Guid`)

Day 2 đã có migration **rỗng** `InitialCreate` và **đã áp** vào DB. Vì [Quyết định 1](00-tong-quan.md) chọn khóa **`Guid`**, mà **đổi kiểu khóa chính phải nằm ở migration ĐẦU TIÊN** (không alter được PK sạch sẽ sau khi bảng đã tạo, [tài liệu Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0#change-the-primary-key-type)), ta **dựng lại** `InitialCreate` để nó chứa cả schema với PK `Guid` ngay từ đầu:

1. Revert DB về trạng thái chưa có migration nào (`database update 0`), gỡ mọi thứ migration rỗng đã áp.
2. Gỡ file migration rỗng (`migrations remove`).
3. Tạo lại `InitialCreate`, lần này mô hình đã có 8 entity nên migration chứa schema đầy đủ, PK kiểu `uuid`.
4. Áp vào DB (`database update`).

> **Hướng A (thêm migration additive) không dùng ở đây** vì nó giữ nguyên bảng PK cũ, không đổi được sang `Guid`. Chỉ chọn A nếu bạn giữ khóa `string` mặc định (ta đã chọn Guid nên bỏ qua).
>
> Vì DB hiện là **DB dev** (chưa có dữ liệu thật), revert + dựng lại hoàn toàn an toàn. Đừng làm cách này trên DB có dữ liệu cần giữ.

## 4.4. Kiểm chứng

Đảm bảo Postgres đang chạy (`docker compose … up -d`). Lệnh dùng **cả hai cờ** như Day 2 (`--project` = nơi chứa DbContext/migration; `--startup-project` = host để EF lấy cấu hình):

```bash
dotnet ef database update 0 --project src/Modules/Identity/EventHub.Identity.Infrastructure --startup-project src/Bootstrap/EventHub.Api
dotnet ef migrations remove --project src/Modules/Identity/EventHub.Identity.Infrastructure --startup-project src/Bootstrap/EventHub.Api
dotnet ef migrations add InitialCreate --project src/Modules/Identity/EventHub.Identity.Infrastructure --startup-project src/Bootstrap/EventHub.Api
dotnet ef database update --project src/Modules/Identity/EventHub.Identity.Infrastructure --startup-project src/Bootstrap/EventHub.Api
```

Xác nhận các bảng đã tạo trong Postgres (thay `<user>`/`<db>` bằng giá trị `POSTGRES_USER`/`POSTGRES_DB` trong `.env`):

```bash
docker compose --env-file .env -f docker/docker-compose.yml exec postgres psql -U <user> -d <db> -c "\dt"
```

Phải thấy **7 bảng `AspNet*`** (`AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`, `AspNetUserRoles`) **+ bảng `RefreshTokens`** (+ `__EFMigrationsHistory`).

Xác nhận PK là `uuid` (chứng minh khóa `Guid` đã ăn). Xem kiểu cột `Id` của `AspNetUsers`:

```bash
docker compose --env-file .env -f docker/docker-compose.yml exec postgres psql -U <user> -d <db> -c "\d \"AspNetUsers\""
```

Cột `Id` phải là kiểu **`uuid`** (không phải `text`/`character varying`).

## 4.5. Cạm bẫy thường gặp

- **Migration sinh ra vẫn rỗng:** thường do [Bước 3](03-dbcontext.md) chưa đổi lớp base hoặc quên `base.OnModelCreating` → EF không thấy entity mới. Kiểm tra context trước khi đổ lỗi cho lệnh.
- **Quên `database update 0` trước khi `migrations remove`:** EF từ chối gỡ migration đã áp vào DB. Phải revert DB trước.
- **Cột `Id` ra `text` chứ không phải `uuid`:** bạn chưa khai generic `IdentityUser<Guid>`/context `<…, Guid>` đúng, hoặc chưa dựng lại migration đầu. Quay lại [Bước 2](02-entities.md)/[Bước 3](03-dbcontext.md).
- **Sai user/db khi `psql`:** dùng đúng biến từ `.env`, không đoán.

## 4.6. Góc kể khi phỏng vấn

*"Vì tôi chọn khóa `uuid`, tôi phải đưa quyết định đó vào migration đầu, không alter PK sạch sau khi bảng đã tạo. Trên DB dev tôi revert + dựng lại `InitialCreate` để có một migration duy nhất chứa cả schema Identity với PK `uuid`. Migration này diff mô hình với snapshot nên tạo đúng 8 bảng."*

## 4.7. Link tài liệu chính thức

- [EF Core Migrations (tổng quan)](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Identity and EF Core Migrations](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0#identity-and-ef-core-migrations)
- [Change the primary key type](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0#change-the-primary-key-type)
- [`dotnet ef migrations` reference](https://learn.microsoft.com/en-us/ef/core/cli/dotnet#dotnet-ef-migrations-add)

## 4.8. Xong bước này khi

- [x] Migration `InitialCreate` mới sinh ra **không rỗng** (có `CreateTable` cho các bảng Identity + `RefreshTokens`).
- [x] `dotnet ef database update` áp thành công.
- [x] `psql \dt` thấy đủ 7 bảng `AspNet*` + `RefreshTokens`.
- [x] Cột `Id` của `AspNetUsers` là kiểu `uuid`.

→ Sang [Bước 5. Verify & commit](05-verify-commit.md).
