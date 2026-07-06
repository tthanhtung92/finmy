# Bước 3. Register, Login & phát refresh token

> Mục tiêu: mở rộng `IIdentityService` với các thao tác user thật, viết `AuthService` điều phối, và hai endpoint `POST /identity/register` + `POST /identity/login`. Cuối bước: đăng ký được user, đăng nhập trả **access token + refresh token thật**, cột mốc "đăng nhập trả JWT" của Day 4.
>
> Nhắc: không dùng `Result<T>` (Day 5). Endpoint map lỗi bằng tay sang `Results.Problem`/`Results.Unauthorized`. Code C# bạn tự gõ.

---

## 3.1. Cái gì

Bốn mảnh:

1. **Mở rộng `IIdentityService`** (interface Application, impl Infrastructure) các thao tác surface-primitive: đăng ký user (email + password → `userId` hoặc danh sách lỗi), kiểm mật khẩu (email + password → `userId?`), lấy roles của user (`userId` → `IReadOnlyList<string>`), và tạo refresh token cho user (`userId` + IP → chuỗi refresh token thô + lưu hash vào DB).
2. **`AuthService`** (Application) với `RegisterAsync` và `LoginAsync`, ghép `IIdentityService` + `IJwtTokenGenerator`, trả DTO `AuthResult` (access token, refresh token, hạn access token).
3. **DTO request/response** (Application): `RegisterRequest`, `LoginRequest`, `AuthResult`, record đơn giản, primitive.
4. **Endpoints** (Api, trong `IdentityModule.MapEndpoints`): `POST /identity/register`, `POST /identity/login` gọi `AuthService`, map kết quả sang HTTP.

## 3.2. Vì sao

**Vì sao `AuthService` điều phối, không nhồi vào endpoint:** login là **chuỗi** thao tác (kiểm mật khẩu → lấy roles → phát access token → sinh + lưu refresh token → gói lại). Nhồi hết vào lambda endpoint khiến endpoint dài, khó test, trộn HTTP với nghiệp vụ. Tách `AuthService` (Application) cho endpoint mỏng (chỉ nhận request, gọi service, map response) và logic auth ở tầng đúng của nó.

**Vì sao Identity thao tác qua `IIdentityService` chứ không để `AuthService` cầm `UserManager`:** `UserManager<ApplicationUser>` sống ở Infrastructure. Nếu `AuthService` (Application) inject nó, Application phải reference Infrastructure, phá ranh giới Day 3. `AuthService` chỉ biết abstraction; `IdentityService` (Infrastructure) mới cầm `UserManager`.

**Vì sao kiểm mật khẩu bằng `UserManager.CheckPasswordAsync`:** Identity hash mật khẩu bằng PBKDF2 + salt lúc đăng ký; `CheckPasswordAsync` hash lại input và so đúng cách, không bao giờ lộ hash. Bạn **không tự** so mật khẩu.

**Vì sao refresh token là chuỗi ngẫu nhiên, KHÔNG phải JWT:** refresh token chỉ cần **khó đoán** và **tra được trong DB để thu hồi**. Nó không cần tự chứa claim. Một chuỗi ngẫu nhiên đủ dài (từ RNG mật mã) là đủ; nó được **lưu hash** trong bảng `RefreshTokens` (giống mật khẩu, không lưu bản thô).

**Vì sao lưu hash refresh token, không lưu thô:** nếu DB rò rỉ, token thô = chiếm tài khoản ngay. Token **đã hash** thì kẻ tấn công không tái tạo được chuỗi thô để dùng. Khi client gửi refresh token lên (Bước 4), server hash rồi so với `TokenHash`. Đây đúng lý do cột tên là `TokenHash` (Day 3), không phải `Token`.

## 3.3. Dữ kiện đã xác minh

