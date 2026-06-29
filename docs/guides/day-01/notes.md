# Ghi chú & đính chính sau review Day 1

> Hai khái niệm dưới đây là phần dễ hiểu *mơ hồ* nhất Day 1. Bản ghi này chốt lại cách hiểu **đúng cơ chế** — chính chỗ này mới là điểm "kể được khi phỏng vấn", không phải chỉ "làm xong".

---

## Ghi chú 1 — `Directory.Build.props` (và CPM) được nạp *lúc nào* và *vì sao* đặt ở gốc

**Nạp per-project, rất sớm — không phải "lúc build solution".**
Khi MSBuild evaluate **mỗi** `.csproj`, nó tự động import `Directory.Build.props` *trước cả khi* xử lý SDK của project đó. Nhờ vậy ta đặt được `TargetFramework` ở file gốc mà SDK vẫn hiểu.

- **Kiểm chứng:** chạy `dotnet build` trên **một** project lẻ (không qua `.slnx`) — file vẫn áp dụng. Solution **không liên quan** tới việc nạp file này.

**Đặt ở gốc vì cơ chế đi-ngược-cây, không phải vì "cùng cấp `.slnx`".**
MSBuild **không nhìn `.slnx`** khi tìm file này. Từ thư mục chứa mỗi `.csproj`, nó **đi ngược lên cây thư mục cha**, gặp `Directory.Build.props` **đầu tiên** thì import rồi **dừng**. Gốc repo là **thư mục tổ tiên của mọi project**, nên mọi project đi ngược lên đều chạm tới. Đặt sai (vd trong `src/`) thì project ngoài nhánh đó sẽ không thấy.

- `Directory.Packages.props` (Central Package Management) dùng **đúng cùng cơ chế** này → cũng đặt ở gốc.

**Câu chốt khi phỏng vấn:** *"Nó được import per-project, rất sớm trong evaluation, qua cơ chế MSBuild đi ngược cây thư mục và dừng ở file đầu tiên — nên đặt ở gốc để là tổ tiên chung của mọi project."*

---

## Ghi chú 2 — Vì sao module Api là `Microsoft.NET.Sdk` chứ không `Microsoft.NET.Sdk.Web`

**Vế đúng:** module Api chỉ *khai báo* endpoint; host `Bootstrap/EventHub.Api` là composition root **duy nhất** nạp các endpoint đó vào.

**Đính chính lý do hỏng:** nhiều project `.Web` **không** gây *lỗi biên dịch* và IDE vẫn cho chọn startup project tay. Vấn đề là **kiến trúc/khái niệm**, không phải compiler:

- `Microsoft.NET.Sdk.Web` nghĩa là *"tôi là một **web application / host**"* — kéo theo `launchSettings.json`, static web assets (`wwwroot`), output coi như app khởi chạy, và ngầm định có **điểm vào host**.
- Nếu mỗi module là `.Web` và có `Program.cs`/`app.Run()` riêng → có **nhiều composition root** → phá vỡ "một process duy nhất".

**Điểm tinh tế (sẽ gặp ở Day 2):** module Api *vẫn cần* kiểu ASP.NET Core (`IEndpointRouteBuilder`, `MapGet`...) để khai endpoint. Cách lấy chúng cho một **library** là thêm `FrameworkReference` tới `Microsoft.AspNetCore.App` vào project `Microsoft.NET.Sdk` thường — **không** đổi sang SDK `.Web`.

- Phân biệt: `FrameworkReference` = *"library dùng nhờ kiểu của ASP.NET Core"*; `Sdk.Web` = *"tôi chính là web app"*. Module muốn cái thứ nhất.
