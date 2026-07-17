# ADR-0004: Ranh giới module Identity theo Option A (Dependency Inversion / IIdentityService)

## Trạng thái

Accepted — 2026-07-12

## Bối cảnh

Module Identity dùng ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`). Vướng mắc kiến trúc: handler đăng nhập/đăng ký (ở lớp Application) cần thao tác với `UserManager<ApplicationUser>`, mà `UserManager` và `ApplicationUser` là type dính chặt framework Identity + EF. Đặt chúng ở đâu để không phá hai luật cùng lúc:

- **Luật DDD:** Domain nên thuần POCO, không dính framework.
- **Luật phân lớp:** Application không được phụ thuộc Infrastructure (chỉ Infra → Application → Domain).

Lối compromise quen thuộc là nhét `ApplicationUser` xuống Domain để Application với tới. Nghe xuôi nhưng nó vá layering bằng cách bẩn Domain — chọn hy sinh luật DDD để cứu luật phân lớp.

## Các phương án đã cân nhắc

- **Compromise "entity ở Domain"** — đặt `ApplicationUser` ở Domain cho Application thấy trực tiếp. Đơn giản, nhưng Domain hết thuần POCO (kéo package Identity/EF vào lõi), và về bản chất là đánh đổi một luật lấy luật kia.

- **Option A (Dependency Inversion qua `IIdentityService`)** — chèn một abstraction: interface `IIdentityService` khai ở Application, surface toàn primitive; implementation giữ `UserManager` nằm ở Infrastructure. Đây là pattern các hệ thống lớn dùng (Clean Architecture template của Jason Taylor, eShopOnWeb). Không hy sinh luật nào; giá chỉ là một lớp adapter.

## Quyết định

Chúng tôi chọn **Option A**. Cụ thể:

- `ApplicationUser` / `ApplicationRole` + `IdentityDbContext` + implementation `IdentityService` đặt ở **`Finmy.Identity.Infrastructure`** — coi auth là hạ tầng.
- `RefreshToken` là POCO thuần ở **`Finmy.Identity.Domain`**, chỉ giữ `UserId` kiểu `Guid` (id-reference), **không** navigation ngược tới `ApplicationUser`.
- Interface **`IIdentityService`** đặt ở **`Finmy.Identity.Application`** — abstraction đi lên, surface primitive (`string`/`bool`/`Result` + `userId`), không lộ type Identity.
- Khóa chính kiểu `Guid`: `ApplicationUser : IdentityUser<Guid>`, `ApplicationRole : IdentityRole<Guid>`, context `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`.
- Dùng `AddIdentityCore` (không `AddIdentity`) vì auth đi qua JWT, không cần cookie/UI.

Nguyên tắc gọn: **abstraction đi lên (Application), implementation đi xuống (Infrastructure)**; DI ở Bootstrap mới ráp hai cái lúc chạy.

## Hệ quả

- Domain thuần POCO tuyệt đối — 0 package Identity/EF; Application không reference Infrastructure. Chiều reference đúng chuẩn: Infra → Application → Domain. Handler inject `IIdentityService`, không hề biết `UserManager` tồn tại. Đây là thứ NetArchTest sẽ kiểm được.
- Giá phải trả: một lớp adapter (interface + impl) và surface primitive — mỗi nhu cầu Identity mới phải thêm một method vào `IIdentityService`. Chi phí này có thật nhưng nhỏ, và đổi lấy lõi tách hoàn toàn khỏi framework auth.
- Khóa `Guid` khiến migration `InitialCreate` phải dựng để PK là uuid từ đầu; đổi lại tránh lộ số tuần tự và hợp cho id-reference cross-module sau này.
