# Bước A (00). Hiểu trước khi gõ: JWT, refresh token & bản đồ code Day 4

> Mục tiêu bước này: **chỉ đọc, chưa gõ gì**. Nắm bốn thứ: (1) JWT là gì và ba phần của nó; (2) vì sao chia access token ngắn + refresh token dài; (3) một điểm trung thực, tự ký JWT là *được* nhưng có giới hạn, biết giới hạn đó là điểm cộng phỏng vấn; (4) ba mảnh code hôm nay đặt ở đâu để **không phá ranh giới Day 3**. Hiểu xong mới sang [Bước 1](01-package-config.md).

---

## A.1. Bức tranh Day 4

Day 3 bạn có cái kho: bảng `AspNetUsers`, `AspNetRoles`, `RefreshTokens`. Chưa ai vào được, chưa ai ra được. Day 4 dựng **luồng auth**:

1. **Register**: tạo user (Identity lo hashing mật khẩu).
2. **Login**: kiểm mật khẩu, phát **access token** (JWT ngắn hạn) + **refresh token** (dài hạn).
3. **Gọi API có bảo vệ**: client gửi access token ở header `Authorization: Bearer …`; middleware verify chữ ký + hạn, dựng lại danh tính user.
4. **Refresh**: access token hết hạn, client đưa refresh token để lấy cặp mới, không phải đăng nhập lại.
5. **Authorize theo role**: vài endpoint chỉ Admin vào được.

## A.2. JWT là cái gì (giải phẫu)

**JWT (JSON Web Token)** là một chuỗi gồm **ba phần** ngăn nhau bởi dấu chấm: `header.payload.signature`. Mỗi phần là JSON được mã hóa **Base64URL** (lưu ý: **mã hóa**, *không phải* mã hóa bí mật, ai cũng đọc được).

- **Header**: thuật toán ký (vd `HS256` = HMAC-SHA256) và loại token (`JWT`).
- **Payload**: các **claim**: mẩu thông tin về chủ thể. Chuẩn có sẵn `sub` (subject = id user), `exp` (thời điểm hết hạn), `iss` (issuer, ai phát), `aud` (audience, phát cho ai), `iat` (phát lúc nào), `jti` (id token). Bạn thêm claim riêng: email, role...
- **Signature**: ký của header+payload bằng **khóa bí mật** của server. Đây là phần chống giả mạo.

**Điểm cốt lõi phải hiểu:** payload **không bí mật**, đừng nhét mật khẩu hay bí mật vào JWT, ai chặn được token cũng đọc được payload. Cái JWT bảo đảm **không phải bí mật mà là tính toàn vẹn**: nếu ai sửa một ký tự trong payload (vd đổi role thành Admin), chữ ký không còn khớp, server verify sẽ **từ chối**. Chỉ người giữ khóa bí mật mới ký lại được, mà khóa chỉ server có.

