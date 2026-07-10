# Ghi chú & đính chính Day 4

> Các ghi chú dưới đây chốt những chỗ *dễ hiểu mơ hồ* nhất khi dựng `JwtTokenGenerator` + cấu hình `AddJwtBearer` (lộ ra trong lúc review code từng bước). Đây là các điểm "kể được khi phỏng vấn", không phải chỉ "chạy được". Bổ sung nếu các bước sau lộ thêm chỗ mơ hồ.

---

## Ghi chú 1. Tên claim: vì sao short-name JWT chứ không `ClaimTypes`, và vì sao `JwtRegisteredClaimNames` không có `Role`

**Hiểu sai thường gặp:** "`ClaimTypes.Sub`, `ClaimTypes.Email`, `ClaimTypes.Role` — cứ dùng bộ `ClaimTypes` cho tiện." Thực ra `ClaimTypes` không hề có member `sub`, còn `ClaimTypes.Email`/`.Role` là các **URI dài kiểu WS/SAML** (`http://schemas.xmlsoap.org/...`) — di sản thời WIF/.NET Framework, không phải tên claim của JWT.

**Đúng cơ chế:** JWT theo RFC 7519 dùng **tên ngắn**: `sub`, `email`, `jti`, `iat`, `exp`... Bộ hằng đúng là **`JwtRegisteredClaimNames`** (trong `Microsoft.IdentityModel.JsonWebTokens`), mỗi hằng chỉ là một chuỗi ngắn — `JwtRegisteredClaimNames.Sub` ≡ `"sub"`. Dùng nó để token gọn, đúng chuẩn, đọc trên jwt.io dễ.

Nhưng `JwtRegisteredClaimNames` **chỉ chứa các registered claim của chuẩn** — `Role` **không** nằm trong RFC 7519 nên không có ở đó. Role phải tự đặt tên. Chọn short-name `"role"` cho nhất quán với `sub`/`email`, đổi lại phải tự khai báo cho phía đọc token (xem Ghi chú 3).

**Một `Claim` là gì:** cặp `(type, value)` đều là string. Nhiều role của một user = **nhiều đối tượng `Claim` cùng type `"role"`**, khác value — *không* phải một claim chứa mảng. Thư viện tự gộp các claim cùng type thành JSON array khi serialize ra token.

- **Kiểm chứng:** `JwtTokenGenerator` dùng `IdentityClaimTypes.Sub/.Email/.Role`, không còn literal `"sub"/"email"/"role"` rải rác. Dán token lên jwt.io: payload có `sub`, `email` short-name và `role` là array khi user nhiều role.

**Câu chốt khi phỏng vấn:** *"Tôi phát claim theo tên chuẩn JWT (`sub`/`email`) qua `JwtRegisteredClaimNames`, không dùng `ClaimTypes` vì đó là URI WS/SAML dài, di sản WIF. `Role` không thuộc RFC 7519 nên tôi tự định một short-name `role`. Nhiều role là nhiều claim cùng type, thư viện gộp thành array khi serialize."*

---

## Ghi chú 2. `IdentityClaimTypes` làm source of truth: gộp bằng *tham chiếu*, không phải gõ lại literal

**Hiểu sai thường gặp:** "Gom hằng cho gọn thì viết `Sub = "sub"`, `Email = "email"`, `Role = "role"` trong một class là xong." Cách này **đẻ thêm magic string thứ hai** — `"sub"` bây giờ tồn tại hai nơi (class của bạn *và* ngầm trong thư viện), sai chính tả compiler không bắt.

**Đúng cơ chế — phân biệt "gộp tốt" vs "gộp xấu":**

- `Sub`/`Email` **đã có nhà** = `JwtRegisteredClaimNames` (hằng thư viện, không bao giờ drift). Trong `IdentityClaimTypes` chỉ **trỏ tới** nó: `Sub = JwtRegisteredClaimNames.Sub`. Đây là *alias*, không copy giá trị → zero magic string mới.
- `Role` **chưa có nhà** (thư viện không cung cấp) → buộc phải tự tạo `Role = "role"`. Đây mới là nơi literal được phép tồn tại — và là *lý do cả class này ra đời*.

