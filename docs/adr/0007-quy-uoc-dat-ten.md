# ADR-0007: Chốt quy ước đặt tên thư mục, file và namespace cho toàn repo

## Trạng thái

Accepted — 2026-07-20

## Bối cảnh

Sau khi làm tới Day 6, cách đặt tên bên trong các project đã lệch nhau đủ để gây khó chịu khi tìm file. Interface khi thì nằm trong `Abstractions/` (ở tầng Application của hai module), khi thì `Interfaces/` (ở `Finmy.Modularity`). Thư mục DTO đặt số ít `Dto/`. Service của Budgeting mang tên số nhiều `EnvelopesService` trong khi service của Identity là `AuthService` số ít. Namespace của Identity lặp tên module hai lần: `Finmy.Identity.Domain.Identity`, `Finmy.Identity.Infrastructure.Identity`. `ValidationFilter` để trơ ở gốc `Finmy.Modularity`. DbContext mang tên dài `IdentityModuleDbContext` / `BudgetingModuleDbContext`.

Không cái nào sai về mặt biên dịch, nhưng cộng lại thì mở một project lạ phải dò mới thấy file cần tìm. Đây là project để kể khi phỏng vấn, nên tính nhất quán đọc-hiểu được xem trọng ngang tính chạy được.

## Các phương án đã cân nhắc

- **Giữ nguyên, chấp nhận lệch.** Rẻ nhất bây giờ, nhưng mỗi module viết sau lại thừa hưởng chỗ lệch, càng về sau sửa càng đắt.

- **Chuẩn hóa tối thiểu** — chỉ gom những chỗ mâu thuẫn rõ (interface folder, service số nhiều), không đụng DbContext và namespace lặp. Đỡ rủi ro nhưng để lại nửa vời.

- **Chuẩn hóa triệt để** — chốt một bảng quy ước, áp cho tất cả: interface về `Abstractions/`, DTO thành `Dtos/`, service số ít, bỏ chữ `Module` khỏi DbContext, tách namespace lặp thành `RefreshTokens/` và `Users/`, đưa `ValidationFilter` vào `Filters/`.

## Quyết định

Chọn phương án triệt để. Bảng quy ước đầy đủ và lý do từng mục nằm ở [docs/naming-conventions.md](../naming-conventions.md); tài liệu đó là nguồn tra cứu, ADR này ghi lại việc đã chốt và vì sao.

Vài điểm đáng ghi riêng vì có đánh đổi:

- **Interface gom về `Abstractions/`.** Hợp với nếp của Microsoft (assembly `*.Abstractions`). Không chọn `Contracts` vì đã có project `Finmy.Contracts` giữ integration event cross-module — trùng tên là trộn hai khái niệm khác hẳn.

- **DbContext bỏ chữ `Module`.** Riêng Identity, `IdentityDbContext` của mình kế thừa `IdentityDbContext<TUser, TRole, TKey>` của ASP.NET Core Identity. Hai cái trùng tên đơn nhưng khác số tham số generic nên C# phân biệt được và biên dịch sạch; danh sách kế thừa ghi rõ tham số nên không có chỗ mơ hồ. Chấp nhận điểm trùng tên này để đổi lấy nhất quán với `BudgetingDbContext`.

- **Đổi tên class DbContext là thao tác an toàn với dữ liệu.** Bảng `__EFMigrationsHistory` khóa theo MigrationId, không theo tên class, nên đổi tên không cần chạy lại `database update`. Chỉ cần sửa đồng bộ tên trong file snapshot và các file Designer để lần `migrations add` sau không sinh ra diff giả. Tên file migration cũ và MigrationId giữ nguyên.

## Hệ quả

- Mở một project bất kỳ là đoán được file nằm đâu; module Ledger viết sau cứ theo cây mẫu mà làm.
- Có một tài liệu để trỏ tới khi review: đặt tên lệch bảng thì sửa bảng trước (kèm lý do) rồi mới đặt.
- Refactor này chỉ đổi tên và vị trí, không đổi hành vi. Build sạch, model snapshot của Identity không phát sinh thay đổi. Còn một khoản nợ có sẵn từ Day 6/7 lộ ra khi kiểm tra — cột `Category.Name` đã khai `HasMaxLength(200)` trong `OnModelCreating` nhưng chưa có migration cho nó — khoản này nằm ngoài phạm vi đổi tên, xử lý riêng.
</content>
