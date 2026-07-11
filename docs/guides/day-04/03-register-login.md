# Bước 3. Register, Login & phát refresh token

> Mục tiêu: mở rộng `IIdentityService` với các thao tác user thật, viết `AuthService` điều phối, và hai endpoint `POST /identity/register` + `POST /identity/login`. Cuối bước: đăng ký được user, đăng nhập trả **access token + refresh token thật**, cột mốc "đăng nhập trả JWT" của Day 4.
>
> Nhắc: không dùng `Result<T>` (Day 5). Endpoint map lỗi bằng tay sang `Results.Problem`/`Results.Unauthorized`. Code C# bạn tự gõ.

---

## 3.1. Cái gì

Bốn mảnh:

1. **Mở rộng `IIdentityService`** (interface Application, impl Infrastructure) các thao tác surface-primitive: đăng ký user (email + password → record `RegisterOutcome` gói `userId` hoặc danh sách lỗi), kiểm mật khẩu (email + password → `userId?`), lấy roles của user (`userId` → `IReadOnlyList<string>`), và tạo refresh token cho user (`userId` + IP → chuỗi refresh token thô + lưu hash vào DB).
2. **`AuthService`** (Application) với `RegisterAsync` và `LoginAsync`, ghép `IIdentityService` + `IJwtTokenGenerator`, trả DTO `AuthResult` (access token, refresh token, hạn access token).
3. **DTO request/response** (Application): `RegisterRequest`, `LoginRequest`, `AuthResult` đã có từ trước; bước này thêm record `RegisterOutcome`.
4. **Endpoints** (Api, trong `IdentityModule.MapEndpoints`): `POST /identity/register`, `POST /identity/login` gọi `AuthService`, map kết quả sang HTTP.

## 3.2. Vì sao

**Vì sao `AuthService` điều phối, không nhồi vào endpoint:** login là một **chuỗi** thao tác (kiểm mật khẩu → lấy roles → phát access token → sinh và lưu refresh token → gói lại). Nhồi hết vào lambda endpoint khiến endpoint dài, khó test, trộn HTTP với nghiệp vụ. Tách `AuthService` (Application) cho endpoint mỏng (chỉ nhận request, gọi service, map response) và logic auth ở đúng tầng của nó.

**Vì sao Identity thao tác qua `IIdentityService` chứ không để `AuthService` cầm `UserManager`:** `UserManager<ApplicationUser>` sống ở Infrastructure. Nếu `AuthService` (Application) inject nó, Application phải reference Infrastructure, phá ranh giới Day 3. `AuthService` chỉ biết abstraction; `IdentityService` (Infrastructure) mới cầm `UserManager`.

**Vì sao kiểm mật khẩu bằng `UserManager.CheckPasswordAsync`:** Identity hash mật khẩu bằng PBKDF2 + salt lúc đăng ký; `CheckPasswordAsync` hash lại input và so đúng cách, không bao giờ lộ hash. Bạn **không tự** so mật khẩu.

**Vì sao refresh token là chuỗi ngẫu nhiên, KHÔNG phải JWT:** refresh token chỉ cần **khó đoán** và **tra được trong DB để thu hồi**. Nó không cần tự chứa claim. Một chuỗi ngẫu nhiên đủ dài (từ RNG mật mã) là đủ; nó được **lưu hash** trong bảng `RefreshTokens` (giống mật khẩu, không lưu bản thô).

**Vì sao lưu hash refresh token, không lưu thô:** nếu DB rò rỉ, token thô là chiếm tài khoản ngay. Token **đã hash** thì kẻ tấn công không tái tạo được chuỗi thô để dùng. Khi client gửi refresh token lên (Bước 4), server hash rồi so với `TokenHash`. Đây đúng lý do cột tên là `TokenHash` (Day 3), không phải `Token`.

## 3.3. Dữ kiện đã xác minh