Đặt trong `Authentication/` của Identity.Infrastructure. Ban đầu để `internal` khi chỉ có nơi phát (`JwtTokenGenerator`) và nơi đọc (`AddJwtBearer`) dùng, cùng assembly. Nhưng khi endpoint `/identity/me` ở **assembly Api** đọc claim bằng chính bộ hằng này, cross-assembly buộc đổi sang `public` (Api reference Infra trong cùng module — không phá ranh giới). Cứ để literal ở Api thì tái sinh magic string; đổi `public` giữ một source of truth duy nhất. **Không** đẩy lên `SharedKernel`/`Contracts`: tên claim là wire-format nội bộ module Identity, đẩy ra là rò rỉ ranh giới cross-module.

- **Kiểm chứng:** grep repo chuỗi `"sub"`, `"email"`, `"role"` → mỗi cái xuất hiện **đúng một lần**, đều trong `IdentityClaimTypes.cs`. `Sub`/`Email` là alias `JwtRegisteredClaimNames.*`, chỉ `Role` là literal.

**Câu chốt khi phỏng vấn:** *"Tên claim gom về một static class làm source of truth, dùng ở cả nơi phát lẫn nơi validate. `Role` là hằng tự định vì JWT không chuẩn hóa role; `Sub`/`Email` thì alias sang `JwtRegisteredClaimNames` thay vì gõ lại chuỗi — gộp một lookup surface mà không nhân đôi nguồn."*

---

## Ghi chú 3. Hai bug im lặng phía đọc token: `RoleClaimType` và `MapInboundClaims`

Đây là cặp lỗi nguy nhất Day 4 — **không** báo compile, **không** ném exception, chỉ sai lặng lẽ lúc chạy. Cả hai đến từ việc chọn short-name (Ghi chú 1) tức đã *bước ra khỏi đường ray mặc định* của ASP.NET Core, nên phải tự khai báo cho khớp.

**Bug A — quên `RoleClaimType`:** ta phát role dưới type `"role"`, nhưng mặc định ASP.NET Core tìm role ở `ClaimTypes.Role` (URI dài). Không nối → `[Authorize(Roles="Admin")]` và `User.IsInRole()` **luôn trượt**, user có role vẫn bị 403. Sửa: set `TokenValidationParameters.RoleClaimType = IdentityClaimTypes.Role`. Chính đây là *nơi thứ hai* dùng source of truth ở Ghi chú 2 — nếu literal ở hai file lệch nhau, đây là chỗ vỡ.

**Bug B — quên `MapInboundClaims = false`:** Microsoft giữ bảng `DefaultInboundClaimTypeMap` (di sản WIF) tự dịch tên claim ngắn → URI dài **khi đọc** token. Bật mặc định → token phát ra có `sub` gọn, nhưng lúc validate `sub` bị đổi thành `nameidentifier`, khiến `User.FindFirst("sub")` trả **null**. Tắt bằng `options.MapInboundClaims = false` (đặt trên `options`, *không* nằm trong `TokenValidationParameters`). Vì đã dùng `JwtRegisteredClaimNames` nhất quán cả phát lẫn đọc nên không dính bảng dịch nào.

- **Kiểm chứng (khi có endpoint):** token có role đúng → gọi `[Authorize(Roles=...)]` ra 200; sai role → 403; `User.FindFirst(IdentityClaimTypes.Sub)` khác null.

**Câu chốt khi phỏng vấn:** *"Chọn short-name JWT nghĩa là rời mặc định của ASP.NET Core, nên tôi khai báo `RoleClaimType` để authorization nhận đúng claim `role`, và tắt `MapInboundClaims` để handler không remap `sub` thành `nameidentifier`. Cả hai nếu quên đều fail lặng, test tay khó thấy."*

---

## Ghi chú 4. Fail-fast đối xứng: signing key phải ném ở *cả* phía phát lẫn phía đọc

**Hiểu sai thường gặp:** phía đọc viết `Encoding.UTF8.GetBytes(jwtOptions?.SigningKey ?? string.Empty)` — key thiếu thì rơi về **chuỗi rỗng** cho "an toàn khỏi null".

**Đúng cơ chế — đây là lỗi bảo mật, không phải null-safety:** validate token bằng **key rỗng** khiến việc kiểm chữ ký gần như vô nghĩa, token giả dễ qua. Phía phát (generator) *đã* ném khi `SigningKey` null; phía đọc lại nuốt lặng bằng `string.Empty` → **bất đối xứng nguy hiểm**. Sửa: rút key ra một biến, `?? throw` một lần ngay lúc khởi động — thiếu config thì app **không chạy** còn hơn chạy với chữ ký rỗng. Sau khi đã chặn null ở `jwtOptions`, các `jwtOptions?.` phía sau thành thừa (che mất ý định), bỏ `?.`.

