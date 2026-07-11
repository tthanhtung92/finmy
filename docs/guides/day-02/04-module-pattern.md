# Bước 4: Pattern `IModule` + `AddModules()` / `UseModules()`

> Mục tiêu: cho host Bootstrap nạp module một cách **có kỷ luật**: mỗi module tự đăng ký service + endpoint của mình; host chỉ gọi chung, không biết tên từng module.
>
> Lưu ý mentor: interface, extension method, `Program.cs` đều là code, **mình không viết hộ**. Mình mô tả hợp đồng (contract) cần dựng; bạn tự gõ.

---

## 4.1. Cái gì

Định nghĩa một interface `IModule` (hợp đồng "một module biết tự đăng ký"), cho module Identity hiện thực nó, và viết hai extension method `AddModules()` (cấu hình DI) + `UseModules()` (cấu hình pipeline/endpoint) để host gọi một lần là nạp hết.

## 4.2. Vì sao

**Đảo từ "host biết mọi module" sang "module tự khai báo".** Với cách wire tay, `Program.cs` của host phải *biết tên* và gọi thẳng từng `AddIdentityServices()`, `MapIdentityEndpoints()`, `AddEventsServices()`... Mỗi lần thêm module = sửa composition root, coupling chặt, và `Program.cs` phình dần thành nơi biết mọi chi tiết. Pattern `IModule` đảo ngược trách nhiệm: mỗi module tự khai "tôi đăng ký service gì, map endpoint gì"; host chỉ gọi chung `AddModules()`/`UseModules()`. Điều này gần với **Open/Closed**: mở để mở rộng (thêm module), đóng để sửa (không phải mổ `Program.cs` mỗi lần), với sắc thái ở mục 4.3 về "explicit vs reflection".

**Nối với Day 1.** Module Api là **class library** (không phải host, xem [Day 1](../day-01/00-kien-truc-tong-quan.md)). Bản thân library không tự chạy; `IModule` chính là **hợp đồng để host "gọi vào" library đó**: host phát hiện các `IModule` rồi kích hoạt phần đăng ký/map của chúng. Pattern này là chất keo giữa "composition root duy nhất" và "các module là library".

**`IModule` KHÔNG phải kênh giao tiếp cross-module.** Nhắc lại ranh giới cứng [ROADMAP mục 3](../../ROADMAP.md): module giao tiếp với nhau **chỉ** qua `EventHub.Contracts` (integration events) trên Wolverine. `IModule` chỉ để *host nạp* module lúc khởi động, module **không** được dùng nó để gọi sang module khác. Nhầm hai vai trò này là phá vỡ tính modular ngay từ nền.

## 4.3. Các bước làm

Mô tả bằng lời (bạn tự gõ):

1. **Định nghĩa `IModule`.** Một interface với (tối thiểu) hai thành viên:
   - Một method nhận `IServiceCollection` (+ `IConfiguration` nếu cần) để module **đăng ký service** của nó vào DI (DbContext, handler, option...).
   - Một method nhận `IEndpointRouteBuilder` để module **map endpoint** Minimal API của nó.
   > **Quyết định đã chốt (ghi lại):** đặt `IModule` ở **project Shared riêng `src/Shared/EventHub.Modularity`**, *không* nhét vào `SharedKernel`. Lý do: `SharedKernel` là "viên gạch domain" (`Result<T>`, guard, domain-event base); còn `IModule` phụ thuộc kiểu ASP.NET Core (`IEndpointRouteBuilder`, `IServiceCollection`), trộn vào SharedKernel sẽ kéo phụ thuộc web vào lớp domain thuần. Tách project riêng giữ trách nhiệm sạch. Việc phát sinh (không ghi ở scaffold Day 1 vì lúc đó chưa cần): `dotnet new classlib` project này, `dotnet sln add`, thêm `FrameworkReference` `Microsoft.AspNetCore.App`, rồi các module Api + host reference nó.

2. **Module Identity hiện thực `IModule`.** Trong `src/Modules/Identity/EventHub.Identity.Api`, tạo một class hiện thực `IModule`: phần đăng ký service gọi vào Infrastructure (DbContext...), phần map endpoint khai các route của Identity. Nhớ từ [notes Day 1](../day-01/notes.md): project này là `Microsoft.NET.Sdk` thường nên cần **`FrameworkReference` tới `Microsoft.AspNetCore.App`** để dùng được `IEndpointRouteBuilder` / `MapGet`, **không** đổi sang SDK `.Web`.

3. **Viết `AddModules()` và `UseModules()` trong host.** Trong `src/Bootstrap/EventHub.Api`, hai extension method:
   - `AddModules()`: tìm tất cả `IModule`, gọi method đăng ký service của từng cái lên `IServiceCollection`.
   - `UseModules()`: gọi method map endpoint của từng `IModule` lên app.
   > **Quyết định đã chốt: danh sách tường minh (explicit), không reflection.** Hai cách host tìm `IModule`:
   >
   > - **Explicit registry (đang dùng):** host giữ một danh sách tường minh, vd `[new IdentityModule(), ...]`, rồi duyệt gọi. Thêm module = **thêm đúng một dòng** vào danh sách này.
   > - **Reflection scan:** quét mọi assembly tìm type hiện thực `IModule` → thêm module = *0 dòng* sửa ở host (Open/Closed "thuần").
   >
   > **Đánh đổi + vì sao chọn explicit:** reflection tự động hơn nhưng (1) **khó trace**: nhìn `Program.cs` không biết module nào được nạp; và (2) dính bẫy **"assembly chưa nạp thì quét ra rỗng"**: nếu host không reference (trực tiếp/gián tiếp) assembly module, JIT chưa nạp nó → scan không thấy → module *âm thầm* không map endpoint (404 khó hiểu, không lỗi). Ta chọn **explicit** để đổi lấy tính traceable và tránh bẫy đó, chấp nhận thêm một dòng mỗi khi có module mới. (Chi tiết ở [notes.md, Ghi chú 2](notes.md).)

