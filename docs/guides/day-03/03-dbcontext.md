# Bước 3. Nâng cấp DbContext → `IdentityModuleDbContext` + cấu hình `RefreshToken` + `AddIdentityCore`

> Mục tiêu: đổi context của module (Day 2 tên `IdentityDbContext`, kế thừa `DbContext` trơn) sang kế thừa lớp base Identity, đồng thời **đổi tên nó thành `IdentityModuleDbContext`**, cấu hình quan hệ `RefreshToken`, và đăng ký dịch vụ Identity trong DI. Sau bước này migration ở [Bước 4](04-migration.md) mới sinh ra được schema đầy đủ.
>
> Lưu ý mentor: DbContext + đăng ký DI là code, **mình không viết hộ**. Mình mô tả cần đổi gì; bạn tự gõ.

---

## 3.1. Cái gì

Ba việc trong project `EventHub.Identity.Infrastructure`:

1. Đổi context của bạn (Day 2 tên `IdentityDbContext`, ở `Persistence/`) sang kế thừa `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` của package Identity thay vì `DbContext`, và **đổi tên class + file thành `IdentityModuleDbContext`** (kèm factory `IdentityModuleDbContextFactory`).
2. Trong `OnModelCreating`: gọi `base.OnModelCreating(builder)` **trước**, rồi khai `DbSet<RefreshToken>` + cấu hình quan hệ 1-n `ApplicationUser`↔`RefreshToken`.
3. Trong `DependencyInjection.AddInfrastructure`: đăng ký Identity qua `AddIdentityCore<ApplicationUser>()` + `AddRoles<ApplicationRole>()` + `AddEntityFrameworkStores<IdentityModuleDbContext>()`.

> **Ranh giới:** `ApplicationUser`/`ApplicationRole` và `IdentityModuleDbContext` cùng ở **Infrastructure** ([Quyết định 2](00-tong-quan.md)). DbContext thấy chúng trực tiếp. `RefreshToken` ở **Domain**, nên Infrastructure phải reference project `EventHub.Identity.Domain` (thêm project reference nếu chưa có) rồi `using` namespace Domain để thấy `RefreshToken`. Chiều Infrastructure → Domain là **đúng**. Domain **không** biết gì về DbContext hay `ApplicationUser`.

## 3.2. Vì sao

**Vì sao kế thừa base Identity:** chính việc `IdentityModuleDbContext` kế thừa `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` là thứ khiến EF "nhìn thấy" 7 entity Identity và sinh 7 bảng `AspNet*` khi migration. `DbContext` trơn (Day 2) không biết gì về Identity nên migration rỗng.

**Vì sao `base.OnModelCreating(builder)` phải gọi TRƯỚC:** lớp base cấu hình toàn bộ mapping Identity (khóa, index normalized username/email, tên bảng, concurrency stamp...) *bên trong* `OnModelCreating` của nó. EF theo luật **last-one-wins**: cái gọi sau ghi đè cái gọi trước. Bạn muốn base dựng nền trước, rồi *mình thêm/chỉnh* lên trên (cấu hình `RefreshToken`). Nếu gọi `base` **sau** cấu hình custom, base sẽ ghi đè phần của bạn. Nếu **quên** gọi `base`, EF không cấu hình các bảng Identity → migration thiếu bảng.

**Vì sao `AddIdentityCore` (không phải `AddIdentity`):** Day 4 xác thực bằng **JWT**, không dùng cookie/giao diện Razor của Identity. `AddIdentity` (và `AddDefaultIdentity`) kéo thêm cookie authentication + UI mặc định, thừa và gây nhiễu scheme khi bạn tự cấu hình JwtBearer. `AddIdentityCore<ApplicationUser>()` chỉ nạp phần lõi (`UserManager`, password hasher, validators), rồi bạn `.AddRoles<ApplicationRole>()` để có `RoleManager` và `.AddEntityFrameworkStores<IdentityDbContext>()` để store chạy trên EF. Gọn, đúng nhu cầu JWT.

