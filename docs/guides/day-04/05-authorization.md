# Bước 5. Role-based authorization

> Mục tiêu: seed vài role (`Admin`/`User`), gán role cho user, đảm bảo role đi vào JWT, rồi bảo vệ endpoint theo role. Cuối bước: user thường gọi endpoint chỉ Admin → **403**; Admin gọi → **200**; ai không đăng nhập → **401**.
>
> Nhắc: code C# bạn tự gõ. Mentor nói cơ chế + chỗ đặt; bạn viết.

---

## 5.1. Cái gì

Bốn mảnh:

1. **Seed role** (`Admin`, `User`) vào bảng `AspNetRoles` lúc khởi động, role phải **tồn tại** trước khi gán cho user.
2. **Gán role cho user**: vd user đầu tiên thành `Admin` (khi register hoặc bằng một seeder).
3. **Role vào JWT**: đảm bảo `IJwtTokenGenerator` (Bước 2) đưa roles của user thành role claim (đã làm; bước này kiểm lại nó chạy).
4. **Bảo vệ endpoint**: một endpoint demo `GET /identity/admin-only` yêu cầu role `Admin`; và bật yêu cầu đăng nhập cho endpoint cần bảo vệ.

## 5.2. Vì sao

**Vì sao phải seed role trước:** `AspNetRoles` trống thì không gán role được (`AddToRoleAsync` báo lỗi "role không tồn tại"). Role là dữ liệu tham chiếu, như danh mục, cần có sẵn khi hệ thống lên. Seed lúc startup đảm bảo môi trường mới (clone + `docker compose up`) tự có role, không cần bước tay.

**Vì sao role phải nằm trong JWT (không tra DB mỗi request):** access token là **self-contained**, middleware phân quyền chỉ đọc claim trong token, **không** tra DB. Nếu role không có trong token, `RequireRole("Admin")` thấy user không có role đó → 403, dù DB ghi user là Admin. Hệ quả quan trọng: **đổi role của user chỉ có hiệu lực ở access token kế tiếp** (sau khi refresh/đăng nhập lại), không tức thì. Đây là đánh đổi cố hữu của JWT stateless, biết nó là điểm cộng.

**Vì sao 401 khác 403:**

- **401 Unauthorized** = *"tôi chưa biết bạn là ai"*, không có token, token sai chữ ký, hết hạn. Vấn đề **xác thực**.
- **403 Forbidden** = *"tôi biết bạn là ai rồi, nhưng bạn không đủ quyền"*, token hợp lệ nhưng thiếu role/claim. Vấn đề **phân quyền**.

Nhầm hai cái là lỗi API design kinh điển. `RequireAuthorization()` cho ra 401 khi chưa xác thực; `RequireRole` cho ra 403 khi xác thực rồi mà thiếu role.

## 5.3. Dữ kiện đã xác minh

