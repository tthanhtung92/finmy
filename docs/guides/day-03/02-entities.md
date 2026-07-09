# Bước 2. Mô hình hóa `ApplicationUser`, `ApplicationRole` (Infrastructure) & `RefreshToken` (Domain)

> Mục tiêu: khai ba entity của module Identity **đúng chỗ theo [Quyết định 2](00-tong-quan.md)**: hai entity Identity ở **`EventHub.Identity.Infrastructure`**, `RefreshToken` (POCO thuần) ở **`EventHub.Identity.Domain`**. Hai cái đầu **mở rộng** type có sẵn của ASP.NET Core Identity; cái thứ ba giữ Domain sạch tuyệt đối.
>
> Lưu ý mentor: entity là code, **mình không viết hộ**. Mình mô tả class/property cần tạo bằng văn xuôi; bạn tự gõ.

---

## 2.1. Cái gì

Tạo ba class, **ở hai project khác nhau**:

- `ApplicationUser`: kế thừa `IdentityUser<Guid>`, trong **`EventHub.Identity.Infrastructure`** (vd thư mục `Identity/` hoặc `Persistence/`).
- `ApplicationRole`: kế thừa `IdentityRole<Guid>`, trong **`EventHub.Identity.Infrastructure`**.
- `RefreshToken`: POCO thuần, chỉ giữ `UserId: Guid`, trong **`EventHub.Identity.Domain`**.

## 2.2. Vì sao

**Kế thừa `IdentityUser` thay vì tự dựng bảng user:** cách chính chủ Microsoft khuyến nghị để thêm dữ liệu riêng cho user (đặt tên `ApplicationUser` theo template). Bạn thừa hưởng sẵn UserName, Email, PasswordHash, SecurityStamp, các cờ lockout... Không phải khai lại, không phải tự lo an toàn.

**Vì sao `ApplicationUser`/`ApplicationRole` ở Infrastructure:** xem lý do đầy đủ ở [Quyết định 2](00-tong-quan.md). Tóm tắt: chúng kế thừa framework Identity (auth = mối lo hạ tầng), nên sống ở Infrastructure để coupling framework **không** rò xuống lõi. Application (Day 4) **không** đụng trực tiếp `ApplicationUser`/`UserManager`, nó đi qua abstraction `IIdentityService`. Nhờ đó Domain thuần POCO **và** Application không phải reference Infrastructure, cả hai luật sạch đều đạt.

**Vì sao `RefreshToken` ở Domain và chỉ giữ `UserId`:** refresh token là **khái niệm nghiệp vụ** (vòng đời, rotation, thu hồi) chứ không phải type của framework, nên nó thuộc Domain. Nhưng Domain **không được** reference Infrastructure, mà `ApplicationUser` lại nằm ở Infrastructure → `RefreshToken` **không thể** cầm navigation trỏ tới `ApplicationUser`. Cách đúng: giữ **`UserId: Guid`** (id-reference qua ranh giới), đúng pattern eShopOnWeb khi aggregate domain trỏ user bằng id chứ không object-reference.

**Vì sao `RefreshToken` tách riêng, không nhét vào `AspNetUserTokens`:** `AspNetUserTokens` (`IdentityUserToken`) là chỗ Identity lưu token của *provider ngoài* (Google, 2FA...), khóa tổng hợp `(UserId, LoginProvider, Name)`, không hợp lưu refresh token JWT với vòng đời/rotation riêng. Nên phải là **bảng của bạn**.

**Vì sao quan hệ 1-n (một User → nhiều RefreshToken):** một user đăng nhập trên **nhiều thiết bị** (mỗi thiết bị một token), và **mỗi lần rotation** (Day 4) sinh token mới thay token cũ. Quan hệ 1-n cho phép **thu hồi từng token** độc lập. Quan hệ này được **cấu hình ở DbContext** ([Bước 3](03-dbcontext.md)) bằng Fluent API, không cần `RefreshToken` cầm navigation ngược.

