# Bước A (00). Hiểu trước khi gõ: Identity model & hai quyết định đã chốt

> Mục tiêu bước này: **chỉ đọc, chưa gõ gì**. Nắm bức tranh: ASP.NET Core Identity cho bạn cái gì sẵn, mô hình dữ liệu Identity gồm những bảng nào, và **hai quyết định thiết kế** (đã chốt cùng mentor) định hình cả Day 3. Hiểu xong mới sang [Bước 1](01-package.md).

---

## A.1. Bức tranh Day 3

Day 2 bạn có `IdentityDbContext` kế thừa `DbContext` **trơn**, chưa `DbSet` nào. Chỉ đủ chứng minh pipeline EF chạy. Day 3 biến nó thành **mô hình dữ liệu auth thật**: User, Role, và RefreshToken.

Bạn sẽ **không** tự viết bảng User/Role từ số 0. Stack project (xem [ROADMAP](../../ROADMAP.md) mục 2) đã chọn **ASP.NET Core Identity + JWT**. Nghĩa là bạn *kế thừa* mô hình có sẵn của ASP.NET Core Identity rồi mở rộng thêm phần của mình (RefreshToken).

## A.2. Vì sao dùng ASP.NET Core Identity thay vì domain model thuần

Auth là chỗ **dễ làm sai một cách nguy hiểm**. Nếu tự dựng bảng user + tự hash mật khẩu, bạn phải tự lo: thuật toán hash đúng (PBKDF2 nhiều vòng + salt), khóa tài khoản sau N lần sai (lockout), `SecurityStamp` để vô hiệu hóa session khi đổi mật khẩu, chuẩn hóa username/email để tra cứu không phân biệt hoa thường, token provider cho reset mật khẩu/xác nhận email... Làm thiếu một mảnh = lỗ hổng.

ASP.NET Core Identity cho **sẵn tất cả** những thứ đó, đã được kiểm nghiệm ở quy mô lớn, kèm API `UserManager`/`RoleManager` để thao tác. Bạn tập trung vào phần *của bạn* (JWT, refresh rotation, phân quyền) thay vì phát minh lại phần hạ tầng auth.

**Đánh đổi:** coupling: entity của bạn phải kế thừa type của framework (`IdentityUser`), kéo phụ thuộc package Identity. Đây chính là lý do có **Quyết định 2** ở dưới (đặt entity ở đâu để coupling framework **không** rò xuống lõi domain).

> Câu chốt khi phỏng vấn: *"Tôi không tự hash mật khẩu. Auth là chỗ tự làm dễ tạo lỗ hổng. Tôi dùng ASP.NET Core Identity cho phần lưu trữ user/hashing/lockout đã kiểm nghiệm, và tự viết phần đặc thù (JWT + refresh token rotation). Coupling với framework tôi cô lập trong Infrastructure và cho Application dùng qua một abstraction (`IIdentityService`), Domain giữ thuần POCO (Quyết định 2)."*

## A.3. ASP.NET Core Identity cho sẵn cái gì (7 bảng)

ASP.NET Core Identity định nghĩa **7 kiểu entity** (đều tiền tố `Identity*`) và ánh xạ ra **7 bảng**:

| Entity (CLR type) | Bảng DB | Vai trò |
|-------------------|---------|---------|
| `IdentityUser` | `AspNetUsers` | Tài khoản người dùng (username, email, password hash…) |
| `IdentityRole` | `AspNetRoles` | Vai trò (Admin, User…) |
| `IdentityUserClaim` | `AspNetUserClaims` | Claim gắn với 1 user |
| `IdentityUserLogin` | `AspNetUserLogins` | Liên kết login ngoài (Google…) với user |
| `IdentityUserToken` | `AspNetUserTokens` | Token xác thực của user (do provider ngoài cấp) |
| `IdentityRoleClaim` | `AspNetRoleClaims` | Claim cấp cho mọi user trong 1 role |
| `IdentityUserRole` | `AspNetUserRoles` | Bảng join user ↔ role (quan hệ n-n) |

Quan hệ (theo tài liệu Microsoft): mỗi `User` có nhiều `UserClaim`/`UserLogin`/`UserToken`; mỗi `Role` có nhiều `RoleClaim`; `User`↔`Role` là **n-n** qua bảng join `UserRole`.

