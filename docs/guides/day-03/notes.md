# Ghi chú & đính chính Day 3

> Ba khái niệm dưới đây là phần dễ hiểu *mơ hồ* nhất Day 3 (lộ ra khi quiz + review code). Bản ghi này chốt cách hiểu **đúng cơ chế**: chính chỗ này mới là điểm "kể được khi phỏng vấn", không phải chỉ "làm xong". Đọc lại sau khi hoàn thành các bước, và bổ sung nếu review lộ thêm chỗ mơ hồ.

---

## Ghi chú 1. Ranh giới: vì sao `ApplicationUser` ở Infrastructure còn `RefreshToken` ở Domain?

**Hiểu sai thường gặp (lối compromise):** "Handler Day 4 cần `UserManager<ApplicationUser>` → Application phải thấy `ApplicationUser` → nhét `ApplicationUser` xuống Domain cho Application với tới." Nghe hợp lý nhưng **sai**: nó vá layering bằng cách bẩn Domain.

**Đúng cơ chế: Dependency Inversion giải *cả hai* luật, không đánh đổi.**

- **Luật DDD:** Domain nên thuần POCO, không dính framework.
- **Luật phân lớp:** Application **không** được depend Infrastructure (chỉ Infra → App/Domain).

Điểm mấu chốt mà hệ thống lớn (Clean Architecture template của Jason Taylor, eShopOnWeb) chỉ ra: **Application không cần thấy `ApplicationUser` chút nào.** Chèn một abstraction:

- Interface **`IIdentityService`** ở **Application**: surface toàn primitive (`string`/`bool`/`Result` + `userId`), không lộ type Identity.
- Impl **`IdentityService`** (giữ `UserManager<ApplicationUser>`) ở **Infrastructure**, **không phải Application**. Đây là chỗ dễ nói lộn (quiz Day 3 vấp): impl ôm `UserManager`/`ApplicationUser` (cả hai ở Infrastructure), nên nếu để impl trong Application thì Application buộc phải thấy chúng, phải ref Infrastructure, phá đúng cái luật đang cứu. Quy tắc gọn: **abstraction đi lên (Application), implementation đi xuống (Infrastructure)**; DI ở Bootstrap mới ráp hai cái lúc chạy.
- **`ApplicationUser`/`ApplicationRole`** ở **Infrastructure** (auth = hạ tầng). **`RefreshToken`** ở **Domain** (khái niệm nghiệp vụ), chỉ giữ `UserId: Guid` (id-reference), không navigation ngược `ApplicationUser`.

Kết quả: Domain thuần POCO tuyệt đối (0 package Identity) **và** Application chỉ dùng interface nằm trong chính nó → không reference Infrastructure. Chiều ref: Infra → App → Domain, đúng chuẩn. Handler Day 4 inject `IIdentityService`, không hề biết `UserManager` tồn tại.

**Vì sao không phải trade-off nữa:** compromise (entity ở Domain) *chọn* vi phạm DDD để cứu layering. Lời giải `IIdentityService` **không hy sinh luật nào**, giá phải trả chỉ là một lớp adapter (interface + impl) và surface primitive (mỗi nhu cầu Identity mới → thêm một method). Đúng thứ đáng khoe cho CV kiến trúc.

- **Kiểm chứng:** `EventHub.Identity.Domain.csproj` **và** `EventHub.Identity.Application.csproj` **không** có package Identity/EF nào. `ApplicationUser`/`ApplicationRole`/`IdentityModuleDbContext`/`IdentityService` đều ở Infrastructure. `RefreshToken` (Domain) chỉ có `UserId: Guid`, không navigation `ApplicationUser`.