> Dán một access token bạn phát ở Day 4 vào [jwt.io](https://jwt.io/) để thấy ba phần: nó decode payload ra cho bạn đọc, và verify chữ ký nếu bạn dán khóa vào. Đây là cách trực quan nhất để "thấy" JWT.

**Vì sao dùng JWT thay vì session cookie truyền thống:** JWT **self-contained** (tự chứa), server verify chỉ cần khóa, không phải tra DB session mỗi request. Điều này hợp kiến trúc nhiều service / API stateless. Cái giá: token đã phát thì **khó thu hồi trước hạn** (server không giữ trạng thái để "hủy" nó), chính vì thế access token phải **ngắn hạn**, và ta cần refresh token (mục sau) để cân bằng.

## A.3. Vì sao hai token: access ngắn + refresh dài

Một token thôi không đủ vì hai yêu cầu đánh nhau:

- **Bảo mật muốn token sống ngắn:** JWT khó thu hồi; nếu bị lộ, càng ngắn hạn thì cửa sổ tấn công càng nhỏ.
- **Trải nghiệm muốn token sống lâu:** không ai muốn nhập lại mật khẩu mỗi 15 phút.

Giải pháp chuẩn ngành là **tách vai**:

| | Access token (JWT) | Refresh token |
|---|---|---|
| Sống bao lâu | Ngắn (vd 15 phút) | Dài (vd 7–30 ngày) |
| Dùng làm gì | Gửi kèm **mỗi** request để vào API | Chỉ gửi tới **một** endpoint `/refresh` để đổi lấy cặp mới |
| Dạng | JWT tự chứa, không lưu server | Chuỗi ngẫu nhiên, **có** lưu server (bảng `RefreshTokens`) để thu hồi được |
| Verify kiểu gì | Kiểm chữ ký + hạn, không tra DB | Tra DB (hash), kiểm chưa revoke/hết hạn |

Vì refresh token **có** lưu server, nó **thu hồi được** (đặt `RevokedAt`), bù đúng cái điểm yếu "JWT khó hủy" của access token. Access token ngắn nên dù lộ cũng mau chết; refresh token dài nhưng nằm trong DB nên kiểm soát được. Hai cái bù nhau.

## A.4. Tự ký JWT: được, nhưng phải biết giới hạn

Tài liệu chính thức của Microsoft ([Configure JWT bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), mục *Recommended approaches to create a JWT*) nói thẳng: cho **production**, **không** nên tự phát access token từ request username/password; nên dùng một **OIDC/OAuth server** (IdentityServer/Duende, Entra ID, Keycloak...) và ký bằng **khóa bất đối xứng** (RSA/EC: server ký bằng private key, mọi API verify bằng public key công khai).

Vậy sao guide này vẫn dạy tự ký? Vì nó **hợp lệ trong đúng ngữ cảnh của EventHub** và bạn cần *hiểu* cơ chế trước khi dựa vào một server dựng sẵn:

- EventHub là **một hệ thống đóng, một API** (modular monolith), tự phát và tự tiêu thụ token của chính mình. Đây đúng là "closed system" mà tài liệu coi là chấp nhận được.
- Ta ký bằng **HMAC đối xứng** (`HS256`): **cùng một khóa bí mật** vừa ký vừa verify. Hợp khi chỉ có một bên (chính API này) làm cả hai. Nếu sau này có **nhiều consumer độc lập** verify token, khóa đối xứng thành gánh nặng (phải chia sẻ khóa bí mật cho mọi bên = rủi ro) → lúc đó chuyển sang **bất đối xứng**.

**Đây chính là một "góc kể khi phỏng vấn" mạnh:** không phải "tôi làm auth", mà *"tôi tự phát JWT ký HMAC đối xứng vì hệ thống đóng một-API; tôi biết production đa-consumer nên chuyển sang OIDC server và khóa bất đối xứng, và biết vì sao"*. Hiểu **giới hạn** của lựa chọn quan trọng hơn việc làm cho nó chạy.

## A.5. Bản đồ code Day 4: ba mảnh, đặt đâu để giữ ranh giới Day 3

Day 3 bạn thắng một ranh giới sạch: chiều tham chiếu **Infrastructure → Application → Domain**, Domain thuần POCO, Application chỉ biết abstraction (`IIdentityService`), coupling framework cô lập trong Infrastructure. Day 4 thêm code auth **mà không được phá** điều đó. Ba mảnh:

**Mảnh 1. Sinh JWT: `IJwtTokenGenerator`.**

- **Interface `IJwtTokenGenerator`** ở **Application**: hợp đồng surface primitive, vào là `userId`/`email`/danh sách `role` (đều primitive), ra là một `string` (access token). Không type JWT nào lọt ra interface.
- **Class `JwtTokenGenerator`** ở **Infrastructure**: chỗ *duy nhất* chạm `JsonWebTokenHandler`, `SymmetricSecurityKey`, khóa ký. Vì ký token = cầm khóa bí mật + dùng type framework = **mối lo hạ tầng**, đúng chỗ của nó là Infrastructure.

Đây là cùng khuôn `IIdentityService` của Day 3: **abstraction đi lên (Application), implementation đi xuống (Infrastructure)**. Reference app chuẩn (Clean Architecture template của Jason Taylor) cũng tách *token generation* thành dịch vụ riêng, không trộn vào identity service.

**Mảnh 2. Thao tác user & refresh token: `IIdentityService` (mở rộng).**

`IIdentityService` (đã đặt tên ở Day 3, hôm nay mới có method thật) lớn thêm các thao tác surface-primitive: đăng ký user, kiểm mật khẩu, lấy danh sách role, và tạo/validate-rotate/revoke refresh token. Impl `IdentityService` ở **Infrastructure** vì nó ôm `UserManager<ApplicationUser>` và `IdentityModuleDbContext` (cả hai là Infrastructure).

> **Quyết định của bạn (khuyến nghị):** các thao tác refresh token có thể tách thành một `IRefreshTokenService` riêng cho gọn, thay vì nhồi hết vào `IIdentityService`. Mentor khuyến nghị **gộp vào `IIdentityService` cho Day 4** (ít mảnh hơn, dễ theo), rồi tách sau nếu interface phình. Chọn cách nào cũng dạy được, miễn **impl nằm Infrastructure**, interface nằm Application.

**Mảnh 3. Điều phối: `AuthService`.**

Login không phải một thao tác đơn, nó là chuỗi: kiểm mật khẩu → phát access token → sinh + lưu refresh token → gói lại trả về. Ai đứng ra ghép? Một **`AuthService`** ở **Application**, phụ thuộc *các abstraction* `IIdentityService` + `IJwtTokenGenerator`, trả về một DTO (vd `AuthResult`: access token + refresh token + hạn). Endpoint (ở Api) chỉ gọi `AuthService` rồi map kết quả sang HTTP.

> **Vì sao không dùng mediator (Wolverine) cho việc điều phối này?** Wolverine là **Tuần 3**. Day 4 chưa có message bus; `AuthService` chỉ là một class dịch vụ thường được DI. Đừng kéo Wolverine vào sớm.

Sơ đồ chiều tham chiếu (y hệt Day 3, chỉ thêm mảnh mới):

```text
EventHub.Identity.Domain         → RefreshToken (POCO, đã có từ Day 3)
EventHub.Identity.Application    → IIdentityService (mở rộng), IJwtTokenGenerator, AuthService, các DTO
EventHub.Identity.Infrastructure → IdentityService (UserManager + DbContext), JwtTokenGenerator (JsonWebTokenHandler),
                                   JwtOptions, cấu hình AddJwtBearer
EventHub.Identity.Api            → endpoints /register /login /refresh /logout + endpoint demo role
EventHub.Api (host)              → UseAuthentication()/UseAuthorization() trong pipeline
Chiều ref: Infrastructure → Application → Domain   (không đổi, không đảo)
```

## A.6. Một điểm đặt code phải quyết sớm: middleware auth ở host

JWT bearer có hai nửa:

- **Cấu hình dịch vụ** (`AddAuthentication().AddJwtBearer(...)` + `TokenValidationParameters`), đăng ký vào DI. Cái này đặt trong **Infrastructure DI** (`AddInfrastructure`) của module Identity là hợp lý: nó là hạ tầng của module.
- **Middleware pipeline** (`UseAuthentication()` rồi `UseAuthorization()`), phải chạy trong pipeline HTTP, **đúng thứ tự**, và **trước** khi map endpoint. Interface `IModule` của bạn hiện chỉ có `MapEndpoints`, **không** có hook cho middleware. Nên hai lệnh `Use...` này phải thêm thẳng vào host `Program.cs`, đặt **trước** `UseModules()`.

Đây là một hệ quả kiến trúc đáng ghi: host (composition root) chịu trách nhiệm **thứ tự pipeline** vì thứ tự middleware là quyết định toàn cục, không phải việc của từng module. Chi tiết ở [Bước 1](01-package-config.md).

## A.7. Xong bước này khi

- [x] Bạn kể được JWT gồm ba phần gì, phần nào chống giả mạo, và vì sao payload **không** bí mật.
- [x] Bạn giải thích được vì sao cần **cả hai** access (ngắn) + refresh (dài), và vì sao refresh token thu hồi được còn access token thì không.
- [x] Bạn nói lại được **vì sao** tự ký JWT bằng HMAC đối xứng là hợp lệ cho EventHub, và **khi nào** phải chuyển sang OIDC/khóa bất đối xứng.
- [x] Bạn chỉ đúng chỗ đặt ba mảnh (`IJwtTokenGenerator`, `IIdentityService` mở rộng, `AuthService`) và vì sao middleware auth phải ở host.

→ Sang [Bước 1. Package & cấu hình JWT bearer](01-package-config.md).
