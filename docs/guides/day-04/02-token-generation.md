# Bước 2. Sinh access token (JWT): `IJwtTokenGenerator`

> Mục tiêu: viết mảnh **tạo** JWT, interface `IJwtTokenGenerator` ở Application (surface primitive) và impl `JwtTokenGenerator` ở Infrastructure dùng `JsonWebTokenHandler`. Cuối bước bạn có một dịch vụ "cho user → trả access token đã ký", sẵn cho `AuthService` gọi ở Bước 3.
>
> Nhắc: code C# **bạn tự gõ**. Mentor nói tên type/method/claim và vì sao; bạn viết.

---

## 2.1. Cái gì

Hai type, hai project:

- **`IJwtTokenGenerator`** (interface) ở **`EventHub.Identity.Application`** (thư mục `Authentication/`, file `IJwtTokenGenerator.cs`): một method *"nhận danh tính user ở dạng primitive → trả `string` access token"*. Chữ ký gõ thẳng được: `string GenerateToken(string userId, string email, IEnumerable<string> roles)`. Lưu ý `userId` để kiểu **`string`** (không `Guid`) cho interface không dính kiểu khóa cụ thể; caller (`AuthService`, Bước 3) truyền `user.Id.ToString()`. **Không** type JWT nào xuất hiện trên chữ ký.
- **`JwtTokenGenerator`** (class implement) ở **`EventHub.Identity.Infrastructure`**: dựng danh sách claim, tạo `SecurityTokenDescriptor`, ký bằng `SigningCredentials` từ khóa HMAC, xuất chuỗi qua `JsonWebTokenHandler`. Đọc issuer/audience/hạn/khóa từ `JwtOptions` (Bước 1).

Đăng ký DI: trong `AddInfrastructure`, map `IJwtTokenGenerator` → `JwtTokenGenerator` (đời sống **singleton** hoặc **scoped** đều được; xem mục 2.6).

## 2.2. Vì sao

**Vì sao tách interface ở Application, impl ở Infrastructure:** đây là cùng khuôn Dependency Inversion của Day 3. `AuthService` (Application, Bước 3) cần *phát token* nhưng **không được** biết `JsonWebTokenHandler` hay khóa ký (framework + bí mật = Infrastructure). Nên nó phụ thuộc **hợp đồng** `IJwtTokenGenerator` nằm ngay trong Application; Infrastructure cắm impl thật lúc chạy. Application vẫn 0 package JWT.

**Vì sao surface primitive (Guid/string, không phải `ApplicationUser`):** nếu method nhận `ApplicationUser`, thì `ApplicationUser` (Infrastructure) phải leo lên Application, phá đúng ranh giới Day 3. Truyền primitive (id, email, roles) giữ interface sạch: nó chỉ mô tả *cần gì để làm token*, không lộ mô hình lưu trữ.

**Vì sao dùng `JsonWebTokenHandler` chứ không `JwtSecurityTokenHandler`:** `JsonWebTokenHandler` (namespace `Microsoft.IdentityModel.JsonWebTokens`) là handler thế hệ mới Microsoft khuyến nghị, nhanh hơn (~30%) và là mặc định nên dùng cho project .NET mới. `JwtSecurityTokenHandler` (namespace `System.IdentityModel.Tokens.Jwt`) là bản cũ còn đầy trong tutorial mạng; **đừng** copy nhầm nó.

**Vì sao chọn đúng claim:** access token phải mang đủ để (a) nhận diện user và (b) phân quyền, nhưng **không thừa** (payload không bí mật, càng gọn càng tốt):

- **`sub`** = `userId`. Đây là danh tính chính; các bước sau đọc `sub` để biết "ai đang gọi".
- **`email`**: tiện hiển thị/log, không bắt buộc.
- **role claim**: mỗi role một claim `role`. Đây là thứ khiến `RequireRole("Admin")` (Bước 5) chặn được. **Không có role trong token thì không phân quyền theo role được.**
- `iss`, `aud`, `exp`, `iat`, do `SecurityTokenDescriptor`/handler tự gắn từ `JwtOptions` (Issuer, Audience, hạn = `AccessTokenLifetimeMinutes`).

## 2.3. Dữ kiện đã xác minh

