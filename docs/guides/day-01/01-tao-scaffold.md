# Bước 1 — Tạo scaffold (solution + project) từ số 0

> Mục tiêu: dựng bộ khung solution Modular Monolith — file `.slnx`, một host, 4 project cho module Identity, 2 project Shared — rồi đưa tất cả vào solution.
>
> Bước này **toàn lệnh CLI** — cứ chạy theo đúng thứ tự. Đây là lúc dễ sai *kiểu project* nhất, nên đọc kỹ phần "vì sao classlib".

---

## 1.1. Tạo file solution

Tại **thư mục gốc repo** (nơi bạn muốn chứa solution), chạy:

```bash
dotnet new sln --name EventHub
```

- `dotnet new sln` — tạo file solution. Từ .NET 10, nó sinh ra định dạng **`.slnx`** (XML, người đọc được) theo mặc định.
- `--name EventHub` — đặt tên file là `EventHub.slnx`. Nếu bỏ cờ này, tên file sẽ lấy theo tên thư mục hiện tại (có thể không phải `EventHub`), nên ta khai rõ.

Sau lệnh, ở gốc repo có `EventHub.slnx`. Mở ra xem: hiện nó gần như rỗng (`<Solution>` chưa có project nào) — đúng như mong đợi.

## 1.2. Tạo host (project DUY NHẤT là web)

```bash
dotnet new web --output src/Bootstrap/EventHub.Api
```

- `dotnet new web` — tạo một ứng dụng **ASP.NET Core Empty (Minimal API)**. Đây là **host / composition root** — project *duy nhất* trong cả solution chạy độc lập (có `Program.cs`, `app.Run()`).
- `--output <đường-dẫn>` — thư mục đích; tên project lấy theo tên thư mục cuối (`EventHub.Api`).

> **Vì sao `dotnet new web` chứ không phải `dotnet new webapi`?** Bản `web` (Empty) sinh `Program.cs` **tối giản** — chỉ một endpoint mẫu `/` trả `"Hello World!"`. Bản `webapi` lại kèm cả demo `weatherforecast` (mảng summaries + record) — rác không cần cho một composition root. Ta chọn bản tối giản; ở [Bước 4](04-don-template.md) chỉ cần chỉnh endpoint mẫu thành endpoint sức khỏe.

## 1.3. Tạo 4 project của module Identity (TẤT CẢ là classlib)

Chạy lần lượt 4 lệnh:

```bash
dotnet new classlib --output src/Modules/Identity/EventHub.Identity.Domain
dotnet new classlib --output src/Modules/Identity/EventHub.Identity.Application
dotnet new classlib --output src/Modules/Identity/EventHub.Identity.Infrastructure
dotnet new classlib --output src/Modules/Identity/EventHub.Identity.Api
```

> **CỰC KỲ QUAN TRỌNG — đọc kỹ:** để ý project thứ tư, `EventHub.Identity.Api`, **cũng** dùng `dotnet new classlib`, **KHÔNG** dùng `dotnet new web`. Module Api chỉ là thư viện chứa định nghĩa endpoint, sẽ được host Bootstrap nạp vào ở [Day 2](../README.md). Nếu lỡ tạo nó bằng `dotnet new web`, bạn sẽ có **hai host** với hai `Program.cs` — phá vỡ "một process duy nhất" của Modular Monolith, và phải xóa làm lại. (Lý do đầy đủ ở [Bước A, mục A3](00-kien-truc-tong-quan.md).)

Mỗi lệnh `classlib` sinh kèm một file `Class1.cs` rỗng — ta xóa ở [Bước 4](04-don-template.md).

## 1.4. Tạo 2 project Shared

```bash
dotnet new classlib --output src/Shared/EventHub.SharedKernel
dotnet new classlib --output src/Shared/EventHub.Contracts
```

(Vai trò hai project này: xem [Bước A, mục A4](00-kien-truc-tong-quan.md). Hôm nay chỉ dựng khung rỗng.)

## 1.5. Đưa tất cả project vào solution

Đến giờ các project mới tồn tại **trên đĩa** nhưng solution chưa "biết" chúng. Thêm cả 7 vào `EventHub.slnx` bằng một lệnh:

```bash
dotnet sln EventHub.slnx add src/Bootstrap/EventHub.Api/EventHub.Api.csproj src/Modules/Identity/EventHub.Identity.Domain/EventHub.Identity.Domain.csproj src/Modules/Identity/EventHub.Identity.Application/EventHub.Identity.Application.csproj src/Modules/Identity/EventHub.Identity.Infrastructure/EventHub.Identity.Infrastructure.csproj src/Modules/Identity/EventHub.Identity.Api/EventHub.Identity.Api.csproj src/Shared/EventHub.SharedKernel/EventHub.SharedKernel.csproj src/Shared/EventHub.Contracts/EventHub.Contracts.csproj
```

- `dotnet sln EventHub.slnx add <csproj...>` — thêm một hoặc nhiều project vào solution.
- Khi đường dẫn project có thư mục cha (vd `src/Modules/Identity`), `dotnet sln` **tự tạo solution folder tương ứng** trong `.slnx` để nhóm chúng cho gọn. Bạn **không phải sửa XML bằng tay**.

*Tham khảo:* [dotnet sln command — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln) · [dotnet new — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new).

## 1.6. Kiểm chứng

Liệt kê project trong solution:

```bash
dotnet sln EventHub.slnx list
```

Phải thấy **đủ 7 project**: host `EventHub.Api`, bốn project Identity, và hai project Shared. Sau đó build:

```bash
dotnet build EventHub.slnx
```

Phải `Build succeeded`. (Lúc này host vẫn còn endpoint mẫu `"Hello World!"`, các library còn `Class1.cs` — bình thường, dọn ở Bước 4.)

> **Cạm bẫy thường gặp:**
> - **Quên `dotnet sln ... add`** → project có trên đĩa nhưng không nằm trong solution; IDE không hiện và `dotnet build EventHub.slnx` không build nó. Luôn chạy `dotnet sln list` để xác nhận.
> - **Lỡ tạo `EventHub.Identity.Api` bằng `dotnet new web`** → có `Program.cs`/`appsettings`/`launchSettings` thừa. Nếu thấy, xóa project đó và tạo lại bằng `classlib` (rồi `dotnet sln add` lại).

## 1.7. Về việc tham chiếu giữa project (chưa làm hôm nay)

Sau này, khi một project (vd `EventHub.Identity.Domain`) cần dùng `Result<T>` từ SharedKernel, bạn sẽ thêm **project reference** bằng `dotnet add <project> reference <project khác>`. **Chưa cần hôm nay** — hôm nay chỉ dựng khung.

> **Ranh giới cứng cần nhớ từ ngày đầu:** module được phép tham chiếu `SharedKernel` và `Contracts`, nhưng **tuyệt đối không** tham chiếu `Domain`/`Infrastructure` của module *khác*. Giữ đúng để Tuần 4 (architecture test) không bắt lỗi.

## 1.8. Xong bước này khi

- [ ] `EventHub.slnx` tồn tại ở gốc repo.
- [ ] `dotnet sln EventHub.slnx list` hiện đủ 7 project.
- [ ] `EventHub.Identity.Api` là class library (không có `Program.cs` riêng).
- [ ] `dotnet build EventHub.slnx` xanh.

→ Sang [Bước 2 — Central Package Management](02-central-package-management.md).