Bạn **không** phải tự tạo 7 bảng này, chỉ cần cho `IdentityModuleDbContext` kế thừa lớp base `IdentityDbContext<TUser, TRole, TKey>`, EF sẽ sinh cả 7 bảng khi migration. Chi tiết ở [Bước 3](03-dbcontext.md).

> Nguồn: [Identity model customization (Microsoft Learn, aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0), mục "The Identity model" liệt kê 7 entity + tên bảng.

## A.4. Phần bạn tự thêm: `RefreshToken`

Identity **không** có sẵn entity cho refresh token kiểu bạn cần (access token ngắn hạn + refresh token dài hạn để cấp lại, dùng ở Day 4). Đây là entity **của riêng bạn**: một bảng `RefreshTokens`, quan hệ **1-n** với User (một user có nhiều refresh token qua thời gian: nhiều thiết bị, và mỗi lần rotation đẻ một token mới).

> **Đừng nhầm** `IdentityUserToken`/`AspNetUserTokens` với `RefreshToken` của bạn. `IdentityUserToken` là chỗ Identity lưu token cho provider ngoài (vd token của Google/2FA), **không phải** refresh token JWT của bạn. Hai khái niệm khác nhau, hai bảng khác nhau.

## A.5. Hai quyết định đã chốt (và vì sao)

Đây là phần **quan trọng nhất** của Day 3. Hai quyết định dưới đã cùng mentor chốt. Các bước sau viết theo lựa chọn này.

### Quyết định 1. Kiểu khóa chính: **`Guid`** ✅

Mặc định `IdentityUser` dùng khóa chính kiểu `string` (một GUID lưu dạng text). Ta đổi sang **`Guid` thật** bằng cách kế thừa bản generic `IdentityUser<Guid>` / `IdentityRole<Guid>` và dùng context `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`.

**Vì sao Guid:**

- Postgres có kiểu `uuid` native (16 byte), gọn và index tốt hơn cột text lưu GUID (37 byte).
- Kiểu rõ ràng ở tầng C# (`Guid` chứ không phải `string` mơ hồ), tránh truyền nhầm.
- Là điểm kể được: "tôi cân nhắc kiểu khóa và chọn `uuid` thật thay vì string default".

**Cạm bẫy quan trọng:** đổi kiểu khóa **phải làm ở migration ĐẦU TIÊN**. Đổi PK sau khi bảng đã tạo là rất khó (phải drop/tạo lại bảng, [xem "Change the primary key type"](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0#change-the-primary-key-type)). Vì Day 2 đã áp một migration rỗng `InitialCreate`, ta sẽ **dựng lại migration đó** (Hướng B ở [Bước 4](04-migration.md)) để `Guid` là kiểu khóa ngay từ đầu.

### Quyết định 2. `ApplicationUser`/`ApplicationRole` ở **Infrastructure**; `RefreshToken` (POCO) ở **Domain**; abstraction **`IIdentityService`** ở Application ✅

Đây là cách **các hệ thống lớn** (mẫu Clean Architecture của Jason Taylor, app tham chiếu **eShopOnWeb** của Microsoft) giải bài toán này. Cốt lõi là **Dependency Inversion**: đừng để lõi biết mặt framework, chỉ cho lõi biết một *hợp đồng* (interface) mà nó cần.

**Mâu thuẫn cốt lõi: hai luật "sạch" đánh nhau.** đặt entity kế thừa `IdentityUser` ở đâu thì project đó phải reference package chứa `IdentityUser`.

- **Luật DDD:** Domain nên thuần POCO, không dính framework.
- **Luật phân lớp:** Application **không** được reference Infrastructure (chỉ Infrastructure → Application/Domain, không ngược).

Cái *bẫy* dẫn tới lời giải sai: "Ở Day 4, handler Login/Register cần inject `UserManager<ApplicationUser>` → Application phải *thấy* type `ApplicationUser` → nên nhét `ApplicationUser` xuống Domain cho Application với tới." Đây chính là giả định mà hệ thống lớn **từ chối**: Application **không** trực tiếp đụng `UserManager` hay `ApplicationUser` chút nào.

**Lời giải: chèn một abstraction (`IIdentityService`).**

- **Interface `IIdentityService`** → đặt ở **`EventHub.Identity.Application`**. Nó là *hợp đồng thuần*, surface toàn **primitive**: vào là `string`/`bool` (userName, password, userId, role…), ra là `Result` + `userId`. **Không** type Identity nào lọt ra ngoài interface. (Method thật khai ở **Day 4** khi viết handler; Day 3 chỉ cần hiểu quyết định này để đặt entity đúng chỗ.)
- **Class hiện thực `IdentityService`** (giữ `UserManager<ApplicationUser>`) → đặt ở **`EventHub.Identity.Infrastructure`**. Đây là chỗ *duy nhất* chạm `UserManager`/`ApplicationUser`.
- **`ApplicationUser`/`ApplicationRole`** (kế thừa `IdentityUser<Guid>`/`IdentityRole<Guid>`) → đặt ở **`EventHub.Identity.Infrastructure`** (auth là mối lo hạ tầng). **Không** leo lên Domain.
- **`RefreshToken`** → POCO thuần ở **`EventHub.Identity.Domain`**, chỉ giữ **`UserId: Guid`** (id-reference), **không** navigation trỏ ngược `ApplicationUser` (điều đó sẽ là Domain → Infrastructure, cấm).

**Vì sao lời giải này thắng *cả hai* luật (không phải đánh đổi):**

| Luật | Nếu nhét `ApplicationUser` vào Domain (compromise) | Lời giải `IIdentityService` |
|------|-----------------------------------------------------|------------------------------|
| DDD: Domain thuần POCO | ❌ vi phạm, Domain kéo package Identity | ✅ Domain sạch trơn, **0** package Identity |
| Phân lớp: App không ref Infra | ✅ giữ được | ✅ App chỉ dùng interface *nằm trong chính nó* |

Cơ chế: Application **sở hữu** cái abstraction nó cần (`IIdentityService`); Infrastructure **implement** và chịu bẩn framework. Chiều tham chiếu vẫn **Infrastructure → Application → Domain**, đúng chuẩn, không đảo. Handler Day 4 inject `IIdentityService`, nó không hề biết `UserManager` tồn tại.

**Cái giá phải trả (nói thẳng):** thêm một lớp adapter (interface + class impl); surface primitive nên mỗi nhu cầu Identity mới (đổi mật khẩu, gen token…) phải khai thêm một method trên interface; Domain trỏ user bằng `UserId` trần thay vì navigation hai chiều. Đổi lại: lõi domain **thuần tuyệt đối** và có thể test/thay Identity mà không đụng Application. Đây đúng là thứ đáng "kể khi phỏng vấn" cho một CV piece về kiến trúc.

```text
EventHub.Identity.Domain         → RefreshToken (POCO thuần: UserId: Guid), KHÔNG package Identity
EventHub.Identity.Application    → IIdentityService (interface, surface primitive)  [method: Day 4]
EventHub.Identity.Infrastructure → ApplicationUser/ApplicationRole (: IdentityUser<Guid>)
                                   + IdentityModuleDbContext (kế thừa base Identity EF) + cấu hình 1-n
                                   + IdentityService : IIdentityService (giữ UserManager, Day 4)
                                   ref Microsoft.AspNetCore.Identity.EntityFrameworkCore
Chiều ref: Infrastructure → Application → Domain   (đúng chuẩn, không đảo)
```

> Câu chốt khi phỏng vấn: *"Tôi coi ASP.NET Core Identity là hạ tầng: `ApplicationUser` và `UserManager` sống trong Infrastructure. Application chỉ phụ thuộc một abstraction `IIdentityService` mà chính nó định nghĩa (Dependency Inversion), nên Domain thuần POCO tuyệt đối và Application không hề reference Infrastructure. Đây là pattern của Clean Architecture template (Jason Taylor) và eShopOnWeb. Domain trỏ user bằng `UserId` (id-reference qua ranh giới), không navigation hai chiều. Giá phải trả là một lớp adapter, nhưng đổi lại lõi domain tách được hoàn toàn khỏi framework auth."*

## A.6. Xong bước này khi

- [x] Bạn kể được 7 bảng `AspNet*` dùng làm gì và quan hệ giữa chúng.
- [x] Bạn phân biệt được `IdentityUserToken` (của Identity) vs `RefreshToken` (của bạn).
- [x] Bạn nói lại được **vì sao** chọn `Guid`, và **vì sao** `ApplicationUser` ở Infrastructure còn Domain chỉ giữ `RefreshToken` (POCO): cơ chế `IIdentityService` giải *cả hai* luật sạch nhờ Dependency Inversion, chứ không đánh đổi. Không chỉ "đã chốt vậy".

→ Sang [Bước 1. Thêm package Identity EF](01-package.md).
