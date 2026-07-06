# Bước 6. Verify end-to-end & commit

> Mục tiêu: chạy trọn vòng auth một lượt (register → login → gọi endpoint bảo vệ → refresh → reuse → role), chắc mọi thứ khớp, rồi commit theo Conventional Commits tiếng Việt.
>
> Lưu ý: chỉ lệnh CLI, cứ chạy theo.

---

## 6.1. Verify end-to-end

Build + chạy host:

```bash
dotnet build EventHub.slnx
dotnet run --project src/Bootstrap/EventHub.Api
```

Gợi ý: gom các lệnh dưới vào **một file `.http`** (đặt tạm ở `src/Bootstrap/EventHub.Api`, ví dụ `EventHub.Api.http`) để chạy tuần tự trong IDE và giữ lại token giữa các call, tiện hơn `curl` copy tay. Dưới đây dùng `curl` cho rõ từng bước (thay `5xxx` bằng port thật in ra lúc host lên):

```bash
# 1. Đăng ký một user thường
curl -i -X POST http://localhost:5xxx/identity/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@eventhub.local","password":"Passw0rd!"}'

# 2. Đăng nhập → lấy accessToken (AT) + refreshToken (R1)
curl -i -X POST http://localhost:5xxx/identity/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@eventhub.local","password":"Passw0rd!"}'

# 3. Gọi endpoint đòi đăng nhập: không token → 401, có token → 200
curl -i http://localhost:5xxx/identity/me
curl -i http://localhost:5xxx/identity/me -H "Authorization: Bearer <AT>"

# 4. User thường vào admin-only → 403
curl -i http://localhost:5xxx/identity/admin-only -H "Authorization: Bearer <AT>"

# 5. Refresh R1 → cặp mới (AT2 + R2); R1 chết
curl -i -X POST http://localhost:5xxx/identity/refresh \
  -H "Content-Type: application/json" -d '{"refreshToken":"<R1>"}'

# 6. Reuse: dùng lại R1 (đã chết) → 401 + thu hồi cụm
curl -i -X POST http://localhost:5xxx/identity/refresh \
  -H "Content-Type: application/json" -d '{"refreshToken":"<R1>"}'

# 7. R2 giờ cũng bị thu hồi → 401
curl -i -X POST http://localhost:5xxx/identity/refresh \
  -H "Content-Type: application/json" -d '{"refreshToken":"<R2>"}'

# 8. Đăng nhập Admin (nếu đã seed Admin user) → admin-only 200
curl -i http://localhost:5xxx/identity/admin-only -H "Authorization: Bearer <AT_ADMIN>"
```

Đối chiếu kỳ vọng:

- Bước 2: 200 + `accessToken` + `refreshToken`. Dán `accessToken` vào [jwt.io](https://jwt.io): `sub`/`email`/role đúng, chữ ký verify bằng khóa.
- Bước 3: 401 khi thiếu token, 200 khi có.
- Bước 4: 403 (đã xác thực, thiếu role), **không** phải 401.
- Bước 5: 200 + cặp token mới.
- Bước 6: **401** và trong DB cụm refresh token của user bị `RevokedAt`.
- Bước 7: 401 (R2 đã bị thu hồi theo cụm ở bước 6).
- Bước 8: 200.

Soi DB refresh token:

```bash
docker compose --env-file .env -f docker/docker-compose.yml exec postgres \
  psql -U <user> -d <db> -c 'SELECT "UserId","RevokedAt","ReplacedByTokenHash" IS NOT NULL AS replaced FROM "RefreshTokens" ORDER BY "CreatedAt";'
```

- `TokenHash` là hash (không phải chuỗi client cầm).
- Sau kịch bản reuse, mọi dòng của user đều có `RevokedAt` ≠ null.

Tắt host (`Ctrl+C`). **Xóa file `.http` tạm** nếu nó chứa token/mật khẩu thật trước khi commit (hoặc chỉ để placeholder).

## 6.2. Định nghĩa "hoàn thành" Day 4

Đối chiếu checklist ở [README Day 4](README.md#định-nghĩa-hoàn-thành-day-4). Cốt lõi:

- [x] `POST /register` + `POST /login` chạy; login trả access + refresh token thật.
- [x] Access token verify được ở jwt.io; khóa ký ở User Secrets, không trong `appsettings`.
- [x] Endpoint bảo vệ: 401 khi thiếu token, 200 khi có; role: 403 vs 200.
- [x] Refresh rotation + reuse detection đúng kịch bản; refresh token lưu hash.
- [x] `IJwtTokenGenerator` ở Application, impl ở Infrastructure; Application 0 package JWT.
- [x] Build xanh.
- [x] Bạn tự giải thích được các điểm ở checklist README (không chỉ "chạy được").

## 6.3. Commit

Conventional Commits, tiếng Việt, imperative (xem [docs/conventional-commits.md](../../conventional-commits.md)). Scope `identity`:

```bash
git add -A
git status
git commit
```

**Khuyến nghị message** (một commit gộp cho cả ngày):

```text
feat(identity): JWT + refresh token rotation + role-based authorization
```

Nếu thích commit nhỏ theo bước (**lựa chọn phong cách của bạn**, mentor khuyến nghị gộp vì cả ngày là một đơn vị "dựng luồng auth"):

```text
build(identity): thêm package JwtBearer + JwtOptions + cấu hình bearer
feat(identity): sinh access token qua IJwtTokenGenerator
feat(identity): endpoint register/login + phát refresh token (lưu hash)
feat(identity): refresh token rotation + reuse detection + logout
feat(identity): seed role + bảo vệ endpoint theo role
```

Đẩy lên remote nếu muốn:

```bash
git push
```

## 6.4. Cạm bẫy thường gặp

- **Lỡ commit khóa ký hoặc token.** Kiểm `appsettings*.json` **không** có `Jwt:SigningKey`; file `.http` tạm không kèm token/mật khẩu thật. `git status` rà trước khi commit.
- **Quên commit thay đổi host.** `Program.cs` (middleware auth + seeder) và `Directory.Packages.props` phải nằm trong commit, không chỉ file module. `git add -A` rồi soi `git status`.
- **Migration mới lỡ sinh ra.** Day 4 **không** đổi schema (dùng lại bảng Day 3). Nếu `dotnet ef migrations add` chạy nhầm, xóa migration thừa trước khi commit.

## 6.5. Xong Day 4 khi

- [x] Toàn bộ kịch bản 6.1 đúng kỳ vọng.
- [ ] Đã commit (và push nếu muốn) với message Conventional Commits tiếng Việt.
- [ ] Nhắn mentor **"review Day 4"**.

→ Quay lại [README Day 4](README.md) hoặc xem trước [Day 5](../README.md).