- **`UserManager<TUser>.CreateAsync(user, password)`** tạo user + hash mật khẩu; trả `IdentityResult` (có `Succeeded` + `Errors` gồm `Code`/`Description`). **`CheckPasswordAsync(user, password)`** trả `bool`. **`FindByEmailAsync`** / **`FindByIdAsync`** / **`GetRolesAsync`** tra user/roles. Nguồn: [UserManager\<TUser\> (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1).
- **`AddIdentityCore` không đăng ký `SignInManager`** → không có lockout/2FA tự động. Dùng `CheckPasswordAsync` (không đếm lần sai). Muốn lockout thì thêm `.AddSignInManager()` và dùng `CheckPasswordSignInAsync`. Nguồn: [Identity configuration / AddIdentityCore](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0).
- **Sinh bytes ngẫu nhiên mật mã**: `System.Security.Cryptography.RandomNumberGenerator.GetBytes(int)` trả mảng byte ngẫu nhiên an toàn; encode base64url ra chuỗi refresh token. **Hash**: `SHA256.HashData(bytes)`. Nguồn: [RandomNumberGenerator.GetBytes](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.getbytes), [SHA256.HashData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256.hashdata).
- **Minimal API** đọc body JSON qua tham số kiểu request; trả `Results.Ok(...)`, `Results.Problem(...)`, `Results.Unauthorized()`, `Results.Conflict(...)`, `Results.ValidationProblem(...)`. Nguồn: [Minimal APIs: responses (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0).

## 3.4. Điểm xuất phát (code đang có)

Trước khi gõ, biết mình đang đứng đâu. Sau Bước 1 và 2, module Identity đã có:

- `Application/Authentication/IIdentityService.cs` — interface **rỗng**, chờ bước này điền.
- `Application/Authentication/IJwtTokenGenerator.cs` — `string GenerateToken(string userId, string email, IEnumerable<string> roles)`.
- `Application/DTO/` — `RegisterRequest(string Email, string Password)`, `LoginRequest(string Email, string Password)`, `AuthResult(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt)`. Cả ba là positional record (property đã `init`-only sẵn).
- `Infrastructure/Authentication/` — `JwtTokenGenerator`, `JwtOptions`, `IdentityClaimTypes` (đã `internal`).
- `Infrastructure/DependencyInjection.cs` — `AddInfrastructure` đã cấu hình DbContext, `AddIdentityCore`, JWT bearer, đăng ký `IJwtTokenGenerator` (Scoped).
- `Infrastructure/Persistence/IdentityModuleDbContext.cs` — có sẵn `DbSet<RefreshToken> RefreshTokens`.
- `Domain/Identity/RefreshToken.cs` — entity với `Id`, `UserId`, `TokenHash`, `ExpiresAt`, `CreatedAt` (kiểu `DateTimeOffset`), `RevokedAt`, `ReplacedByTokenHash`, `CreatedByIp`.
- `Api/IdentityModule.cs` — `MapEndpoints` mới có mỗi `/identity/ping`.

Bước này **thêm mới**: record `RegisterOutcome`, class `IdentityService`, class `AuthService`, hai route; và **sửa**: `IIdentityService` (thêm method), `JwtOptions` (thêm hạn refresh), `AddInfrastructure` (đăng ký thêm), `MapEndpoints` (thêm route).

## 3.5. Sơ đồ trace một request `/login`

```mermaid
sequenceDiagram
    participant Client
    participant Endpoint as Endpoint (Api, mỏng)
    participant Auth as AuthService (Application)
    participant Id as IIdentityService (Infra impl)
    participant Jwt as IJwtTokenGenerator (Infra impl)
    Client->>Endpoint: POST /identity/login { email, password }
    Endpoint->>Auth: LoginAsync(request, ip)
    Auth->>Id: VerifyPasswordAsync(email, password)
    Note right of Id: Infra: UserManager.CheckPasswordAsync
    Id-->>Auth: Guid? userId
    alt userId == null (sai credentials)
        Auth-->>Endpoint: null
        Endpoint-->>Client: 401 Results.Unauthorized()
    else userId hợp lệ
        Auth->>Id: GetRolesAsync(userId)
        Id-->>Auth: roles
        Auth->>Jwt: GenerateToken(userId, email, roles)
        Jwt-->>Auth: access token
        Auth->>Id: CreateRefreshTokenAsync(userId, ip)
        Note right of Id: Infra: RNG + hash + DbContext
        Id-->>Auth: refresh token (thô)
        Auth-->>Endpoint: AuthResult(accessToken, refreshToken, expiresAt)
        Endpoint-->>Client: 200 Results.Ok(authResult)
    end
```

**Ranh giới cốt tử:** `AuthService` (Application) **không bao giờ** thấy `UserManager`, `IdentityResult`, hay `JwtOptions` — cả ba ở Infrastructure. `AuthService` chỉ biết `IIdentityService` và `IJwtTokenGenerator`. Đây là điểm dễ phá nhất, cũng là điểm đáng kể khi phỏng vấn.

## 3.6. Các bước làm (thứ tự build từ trong ra ngoài)

Làm từ **mảnh phụ thuộc ít nhất** ra ngoài. **Build xanh xong một mảnh mới sang mảnh sau** — lỗi lộ ra sớm, dễ khoanh vùng.

### Mảnh 1 — record `RegisterOutcome` (Application)

Kết quả đăng ký. Day 4 chưa có `Result<T>` nên gói `(thành công?, userId, lý do lỗi, danh sách lỗi)` vào một record.

- Chữ ký: `record RegisterOutcome(bool Succeeded, Guid? UserId, RegisterFailureReason Reason, string[] Errors)`. Khi fail, `UserId` phải là `null` (không phải `Guid.Empty` — `Guid?` mà trả guid toàn-0 thì caller check `is null` hiểu sai).
- File: `RegisterOutcome.cs` đặt ở `src/Modules/Identity/EventHub.Identity.Application/Authentication/`, **cạnh interface** — không để ở `DTO/`, vì đây là kết quả nội bộ của thao tác auth, không phải request/response HTTP. Nó **không** phải `IdentityResult` (`IdentityResult` là type Infra, không được lộ lên Application).
- Kèm enum `RegisterFailureReason { None, DuplicateEmail, WeakPassword, Unknown }` (cùng thư mục Application) để endpoint chọn HTTP status theo *loại* lỗi thay vì gộp hết một mã. Vì sao cần: `string[] Errors` chỉ là mô tả, mất thông tin phân loại; enum cho endpoint map `DuplicateEmail → 409`, `WeakPassword → 400`. Ai điền enum: xem Mảnh 3.

### Mảnh 2 — mở rộng `IIdentityService` (Application)

Interface đang rỗng. Thêm 4 method, **chữ ký toàn primitive/record của Application** (không `ApplicationUser`, không `IdentityResult` — hai type đó ở Infra, lộ lên là phá ranh giới):

- `Task<RegisterOutcome> RegisterUserAsync(string email, string password)`
- `Task<Guid?> VerifyPasswordAsync(string email, string password)` — `Guid?`: userId nếu credentials đúng, `null` nếu sai. Gọn hơn ném exception; endpoint chỉ cần đúng/sai.
- `Task<IReadOnlyList<string>> GetRolesAsync(Guid userId)`
- `Task<string> CreateRefreshTokenAsync(Guid userId, string ip)` — trả token **thô** cho caller, tự lưu bản **hash** vào DB.

### Mảnh 3 — impl `IdentityService` (Infrastructure) — file mới

`IdentityService : IIdentityService`, file `IdentityService.cs` ở `src/Modules/Identity/EventHub.Identity.Infrastructure/Authentication/` (cạnh `JwtTokenGenerator`). Inject qua primary constructor: `UserManager<ApplicationUser>`, `IdentityModuleDbContext`, `TimeProvider`.

- **`RegisterUserAsync`**: dựng `ApplicationUser` với `Email` + `UserName` (đặt `UserName = email` cho đơn giản; Identity đòi có `UserName`). Gọi `userManager.CreateAsync(user, password)`. Thành công → `new RegisterOutcome(true, user.Id, RegisterFailureReason.None, [])`. Thất bại → `new RegisterOutcome(false, null, <reason>, result.Errors.Select(e => e.Description).ToArray())`. `IdentityResult` chỉ xuất hiện nội bộ Infra, không leak ra interface.
- **Suy `Reason` từ `IdentityResult` — làm ở Infra, không leak:** `IdentityResult.Errors` là các `IdentityError` có `Code` (string) + `Description`. **Không có enum** cho code — `Code` chính là `nameof` method của `IdentityErrorDescriber`, nên so `e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)` là cách chính chủ (dùng `nameof` làm source of truth thay literal `"DuplicateEmail"`; so `==`/`Array.Contains` ordinal, **không** `string.Contains`). Gom về một extension `internal static RegisterFailureReason ToRegisterFailureReason(this IdentityResult result)`: có code `DuplicateEmail`/`DuplicateUserName` → `DuplicateEmail`; có code nhóm `Password*` (`PasswordTooShort`, `PasswordRequiresDigit`, `PasswordRequiresUpper`, `PasswordRequiresLower`, `PasswordRequiresNonAlphanumeric`, `PasswordRequiresUniqueChars`) → `WeakPassword`; còn lại → `Unknown`. Đặt extension ở Infra (nơi `IdentityResult` sống); Application chỉ nhận enum, không hề thấy `IdentityError`.
- **`VerifyPasswordAsync`**: `userManager.FindByEmailAsync(email)`; null → trả `null`. Có user → `userManager.CheckPasswordAsync(user, password)` (không tự so hash, nó lo PBKDF2 + salt); đúng thì trả `user.Id`.
- **`GetRolesAsync`**: `userManager.FindByIdAsync(userId.ToString())` — **chú ý** `FindByIdAsync` nhận `string`, phải `.ToString()`. Rồi `userManager.GetRolesAsync(user)` trả `IList<string>`; `.ToList()` để khớp `IReadOnlyList<string>`.
- **`CreateRefreshTokenAsync`** (phần cryptographic, làm cẩn thận):
  1. `RandomNumberGenerator.GetBytes(32)` (hoặc 64) — static, trả `byte[]`. **KHÔNG** dùng `Random`/`Guid` (đoán được).
  2. Encode bytes → token thô: `WebEncoders.Base64UrlEncode(bytes)` (`Microsoft.AspNetCore.WebUtilities`) hoặc `Base64UrlEncoder.Encode(bytes)` (`Microsoft.IdentityModel.Tokens`). Đây là chuỗi trả cho caller.
  3. `SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))` → encode hash ra string (base64/hex). Đây là thứ lưu DB.
  4. Dựng entity `RefreshToken` (Domain): `Id = Guid.NewGuid()`, `UserId`, `TokenHash` = hash bước 3, `CreatedAt = timeProvider.GetUtcNow()`, `ExpiresAt = CreatedAt.AddDays(RefreshTokenLifetimeDays)`, `CreatedByIp = ip`. (`RevokedAt`/`ReplacedByTokenHash` để null, Bước 4 dùng.)
     - **Kiểu ngày**: `CreatedAt`/`ExpiresAt` là `DateTimeOffset`, `GetUtcNow()` trả đúng `DateTimeOffset` → khớp thẳng, **KHÔNG cần `.UtcDateTime`** (khác `AuthResult` bên Mảnh 4, xem gotcha ở đó).
  5. `dbContext.RefreshTokens.Add(entity)` → **`SaveChangesAsync()`**. Quên là Bước 4 tra không thấy.
  6. `return rawToken` (chuỗi ở bước 2).
  - Hạn refresh: thêm property `RefreshTokenLifetimeDays` (int) vào `JwtOptions`, chọn 7–30 ngày (xem Quyết định 3.7).

### Mảnh 4 — `AuthService` (Application) — file mới

Class thường (không cần interface cho Day 4), file `AuthService.cs` ở `src/Modules/Identity/EventHub.Identity.Application/Authentication/`. Inject `IIdentityService` + `IJwtTokenGenerator` (+ `TimeProvider` nếu bạn cần đóng dấu thời điểm).

- `Task<RegisterOutcome> RegisterAsync(RegisterRequest request)`: gọi `RegisterUserAsync`, trả thẳng `RegisterOutcome` cho endpoint map. Day 4 **không auto-login** (xem Quyết định 3.7).
- `Task<AuthResult?> LoginAsync(LoginRequest request, string ip)`: `VerifyPasswordAsync` → `null` thì trả `null` (login fail). Đúng thì: `GetRolesAsync` → `GenerateToken(userId.ToString(), request.Email, roles)` → `CreateRefreshTokenAsync(userId, ip)` → gói `AuthResult`.
  - **Gotcha `GenerateToken`**: nhận `userId` kiểu `string`, nhớ `.ToString()` (userId là `Guid`).
  - **Gotcha `AccessTokenExpiresAt`**: field này ở `AuthResult` là **`DateTime`** (không phải `DateTimeOffset`), nên nếu bạn tự tính thì phải `.UtcDateTime`. Nhưng thời hạn access token nằm trong `JwtOptions` (**Infrastructure**) — `AuthService` (Application) **không** được đọc `JwtOptions`, sẽ phá ranh giới. Đây là một **Quyết định của bạn** (3.7): cho `IJwtTokenGenerator` trả kèm thời điểm hết hạn thay vì để `AuthService` tự tính.

### Mảnh 5 — DI (`AddInfrastructure`)

Trong `DependencyInjection.AddInfrastructure`: `services.AddScoped<IIdentityService, IdentityService>()` và `services.AddScoped<AuthService>()`. Gộp vào `AddInfrastructure` cho Day 4 (bớt file); tách một `AddApplication` riêng là bước dọn về sau.

- **Vì sao Scoped**: `IdentityService` đụng `IdentityModuleDbContext` (đăng ký Scoped theo request); lifetime phải khớp, không được rộng hơn (Singleton ôm DbContext là bug captive dependency).

### Mảnh 6 — Endpoints (`IdentityModule.MapEndpoints`)

Hiện chỉ có `/identity/ping`. Thêm hai route, mỏng:

- `POST /identity/register`: tham số `(RegisterRequest req, AuthService svc)` — minimal API tự bind body + inject service. Gọi `RegisterAsync`. Guard success trước: `outcome.Succeeded` → `Results.Ok()`/`Results.Created(...)`. Fail thì **switch expression** trên `outcome.Reason`: `DuplicateEmail → Results.Conflict(outcome.Errors)` (409), `WeakPassword → Results.ValidationProblem(...)` (400), `_ → Results.Problem()`.
  - **Bẫy `Results.ValidationProblem`:** chữ ký nhận `IDictionary<string, string[]>`, **không** nhận `string[]` — bọc `outcome.Errors` vào một dictionary một entry (vd key `"password"`) trước khi truyền.
  - **Bẫy switch expression:** nhánh discard `_ =>` phải để **cuối**; đặt đầu thì mọi nhánh sau unreachable (compiler cảnh báo "pattern is unreachable").
- `POST /identity/login`: thêm tham số `HttpContext` để lấy IP (`ctx.Connection.RemoteIpAddress?.ToString()`). Gọi `LoginAsync`. Đúng → `Results.Ok(authResult)`; sai → `Results.Unauthorized()`.

**Naming — vì sao interface method là `RegisterUserAsync` còn `AuthService` là `RegisterAsync`:** hai type khác nhau nên tên có thể trùng mà không đụng compiler, nhưng đặt khác nhau (`RegisterUserAsync` cho tầng Identity, `RegisterAsync` cho tầng orchestration) để đọc code không lẫn "đang ở tầng nào".

## 3.7. Quyết định của bạn

- **`AuthService` lấy `AccessTokenExpiresAt` ở đâu?** Field ở `AuthResult` là `DateTime`, nhưng hạn token nằm trong `JwtOptions` (Infrastructure) mà Application không được chạm. Ba hướng: (a) **cho `IJwtTokenGenerator` trả kèm thời điểm hết hạn** — đổi return type sang một record nhỏ ví dụ `record AccessToken(string Value, DateTime ExpiresAt)`, vì generator vốn đã tính `Expires` bên trong; (b) client tự đọc claim `exp` trong JWT, `AuthResult` bỏ field; (c) đưa hạn token thành abstraction Application đọc được. Mentor khuyến nghị **(a)**: generator đã biết hạn, trả luôn là sạch nhất và không rò `JwtOptions` qua ranh giới. Lưu ý (a) **sửa lại interface của Bước 2** — cập nhật cả `JwtTokenGenerator` cho khớp.
- **Register có tự đăng nhập luôn không?** Trả token ngay sau đăng ký tiện cho client (đỡ một vòng gọi `/login`); hoặc chỉ trả `userId` rồi bắt client `/login` riêng, tách bạch hơn. Mentor khuyến nghị **trả 200/201 không kèm token** cho Day 4 (một luồng một việc); nối auto-login sau nếu muốn.
- **Bật lockout (chống brute-force) không?** `AddIdentityCore` không có `SignInManager` nên `CheckPasswordAsync` **không** đếm lần sai. Muốn khóa tài khoản sau N lần sai: thêm `.AddSignInManager()` (Bước 1 DI) và dùng `CheckPasswordSignInAsync(lockoutOnFailure: true)`. Mentor khuyến nghị **ghi nhận đây là hạn chế đã biết** cho Day 4, để lockout thành một cải tiến kể được ("tôi biết `CheckPasswordAsync` bỏ qua lockout, nâng cấp là `SignInManager`").
- **Hạn refresh token:** gợi ý 7–30 ngày. Chọn một con số, đặt vào `JwtOptions` (thêm `RefreshTokenLifetimeDays`) cho cùng chỗ với các cấu hình token khác.

## 3.8. Kiểm chứng

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

- Register lần 1 → 200/201; gọi lại cùng email → 409/400 (đã tồn tại).
- Login đúng → 200 + body có `accessToken` + `refreshToken`. Login sai mật khẩu → 401 với **cùng** thông báo như sai email.
- Dán `accessToken` vào [jwt.io](https://jwt.io): payload có `sub`, `email`; `iss`/`aud` khớp `JwtOptions`; dán khóa vào thì chữ ký **verify** xanh.
- Kiểm DB: bảng `RefreshTokens` có một dòng, cột `TokenHash` là **hash** (khác chuỗi refresh nhận ở response).

```bash
docker compose --env-file .env -f docker/docker-compose.yml exec postgres \
  psql -U <user> -d <db> -c 'SELECT "Id","UserId","ExpiresAt","RevokedAt" FROM "RefreshTokens";'
```

## 3.9. Cạm bẫy thường gặp

- **Lộ user enumeration.** Thông báo login sai phải mơ hồ và luôn 401. Phân biệt "email không tồn tại" với "sai mật khẩu" là giúp kẻ tấn công dò email nào có thật. Trả một thông báo chung ("email hoặc mật khẩu không đúng").
- **Trả refresh token thô nhưng lưu thô luôn.** Phải lưu **hash**, trả **thô**. Nhầm hai cái: hoặc lưu thô (rò DB là mất), hoặc trả hash (client không dùng được).
- **`AuthService` inject `UserManager` (hoặc `JwtOptions`).** Sai ranh giới. `AuthService` (Application) chỉ được biết `IIdentityService`/`IJwtTokenGenerator`. `UserManager` và `JwtOptions` chỉ trong Infrastructure.
- **Quên `SaveChangesAsync` khi tạo refresh token.** `Add` mà không `SaveChangesAsync` → token không vào DB → Bước 4 refresh không tra thấy.
- **Nhầm kiểu ngày.** `RefreshToken.CreatedAt/ExpiresAt` là `DateTimeOffset` (khớp `GetUtcNow()` thẳng), còn `AuthResult.AccessTokenExpiresAt` là `DateTime` (cần `.UtcDateTime`). Trộn hai kiểu là lỗi compile hoặc lệch offset.
- **Password policy chặn lúc đăng ký mà không hiện lỗi.** Identity mặc định đòi mật khẩu có hoa/thường/số/ký tự đặc biệt, ≥ 6. Nếu register trả lỗi khó hiểu, đó là policy: map `IdentityResult.Errors` (qua `RegisterOutcome.Errors`) ra response để thấy lý do.
- **Đọc IP sai.** `RemoteIpAddress` có thể null hoặc là IP proxy sau reverse proxy. Day 4 chỉ cần lưu lại để audit; đừng dựa vào nó cho bảo mật.

## 3.10. Ba bẫy dễ dính nhất

Nếu chỉ nhớ ba thứ:

1. **`AuthService` inject `UserManager`/`JwtOptions`** — phá ranh giới, hỏng điểm cốt lõi của project. Chỉ `IdentityService` (Infra) cầm.
2. **Lưu refresh token thô thay vì hash** (hoặc trả hash cho client). Nhớ: **trả thô, lưu hash**.
3. **Quên `SaveChangesAsync`** sau `Add` refresh token → DB rỗng → Bước 4 gãy.

## 3.11. Góc kể khi phỏng vấn

*"Login tôi tách endpoint mỏng, chỉ nhận request, gọi AuthService, map response, còn AuthService điều phối chuỗi: kiểm mật khẩu qua IIdentityService, lấy roles, phát access token qua IJwtTokenGenerator, sinh refresh token. AuthService không hề thấy UserManager hay JwtOptions; chúng ở Infrastructure sau abstraction. Refresh token tôi sinh bằng RNG mật mã, trả bản thô cho client nhưng chỉ lưu hash SHA-256 trong DB, như mật khẩu, rò DB không đồng nghĩa lộ token. Thông báo login sai tôi để mơ hồ và luôn 401 để không lộ email nào có thật."*

## 3.12. Link tài liệu chính thức

- [UserManager\<TUser\>](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1)
- [Introduction to Identity on ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [RandomNumberGenerator.GetBytes](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.getbytes) · [SHA256.HashData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256.hashdata)
- [Minimal APIs: Create responses](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0)

## 3.13. Xong bước này khi

- [x] `RegisterOutcome` (Application/Authentication/) + `IIdentityService` mở rộng đủ 4 method; impl `IdentityService` ở Infrastructure cầm `UserManager` + `DbContext` + `TimeProvider`.
- [x] `AuthService` (Application) ghép `IIdentityService` + `IJwtTokenGenerator`, trả `AuthResult`; **không** thấy `UserManager`/`JwtOptions`.
- [x] `POST /identity/register` tạo user; trùng email → lỗi rõ ràng.
- [x] `POST /identity/login` đúng → access + refresh token; sai → 401 thông báo mơ hồ.
- [x] Access token verify được ở jwt.io; `RefreshTokens` lưu **hash**.
- [x] `dotnet build` xanh.

→ Sang [Bước 4. Refresh (rotation) & thu hồi token](04-refresh-revoke.md).