**Câu chốt khi phỏng vấn:** *"Signing key thiếu thì tôi cho fail-fast lúc khởi động ở cả nơi phát lẫn nơi validate, không bao giờ fallback chuỗi rỗng — validate bằng key rỗng là lỗ bảo mật, không phải xử lý null."*

---

## Ghi chú 5. `TimeProvider` cho thời điểm hết hạn: bỏ `DateTime.UtcNow` để test được `exp`

**Hiểu sai thường gặp:** tính `Expires = DateTime.UtcNow.AddMinutes(...)` trực tiếp trong generator.

**Đúng cơ chế:** `DateTime.UtcNow` là phụ thuộc ngầm vào đồng hồ hệ thống → không viết được test xác định cho hạn token (không tua được thời gian). .NET có abstraction **`TimeProvider`**: inject vào constructor, gọi `_timeProvider.GetUtcNow()`. Chạy thật thì đăng ký `TimeProvider.System`; test thì bơm `FakeTimeProvider` để kiểm `exp` đúng đến từng phút, hoặc giả lập token đã hết hạn.

- **Kiểm chứng:** generator nhận `TimeProvider` qua primary constructor, không còn `DateTime.UtcNow`. DI đăng ký `TimeProvider.System`.

**Câu chốt khi phỏng vấn:** *"Tôi inject `TimeProvider` thay vì gọi thẳng `DateTime.UtcNow`, nên test bơm `FakeTimeProvider` kiểm được thời điểm hết hạn token một cách xác định — không phụ thuộc đồng hồ máy chạy test."*

---

## Ghi chú 6. DI: đăng ký ≠ resolve, và captive dependency (lộ ra khi vá lỗi startup `svc UNKNOWN`)

**Lỗi khởi động gặp thật:** `dotnet run` ném `Failure to infer one or more parameters` — bảng liệt kê `svc | UNKNOWN`. Không phải lỗi binding body: minimal API đọc chữ ký lambda `(RegisterRequest req, AuthService svc)`, hỏi DI "có `AuthService` không?", container trả không → nguồn `UNKNOWN`. Gốc rễ: quên `AddScoped<AuthService>()`. Minimal API **không** tự `new` một concrete class chưa đăng ký — chỉ nhận từ DI. Lỗi nổ lúc startup (không phải lúc gọi route) vì `AuthorizationMiddleware` build endpoint data source ngay khi host lên, quét mọi endpoint.

**Đăng ký ≠ resolve → thứ tự `Add...` không đổi đúng/sai:** `services.Add...` chỉ nhét descriptor vào một list. Không gì được resolve lúc đăng ký; resolve xảy ra **lazy** lúc runtime khi có ai request. Nên đăng ký `AuthService` trước cả dependency của nó vẫn chạy. Thứ tự chỉ đáng kể khi: đăng ký trùng cùng type (cái cuối thắng với `GetRequiredService`, giữ hết với `IEnumerable<T>`), hoặc `TryAdd`. Đừng lẫn với **middleware pipeline** (`UseAuthentication` → `UseAuthorization`) — chỗ đó thứ tự cực quan trọng, nhưng đó là pipeline HTTP, không phải service registration.

**Lifetime — cái nào singleton được, cái nào bắt buộc scoped:**

- `JwtOptions`, `TimeProvider.System` = **bất biến / stateless / thread-safe** → singleton an toàn.
- `DbContext`, `UserManager` = **có state theo request, không thread-safe** → bắt buộc scoped. Chúng **đã** được đăng ký sẵn (scoped) bởi `AddDbContext` / `AddIdentityCore` — không tự đăng ký lại, và **tuyệt đối không** `AddSingleton`.
- **Captive dependency:** một Singleton không được phụ thuộc một Scoped — scoped bị "giam" trong singleton, sống mãi thay vì chết cuối request → container ném lỗi (hoặc sai âm thầm). Đây là lý do `IdentityService` (đụng DbContext scoped) phải scoped, không rộng hơn.

