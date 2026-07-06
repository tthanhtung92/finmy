# Day 4. JWT + Refresh token + Role-based authorization

> **Mentor mode.** Tài liệu giải thích *vì sao* và *làm gì*, **không kèm code C#/cấu hình**, bạn tự gõ. Mọi lệnh CLI (`dotnet`, `docker`, `git`, `curl`) thì cứ chạy theo. Mỗi file dưới đây là **một bước**, làm tuần tự từ trên xuống.
>
> Viết cho người **mới**: nếu một câu khiến bạn phải đoán, đó là lỗi của tài liệu. Nhắn mentor để bổ sung.

---

## Mục tiêu Day 4

Theo [ROADMAP](../../ROADMAP.md) (mục 5, Tuần 1, Ngày 4): *JWT authentication + refresh token + role-based authorization; Login/Register endpoints → **đăng nhập trả JWT thật**.*

Kết thúc Day 4 bạn có: *một luồng auth chạy thật end-to-end, đăng ký user, đăng nhập nhận **access token (JWT)** ngắn hạn + **refresh token** dài hạn; gọi được endpoint đòi đăng nhập bằng Bearer token; refresh để lấy cặp token mới (có **rotation**); và một endpoint chỉ Admin vào được (**role-based**).*

Đây là ngày biến mô hình dữ liệu Identity dựng ở [Day 3](../day-03/README.md) thành hành vi. Day 3 là cái kho; Day 4 là cái cửa và người gác cửa.

Quỹ thời gian: đây là ngày **nặng nhất Tuần 1** (nhiều mảnh ghép: sinh token, đăng nhập, rotation, phân quyền). Chia làm 7 bước, làm chậm, verify sau mỗi bước.

> **Lưu ý phạm vi:** Day 4 **chưa** dùng `Result<T>` (đó là [Day 5](../README.md)), hôm nay endpoint map lỗi bằng tay sang `Results.Problem`/`Results.Unauthorized`. Cũng **chưa** có FluentValidation hay ProblemDetails chuẩn RFC 7807 (Day 5). Đừng làm sớm, kẻo trộn hai ngày.

## Bạn cần có sẵn trước khi bắt đầu

- **Đã hoàn thành [Day 3](../day-03/README.md)**: schema Identity (7 bảng `AspNet*` + `RefreshTokens`) đã áp vào Postgres; `dotnet build EventHub.slnx` xanh; host chạy được (`/identity/ping` trả `Identity pong!`).
- **Hạ tầng đang chạy**: `docker compose --env-file .env -f docker/docker-compose.yml ps` thấy Postgres `healthy`.
- **Connection string `IdentityDb`** vẫn ở User Secrets của host (từ Day 2).
- Terminal mở tại **thư mục gốc repo** (nơi có `EventHub.slnx`).
- Một công cụ gọi HTTP: file `.http` trong IDE, hoặc `curl` (bước verify dùng cả hai).

## Các bước (làm theo thứ tự)

| Bước | File | Việc |
|------|------|------|
| A | [00-tong-quan.md](00-tong-quan.md) | **Hiểu** JWT là gì, access vs refresh token, vì sao tự ký JWT là "được nhưng có giới hạn", và bản đồ 3 mảnh code hôm nay (đọc, chưa gõ) |
| 1 | [01-package-config.md](01-package-config.md) | Thêm package `Microsoft.AspNetCore.Authentication.JwtBearer` + cấu hình `JwtOptions`, JWT bearer, và middleware auth ở host |
| 2 | [02-token-generation.md](02-token-generation.md) | `IJwtTokenGenerator` (Application) + impl sinh JWT bằng `JsonWebTokenHandler` (Infrastructure) |
| 3 | [03-register-login.md](03-register-login.md) | Mở rộng `IIdentityService`, viết `AuthService`, endpoint `/register` + `/login`, sinh & lưu refresh token (hash) |
| 4 | [04-refresh-revoke.md](04-refresh-revoke.md) | Endpoint refresh: rotation + reuse detection; endpoint logout thu hồi token |
| 5 | [05-authorization.md](05-authorization.md) | Seed role, gắn role claim vào JWT, endpoint chỉ Admin, phân biệt 401 vs 403 |
| 6 | [06-verify-commit.md](06-verify-commit.md) | Verify e2e qua HTTP → commit → push |

## Quy tắc kiểm chứng xuyên suốt

Sau **mỗi** bước, chạy lại lệnh kiểm chứng ở cuối file bước đó. Mỏ neo build:

```bash
dotnet build EventHub.slnx
```

Build phải xanh trước khi sang bước sau. Từ Bước 3 trở đi, mỏ neo thứ hai là **chạy host và gọi thật**:

```bash
dotnet run --project src/Bootstrap/EventHub.Api
```

## Định nghĩa "hoàn thành" Day 4

- [ ] `Microsoft.AspNetCore.Authentication.JwtBearer` khai trong CPM (không kèm version ở `.csproj`), version căn khớp cụm 10.0.9.
- [ ] Khóa ký JWT nằm ở **User Secrets/biến môi trường**, **không** commit vào `appsettings.json`.
- [ ] `IJwtTokenGenerator` ở **Application** (surface primitive); impl `JwtTokenGenerator` ở **Infrastructure** dùng `JsonWebTokenHandler`. Application **không** dính package JWT.
- [ ] `POST /identity/register` tạo được user; `POST /identity/login` trả **access token + refresh token** thật.
- [ ] Access token dán vào [jwt.io](https://jwt.io) đọc được claim `sub`, `email`, role; chữ ký verify bằng khóa của bạn.
- [ ] Gọi endpoint bảo vệ **không** kèm token → **401**; kèm token hợp lệ → **200**.
- [ ] `POST /identity/refresh` cấp cặp token mới; refresh token cũ bị **revoke** (rotation). Dùng lại refresh token đã revoke → bị từ chối **và** thu hồi cả cụm token đang sống của user (reuse detection).
- [ ] User thường gọi endpoint chỉ Admin → **403**; Admin gọi → **200**.
- [ ] Refresh token lưu trong DB là **hash** (cột `TokenHash`), không phải token thô.
- [ ] `dotnet build EventHub.slnx` xanh.
- [ ] **Bạn tự nói thành lời được:** JWT gồm ba phần gì, phần nào chống giả mạo; vì sao access token ngắn còn refresh dài; **vì sao tự ký JWT bằng HMAC đối xứng là hợp lệ cho project này nhưng production đa-consumer nên chuyển OIDC/khóa bất đối xứng**; refresh token **rotation** giải quyết rủi ro gì và **reuse detection** bắt được kịch bản tấn công nào; role đi vào JWT thế nào để `RequireRole` chặn được; khác biệt **401** (chưa xác thực) vs **403** (đã xác thực nhưng thiếu quyền).

Xong Day 4, nhắn mentor **"review Day 4"** trước khi sang [Day 5](../README.md).
