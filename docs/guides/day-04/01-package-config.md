# Bước 1. Package JWT bearer + `JwtOptions` + cấu hình xác thực

> Mục tiêu: thêm package validate JWT vào CPM, khai một `JwtOptions` để gom cấu hình JWT, đăng ký JWT bearer trong DI của module Identity, và cắm middleware xác thực vào pipeline host **đúng thứ tự**. Cuối bước host **hiểu** được Bearer token (dù chưa có endpoint nào phát ra token, đó là Bước 2–3).
>
> Nhắc: `.csproj`, `Directory.Packages.props`, `appsettings.json`, `Program.cs` đều là cấu hình/code, **bạn tự gõ**. Mentor nói tên, chỗ đặt, thứ tự; bạn viết.

---

## 1.1. Cái gì

Bốn việc nhỏ:

1. Khai `PackageVersion` cho `Microsoft.AspNetCore.Authentication.JwtBearer` trong CPM, reference **chỉ ở `EventHub.Identity.Infrastructure`**.
2. Định nghĩa một lớp cấu hình **`JwtOptions`** (ở Infrastructure) gom: issuer, audience, hạn access token, và **khóa ký**.
3. Trong `AddInfrastructure` (Infrastructure DI), đăng ký `AddAuthentication(...).AddJwtBearer(...)` với `TokenValidationParameters`, cộng `AddAuthorization`.
4. Trong host `Program.cs`, thêm `UseAuthentication()` rồi `UseAuthorization()` **trước** `UseModules()`.

## 1.2. Vì sao

**Package `Microsoft.AspNetCore.Authentication.JwtBearer`** là middleware **validate** token đến: nó đọc header `Authorization: Bearer …`, verify chữ ký + issuer + audience + hạn theo `TokenValidationParameters`, rồi dựng `ClaimsPrincipal` (danh tính user) gắn vào `HttpContext.User`. Không có nó, `[Authorize]`/`RequireAuthorization` không biết dựa vào đâu.

> Lưu ý phân biệt: package này lo **validate** (đọc token vào). Việc **tạo** token (Bước 2) dùng `JsonWebTokenHandler`, type này về **transitively** qua chính package JwtBearer (nó kéo theo `Microsoft.IdentityModel.JsonWebTokens`), nên bạn **không** cần thêm package riêng để sinh token.

**Vì sao chỉ Infrastructure reference:** cùng logic Day 3, mọi type framework auth (`TokenValidationParameters`, `SymmetricSecurityKey`, `JsonWebTokenHandler`) cô lập ở Infrastructure. Application/Domain sạch package JWT.

**Vì sao gom `JwtOptions`:** issuer/audience/hạn/khóa bị dùng ở **hai chỗ**, lúc *tạo* token (Bước 2) và lúc *validate* token (bước này). Nếu rải chuỗi ma thuật hai nơi, sớm muộn lệch nhau (token phát ra `aud=A` mà validate đòi `aud=B` → mọi request 401 mà không rõ vì sao). Gom một `JwtOptions` bind từ config là **một nguồn sự thật**.

**Vì sao khóa ký ở User Secrets, KHÔNG ở appsettings:** khóa ký HMAC là **bí mật tối cao** của hệ thống, ai có nó **ký được token giả cho bất kỳ user/role nào**, kể cả Admin. Commit khóa vào `appsettings.json` (đi lên Git) = phát khóa cho cả thế giới. Nên khóa nằm ở **User Secrets** (dev, như connection string Day 2) hoặc **biến môi trường** (production). `appsettings.json` chỉ chứa phần **không bí mật**: issuer, audience, hạn.

**Vì sao middleware ở host và đúng thứ tự:** `UseAuthentication()` phải chạy **trước** `UseAuthorization()`, xác thực (bạn là ai) trước, rồi mới phân quyền (bạn được làm gì). Cả hai phải **trước** khi endpoint chạy. Vì `IModule` không có hook middleware (chỉ `MapEndpoints`), và thứ tự pipeline là quyết định **toàn cục**, host `Program.cs` là chỗ đúng.

## 1.3. Dữ kiện đã xác minh