- **Seed role** bằng `RoleManager<ApplicationRole>.CreateAsync(new ApplicationRole { Name = "Admin" })` nếu `RoleExistsAsync` trả false. `RoleManager` được đăng ký nhờ `.AddRoles<ApplicationRole>()` (đã có ở DI Day 3). Nguồn: [RoleManager<TRole>](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.rolemanager-1).
- **Gán role**: `UserManager.AddToRoleAsync(user, "Admin")`. **Đọc role**: `UserManager.GetRolesAsync(user)` (dùng ở Bước 2/3 để nhét vào token). Nguồn: [UserManager<TUser>](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1).
- **Bảo vệ endpoint (minimal API)**: `endpoints.MapGet(...).RequireAuthorization()` yêu cầu đã đăng nhập; theo role: `.RequireAuthorization(policy => policy.RequireRole("Admin"))`, hoặc gắn `[Authorize(Roles = "Admin")]`. Nguồn: [Authorization in minimal APIs / RequireAuthorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0), [Minimal API auth](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-10.0).
- **Chạy seeder lúc startup**: tạo một `IServiceScope` từ `app.Services.CreateScope()` trong host, lấy `RoleManager`/`UserManager`, seed, trước `app.Run()`. Nguồn: [Dependency injection: resolving scoped services at startup](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0#scope-validation).

## 5.4. Các bước làm

1. **Seeder (Infrastructure):** viết một seeder (vd static class `IdentitySeeder` hoặc method mở rộng) nhận `RoleManager<ApplicationRole>` (và `UserManager` nếu seed cả Admin user): với mỗi role trong danh sách (`Admin`, `User`), nếu chưa tồn tại thì tạo. Tùy chọn: tạo một user Admin mặc định và `AddToRoleAsync(..., "Admin")` để có sẵn tài khoản Admin demo.
2. **Gọi seeder ở host:** trong `Program.cs`, sau `var app = builder.Build();` và **trước** `app.Run();`, mở một scope, resolve seeder, chạy nó (await). Đặt trước hoặc sau `UseModules` đều được, miễn trước `Run`.
3. **Đảm bảo role vào token:** kiểm lại `AuthService.LoginAsync` gọi `GetRolesAsync` và truyền vào `IJwtTokenGenerator` (Bước 2/3). Không có bước này thì token không có role.
4. **Endpoint demo (Api):** trong `IdentityModule.MapEndpoints`, thêm:
   - `GET /identity/me`, `.RequireAuthorization()` (bất kỳ ai đã đăng nhập); trả về vài claim đọc từ `HttpContext.User` (vd `sub`, `email`) để chứng minh danh tính dựng đúng.
   - `GET /identity/admin-only`, `.RequireAuthorization(p => p.RequireRole("Admin"))`; trả một thông báo.

> **Quyết định của bạn:** dùng `RequireRole` inline hay khai một **named policy** (vd `"AdminOnly"`) qua `AddAuthorizationBuilder().AddPolicy(...)` rồi `.RequireAuthorization("AdminOnly")`? Inline nhanh cho Day 4. Named policy gọn hơn khi nhiều endpoint dùng chung một luật và là hướng đi khi luật phức tạp hơn role đơn (claim, requirement tùy biến). Mentor khuyến nghị **inline cho Day 4**, chuyển named policy khi bắt đầu lặp.

## 5.5. Kiểm chứng

```bash
dotnet build EventHub.slnx
dotnet run --project src/Bootstrap/EventHub.Api
```

```bash
# 1) chưa đăng nhập → 401
curl -i http://localhost:5xxx/identity/admin-only

# 2) đăng nhập user thường, lấy access token AT_USER, rồi:
curl -i http://localhost:5xxx/identity/admin-only -H "Authorization: Bearer <AT_USER>"   # 403
curl -i http://localhost:5xxx/identity/me         -H "Authorization: Bearer <AT_USER>"   # 200

# 3) đăng nhập Admin, lấy AT_ADMIN:
curl -i http://localhost:5xxx/identity/admin-only -H "Authorization: Bearer <AT_ADMIN>"  # 200
```

- Không token → **401**. User thường vào admin-only → **403**. Admin → **200**. `/me` với token bất kỳ hợp lệ → **200** kèm claim.
- Kiểm DB: `AspNetRoles` có `Admin`/`User`; `AspNetUserRoles` có dòng gán Admin.
- Dán `AT_ADMIN` vào jwt.io: có claim role = `Admin`.

## 5.6. Cạm bẫy thường gặp

- **403 oan do lệch tên role claim (nối tiếp Bước 2).** Nếu token ghi role dưới tên `"role"` nhưng authorization đọc theo `ClaimTypes.Role`, `RequireRole` không thấy → Admin thật vẫn 403. Thống nhất tên role claim giữa lúc tạo (Bước 2) và lúc validate. Kiểm nhanh: jwt.io xem tên claim thực tế; nếu là `role` mà validate đòi khác, set `RoleClaimType`/`MapInboundClaims` cho khớp.
- **Seed lỗi vì chạy sai đời sống dịch vụ.** `RoleManager`/`UserManager` là **scoped**; resolve thẳng từ `app.Services` (root) sẽ ném lỗi scope. Phải `CreateScope()` rồi resolve trong scope đó.
- **Gán role trước khi role tồn tại.** `AddToRoleAsync` đòi role có sẵn. Seed role **trước** khi gán.
- **Quên `RequireAuthorization` → endpoint hớ hênh.** Endpoint không gắn gì thì **ai cũng vào**, kể cả không token. Với `AddIdentityCore` không có fallback policy mặc định, nên phải gắn `RequireAuthorization` cho từng endpoint cần bảo vệ (hoặc set fallback policy toàn cục nếu muốn "mặc định phải đăng nhập").
- **Kỳ vọng đổi role có hiệu lực ngay.** Đổi role trong DB không đổi token đang cầm. Chỉ có access token phát **sau đó** mới mang role mới. Nhớ giải thích được điều này.

## 5.7. Góc kể khi phỏng vấn

*"Phân quyền theo role của tôi dựa trên role claim trong JWT, nên middleware không tra DB mỗi request, đánh đổi là đổi role chỉ có hiệu lực ở access token kế tiếp. Tôi seed role lúc startup trong một scope riêng vì RoleManager là scoped. Tôi phân biệt rõ 401 (chưa xác thực, thiếu/sai token) với 403 (đã xác thực nhưng thiếu role). Một lỗi tôi lường trước là 403 oan khi tên role claim lúc phát lệch với RoleClaimType lúc validate, nên tôi thống nhất tên claim hai đầu."*

## 5.8. Link tài liệu chính thức

- [Role-based authorization in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0)
- [Authorization in minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-10.0)
- [RoleManager<TRole>](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.rolemanager-1) · [UserManager<TUser>](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1)
- [Mapping, customizing, transforming claims (RoleClaimType)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims?view=aspnetcore-10.0)

## 5.9. Xong bước này khi

- [x] Role `Admin`/`User` seed tự động lúc startup (scope đúng); có thể có sẵn một Admin user demo.
- [x] Access token của Admin mang role claim `Admin` (thấy trên jwt.io).
- [x] `GET /identity/admin-only`: không token → 401, user thường → 403, Admin → 200.
- [x] `GET /identity/me` với token hợp lệ → 200 kèm claim đọc từ `HttpContext.User`.
- [x] `dotnet build` xanh.

→ Sang [Bước 6. Verify end-to-end & commit](06-verify-commit.md).
