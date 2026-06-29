# Day 1 — Nền móng Solution (dựng từ số 0)

> **Mentor mode.** Tài liệu giải thích *vì sao* và *làm gì*, **không kèm code C#/cấu hình** — bạn tự gõ. Mọi lệnh CLI (`dotnet`, `git`) thì cứ chạy theo. Mỗi file dưới đây là **một bước**, làm tuần tự từ trên xuống.
>
> Viết cho người **mới**: nếu một câu khiến bạn phải đoán, đó là lỗi của tài liệu — nhắn mentor để bổ sung.

---

## Mục tiêu Day 1

Theo [ROADMAP](../../ROADMAP.md) (mục 5, Tuần 1 — Ngày 1): *Khởi tạo `EventHub.slnx`, cấu trúc thư mục, Central Package Management, `Directory.Build.props`, LICENSE (MIT), README khung → repo public push lên GitHub.*

Kết thúc Day 1 bạn có: *solution Modular Monolith **dựng từ số 0**, build sạch, có Central Package Management + build settings dùng chung, đã dọn rác template, LICENSE/README ổn, và đã push lên GitHub.*

Quỹ thời gian: ~1–2h. Nhẹ về tay, nhưng đây là lúc đặt "luật chơi" cho toàn project — làm chắc.

## Bạn cần có sẵn trước khi bắt đầu

- **.NET 10 SDK** đã cài. Kiểm tra: mở terminal, chạy `dotnet --version` → phải ra số bắt đầu bằng `10.` (vd `10.0.300`). Nếu chưa có, tải ở [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0).
- **Git** đã cài: `git --version` ra số bất kỳ là được.
- Một editor: Visual Studio 2022 (17.13+), Rider, hoặc VS Code + C# Dev Kit.
- Terminal mở tại **thư mục gốc repo** (thư mục bạn muốn chứa `EventHub.slnx`).

> **Lưu ý quan trọng về phạm vi:** Day 1 chỉ dựng **module Identity** (cùng 2 project Shared và host). Hai module Events/Ticketing được tạo ở các ngày sau khi thực sự cần — đừng tạo sớm.

## Các bước (làm theo thứ tự)

| Bước | File | Việc |
|------|------|------|
| A | [00-kien-truc-tong-quan.md](00-kien-truc-tong-quan.md) | **Hiểu** kiến trúc bạn sắp dựng (đọc, chưa gõ gì) |
| 1 | [01-tao-scaffold.md](01-tao-scaffold.md) | Tạo solution `.slnx` + host + 4 project Identity + 2 Shared, add vào solution |
| 2 | [02-central-package-management.md](02-central-package-management.md) | Tạo `Directory.Packages.props` (CPM) |
| 3 | [03-directory-build-props.md](03-directory-build-props.md) | Tạo `Directory.Build.props` + dọn trùng trong `.csproj` |
| 4 | [04-don-template.md](04-don-template.md) | Dọn rác template (`Class1.cs`, chỉnh endpoint mẫu của host) |
| 5 | [05-license-readme.md](05-license-readme.md) | LICENSE (MIT) + README khung |
| 6 | [06-commit-push.md](06-commit-push.md) | Build sạch → commit → push GitHub |
| 📝 | [notes.md](notes.md) | **Ghi chú & đính chính sau review** — cơ chế nạp `Directory.Build.props`/CPM, vì sao module Api là library |

## Quy tắc kiểm chứng xuyên suốt

Sau **mỗi** bước thay đổi cấu trúc, chạy:

```bash
dotnet build EventHub.slnx
```

Phải thấy `Build succeeded`. Nền móng không build được thì mọi ngày sau xây trên cát. Đừng sang bước mới khi bước hiện tại còn đỏ.

## Định nghĩa "hoàn thành" Day 1

- [ ] `dotnet build EventHub.slnx` xanh, không warning.
- [ ] Chỉ có **một** host: `dotnet run --project src/Bootstrap/EventHub.Api` lên được, có endpoint sức khỏe (không còn `"Hello World!"` mặc định).
- [ ] **Module Api là class library** (`EventHub.Identity.Api`), **không** có `Program.cs`/`appsettings`/`launchSettings` riêng — chỉ host Bootstrap mới là web host.
- [ ] `Directory.Packages.props` + `Directory.Build.props` tồn tại ở gốc; các `.csproj` đã gọn (không lặp version/build settings).
- [ ] `EventHub.SharedKernel` + `EventHub.Contracts` xuất hiện trong solution, build cùng.
- [ ] Không còn `Class1.cs` nào.
- [ ] LICENSE là MIT (đúng tên & năm); README không còn placeholder `<user>/<repo>`.
- [ ] Đã push lên GitHub (repo public).
- [ ] **Bạn tự nói thành lời được**: vì sao modular monolith, vì sao 4 project mỗi module, vì sao module Api là library chứ không phải host, khác nhau giữa SharedKernel và Contracts, CPM giải quyết vấn đề gì.

Xong Day 1, nhắn mentor **"review Day 1"** trước khi sang [Day 2](../README.md).