- **`Microsoft.AspNetCore.Authentication.JwtBearer`**: stable **10.0.9**, target `net10.0`; kéo theo transitively `Microsoft.IdentityModel.JsonWebTokens` (chứa `JsonWebTokenHandler` cho Bước 2). Nguồn: [NuGet](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer). Căn cùng patch `10.0.9` với cụm EF/Identity đã có.
- **`AddJwtBearer`** nhận cấu hình `JwtBearerOptions`, trong đó `TokenValidationParameters` bật các cờ: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`, và đặt `ValidIssuer`/`ValidAudience`/`IssuerSigningKey`. Khóa HMAC là một `SymmetricSecurityKey` dựng từ mảng byte của chuỗi khóa. Nguồn: [Configure JWT bearer authentication (Microsoft Learn, aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).
- **Thứ tự middleware**: `UseAuthentication` trước `UseAuthorization`, đặt sau routing và trước map endpoint. Nguồn: [Middleware ordering (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-10.0#middleware-order).
- **`AddAuthentication(JwtBearerDefaults.AuthenticationScheme)`**: hằng scheme mặc định cho bearer, nằm namespace `Microsoft.AspNetCore.Authentication.JwtBearer`.

## 1.4. Các bước làm

1. **CPM:** mở `Directory.Packages.props` (gốc repo), thêm một `PackageVersion` `Microsoft.AspNetCore.Authentication.JwtBearer` version `10.0.9` (đặt cạnh nhóm ASP.NET/Identity cho dễ đọc).
2. **Reference:** vào `EventHub.Identity.Infrastructure`, thêm `PackageReference` tới package đó (**không** version, CPM lo). Không thêm vào Application/Domain/host.
3. **`JwtOptions`:** trong Infrastructure (gợi ý thư mục `Authentication/` hoặc `Options/`), tạo một lớp `JwtOptions` với các property: `Issuer` (string), `Audience` (string), `SigningKey` (string), `AccessTokenLifetimeMinutes` (int). Đặt một hằng tên section, vd `"Jwt"`.
4. **Config không bí mật:** trong `appsettings.json` (hoặc `appsettings.Development.json`) của **host**, thêm section `Jwt` gồm `Issuer`, `Audience`, `AccessTokenLifetimeMinutes`. **Không** ghi `SigningKey` ở đây.
5. **Khóa bí mật:** đặt `SigningKey` vào **User Secrets** của host bằng CLI (mục 1.5). Khóa phải **đủ dài** cho HS256, tối thiểu **32 ký tự (256 bit)**, sinh ngẫu nhiên (mục 1.6 giải thích vì sao).
6. **Đăng ký DI:** trong `AddInfrastructure` (file `DependencyInjection.cs` của Infrastructure), bind `JwtOptions` từ `configuration.GetSection("Jwt")`; đọc ra để lấy khóa; gọi `services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` cấu hình `TokenValidationParameters` với đủ bốn cờ `Validate*` = true, `ValidIssuer`/`ValidAudience` từ options, `IssuerSigningKey` = `SymmetricSecurityKey` dựng từ `SigningKey`. Gọi thêm `services.AddAuthorization()`.
7. **Middleware host:** trong `Program.cs`, thêm `app.UseAuthentication();` rồi `app.UseAuthorization();` **trước** `app.UseModules();`.

> **Quyết định của bạn (khuyến nghị):** `AccessTokenLifetimeMinutes` để bao nhiêu? Mentor khuyến nghị **15 phút**, đủ ngắn để giảm rủi ro lộ token, đủ dài để không refresh liên tục. Có thể để **60** khi dev cho đỡ phiền. Đây là con số bạn chọn; ghi lý do vào ADR sau này.

## 1.5. Đặt khóa ký vào User Secrets

Ở host (`src/Bootstrap/EventHub.Api`), nếu chưa init User Secrets thì làm một lần, rồi set khóa:

```bash
dotnet user-secrets init --project src/Bootstrap/EventHub.Api
dotnet user-secrets set "Jwt:SigningKey" "<chuỗi-ngẫu-nhiên-≥32-ký-tự>" --project src/Bootstrap/EventHub.Api
```

Sinh một khóa ngẫu nhiên đủ mạnh (một trong hai cách):

```bash
openssl rand -base64 48
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/Bootstrap/EventHub.Api
```

> Nếu máy không có `openssl`: dùng bất kỳ trình sinh chuỗi ngẫu nhiên dài nào, miễn ≥ 32 ký tự và **không** phải chuỗi bạn tự nghĩ ra kiểu `"mysecretkey"`.

## 1.6. Kiểm chứng

```bash
dotnet build EventHub.slnx
dotnet run --project src/Bootstrap/EventHub.Api
```

- Build xanh (không warning downgrade/lệch version, nếu có, căn lại patch `10.0.9`).
- Host **lên được không nổ** khi khởi động: nếu `SigningKey` chưa set hoặc quá ngắn, một số cấu hình sẽ ném lỗi lúc dựng key, đó là dấu hiệu cần quay lại mục 1.5.
- Chưa có endpoint bảo vệ nào để thử token ở bước này; xác nhận `GET /identity/ping` và `GET /health` vẫn trả bình thường (chưa chặn ai). Tắt host (`Ctrl+C`).

Xác nhận Application sạch: mở `EventHub.Identity.Application.csproj`, **không** có `Microsoft.AspNetCore.Authentication.JwtBearer`.

## 1.7. Cạm bẫy thường gặp

- **Khóa quá ngắn → lỗi khó hiểu lúc chạy.** HS256 đòi khóa ≥ 256 bit (32 byte). Khóa ngắn khiến `SymmetricSecurityKey` ném `ArgumentOutOfRangeException`/`IDX10653` lúc ký hoặc validate. Sinh khóa dài, ngẫu nhiên.
- **Commit nhầm khóa vào `appsettings.json`.** Kiểm trước khi push: `appsettings*.json` chỉ có Issuer/Audience/hạn, **tuyệt đối không** `SigningKey`. Nếu lỡ commit khóa, coi như khóa đã lộ, phải đổi khóa mới.
- **Đảo thứ tự middleware.** `UseAuthorization` trước `UseAuthentication` → `HttpContext.User` chưa dựng xong khi phân quyền chạy → endpoint bảo vệ luôn 401 dù token đúng. Nhớ: **Authentication trước, Authorization sau**.
- **Quên `AddAuthorization()`.** Có `AddAuthentication` nhưng thiếu `AddAuthorization` → `RequireAuthorization`/`RequireRole` (Bước 5) không có dịch vụ để chạy.
- **Issuer/Audience lệch giữa tạo và validate.** Token phát ra phải mang đúng `iss`/`aud` mà `TokenValidationParameters` đòi. Cùng đọc từ `JwtOptions` để khỏi lệch (đây là lý do gom options).
- **`ValidateLifetime` và lệch giờ (clock skew).** Mặc định handler cho phép lệch 5 phút. Không cần chỉnh ở dev; chỉ nhớ khái niệm này khi token "hết hạn sớm/muộn vài phút".

## 1.8. Góc kể khi phỏng vấn

*"Tôi tách rõ hai nửa của JWT bearer: cấu hình dịch vụ (AddJwtBearer + TokenValidationParameters) nằm trong DI của module Identity, còn middleware UseAuthentication/UseAuthorization nằm ở host vì thứ tự pipeline là quyết định toàn cục. Khóa ký HMAC tôi để ở User Secrets, không bao giờ trong appsettings, vì ai có khóa là ký được token Admin giả. Issuer/audience/hạn tôi gom vào một JwtOptions làm nguồn sự thật duy nhất cho cả lúc phát lẫn lúc validate token, tránh lệch cấu hình gây 401 mù."*

## 1.9. Link tài liệu chính thức

- [NuGet: Microsoft.AspNetCore.Authentication.JwtBearer](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer)
- [Configure JWT bearer authentication in ASP.NET Core (aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [Middleware ordering](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-10.0#middleware-order)
- [Safe storage of app secrets in development (User Secrets)](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)

## 1.10. Xong bước này khi

- [x] `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.9` trong CPM; chỉ Infrastructure reference; không kèm version ở `.csproj`.
- [x] `JwtOptions` khai; `appsettings` có Issuer/Audience/hạn; `SigningKey` **chỉ** ở User Secrets.
- [x] `AddJwtBearer` + `TokenValidationParameters` (đủ bốn cờ `Validate*`) đăng ký trong `AddInfrastructure`; có `AddAuthorization`.
- [x] `Program.cs`: `UseAuthentication()` trước `UseAuthorization()`, cả hai trước `UseModules()`.
- [x] `dotnet build` xanh; host lên được không nổ.

→ Sang [Bước 2. Sinh access token (JWT)](02-token-generation.md).
