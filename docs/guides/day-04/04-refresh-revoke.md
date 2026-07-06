# Bước 4. Refresh (rotation) & thu hồi token

> Mục tiêu: endpoint `POST /identity/refresh` đổi refresh token cũ lấy **cặp token mới** với **rotation** (token cũ chết ngay) + **reuse detection** (dùng lại token đã chết = báo động, thu hồi cả cụm); và `POST /identity/logout` thu hồi token. Đây là phần "đinh" của Day 4, câu chuyện bảo mật kể được khi phỏng vấn.
>
> Nhắc: code C# bạn tự gõ. Mentor mô tả thuật toán từng nhánh; bạn viết.

---

## 4.1. Cái gì

- **Method mới trên `IIdentityService`** (impl Infrastructure): "validate + rotate một refresh token" và "thu hồi cụm token của user". Đầu vào là **chuỗi refresh token thô** client gửi lên.
- **`AuthService.RefreshAsync(refreshToken, ip)`**: chạy thuật toán rotation + reuse detection, phát access token mới + refresh token mới, trả `AuthResult`; hoặc trả thất bại (client phải đăng nhập lại).
- **`AuthService.LogoutAsync(refreshToken)`**: thu hồi refresh token đang dùng.
- **Endpoints (Api):** `POST /identity/refresh` (body: refresh token), `POST /identity/logout`.

## 4.2. Vì sao cần rotation & reuse detection

Refresh token sống dài (nhiều ngày). Nếu nó bị lộ (máy client nhiễm mã độc, log rò, sao lưu lộ), kẻ tấn công có cửa sổ dài. Hai kỹ thuật bù rủi ro đó:

**Rotation (xoay vòng):** mỗi lần refresh, cấp refresh token **mới** và **giết token vừa dùng** (`RevokedAt` = giờ, `ReplacedByTokenHash` = hash token mới). Một refresh token **chỉ dùng được đúng một lần**. Lợi ích: cửa sổ hữu ích của một token bị lộ co lại còn "tới lần refresh kế tiếp của người dùng thật".

**Reuse detection (phát hiện tái sử dụng):** vì token đã dùng bị revoke, nếu **một refresh token đã revoke lại được trình lên**, chỉ có thể do một trong hai kịch bản, và cả hai đều nguy:

- Kẻ tấn công cướp được token, người dùng thật đã refresh (làm token đó revoke), giờ kẻ tấn công mới dùng bản cũ; **hoặc**
- Kẻ tấn công dùng trước, người dùng thật dùng lại bản cũ sau.

Dù chiều nào, **có hai bên đang dùng chung một dây token** = token đã bị nhân bản = bị lộ. Hệ thống thiết kế tốt coi đây là **compromise, không phải retry**: **thu hồi toàn bộ refresh token đang sống của user** và bắt đăng nhập lại. Kẻ tấn công (và cả người dùng thật) đều bị đá ra; người dùng thật đăng nhập lại bằng mật khẩu, kẻ tấn công không có mật khẩu nên hết cửa.

> Đây là chuẩn ngành (Auth0, hướng dẫn OAuth 2 hiện đại). Các field `RevokedAt` + `ReplacedByTokenHash` bạn đặt ở Day 3 chính là để chạy được đúng thuật toán này, hôm nay chúng có việc.

**Vì sao tra bằng hash:** client gửi token **thô**; bạn **hash** nó rồi tra cột `TokenHash` (không lưu thô, Bước 3). Nên tra là "hash input → tìm dòng có `TokenHash` khớp".

## 4.3. Thuật toán refresh (viết theo từng nhánh)

Nhận `refreshToken` (thô) + `ip`:

1. **Hash** `refreshToken` → `hash`.
2. Tra `RefreshTokens` theo `TokenHash == hash`. **Không thấy** → token bịa/không tồn tại → trả **thất bại (401)**. Dừng.
3. **Thấy dòng** `token`. Xét trạng thái:
   - **Đã revoke** (`RevokedAt != null`) → **REUSE DETECTED**. Đây là báo động: gọi "thu hồi cụm", revoke **mọi** refresh token đang sống của `token.UserId` (mọi dòng cùng `UserId` có `RevokedAt == null`). Trả **thất bại (401)**. Dừng. *(Người dùng thật sẽ phải đăng nhập lại, đây là cái giá đúng để chặn token bị nhân bản.)*
   - **Hết hạn** (`ExpiresAt <= giờ`) → trả **thất bại (401)** (không cần thu hồi cụm; chỉ là token quá đát). Dừng.
   - **Hợp lệ** (chưa revoke, chưa hết hạn) → sang bước 4.
4. **Rotate:** sinh refresh token mới (như Bước 3: RNG → thô → hash → entity mới cho cùng `UserId`, `ExpiresAt` mới). Trên `token` cũ: đặt `RevokedAt` = giờ, `ReplacedByTokenHash` = hash của token mới. `SaveChangesAsync` (một transaction, xem cạm bẫy).
5. **Phát access token mới:** lấy roles của `token.UserId` → `IJwtTokenGenerator`.
6. Trả `AuthResult` (access token mới + refresh token **thô** mới + hạn).

## 4.4. Dữ kiện đã xác minh

