# Bước 2 — EF Core 10 + Npgsql vào CPM, cài `dotnet-ef`

> Mục tiêu: đưa EF Core 10 và provider Postgres (Npgsql) vào solution **đúng kiểu CPM** (đã bật từ Day 1), và cài công cụ CLI `dotnet-ef` để tạo/áp migration.
>
> Lưu ý mentor: `.csproj` và `Directory.Packages.props` là XML, **mình không viết hộ**. Mình mô tả cần thêm gì ở đâu; bạn tự gõ.

---

## 2.1. Cái gì

Khai version các package EF Core ở **một nơi** (`Directory.Packages.props`), rồi cho project **Infrastructure** của module Identity tham chiếu chúng (không kèm version, đúng luật CPM). Cài công cụ `dotnet-ef`.

## 2.2. Vì sao

> TODO mentor: giải thích vì sao EF Core lại nằm ở **Infrastructure** chứ không phải Domain/Application (nhắc lại chiều phụ thuộc Clean Architecture ở [Day 1](../day-01/00-kien-truc-tong-quan.md): Domain không biết EF). Vì sao cần **provider riêng** (`Npgsql.EntityFrameworkCore.PostgreSQL`) ngoài package EF Core core. Phân biệt **`dotnet-ef` (công cụ CLI)** với **`Microsoft.EntityFrameworkCore.Design` (package design-time)** — vì sao thường cần cả hai để migration chạy.

## 2.3. Các bước làm

Mô tả bằng lời:

1. **Khai version trong CPM:** mở `Directory.Packages.props` ở gốc, thêm các phần tử `PackageVersion` cho hai package sau (đã đối chiếu NuGet, dòng .NET 10 — xem mục Link):
   - **`Npgsql.EntityFrameworkCore.PostgreSQL`** — version **`10.0.2`**. Đây là provider Postgres; nó **đã kéo theo** `Microsoft.EntityFrameworkCore` và `Microsoft.EntityFrameworkCore.Relational` (yêu cầu `>= 10.0.4`) như phụ thuộc, nên bạn **không cần** tự khai gói EF Core core.
   - **`Microsoft.EntityFrameworkCore.Design`** — version **`10.0.x`** (cùng dòng EF Core 10, vd `10.0.4` trở lên cho khớp). Gói design-time mà `dotnet ef` cần để tạo/áp migration.
2. **Tham chiếu trong project Infrastructure:** mở `src/Modules/Identity/EventHub.Identity.Infrastructure/EventHub.Identity.Infrastructure.csproj`, thêm `PackageReference` tới hai package trên — **chỉ `Include`, KHÔNG `Version`** (CPM lo version). Gói `Microsoft.EntityFrameworkCore.Design` nên đặt **`PrivateAssets="all"`** (nó chỉ phục vụ thời điểm dev/migration, không nên chảy sang project khác tham chiếu Infrastructure).
3. **Cài công cụ `dotnet-ef`:** cài như tool. Khuyến nghị tạo **tool manifest cục bộ** để version `dotnet-ef` cũng cố định theo repo (chạy `dotnet new tool-manifest` rồi cài vào manifest), thay vì cài global lệch máy — máy khác chỉ cần `dotnet tool restore`.

> Lưu ý version: `10.0.2` là bản provider Npgsql mới nhất tại thời điểm viết; trước khi gõ hãy mở [trang NuGet của provider](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/) xác nhận bản ổn định mới nhất của dòng `10.x` và để `Microsoft.EntityFrameworkCore.Design` **cùng hoặc cao hơn** version EF Core mà provider yêu cầu (`>= 10.0.4`).

Các lệnh CLI liên quan (chạy được, không phải code):

```bash
dotnet new tool-manifest          # tạo .config/dotnet-tools.json (1 lần)
dotnet tool install dotnet-ef     # cài vào manifest cục bộ
dotnet tool restore               # máy khác clone về thì restore tool
dotnet ef --version               # xác nhận chạy được
```

## 2.4. Kiểm chứng

```bash
dotnet restore EventHub.slnx
dotnet build EventHub.slnx
dotnet ef --version
```

- Restore không báo `NU1008` (lỗi này nghĩa là lỡ để `Version` trong `PackageReference` khi CPM đang bật — sửa lại cho đúng luật).
- Build xanh; `dotnet ef --version` ra số.

## 2.5. Cạm bẫy thường gặp

- **`NU1008`:** để cả `Version` trong `PackageReference` lẫn CPM bật → restore lỗi. Bỏ `Version` khỏi `PackageReference`, chỉ giữ trong `Directory.Packages.props`.
- **Lệch version provider vs EF Core:** Npgsql provider phải cùng dòng major với EF Core 10, nếu không sẽ lỗi runtime khó hiểu.
- **Quên `*.Design`:** thiếu package design-time → `dotnet ef migrations add` báo không tìm được DbContext / thiếu công cụ.
- **`dotnet-ef` global lệch version giữa các máy/CI:** tool manifest cục bộ tránh được điều này.

## 2.6. Góc kể khi phỏng vấn

> TODO mentor: điền — gợi ý "fix version qua CPM + tool manifest để build tái lập", "tách provider khỏi EF core thể hiện hiểu kiến trúc pluggable của EF".

## 2.7. Link tài liệu chính thức

- [Npgsql.EntityFrameworkCore.PostgreSQL trên NuGet (10.0.2)](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/) · [Npgsql EF Core 10.0 release notes](https://www.npgsql.org/efcore/release-notes/10.0.html)
- [EF Core — Npgsql provider (docs)](https://www.npgsql.org/efcore/)
- [Cài đặt EF Core tools (`dotnet-ef`)](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)
- [Tool manifest — local tools](https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use)
- [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)

## 2.8. Xong bước này khi

- [x] `Directory.Packages.props` có `PackageVersion` cho provider Npgsql + `*.Design` (+ core nếu cần).
- [x] Project Infrastructure tham chiếu các package (không kèm version).
- [x] `dotnet ef --version` chạy được.
- [x] `dotnet build EventHub.slnx` vẫn xanh.

→ Sang [Bước 3 — DbContext + migration đầu tiên](03-dbcontext-migration.md).