## 2.3. Dữ kiện đã xác minh: cách mở rộng đúng chuẩn

Theo tài liệu Microsoft ([customize-identity-model, aspnetcore-10.0](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0)):

- Thêm dữ liệu riêng cho user: tạo class kế thừa `IdentityUser` (lệ đặt tên `ApplicationUser`); EF map property mới **theo convention**.
- Đổi khóa sang `Guid` (Quyết định 1): **bắt buộc** dùng bản generic: `ApplicationUser : IdentityUser<Guid>`, `ApplicationRole : IdentityRole<Guid>`.
- `IdentityUser<TKey>`/`IdentityRole<TKey>` về theo transitive từ `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (đã reference ở [Bước 1](01-package.md), **chỉ** ở Infrastructure).

Pattern đặt `ApplicationUser` ở Infrastructure + trỏ user bằng id: [Clean Architecture template (Jason Taylor)](https://github.com/jasontaylordev/CleanArchitecture) và [eShopOnWeb (ApplicationUser trong Infrastructure/Identity)](https://github.com/dotnet-architecture/eShopOnWeb/blob/main/src/Infrastructure/Identity/ApplicationUser.cs).

## 2.4. Các bước làm

**Trong `EventHub.Identity.Infrastructure`:**

1. **`ApplicationUser`:** class kế thừa `IdentityUser<Guid>`. Chưa cần thêm field domain nào, có thể thêm sau. Không tự khai lại UserName/Email/PasswordHash: base đã có. (Tùy chọn: có thể thêm navigation `ICollection<RefreshToken>`, hợp lệ vì đây là Infrastructure, thấy được Domain, nhưng **không bắt buộc**; cấu hình 1-n ở DbContext không cần navigation này.)
2. **`ApplicationRole`:** class kế thừa `IdentityRole<Guid>`, cùng kiểu khóa `Guid` với `ApplicationUser` (nếu lệch, khai DbContext generic sẽ không biên dịch).

**Trong `EventHub.Identity.Domain`:**

1. **`RefreshToken`:** POCO thuần, **không** kế thừa/không import gì của Identity. Thiết kế **đầy đủ rotation** (đã chốt, phục vụ Day 4):
   - `Id`: khóa chính riêng (`Guid`).
   - `UserId` (`Guid`): **khóa ngoại** trỏ về user (id-reference, **không** navigation `ApplicationUser`).
   - `TokenHash`: **lưu hash** của refresh token, không lưu token thô.
   - `ExpiresAt` (`DateTimeOffset`): thời điểm hết hạn.
   - `CreatedAt` (`DateTimeOffset`): thời điểm tạo.
   - `RevokedAt` (`DateTimeOffset?`): null nếu còn hiệu lực.
   - `ReplacedByTokenHash` (`string?`): khi rotation, trỏ token mới thay nó (phát hiện tái sử dụng token cũ).
   - (Tùy chọn) `CreatedByIp`: audit.

> **Vì sao lưu `TokenHash` chứ không token thô, và vì sao `DateTimeOffset`:** hash để rò rỉ DB không đồng nghĩa lộ token dùng được (giống lý do không lưu mật khẩu thô). `DateTimeOffset` map sang Postgres `timestamptz` (có offset), an toàn khi so sánh thời điểm hết hạn qua các múi giờ. Logic sinh/verify/rotation token ở **Day 4**; Day 3 chỉ dựng *hình dạng dữ liệu*.

> **Lưu ý ranh giới (quan trọng):** `RefreshToken` ở Domain **không được** cầm navigation `ApplicationUser` (nằm ở Infrastructure), đó sẽ là Domain → Infrastructure, phá phân lớp. Chỉ giữ `UserId: Guid`. Chiều navigation (nếu có) đi **một chiều** từ phía Infrastructure (`ApplicationUser` → `RefreshToken`), không ngược lại.

## 2.5. Kiểm chứng

```bash
dotnet build EventHub.slnx
```

Build xanh nghĩa là ba entity hợp lệ về kiểu: `ApplicationUser`/`ApplicationRole` (Infrastructure) thấy `IdentityUser<Guid>` qua package Identity, và `RefreshToken` (Domain) biên dịch **mà không** cần package Identity nào. (Nếu Domain đỏ vì thiếu type Identity → bạn đang lỡ dùng type Identity trong `RefreshToken`; gỡ ra, chỉ giữ `UserId: Guid`.) Bảng xuất hiện sau migration ([Bước 4](04-migration.md)).

## 2.6. Cạm bẫy thường gặp

- **Đặt `ApplicationUser` nhầm vào Domain:** phá [Quyết định 2](00-tong-quan.md). Kéo framework Identity xuống lõi. `ApplicationUser`/`ApplicationRole` phải ở Infrastructure.
- **`RefreshToken` cầm navigation `ApplicationUser`:** Domain → Infrastructure, sai chiều phân lớp và Domain sẽ đỏ (không thấy type). Chỉ giữ `UserId: Guid`.
- **Lệch kiểu khóa User vs Role:** `ApplicationUser : IdentityUser<Guid>` nhưng `ApplicationRole : IdentityRole` (string) → khai `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` không biên dịch. Hai cái phải cùng `Guid`.
- **Tự khai lại field có sẵn:** UserName, Email, PasswordHash, SecurityStamp… đã có trong `IdentityUser`.

## 2.7. Ba bẫy dễ dính nhất

Nếu chỉ nhớ ba thứ:

1. **Đặt `ApplicationUser`/`ApplicationRole` nhầm vào Domain** — kéo framework Identity xuống lõi, phá Quyết định 2. Chúng ở Infrastructure.
2. **`RefreshToken` cầm navigation `ApplicationUser`** — Domain → Infrastructure, sai chiều phân lớp và Domain đỏ. Chỉ giữ `UserId: Guid`.
3. **Lệch kiểu khóa User vs Role** (`IdentityUser<Guid>` nhưng `IdentityRole` string) — DbContext generic không biên dịch. Hai cái cùng `Guid`.

## 2.8. Góc kể khi phỏng vấn

*"`ApplicationUser`/`ApplicationRole` tôi để ở Infrastructure vì chúng kế thừa framework Identity, auth là mối lo hạ tầng. `RefreshToken` là khái niệm nghiệp vụ nên ở Domain, nhưng nó trỏ user bằng `UserId` (id-reference) thay vì navigation, vì Domain không được biết `ApplicationUser` ở Infrastructure. Quan hệ 1-n tôi cấu hình ở DbContext bằng Fluent API, không cần navigation hai chiều. Refresh token lưu hash thay token thô, đủ field để rotation và thu hồi từng token trên từng thiết bị."*

## 2.9. Link tài liệu chính thức

- [Custom user data (Identity model customization)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0#custom-user-data)
- [IdentityUser.cs (dotnet/aspnetcore)](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Stores/src/IdentityUser.cs)
- [eShopOnWeb (ApplicationUser trong Infrastructure/Identity)](https://github.com/dotnet-architecture/eShopOnWeb/blob/main/src/Infrastructure/Identity/ApplicationUser.cs)
- [Date/time trong Npgsql (timestamptz vs timestamp)](https://www.npgsql.org/doc/types/datetime.html)

## 2.10. Xong bước này khi

- [x] `ApplicationUser`/`ApplicationRole` ở **Infrastructure**, kế thừa `IdentityUser<Guid>`/`IdentityRole<Guid>`.
- [x] `RefreshToken` ở **Domain**, POCO thuần, chỉ giữ `UserId: Guid` + các field rotation, **không** navigation `ApplicationUser`.
- [x] `dotnet build` xanh; `EventHub.Identity.Domain` **không** dính package Identity, biên dịch được mà không cần type Identity.

→ Sang [Bước 3. Nâng cấp DbContext](03-dbcontext.md).