**Factory lambda vs dạng trần:** factory (`AddScoped<I>(provider => new Impl(...))`) chỉ cần khi ctor có param **không nằm trong container** (giá trị runtime, chọn impl theo config). Khi đã đăng ký đủ mọi dependency vào container → dùng dạng trần `AddScoped<IInterface, Impl>()`, DI tự resolve đệ quy, hết lambda, dễ đọc.

- **Kiểm chứng:** `dotnet run` không còn ném lúc startup; `AddInfrastructure` đăng ký `AuthService` + `IIdentityService`/`IJwtTokenGenerator` dạng trần, `TimeProvider`/`JwtOptions` singleton.

**Câu chốt khi phỏng vấn:** *"`AddX` chỉ ghi descriptor, resolve là lazy lúc runtime nên thứ tự đăng ký không ảnh hưởng — trừ đăng ký trùng type. Startup crash `svc UNKNOWN` là vì minimal API xin service từ DI mà tôi quên đăng ký, chứ không tự new. DbContext phải scoped vì không thread-safe; nhét vào singleton là captive dependency."*

---

## Ghi chú 7. Phân loại lỗi Identity: không có enum, `Code` là string = `nameof(describer)`, map ở Infra

**Bối cảnh:** cần tách register-fail thành 409 (trùng email) vs 400 (mật khẩu yếu). `RegisterOutcome` ban đầu chỉ mang `string[]` mô tả → mất thông tin để phân loại.

**Sự thật về `IdentityResult`:** `Errors` là `IEnumerable<IdentityError>`, mỗi cái có `Code` (string) + `Description` (string). **Không có enum** cho error code — string chính là cơ chế framework thiết kế. Nhìn source `IdentityErrorDescriber`: mỗi method gán `Code = nameof(<TênMethod>)`, nên `DuplicateEmail().Code == "DuplicateEmail"`. Vì thế so `e.Code` với string **là** cách chính chủ, không phải hack.

**Chuẩn hơn = source-of-truth thay literal:** dùng `nameof(IdentityErrorDescriber.DuplicateEmail)` thay chuỗi `"DuplicateEmail"` — compiler check, đổi tên là gãy build ngay, rõ nguồn. So bằng `==` / `Array.Contains` (ordinal), **không** `string.Contains` (khớp chuỗi con, dính nhầm).

**Ranh giới:** `IdentityError` là type Infra. Việc "đọc `Code` → quyết loại lỗi" làm **trong Infra** (extension `ToRegisterFailureReason()` trên `IdentityResult`), trả lên Application một enum miền trung tính (`RegisterFailureReason`). Application/endpoint chỉ `switch` trên enum, không hề chạm `IdentityError` → không leak Infra. Endpoint map: `DuplicateEmail → Conflict` (409), `WeakPassword → ValidationProblem` (400), còn lại → `Problem`.

- **Cạm bẫy switch expression:** nhánh discard `_ =>` phải để **cuối**. Đặt đầu → mọi nhánh sau unreachable (compiler cảnh báo "pattern is unreachable").
- **Kiểm chứng:** register trùng email → 409; mật khẩu yếu → 400 kèm mảng lỗi; grep không còn literal `"DuplicateEmail"`/`"PasswordTooShort"`, chỉ còn `nameof(IdentityErrorDescriber.*)`.

**Câu chốt khi phỏng vấn:** *"Identity không có enum lỗi — `IdentityResult.Errors` là `Code`/`Description` string, `Code` chính là `nameof` method của `IdentityErrorDescriber`. Tôi map `Code` sang một enum miền ngay trong Infra rồi trả lên, nên endpoint chọn 409/400 mà không dính type Identity. So bằng `==` và dùng `nameof` làm source of truth thay magic string."*

---

## Ghi chú 8. Ranh giới orchestration: `AuthService` mù Infra, và `RegisterOutcome` fail = `UserId` null

**Điểm cốt tử của bước (đắt nhất khi phỏng vấn):** `AuthService` (Application) chỉ inject **hai abstraction** `IIdentityService` + `IJwtTokenGenerator`. Nó **không bao giờ** thấy `UserManager`, `IdentityResult`, hay `JwtOptions` — cả ba ở Infrastructure. Cơ chế ép điều này không phải "kỷ luật" mà là **project reference**: `AuthService` inject `UserManager` thì `Application.csproj` phải reference `Infrastructure`, tạo phụ thuộc ngược tầng (Application → Infrastructure) — đúng thứ NetArchTest sẽ fail. `AuthService` chỉ điều phối chuỗi (verify → roles → phát access token → sinh refresh token → gói `AuthResult`); mọi thứ đụng framework/secret nằm sau abstraction ở Infra.