**Câu chốt khi phỏng vấn:** *"Tôi coi Identity là hạ tầng: `ApplicationUser`, `UserManager` ở Infrastructure. Application chỉ phụ thuộc abstraction `IIdentityService` mà chính nó định nghĩa (Dependency Inversion), nên Domain thuần POCO tuyệt đối và Application không reference Infrastructure. Đây là pattern Clean Architecture / eShopOnWeb. Domain trỏ user bằng `UserId` (id-reference), không navigation hai chiều. Giá là một lớp adapter, đổi lại lõi tách hoàn toàn khỏi framework auth."*

---

## Ghi chú 2. `base.OnModelCreating` gọi trước: cơ chế last-one-wins

**Hiểu sai thường gặp:** "gọi `base.OnModelCreating` ở đâu trong hàm cũng được, miễn có gọi."

**Đúng cơ chế:** EF Core cấu hình mô hình theo luật **last-one-wins**: lệnh cấu hình gọi *sau* ghi đè lệnh gọi *trước* cho cùng một thuộc tính. Lớp base `IdentityDbContext<…>` cấu hình toàn bộ 7 bảng Identity *bên trong* `base.OnModelCreating`. Vì vậy:

- Gọi `base` **đầu tiên** → base dựng nền, rồi cấu hình custom của bạn (`RefreshToken`, index, đổi tên...) chồng lên → đúng ý.
- Gọi `base` **sau** cấu hình custom → base ghi đè lại phần bạn vừa chỉnh → mất tùy biến.
- **Quên** gọi `base` → EF không hề cấu hình 7 bảng Identity → migration sinh ra thiếu bảng (hoặc chỉ có `RefreshTokens`).

**Câu chốt khi phỏng vấn:** *"EF last-one-wins nên tôi gọi `base.OnModelCreating` trước để nó dựng mapping Identity, rồi tôi cấu hình RefreshToken chồng lên. Đảo thứ tự thì base ghi đè phần của tôi; quên gọi thì migration mất 7 bảng Identity."*

> **Đính chính từ review Day 3:** code hiện đặt `base.OnModelCreating(modelBuilder)` ở **dòng cuối** `OnModelCreating` (sau phần cấu hình `RefreshToken`). Migration lần này vẫn ra đủ 7 bảng vì cấu hình custom **không đụng** entity nào base cấu hình → chưa có va chạm. Nhưng đây là bug ngầm: ngày nào custom chồng lên một bảng Identity (vd đổi maxlength `AspNetUsers.Email`), `base` gọi sau sẽ **xóa** tùy biến đó mà không báo lỗi. Cần **dời `base` lên dòng đầu** hàm.

---

## Ghi chú 3. `RefreshToken` ≠ `AspNetUserTokens`, và vì sao lưu hash

**Hiểu sai thường gặp:** "Identity đã có bảng `AspNetUserTokens` rồi, refresh token nhét vào đó cho đỡ đẻ bảng."

**Đúng cơ chế:** `AspNetUserTokens` (`IdentityUserToken`) dành cho token của **provider ngoài** (Google, Microsoft, 2FA...), khóa tổng hợp `(UserId, LoginProvider, Name)`, không có chỗ cho `ExpiresAt`, `RevokedAt`, `ReplacedByTokenHash` mà refresh token JWT cần cho **rotation** và **thu hồi**. Nên refresh token phải là **bảng riêng** `RefreshTokens` với vòng đời riêng.

**Vì sao lưu `TokenHash` chứ không phải token thô:** cùng lý do không lưu mật khẩu thô. Nếu DB rò rỉ, token thô = tài khoản bị chiếm; token *đã hash* thì kẻ tấn công không tái tạo được token dùng được. Khi verify (Day 4), bạn hash token client gửi lên rồi so với `TokenHash` trong DB.

**Câu chốt khi phỏng vấn:** *"`AspNetUserTokens` là cho provider ngoài, không hợp refresh token JWT vì thiếu vòng đời expiry/revoke/rotation. Tôi tách bảng riêng và lưu hash thay token thô để rò rỉ DB không đồng nghĩa lộ token dùng được."*