4. **Gọi trong `Program.cs` của host.** Thay phần wire tay (nếu có) bằng `builder.Services.AddModules(...)` và `app.UseModules()`.

Luồng nạp module lúc host khởi động, hai pha tách nhau (đăng ký service trước khi build `app`, map endpoint sau):

```mermaid
sequenceDiagram
    participant Program as Program.cs (host)
    participant Registry as danh sách IModule (explicit)
    participant Module as IdentityModule
    Note over Program,Module: Pha 1 — cấu hình DI (trước app = builder.Build())
    Program->>Registry: AddModules(services, configuration)
    loop mỗi IModule trong danh sách
        Registry->>Module: ConfigureServices(services, configuration)
        Module-->>Registry: đăng ký DbContext, handler, option...
    end
    Note over Program,Module: Pha 2 — map endpoint (sau khi có app)
    Program->>Registry: UseModules(app)
    loop mỗi IModule trong danh sách
        Registry->>Module: MapEndpoints(app)
        Module-->>Registry: khai route (vd GET /identity/ping)
    end
```

> **Ranh giới:** `IModule` chỉ để **host nạp** module lúc khởi động. Module **không** dùng nó để gọi sang module khác — cross-module vẫn chỉ qua `EventHub.Contracts` trên Wolverine.

## 4.4. Kiểm chứng

```bash
dotnet build EventHub.slnx
dotnet run --project src/Bootstrap/EventHub.Api
```

- Build xanh.
- Host chạy; endpoint của module Identity (vd một endpoint thử do bạn map qua `IModule`) **gọi được**, chứng tỏ host đã nạp module qua `UseModules()`, không phải wire tay.
- Tắt host bằng `Ctrl+C`.

**Kiểm chứng cụ thể: endpoint "ping" tự chứng minh việc nạp.** Trong `IModule` của Identity, map một route thử, vd `GET /identity/ping` trả về một chuỗi. **Không** khai route này ở `Program.cs`. Chạy host rồi gọi nó (chỉnh port theo `launchSettings` của host):

```bash
curl http://localhost:<port>/identity/ping
```

Nếu ping trả về đúng chuỗi mà bạn **chỉ** khai trong module (không đụng `Program.cs`) → chứng minh host đã map endpoint *qua* `UseModules()`. Muốn chắc hơn nữa: tạm bỏ module khỏi danh sách trong `AddModules`/`UseModules` → gọi lại phải ra **404**; thêm lại → 200. Đó là bằng chứng trực tiếp pattern hoạt động. (Xóa endpoint ping sau khi kiểm xong nếu không muốn giữ.)

## 4.5. Cạm bẫy thường gặp

- **Module không được nạp nên reflection quét không thấy:** nếu host không reference (trực tiếp hoặc gián tiếp) assembly module, JIT chưa nạp nó → quét reflection ra rỗng. Đảm bảo host reference các project module Api.
- **Nhầm pattern module thành kênh giao tiếp cross-module:** `IModule` chỉ để host *nạp* module; module **không** được dùng nó để gọi sang module khác, cross-module vẫn chỉ qua Contracts/Wolverine.
- **Quên `FrameworkReference`:** module Api là library thường, thiếu `FrameworkReference` thì không có kiểu `IEndpointRouteBuilder` → không compile.
- **Thứ tự đăng ký:** một số service phải đăng ký trước khi build `app`. `AddModules()` chạy lúc cấu hình `builder.Services`; `UseModules()` chạy sau khi có `app`. Đừng đảo.

## 4.6. Góc kể khi phỏng vấn

*"Composition root của tôi không wire tay từng module. Mỗi module hiện thực `IModule` tự đăng ký service và map endpoint; host gọi chung qua `AddModules`/`UseModules`. Tôi chọn danh sách module tường minh thay vì reflection, chấp nhận thêm một dòng khi có module mới để đổi lấy tính traceable và tránh bẫy 'assembly chưa nạp thì quét ra rỗng'. Và tôi tách bạch hai khái niệm: `IModule` chỉ để host **nạp** module lúc khởi động; còn module **giao tiếp** với nhau thì chỉ qua integration events trên Wolverine, không bao giờ gọi trực tiếp."*

## 4.7. Link tài liệu chính thức

- [Minimal APIs: route groups & `IEndpointRouteBuilder`](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers)
- [Dependency injection trong ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection)
- [`FrameworkReference` cho thư viện dùng ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/target-aspnetcore)

## 4.8. Xong bước này khi

- [ ] `IModule` tồn tại (ghi rõ đặt ở project nào + lý do).
- [ ] Module Identity hiện thực `IModule` (đăng ký service + map endpoint).
- [ ] Host có `AddModules()` / `UseModules()`; `Program.cs` không còn wire tay từng module.
- [ ] Host chạy, endpoint của module gọi được.

→ Sang [Bước 5: Verify & commit](05-verify-commit.md).
