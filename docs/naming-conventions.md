# Quy ước đặt tên thư mục & file

Tài liệu này chốt cách đặt tên thư mục, file, namespace và class trong Finmy. Mục đích: mở một project bất kỳ là đoán được file nằm ở đâu, không phải đi dò. Áp cho mọi module đang có (Identity, Budgeting) và mọi module viết sau (Ledger).

Nền tảng tham khảo: [Framework Design Guidelines của Microsoft](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/), cách tổ chức feature-folder của [Milan Jovanović](https://milanjovanovic.tech/blog/clean-architecture-folder-structure) và [Anton Martyniuk](https://antondevtips.com/blog/how-to-structure-production-apps-with-vertical-slice-architecture-in-dotnet-in-2026).

## Bảng chốt

| Thành phần | Quy tắc | Ví dụ |
|---|---|---|
| Project | `Finmy.{Module}.{Layer}` | `Finmy.Budgeting.Application` |
| Feature folder | số nhiều theo entity, hoặc noun cho năng lực | `Envelopes/`, `Categories/`, `RefreshTokens/`, `Users/`, `Authentication/` |
| Port / interface | gom trong `Abstractions/` ở gốc project | `Application/Abstractions/IEnvelopeRepository.cs` |
| DTO (request, response, validator) | `{feature}/Dtos/` | `Envelopes/Dtos/CreateEnvelopeRequest.cs` |
| Service | số ít + hậu tố `Service` | `EnvelopeService`, `AuthService` |
| Endpoints | `{Entity}Endpoints` | `EnvelopeEndpoints` |
| DbContext | `{Module}DbContext` | `BudgetingDbContext`, `IdentityDbContext` |
| Persistence | `Persistence/` chứa DbContext, factory, repository | `Infrastructure/Persistence/` |
| Filter, behavior | gom theo vai trò, không để rơi ở gốc project | `Filters/ValidationFilter.cs` |

## Tại sao chọn từng cái

**`Abstractions/` cho interface.** Microsoft đóng gói interface vào assembly `*.Abstractions` (ví dụ `Microsoft.Extensions.Logging.Abstractions`), nên tên này quen mắt với người đọc .NET. Trước đây repo dùng lẫn cả `Abstractions/` (ở Application) lẫn `Interfaces/` (ở Modularity) — giờ gom về một tên. Tránh `Contracts` vì đã có project `Finmy.Contracts` giữ integration event cross-module, đặt trùng sẽ gây nhầm hai khái niệm khác hẳn nhau.

**Feature folder số nhiều.** Một thư mục `Envelopes/` gom mọi thứ thuộc về envelope: entity, error, service, DTO. Khi cần tách envelope thành service riêng sau này, bê cả folder đi là xong. Số nhiều vì nó chứa nhóm thứ liên quan tới nhiều envelope, không phải một class đơn lẻ. `Authentication/` là ngoại lệ hợp lý: nó là một năng lực, không phải một entity, nên để số ít.

**Class service số ít.** `EnvelopeService` chứ không `EnvelopesService`. Class là một thứ (một service), khác với folder là một nhóm. Đây cũng là nếp đặt tên chung của .NET cho service và handler.

**DbContext bỏ chữ `Module`.** `BudgetingDbContext` gọn hơn `BudgetingModuleDbContext` và vẫn rõ nó thuộc module nào. Riêng Identity, class `IdentityDbContext` của mình kế thừa `IdentityDbContext<TUser, TRole, TKey>` của ASP.NET Core Identity — hai cái trùng tên đơn nhưng khác số tham số generic nên C# phân biệt được, biên dịch không lỗi; danh sách kế thừa ghi rõ tham số generic nên không có chỗ nào mơ hồ.

**Không để namespace lặp tên module.** Trước đây có `Finmy.Identity.Domain.Identity` và `Finmy.Identity.Infrastructure.Identity` — chữ `Identity` lặp hai lần, đọc rối. Giờ tách theo đúng thứ nó chứa: `RefreshTokens/` cho `RefreshToken`, `Users/` cho `ApplicationUser` và `ApplicationRole`.

**File không nằm trơ ở gốc project.** `ValidationFilter` từng để thẳng ở gốc `Finmy.Modularity`, giờ vào `Filters/`. Mỗi file có một folder nói lên vai trò của nó.

## Cây một module mẫu

```text
Finmy.Budgeting.Domain/
  Envelopes/            Envelope.cs, EnvelopeErrors.cs
  Categories/           Category.cs
Finmy.Budgeting.Application/
  Abstractions/         IEnvelopeRepository.cs, ICategoryRepository.cs
  Envelopes/            EnvelopeService.cs
    Dtos/               CreateEnvelopeRequest.cs, EnvelopeResponse.cs, CreateEnvelopeRequestValidator.cs
Finmy.Budgeting.Infrastructure/
  Persistence/          BudgetingDbContext.cs, BudgetingDbContextFactory.cs, EnvelopeRepository.cs, CategoryRepository.cs
  Migrations/
  DependencyInjection.cs
Finmy.Budgeting.Api/
  Endpoints/            EnvelopeEndpoints.cs
  BudgetingModule.cs
```

## Khi thêm module mới

Theo đúng cây trên. Nếu thấy mình sắp đặt một tên không nằm trong bảng chốt, sửa bảng trước (kèm lý do), rồi mới đặt — để tài liệu luôn là nguồn tra cứu đúng, không phải bản mô tả lạc hậu.
</content>