> Dữ kiện: tài liệu Microsoft nêu rõ `AddDefaultIdentity` ≈ `AddAuthentication(cookies)` + `AddIdentityCore` + `AddDefaultUI`, tức phần cookie/UI là thứ `AddIdentityCore` **không** kéo theo. Nguồn: [customize-identity-model, mục AddDefaultIdentity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0#custom-user-data).

## 3.3. Dữ kiện đã xác minh

Theo [customize-identity-model (aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0):

- Context custom kế thừa `IdentityDbContext<TUser, TRole, TKey>`, điền `ApplicationUser`, `ApplicationRole`, `Guid`.
- Khi override `OnModelCreating`, **`base.OnModelCreating` phải gọi trước**, cấu hình custom gọi sau (EF *last-one-wins*).
- Cấu hình quan hệ 1-n dùng Fluent API: `HasMany`/`WithOne`/`HasForeignKey`.

## 3.4. Các bước làm

1. **Đổi lớp base + đổi tên context:** context của bạn (Day 2 đang `IdentityDbContext : DbContext(options)`) đổi thành kế thừa `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`, đồng thời **đổi tên class + file thành `IdentityModuleDbContext`**. Constructor nhận `DbContextOptions<IdentityModuleDbContext>` và chuyền xuống base.
   - ⚠️ **Vì sao phải đổi tên:** nếu giữ tên `IdentityDbContext`, class của bạn trùng tên hệt lớp base `IdentityDbContext<…>` (namespace `Microsoft.AspNetCore.Identity.EntityFrameworkCore`) → trình biên dịch dễ hiểu thành "class kế thừa chính nó", lỗi khó đọc. Đổi tên thành `IdentityModuleDbContext` **né hẳn** trùng tên (khỏi phải qualify full namespace hay `using` alias), lại nói rõ "đây là DbContext của module Identity" — đọc code dễ hơn. Nhớ đổi cả file factory sang `IdentityModuleDbContextFactory` cho khớp.
2. **`OnModelCreating`:** gọi `base.OnModelCreating(builder)` ở **dòng đầu**. Sau đó khai `DbSet<RefreshToken>` (thuộc tính trên context) và cấu hình quan hệ 1-n. Vì `RefreshToken` (Domain) **không** cầm navigation trỏ ngược `ApplicationUser` (Infrastructure) ([Quyết định 2](00-tong-quan.md)), cấu hình quan hệ **một phía**: từ `ApplicationUser` `HasMany<RefreshToken>()` … `WithOne()` (để **trống**, không có navigation ngược) … `HasForeignKey(rt => rt.UserId)` … `IsRequired`. (Nếu bạn *không* thêm navigation `ICollection<RefreshToken>` trên `ApplicationUser` thì khai từ phía `RefreshToken`: `Entity<RefreshToken>().HasOne<ApplicationUser>().WithMany()...`, cũng một phía, FK vẫn `UserId`.) Cân nhắc thêm index trên `RefreshToken.TokenHash` (tra cứu nhanh khi verify token ở Day 4).
3. **Đăng ký DI trong `AddInfrastructure`:** sửa `AddDbContext<IdentityModuleDbContext>(UseNpgsql(...))` (đổi generic theo tên mới), rồi nối chuỗi `AddIdentityCore<ApplicationUser>()` → `.AddRoles<ApplicationRole>()` → `.AddEntityFrameworkStores<IdentityModuleDbContext>()`.
4. **Design-time factory:** đổi factory Day 2 sang `IdentityModuleDbContextFactory` (khớp tên context mới). Vì Day 3 **không** đặt option ảnh hưởng model (không đụng `MaxLengthForKeys`/`SchemaVersion`), factory hiện tại vẫn dùng được nguyên. Nếu sau này bạn có đặt các option đó, phải áp cùng cấu hình ở design-time (hoặc để `dotnet ef` mượn cấu hình host qua `--startup-project` như Day 2).

## 3.5. Kiểm chứng

```bash
dotnet build EventHub.slnx
```

Build xanh nghĩa là context kế thừa hợp lệ, generic khớp `Guid`, quan hệ cấu hình đúng cú pháp, và đã xử lý trùng tên. (Bảng vẫn chưa có, migration ở [Bước 4](04-migration.md).)

## 3.6. Cạm bẫy thường gặp

- **Trùng tên với base (bẫy đặc trưng Day 3):** nếu giữ nguyên tên `IdentityDbContext`, class trùng tên base → lỗi biên dịch khó hiểu kiểu "circular base class". Ở đây ta né bằng cách **đổi tên context thành `IdentityModuleDbContext`** (mục 3.4 bước 1); ai muốn giữ tên cũ thì phải qualify full namespace hoặc `using` alias cho base.
- **Quên `base.OnModelCreating(builder)` hoặc gọi sai thứ tự:** quên → migration thiếu 7 bảng Identity. Gọi *sau* cấu hình custom → base ghi đè cấu hình của bạn.
- **Generic lệch kiểu khóa:** `<…, Guid>` nhưng entity khai `string` → không biên dịch.
- **Dùng `AddIdentity`/`AddDefaultIdentity` thay vì `AddIdentityCore`:** kéo cookie scheme thừa, dễ đụng độ khi cấu hình JwtBearer ở Day 4.

## 3.7. Ba bẫy dễ dính nhất

Nếu chỉ nhớ ba thứ:

1. **Giữ nguyên tên `IdentityDbContext`** — trùng tên lớp base, lỗi biên dịch "circular base class". Đổi thành `IdentityModuleDbContext`.
2. **Quên `base.OnModelCreating(builder)` hoặc gọi sau cấu hình custom** — quên thì migration thiếu 7 bảng Identity; gọi sau thì base ghi đè cấu hình `RefreshToken` của bạn (EF last-one-wins). Gọi base **dòng đầu**.
3. **Dùng `AddIdentity`/`AddDefaultIdentity` thay `AddIdentityCore`** — kéo cookie scheme + UI thừa, đụng độ khi cấu hình JwtBearer ở Day 4.

## 3.8. Góc kể khi phỏng vấn

*"Kế thừa `IdentityDbContext<…, Guid>` để EF tự sinh schema Identity; tôi thêm `RefreshToken` là bảng của mình, cấu hình 1-n bằng Fluent API **sau khi** gọi `base.OnModelCreating`. Vì EF last-one-wins, gọi base trước để nó dựng nền rồi mình chỉnh lên trên. Tôi chọn `AddIdentityCore` thay vì `AddIdentity` vì auth qua JWT nên không cần lớp cookie/UI."*

## 3.9. Link tài liệu chính thức

- [Customize the model (base context types & OnModelCreating)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0#customize-the-model)
- [Configure ASP.NET Core Identity (`AddIdentityCore` vs `AddIdentity`)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0)
- [EF Core Relationships (one-to-many)](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-many)

## 3.10. Xong bước này khi

- [x] Context đổi tên thành `IdentityModuleDbContext`, kế thừa `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` (né trùng tên base), factory đổi theo.
- [x] `OnModelCreating` gọi `base` **trước**, rồi cấu hình `RefreshToken` (DbSet + quan hệ 1-n + index TokenHash).
- [x] `AddInfrastructure` đăng ký `AddIdentityCore`/`AddRoles`/`AddEntityFrameworkStores<IdentityModuleDbContext>`.
- [x] `dotnet build` xanh.

→ Sang [Bước 4. Sinh & áp migration](04-migration.md).