**Bẫy semantic nhỏ:** `RegisterOutcome` khi thất bại phải trả `UserId = null` (không phải `Guid.Empty`). Signature là `Guid?`; `Guid.Empty` là guid toàn-0 **khác null**, caller check `UserId is null` sẽ hiểu sai là "có userId = 0". Fail = không có userId = `null`.

- **Kiểm chứng:** `AuthService.csproj` không reference `Infrastructure`; grep `AuthService` không thấy `UserManager`/`JwtOptions`. `RegisterUserAsync` nhánh fail trả `null`.

**Câu chốt khi phỏng vấn:** *"Endpoint mỏng gọi `AuthService`; `AuthService` điều phối qua `IIdentityService`/`IJwtTokenGenerator`, không hề thấy `UserManager` hay `JwtOptions` — ranh giới được ép bằng project reference, không phải quy ước, nên NetArchTest bắt được vi phạm."*

---

## Ghi chú 9. Seed role/admin lúc startup: `IHostedService` + scope thủ công, và seeding phải "câm"

**Hiểu sai thường gặp:** gọi seeder tay trong `Program.cs` giữa `Build()` và `Run()`, hoặc tiện tay tái dùng `RegisterUserAsync` để tạo admin cho "DRY".

**Đúng cơ chế — hai điểm:**

- **Nơi chạy:** `IHostedService` đăng ký trong `AddInfrastructure`; host tự gọi `StartAsync` **trước khi mở cổng** nhận request → role có sẵn trước request đầu. Đóng gói đúng chỗ (seeding thuộc module Identity, `Program.cs` chỉ `AddModules`). Cạm bẫy đời sống dịch vụ: hosted service là **singleton**, còn `RoleManager`/`UserManager` là **scoped** → **không** inject thẳng vào constructor (captive dependency, xem Ghi chú 6). Phải inject `IServiceScopeFactory`, `CreateScope()` rồi resolve `RoleManager`/`UserManager` bên trong scope đó.
- **Seeding phải "câm":** gọi thẳng `UserManager`/`RoleManager` (API hạ tầng thuần), **đừng** route qua `RegisterUserAsync` (lớp use-case). Lý do sâu: use-case đăng ký công khai và bootstrap dữ liệu hệ thống là **hai mối lo khác nhau**, chỉ tình cờ cùng gọi `CreateAsync`. Khi Ticketing/Contracts lên, `RegisterUserAsync` rất có thể publish integration event `UserRegistered` qua Wolverine (welcome email…) — ghép vào seeder thì **mỗi lần boot môi trường mới** sẽ bắn event đó. Seeding chỉ được ghi DB, không kích side-effect nghiệp vụ.

- **Kiểm chứng:** log startup thấy seeder query `AspNetRoles`/`AspNetUsers` rồi app `started` (không throw); DB có `admin@eventhub.com` gán `Admin`. Muốn DRY phần map lỗi thì tách helper `EnsureSucceeded(this IdentityResult, string context)` dùng chung — reuse **đúng tầng** (helper hạ tầng), không kéo theo semantic của registration.

**Câu chốt khi phỏng vấn:** *"Tôi seed trong một `IHostedService` chạy lúc startup; vì `RoleManager` là scoped còn hosted service là singleton nên tôi mở scope qua `IServiceScopeFactory` thay vì inject thẳng. Tôi cố tình không tái dùng `RegisterUserAsync` cho seeding — nó là use-case đăng ký, sau này sẽ publish integration event, seeding phải câm chỉ ghi DB."*

---

## Ghi chú 10. Config bắt buộc vs tùy chọn: `Get<T>()` trả null khi section vắng, và secret vs non-secret

**Bug gặp thật:** đặt `IsSeedAdmin = false` mặc định trong Options rồi tưởng "section vắng thì tự tắt seed". Nhưng `GetSection("IdentitySeed").Get<IdentitySeedOptions>()` trả **null** khi section vắng hẳn — **không object nào được dựng**, nên property default `false` chưa kịp áp. Nếu phía sau còn `?? throw` (copy từ khuôn `jwtOptions`), app **chết lúc boot** dù chỉ muốn không seed.

