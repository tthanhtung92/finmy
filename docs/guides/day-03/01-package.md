# Bước 1. Thêm package Identity vào CPM + reference đúng project

> Mục tiêu: khai package Identity EF trong Central Package Management, rồi cho **đúng một** project tham chiếu. Vì [Quyết định 2](00-tong-quan.md) đặt `ApplicationUser`/`ApplicationRole` ở **Infrastructure** và giữ **Domain thuần POCO**, chỉ **Infrastructure** cần package Identity. Domain **không** dính gói nào.
>
> Lưu ý mentor: `.csproj` và `Directory.Packages.props` là cấu hình: **mình không viết hộ**. Mình nói tên package, version, đặt ở đâu; bạn tự gõ.

---

## 1.1. Cái gì

Khai **một** `PackageVersion` trong `Directory.Packages.props`, rồi reference **chỉ ở Infrastructure**:

- **`EventHub.Identity.Infrastructure`** → `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (lớp base `IdentityDbContext<…>` + `UserStore`/`RoleStore` chạy trên EF; và là nơi khai `ApplicationUser`/`ApplicationRole` ở [Bước 2](02-entities.md)).
- **`EventHub.Identity.Domain`** → **không** package Identity nào. `RefreshToken` là POCO thuần, không cần type nào của Identity.
- **`EventHub.Identity.Application`** → **không** package Identity nào. Interface `IIdentityService` (Day 4) chỉ dùng primitive (`string`/`bool`/`Result`).

## 1.2. Vì sao

**CPM (nhắc lại từ Day 1):** `Directory.Packages.props` giữ **version tập trung một chỗ**; các `.csproj` chỉ khai *tên* package. Nâng/căn version chỉ sửa một file, mọi project đồng bộ.

**Vì sao chỉ một package, chỉ ở Infrastructure:** đây là hệ quả trực tiếp của [Quyết định 2](00-tong-quan.md). Coupling với framework Identity được **cô lập trong Infrastructure**:

- `ApplicationUser`/`ApplicationRole` (ở **Infrastructure**) kế thừa `IdentityUser<Guid>`/`IdentityRole<Guid>`; `IdentityModuleDbContext` kế thừa lớp base EF; `IdentityService` (Day 4) giữ `UserManager<ApplicationUser>`. Tất cả đều cần **`Microsoft.AspNetCore.Identity.EntityFrameworkCore`**: gói này kéo theo (transitive) `Microsoft.Extensions.Identity.Stores` + `Microsoft.EntityFrameworkCore.Relational`. Một reference là đủ cho cả cụm.
- **Domain** không có bất kỳ type Identity nào (`RefreshToken` chỉ giữ `UserId: Guid`) → **không** cần, và **không được** kéo package Identity. Đây là điều làm Domain thuần POCO tuyệt đối.
- **Application** chỉ định nghĩa/dùng interface `IIdentityService` với surface primitive → cũng không cần package Identity.

So với phương án compromise (nhét entity xuống Domain, phải tách gói `…Identity.Stores` cho Domain và `…Identity.EntityFrameworkCore` cho Infra): lời giải này **gọn hơn** (một package, một tầng) mà lõi vẫn sạch hơn.

## 1.3. Dữ kiện đã xác minh

- **`Microsoft.AspNetCore.Identity.EntityFrameworkCore`**: stable mới nhất **10.0.9** (2026-06-09); chứa lớp base `IdentityDbContext<…>` và kéo theo `Microsoft.Extensions.Identity.Stores` (chứa `IdentityUser<TKey>`/`IdentityRole<TKey>`) + `Microsoft.EntityFrameworkCore.Relational` (≥ 10.0.9). Nguồn: [NuGet](https://www.nuget.org/packages/Microsoft.AspNetCore.Identity.EntityFrameworkCore).
- **Type `IdentityUser<TKey>`/`IdentityRole<TKey>`** nằm ở namespace `Microsoft.AspNetCore.Identity`, gốc trong package `Microsoft.Extensions.Identity.Stores`, nhưng ta **không** reference gói này trực tiếp; nó về theo transitive qua gói EF ở trên. Nguồn: [dotnet/aspnetcore, IdentityUser.cs](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Stores/src/IdentityUser.cs).

## 1.4. Các bước làm

1. Mở `Directory.Packages.props` ở **gốc repo**. Thêm **một** `PackageVersion`: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, version `10.0.9` (đặt cùng nhóm EF Core cho dễ đọc).
2. **Căn version EF Core (quan trọng, xem mục 1.6).** Hiện CPM có `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.2` và `Microsoft.EntityFrameworkCore.Design 10.0.4`. Identity EF `10.0.9` kéo EF Core Relational `10.0.9` → lệch patch. **Nâng cả cụm EF Core lên cùng một patch** (khuyến nghị `10.0.9` cho tất cả) để tránh NuGet cảnh báo/nâng ngầm. Kiểm bản Npgsql provider tương thích tại [nuget Npgsql.EntityFrameworkCore.PostgreSQL](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL).
3. Vào **`EventHub.Identity.Infrastructure`**, thêm `PackageReference` tới `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (**không** version, CPM lo).
4. **Không** thêm package Identity nào vào `EventHub.Identity.Domain` hay `EventHub.Identity.Application`.