- **`JsonWebTokenHandler.CreateToken(SecurityTokenDescriptor)`** trả `string` token đã ký; là API tạo token của `Microsoft.IdentityModel.JsonWebTokens`. Nguồn: [JsonWebTokenHandler.CreateToken (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler.createtoken).
- **`SecurityTokenDescriptor`** (namespace `Microsoft.IdentityModel.Tokens`) có: `Subject` (`ClaimsIdentity` chứa các claim), `Issuer`, `Audience`, `Expires`, `SigningCredentials`. Nguồn: [SecurityTokenDescriptor (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.securitytokendescriptor).
- **`SigningCredentials(SymmetricSecurityKey, SecurityAlgorithms.HmacSha256)`** ký HMAC-SHA256. `SymmetricSecurityKey` dựng từ mảng byte của chuỗi khóa (`Encoding.UTF8.GetBytes(...)`). Nguồn: [SigningCredentials](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.signingcredentials), [SymmetricSecurityKey](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.symmetricsecuritykey).
- **Tên claim chuẩn** (`sub`, `email`, `role`...) có sẵn dưới dạng hằng trong `JwtRegisteredClaimNames` (namespace `Microsoft.IdentityModel.JsonWebTokens`), tránh gõ tay chuỗi. Nguồn: [JwtRegisteredClaimNames](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.jsonwebtokens.jwtregisteredclaimnames).

## 2.4. Các bước làm

1. **Interface (Application):** trong `EventHub.Identity.Application/Authentication/`, khai `IJwtTokenGenerator` với chữ ký `string GenerateToken(string userId, string email, IEnumerable<string> roles)` (primitive, `userId` là `string`).
2. **Impl (Infrastructure):** trong `EventHub.Identity.Infrastructure`, tạo `JwtTokenGenerator : IJwtTokenGenerator`. Inject `JwtOptions` (qua `IOptions<JwtOptions>` hoặc đọc trực tiếp, xem 2.6). Trong method:
   - Dựng danh sách claim: `sub` = userId, `email` = email, và **một** claim `role` cho **mỗi** phần tử `roles`.
   - Dựng `SigningCredentials` từ `SymmetricSecurityKey` (bytes của `JwtOptions.SigningKey`) + `HmacSha256`.
   - Dựng `SecurityTokenDescriptor`: `Subject` = `ClaimsIdentity` chứa các claim; `Issuer`/`Audience` từ options; `Expires` = `DateTime.UtcNow` + hạn; `SigningCredentials`.
   - Gọi `new JsonWebTokenHandler().CreateToken(descriptor)` → trả chuỗi.
3. **DI:** trong `AddInfrastructure`, đăng ký `IJwtTokenGenerator` → `JwtTokenGenerator`.

> **Lưu ý claim role & validate:** ở Bước 1 nếu bạn để `MapInboundClaims` mặc định, handler validate sẽ ánh xạ tên claim ngắn (`sub`, `role`) sang URI dài kiểu `ClaimTypes.*`. Để `RequireRole` (Bước 5) hoạt động, role claim khi **đọc ra** phải khớp `ClaimsIdentity.RoleClaimType`. Cách chắc ăn: khi dựng claim role dùng `ClaimTypes.Role` (đường dài) *hoặc* cấu hình `RoleClaimType` trong `TokenValidationParameters`. Mục 2.7 nói kỹ cạm bẫy này, đây là chỗ hay làm 403 oan.

## 2.5. Kiểm chứng

Bước này chưa có endpoint phát token, nên verify gián tiếp:

```bash
dotnet build EventHub.slnx
```

- Build xanh; `EventHub.Identity.Application.csproj` vẫn **không** dính package JWT (chỉ khai interface + primitive).
- Rà bằng mắt: `JwtTokenGenerator` là **chỗ duy nhất** trong solution `import` `Microsoft.IdentityModel.*` / `JsonWebTokenHandler`.

Verify "token thật" sẽ làm ở [Bước 3](03-register-login.md) khi `/login` gọi generator này và bạn dán token vào [jwt.io](https://jwt.io).

## 2.6. Quyết định của bạn (đời sống DI)

`JwtTokenGenerator` **không giữ trạng thái thay đổi** (chỉ đọc options + tạo token). Đăng ký kiểu nào cũng chạy:

- **Singleton**: hợp lý vì stateless; đọc `JwtOptions` một lần. Khuyến nghị nếu bạn inject `IOptionsMonitor`/đọc snapshot cố định.
- **Scoped/Transient**: an toàn mặc định, đồng bộ với đa số dịch vụ khác. Nếu inject `IOptions<JwtOptions>` cũng ổn.

Mentor khuyến nghị **scoped** cho Day 4 (đồng nhất với `IdentityService`/`AuthService` đều scoped, đỡ phải lý luận về singleton bắt scoped). Tối ưu sang singleton là việc sau.

## 2.7. Cạm bẫy thường gặp

- **Copy nhầm `JwtSecurityTokenHandler`.** Tutorial cũ đầy nó. Dùng `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`). Hai cái khác namespace, khác API `CreateToken`.
- **Role claim không khớp lúc validate → 403 oan (cạm bẫy số 1 của Day 4).** Nếu lúc tạo bạn đặt claim tên `"role"` nhưng lúc validate `RoleClaimType` lại là URI `ClaimTypes.Role`, thì `RequireRole` không thấy role → Admin thật vẫn 403. Thống nhất một kiểu: hoặc dùng `ClaimTypes.Role` khi tạo, hoặc set `RoleClaimType = "role"` (và cân nhắc `MapInboundClaims = false`) khi validate. Kiểm bằng cách dán token vào jwt.io xem tên claim thực tế.
- **Nhét thông tin nhạy cảm vào claim.** Payload đọc được bởi bất kỳ ai cầm token. Đừng để mật khẩu, số thẻ, secret. Chỉ id + email + role + cờ vô hại.
- **Đặt `Expires` bằng giờ local thay vì UTC.** JWT `exp` là UTC. Dùng `DateTime.UtcNow`, tránh lệch múi giờ khiến token "hết hạn" ngay.
- **Khóa đọc sai/rỗng.** Nếu `JwtOptions.SigningKey` rỗng (quên set User Secrets), `SymmetricSecurityKey` sẽ nổ hoặc token ký bằng khóa rỗng. Verify khóa nạp đúng (Bước 1 mục 1.5).

## 2.8. Ba bẫy dễ dính nhất

Nếu chỉ nhớ ba thứ:

1. **Role claim lệch `RoleClaimType` → 403 oan** (bẫy số 1 của Day 4): tên role claim lúc tạo phải khớp lúc validate.
2. **Copy nhầm `JwtSecurityTokenHandler`** (bản cũ) thay vì `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`).
3. **`Expires` bằng giờ local** thay vì UTC → token lệch hạn. Dùng `GetUtcNow().UtcDateTime`.

## 2.9. Góc kể khi phỏng vấn

*"Việc tạo token tôi tách thành IJwtTokenGenerator: interface ở Application chỉ nhận primitive (userId, email, roles) trả string, còn impl ở Infrastructure là chỗ duy nhất chạm JsonWebTokenHandler và khóa ký. Nhờ đó AuthService điều phối login mà không hề biết JWT được ký thế nào, đổi sang khóa bất đối xứng hay đổi handler chỉ sửa một class Infrastructure. Tôi dùng JsonWebTokenHandler thay JwtSecurityTokenHandler vì nó là bản mới, nhanh hơn. Claim role tôi đặt khớp với RoleClaimType lúc validate, đó là chỗ dễ gây 403 oan nếu tên claim lệch nhau."*

## 2.10. Link tài liệu chính thức

- [JsonWebTokenHandler.CreateToken](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler.createtoken)
- [SecurityTokenDescriptor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.securitytokendescriptor)
- [SigningCredentials](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.signingcredentials) · [SymmetricSecurityKey](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.symmetricsecuritykey)
- [JwtRegisteredClaimNames](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.jsonwebtokens.jwtregisteredclaimnames)
- [Mapping claims / RoleClaimType](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims?view=aspnetcore-10.0)

## 2.11. Xong bước này khi

- [x] `IJwtTokenGenerator` ở Application, surface toàn primitive; Application vẫn 0 package JWT.
- [x] `JwtTokenGenerator` ở Infrastructure dùng `JsonWebTokenHandler` + `SecurityTokenDescriptor` + `SigningCredentials` (HMAC-SHA256), đọc `JwtOptions`.
- [x] Claim gồm `sub` (userId), `email`, và một `role` cho mỗi role; tên role claim khớp `RoleClaimType` lúc validate.
- [x] DI map `IJwtTokenGenerator` → `JwtTokenGenerator`.
- [x] `dotnet build` xanh.

→ Sang [Bước 3. Register, Login & phát refresh token](03-register-login.md).