**Đúng cơ chế:**

- **Bắt buộc vs tùy chọn xử khác nhau.** Config **bắt buộc** (JWT — thiếu là không auth được) → `?? throw` fail-fast. Config **tùy chọn** (IdentitySeed — thiếu = "không seed", hợp lệ) → `?? new IdentitySeedOptions()` để lấy object toàn default. Đừng bê nguyên khuôn `?? throw` chỉ vì trông giống. Property default chỉ cứu được trường hợp section **có** nhưng thiếu field lẻ (khi đó `Get` dựng object, field vắng lấy default) — không cứu được section vắng hẳn.
- **Secret vs non-secret — để đúng provider:** `SigningKey`, `AdminPassword` = **bí mật** → User Secrets (dev) / environment variable (prod, vd `.env` của docker compose, key phân cấp map sang `IdentitySeed__AdminPassword` hai gạch dưới). `Issuer`, `Audience` = định danh công khai → `appsettings.json` (commit được). `UserSecretsId` (GUID trong csproj) **không phải secret** — chỉ là tên thư mục trỏ tới `secrets.json`, an toàn commit. `IConfiguration` gộp mọi provider (thứ tự sau đè trước) nên code chỉ đọc `configuration["Section:Key"]`, không quan tâm nguồn — dev lấy từ User Secrets, prod lấy từ env var, cùng một key.

- **Kiểm chứng:** xoá section `IdentitySeed` → app vẫn boot (không seed admin), không throw. `appsettings*.json` không chứa `Jwt:SigningKey` hay `AdminPassword`. `dotnet user-secrets list` thấy `IdentitySeed:*` đúng prefix (khớp `SectionName`).

**Câu chốt khi phỏng vấn:** *"Config bắt buộc thì fail-fast, config tùy chọn thì coalesce về default — `Get<T>()` trả null khi section vắng nên property default không tự cứu. Secret (signing key, admin password) để User Secrets/env; định danh công khai (issuer, audience) để appsettings. Code đọc qua `IConfiguration` nên đổi provider giữa dev và prod mà không đụng code."*

---

## Ghi chú 11. "Build xanh ≠ chạy đúng": chuỗi bug config/logic mà compiler mù

**Bối cảnh:** dựng seeding + authorization vấp một loạt bug **không cái nào** làm đỏ build — chỉ lộ lúc chạy thật: helper `EnsureSucceeded` thiếu guard `if (result.Succeeded) return;` nên **luôn throw** (thành công cũng nổ); copy-paste đọc nhầm field Options; quên `Configure<T>`/bind nên `IsSeedAdmin` mãi false (admin không seed); sai prefix secret (`Identity:` vs `IdentitySeed:`); và `Jwt:Audience` để chuỗi rỗng → token phát không có `aud` → validate ném `The audience 'empty' is invalid`.

**Đúng cơ chế:**

- **Config/logic sai lọt qua compile và cả DI resolution.** `IOptions<T>` vẫn resolve được dù chưa bind (trả default), token vẫn phát được dù `aud` rỗng — lỗi dời tới tận runtime với message mù mờ. Cách chặn: **fail-fast lúc khởi động** với message rõ. Lưu ý dùng `string.IsNullOrWhiteSpace` **không** phải null-check khi giá trị có thể là `""` (config rỗng là chuỗi rỗng, không phải null → null-check trượt).
- **Bằng chứng trước, kết luận sau.** Build xanh chỉ chứng minh compile; phải chạy host thật + gọi endpoint mới đóng được "Định nghĩa hoàn thành". Cả loạt bug trên chỉ hiện khi login admin / gọi `/me` / decode token.

- **Kiểm chứng:** chạy host thật trên Postgres → login admin ra token, decode thấy `role=Admin`; `/me` không token 401, có token 200; `/admin-only` user 403, admin 200; refresh rotation + reuse detection ra 401 đúng cụm.

**Câu chốt khi phỏng vấn:** *"Tôi không tin build xanh là xong — cả loạt bug của tôi (helper luôn throw, quên bind options, config aud rỗng) đều lọt qua compiler lẫn DI, chỉ lộ lúc chạy thật. Nên tôi verify end-to-end bằng cách chạy host và gọi endpoint, và fail-fast config sai lúc boot với `IsNullOrWhiteSpace` thay vì để lỗi mù mờ tới runtime."*