> Ghi chú phân lớp: Infrastructure sẽ reference project Domain (để thấy `RefreshToken` khi cấu hình DbContext ở [Bước 3](03-dbcontext.md)) và project Application (để implement `IIdentityService` ở Day 4). Domain **không** reference ai và **không** dính package Identity.

## 1.5. Kiểm chứng

```bash
dotnet restore EventHub.slnx
dotnet build EventHub.slnx
```

Build phải xanh **và không có warning `NU1605` (package downgrade)** hay cảnh báo lệch version EF Core. Nếu có → quay lại mục 1.4 bước 2 căn lại version.

Xác nhận Domain **hoàn toàn sạch** Identity: mở `EventHub.Identity.Domain.csproj`: **không** có `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, **không** `Microsoft.Extensions.Identity.Stores`, **không** package EF Core nào. (Application cũng vậy.)

## 1.6. Cạm bẫy thường gặp

- **Lệch version EF Core (hay gặp nhất):** Identity EF `10.0.9` kéo EF Core `10.0.9`, trong khi Npgsql provider ghim `10.0.2` → NuGet có thể cảnh báo hoặc nâng ngầm. Căn tất cả package họ EF Core về **cùng một patch**.
- **Lỡ kéo package Identity xuống Domain:** nếu `EventHub.Identity.Domain` reference bất kỳ gói Identity nào, bạn phá [Quyết định 2](00-tong-quan.md). Domain hết thuần POCO. Domain phải trắng trơn gói Identity.
- **Khai `Version` trong `.csproj`:** vi phạm CPM, version chỉ khai ở `Directory.Packages.props`.

## 1.7. Góc kể khi phỏng vấn

*"Tôi cô lập coupling với ASP.NET Core Identity trong đúng một project (Infrastructure), bằng đúng một package `Microsoft.AspNetCore.Identity.EntityFrameworkCore`. Domain và Application không dính gói Identity nào: Domain thuần POCO, Application chỉ biết abstraction `IIdentityService`. Version cả cụm EF Core tôi căn cùng patch qua CPM để tránh lệch provider vs core."*

## 1.8. Link tài liệu chính thức

- [NuGet (Microsoft.AspNetCore.Identity.EntityFrameworkCore)](https://www.nuget.org/packages/Microsoft.AspNetCore.Identity.EntityFrameworkCore)
- [Central Package Management (Microsoft Learn)](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- [ASP.NET Core Identity (tổng quan)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [Clean Architecture template (Jason Taylor), Identity trong Infrastructure](https://github.com/jasontaylordev/CleanArchitecture)

## 1.9. Xong bước này khi

- [x] `Directory.Packages.props` có `PackageVersion` cho `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (`10.0.9`); cụm EF Core căn cùng patch.
- [x] **Chỉ** Infrastructure reference package Identity; không kèm version.
- [x] `EventHub.Identity.Domain.csproj` **và** `EventHub.Identity.Application.csproj` **không** dính package Identity/EF Core nào.
- [x] `dotnet build` xanh, không warning downgrade/lệch version.

→ Sang [Bước 2. Mô hình hóa entity](02-entities.md).