- Truy vấn EF theo cột hash: `FirstOrDefaultAsync(rt => rt.TokenHash == hash)`; cập nhật rồi `SaveChangesAsync`. Cột `TokenHash` đã có **unique index** (Day 3), nên tra một dòng là xác định. Nguồn: [EF Core querying / saving](https://learn.microsoft.com/en-us/ef/core/saving/basic).
- Thu hồi cụm = một update nhiều dòng: nạp các dòng `UserId == x && RevokedAt == null` rồi set `RevokedAt`, hoặc `ExecuteUpdateAsync` một phát. Nguồn: [ExecuteUpdate (EF Core 10)](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete#executeupdate).
- Hash + so khớp phải **cùng thuật toán** với lúc tạo (SHA-256) và cùng cách encode. Nguồn: [SHA256.HashData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256.hashdata).

## 4.5. Các bước làm

1. **`IIdentityService` (Infrastructure impl):** thêm method "validate + rotate" thực thi thuật toán mục 4.3 (trả về `userId` + refresh token thô mới, hoặc null báo thất bại), và method "thu hồi cụm theo userId". Cân nhắc gộp rotation vào một method để nó chạy trong **một** `SaveChanges` (atomic).
2. **`AuthService.RefreshAsync`:** gọi Identity validate+rotate; nếu thất bại trả kết quả lỗi; nếu ok, lấy roles → phát access token mới → gói `AuthResult`.
3. **`AuthService.LogoutAsync`:** hash token → tìm → nếu thấy và chưa revoke thì đặt `RevokedAt` = giờ; luôn trả 200/204 (idempotent, xem cạm bẫy).
4. **Endpoints:** `POST /identity/refresh` nhận body chứa refresh token (record `RefreshRequest`), gọi `AuthService.RefreshAsync`, map ok → `Results.Ok(authResult)`, thất bại → `Results.Unauthorized()`. `POST /identity/logout` gọi `LogoutAsync` → `Results.NoContent()`.

## 4.6. Kiểm chứng

```bash
dotnet build EventHub.slnx
dotnet run --project src/Bootstrap/EventHub.Api
```

Kịch bản thành công (rotation):

```bash
# đăng nhập lấy refresh token R1 (từ Bước 3), rồi:
curl -i -X POST http://localhost:5xxx/identity/refresh \
  -H "Content-Type: application/json" -d '{"refreshToken":"<R1>"}'
```

- Trả 200 + access token mới + refresh token mới `R2`.
- Trong DB: dòng của `R1` giờ có `RevokedAt` ≠ null và `ReplacedByTokenHash` = hash của `R2`; dòng `R2` mới, chưa revoke.

Kịch bản reuse detection (điểm nhấn):

```bash
# dùng LẠI R1 (đã bị R2 thay):
curl -i -X POST http://localhost:5xxx/identity/refresh \
  -H "Content-Type: application/json" -d '{"refreshToken":"<R1>"}'
```

- Trả **401**.
- Trong DB: **cả** `R2` (và mọi refresh token đang sống của user) giờ đều `RevokedAt` ≠ null → cụm bị thu hồi. Thử refresh bằng `R2` sau đó cũng 401. Người dùng phải `/login` lại.

Kịch bản logout:

```bash
curl -i -X POST http://localhost:5xxx/identity/logout \
  -H "Content-Type: application/json" -d '{"refreshToken":"<R2>"}'
```

- Trả 204; refresh bằng token đó sau đó → 401.

## 4.7. Cạm bẫy thường gặp

- **Rotation không atomic → race/cửa sổ hai token sống.** Revoke cũ và tạo mới phải trong **một** `SaveChanges` (một transaction). Nếu tách hai lần lưu, hai request refresh song song có thể cùng thấy token hợp lệ → cấp hai token → hỏng bất biến "một lần dùng". Gói trong một transaction; unique index trên `TokenHash` là lưới an toàn cuối.
- **Quên nhánh reuse detection.** Nếu token đã revoke chỉ trả 401 mà **không** thu hồi cụm, bạn mất toàn bộ giá trị bảo mật, kẻ tấn công cứ thử là biết token nào từng hợp lệ. Nhánh "đã revoke → thu hồi cụm" là linh hồn của bước này.
- **So token thô với `TokenHash`.** Phải hash input trước khi tra. Quên hash → không bao giờ khớp → mọi refresh 401.
- **Logout không idempotent.** Client có thể logout hai lần, hoặc logout token đã hết hạn. Đừng ném lỗi; thu hồi nếu còn sống, luôn trả 204. Cũng đừng tiết lộ token có tồn tại hay không.
- **Không dọn token chết.** Bảng `RefreshTokens` phình theo thời gian (mỗi lần refresh đẻ một dòng). Day 4 chưa cần; ghi nhận "cần job dọn token hết hạn/đã revoke" là một cải tiến kể được.
- **Trả refresh token mới nhưng quên trả bản thô.** Client cần **thô** để lần sau gửi lại; DB giữ **hash**. Đừng trả hash.

## 4.8. Góc kể khi phỏng vấn

*"Refresh token của tôi xoay vòng: mỗi lần refresh cấp token mới và revoke token vừa dùng, đánh dấu ReplacedByTokenHash để lần vết. Vì token đã dùng bị vô hiệu, nếu một token đã revoke lại được trình lên thì chắc chắn token bị nhân bản, tôi coi đó là compromise, thu hồi cả cụm refresh token của user và bắt đăng nhập lại, thay vì lặng lẽ từ chối. Rotation tôi gói trong một transaction để hai request song song không cùng qua được, cộng unique index trên hash làm lưới cuối. Đây là mô hình rotation + reuse detection kiểu Auth0."*

## 4.9. Link tài liệu chính thức

- [Refresh Token Rotation (Auth0 Docs)](https://auth0.com/docs/secure/tokens/refresh-tokens/refresh-token-rotation)
- [EF Core: Save data / transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [EF Core: ExecuteUpdate/ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)
- [SHA256.HashData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256.hashdata)

## 4.10. Xong bước này khi

- [x] `POST /identity/refresh` hợp lệ → cặp token mới; token cũ bị revoke + trỏ `ReplacedByTokenHash`.
- [x] Dùng lại refresh token đã revoke → 401 **và** thu hồi cụm token đang sống của user.
- [x] Token hết hạn → 401 (không thu hồi cụm).
- [x] Rotation chạy trong một transaction (atomic).
- [x] `POST /identity/logout` idempotent, thu hồi token, trả 204.
- [x] `dotnet build` xanh; ba kịch bản curl ở 4.6 đúng kỳ vọng.

→ Sang [Bước 5. Role-based authorization](05-authorization.md).