- **`UserManager<TUser>.CreateAsync(user, password)`** tạo user + hash mật khẩu; trả `IdentityResult` (có `Succeeded` + `Errors` gồm `Code`/`Description`). **`CheckPasswordAsync(user, password)`** trả `bool`. **`FindByEmailAsync`** / **`GetRolesAsync`** tra user/roles. Nguồn: [UserManager<TUser> (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1).
- **`AddIdentityCore` không đăng ký `SignInManager`** → không có lockout/2FA tự động. Dùng `CheckPasswordAsync` (không đếm lần sai). Muốn lockout thì thêm `.AddSignInManager()` và dùng `CheckPasswordSignInAsync`. Nguồn: [Identity configuration / AddIdentityCore](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0).
- **Sinh bytes ngẫu nhiên mật mã**: `System.Security.Cryptography.RandomNumberGenerator.GetBytes(int)` trả mảng byte ngẫu nhiên an toàn; encode base64url ra chuỗi refresh token. **Hash**: `SHA256.HashData(bytes)`. Nguồn: [RandomNumberGenerator.GetBytes](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.getbytes), [SHA256.HashData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256.hashdata).
- **Minimal API** đọc body JSON qua tham số kiểu request; trả `Results.Ok(...)`, `Results.Problem(...)`, `Results.Unauthorized()`, `Results.Conflict(...)`, `Results.ValidationProblem(...)`. Nguồn: [Minimal APIs: responses (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0).

## 3.4. Các bước làm

1. **DTO (Application):** tạo `RegisterRequest` (email, password), `LoginRequest` (email, password), `AuthResult` (accessToken, refreshToken, accessTokenExpiresAt). Record primitive, không type Identity.
2. **Mở rộng `IIdentityService` (Application):** thêm method cho: đăng ký (trả `userId` thành công hoặc lỗi, gợi ý trả một tuple/record nhỏ `(bool Succeeded, Guid? UserId, string[] Errors)` vì **chưa** có `Result<T>`), kiểm mật khẩu (trả `Guid?` userId nếu đúng), lấy roles (`Guid` → `IReadOnlyList<string>`), tạo refresh token (`Guid userId, string ip` → `string` refresh token thô, đồng thời lưu bản hash vào DB).
3. **Impl `IdentityService` (Infrastructure):** implement các method trên bằng `UserManager<ApplicationUser>` + `IdentityModuleDbContext`:
   - Đăng ký: dựng `ApplicationUser` (email/username), `CreateAsync(user, password)`; nếu thất bại, gói `IdentityResult.Errors` thành mảng string.
   - Kiểm mật khẩu: `FindByEmailAsync` → nếu null trả null; `CheckPasswordAsync` → đúng thì trả `user.Id`.
   - Roles: `FindByIdAsync` → `GetRolesAsync`.
   - Tạo refresh token: sinh bytes ngẫu nhiên → chuỗi thô (base64url) → **hash SHA-256** → tạo entity `RefreshToken` (Domain) với `UserId`, `TokenHash`, `CreatedAt`, `ExpiresAt` (giờ + hạn refresh), `CreatedByIp`; `Add` vào `DbContext`; `SaveChangesAsync`; trả **chuỗi thô** cho caller.
4. **`AuthService` (Application):**
   - `RegisterAsync(RegisterRequest)`: gọi Identity đăng ký; nếu lỗi trả kết quả lỗi cho endpoint; nếu ok, có thể tự đăng nhập luôn (phát token) hoặc chỉ trả `userId` (tùy bạn, xem Quyết định).
   - `LoginAsync(LoginRequest, ip)`: kiểm mật khẩu → nếu sai trả "thất bại"; nếu đúng: lấy roles → `IJwtTokenGenerator` phát access token → `IIdentityService` tạo refresh token → gói `AuthResult`.
5. **DI (Infrastructure):** đăng ký `IIdentityService` → `IdentityService`, và `AuthService` (Application), `AuthService` có thể đăng ký ở Infrastructure DI (nơi đã có `AddInfrastructure`) hoặc bạn tạo một `AddApplication` riêng cho Application. Mentor gợi ý đăng ký cùng `AddInfrastructure` cho Day 4 để bớt file; tách `AddApplication` là bước dọn về sau.
6. **Endpoints (Api):** trong `IdentityModule.MapEndpoints`, map `POST /identity/register` và `POST /identity/login`. Lấy IP client từ `HttpContext.Connection.RemoteIpAddress`. Gọi `AuthService`, rồi map:
   - Register ok → `Results.Ok` (hoặc `Results.Created`); trùng email/mật khẩu yếu → `Results.Conflict`/`Results.ValidationProblem` với danh sách lỗi.
   - Login đúng → `Results.Ok(authResult)`; sai email/mật khẩu → `Results.Unauthorized()`.

> **Bảo mật quan trọng, thông báo lỗi login mơ hồ:** khi login sai, **đừng** nói rõ "email không tồn tại" vs "sai mật khẩu". Trả **một** thông báo chung ("email hoặc mật khẩu không đúng") + 401. Nói rõ cái nào sai = giúp kẻ tấn công dò email nào có thật (user enumeration).

## 3.5. Quyết định của bạn

- **Register có tự đăng nhập luôn không?** Trả token ngay sau đăng ký tiện cho client (đỡ một vòng gọi `/login`). Hoặc chỉ trả `userId` và bắt client `/login` riêng, tách bạch hơn. Mentor khuyến nghị **trả 200/201 không kèm token** cho Day 4 (đơn giản, một luồng một việc); nối "auto-login" sau nếu muốn.
- **Bật lockout (chống brute-force) không?** `AddIdentityCore` không có `SignInManager` nên `CheckPasswordAsync` **không** đếm lần sai. Muốn khóa tài khoản sau N lần sai: thêm `.AddSignInManager()` (Bước 1 DI) và dùng `CheckPasswordSignInAsync(lockoutOnFailure: true)`. Mentor khuyến nghị **ghi nhận đây là hạn chế đã biết** cho Day 4, để lockout thành một cải tiến kể được ("tôi biết CheckPasswordAsync bỏ qua lockout, nâng cấp là dùng SignInManager"), làm hay không tùy quỹ thời gian.
- **Hạn refresh token:** gợi ý 7–30 ngày. Chọn một con số, đặt vào `JwtOptions` (thêm `RefreshTokenLifetimeDays`) cho cùng chỗ với các cấu hình token khác.

## 3.6. Kiểm chứng

```bash
dotnet build EventHub.slnx
dotnet run --project src/Bootstrap/EventHub.Api
```

Gọi thật (thay port cho đúng; dùng file `.http` hoặc `curl`):

```bash
curl -i -X POST http://localhost:5xxx/identity/register \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@eventhub.local","password":"Passw0rd!"}'

curl -i -X POST http://localhost:5xxx/identity/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@eventhub.local","password":"Passw0rd!"}'
```

- Register → 200/201; gọi lại lần hai cùng email → 409/400 (đã tồn tại).
- Login đúng → 200 + body có `accessToken` + `refreshToken`. Login sai mật khẩu → 401 với **cùng** thông báo như sai email.
- Dán `accessToken` vào [jwt.io](https://jwt.io): payload có `sub`, `email`; `iss`/`aud` khớp `JwtOptions`; dán khóa vào thì chữ ký **verify** xanh.
- Kiểm DB: bảng `RefreshTokens` có một dòng, cột `TokenHash` là **hash** (không phải chuỗi bạn nhận ở response).

```bash
docker compose --env-file .env -f docker/docker-compose.yml exec postgres \
  psql -U <user> -d <db> -c 'SELECT "Id","UserId","ExpiresAt","RevokedAt" FROM "RefreshTokens";'
```

## 3.7. Cạm bẫy thường gặp

- **Lộ user enumeration.** Thông báo login sai phải mơ hồ + luôn 401. Xem mục 3.4.
- **Trả refresh token thô nhưng lưu thô luôn.** Phải lưu **hash**, trả **thô**. Nhầm hai cái = hoặc lưu thô (rò DB là mất), hoặc trả hash (client không dùng được).
- **`AuthService` inject `UserManager`.** Sai ranh giới, `AuthService` (Application) chỉ được biết `IIdentityService`/`IJwtTokenGenerator`. `UserManager` chỉ trong `IdentityService` (Infrastructure).
- **Không SaveChanges khi tạo refresh token.** Quên `SaveChangesAsync` → token không vào DB → Bước 4 refresh không tra thấy.
- **Password policy chặn lúc đăng ký mà không hiện lỗi.** Identity mặc định đòi mật khẩu có hoa/thường/số/ký tự đặc biệt, ≥ 6. Nếu register trả lỗi khó hiểu, đó là policy, map `IdentityResult.Errors` ra response để thấy lý do.
- **Đọc IP sai.** `RemoteIpAddress` có thể null hoặc là IP proxy sau reverse proxy. Day 4 chỉ cần lưu lại để audit; đừng dựa vào nó cho bảo mật.

## 3.8. Góc kể khi phỏng vấn

*"Login tôi tách endpoint mỏng, chỉ nhận request, gọi AuthService, map response, còn AuthService điều phối chuỗi: kiểm mật khẩu qua IIdentityService, lấy roles, phát access token qua IJwtTokenGenerator, sinh refresh token. AuthService không hề thấy UserManager; nó ở Infrastructure sau IIdentityService. Refresh token tôi sinh bằng RNG mật mã, trả bản thô cho client nhưng chỉ lưu hash SHA-256 trong DB, như mật khẩu, rò DB không đồng nghĩa lộ token. Thông báo login sai tôi để mơ hồ và luôn 401 để không lộ email nào có thật."*

## 3.9. Link tài liệu chính thức

- [UserManager<TUser>](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1)
- [Introduction to Identity on ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [RandomNumberGenerator.GetBytes](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.getbytes) · [SHA256.HashData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256.hashdata)
- [Minimal APIs: Create responses](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0)

## 3.10. Xong bước này khi

- [x] `IIdentityService` mở rộng (đăng ký, kiểm mật khẩu, roles, tạo refresh token); impl ở Infrastructure cầm `UserManager` + `DbContext`.
- [x] `AuthService` (Application) ghép `IIdentityService` + `IJwtTokenGenerator`, trả `AuthResult`; **không** thấy `UserManager`.
- [x] `POST /identity/register` tạo user; trùng email → lỗi rõ ràng.
- [x] `POST /identity/login` đúng → access + refresh token; sai → 401 thông báo mơ hồ.
- [x] Access token verify được ở jwt.io; `RefreshTokens` lưu **hash**.
- [x] `dotnet build` xanh.

→ Sang [Bước 4. Refresh (rotation) & thu hồi token](04-refresh-revoke.md).
