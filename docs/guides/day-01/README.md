# Day 1 — Nền móng Solution

> **Mentor mode.** Tài liệu giải thích *vì sao* và *làm gì*, **không kèm code** — bạn tự gõ. Mọi lệnh CLI (`dotnet`, `git`) thì cứ chạy theo. Mỗi file dưới đây là **một bước**, làm tuần tự từ trên xuống.
>
> Viết cho người **mới**: nếu một câu khiến bạn phải đoán, đó là lỗi của tài liệu — nhắn mentor để bổ sung.

---

## Mục tiêu Day 1

Theo [ROADMAP](../../ROADMAP.md), kết thúc Day 1 bạn có: *solution Modular Monolith build sạch, có Central Package Management + build settings dùng chung, đã dọn rác template, LICENSE/README ổn, và đã push lên GitHub.*

Quỹ thời gian: ~1–2h. Nhẹ về tay, nhưng đây là lúc đặt "luật chơi" cho toàn project — làm chắc.

## Bạn cần có sẵn trước khi bắt đầu

- **.NET 10 SDK** đã cài. Kiểm tra: mở terminal, chạy `dotnet --version` → phải ra số bắt đầu bằng `10.` (vd `10.0.100`). Nếu chưa có, tải ở [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0).
- **Git** đã cài: `git --version` ra số bất kỳ là được.
- Một editor: Visual Studio 2022 (17.13+), Rider, hoặc VS Code + C# Dev Kit.
- Terminal mở tại thư mục gốc repo (`d:\projects\eventhub`) — nơi có file `EventHub.slnx`.

## Các bước (làm theo thứ tự)

| Bước | File | Việc |
|------|------|------|
| A | [00-hieu-scaffold.md](00-hieu-scaffold.md) | **Hiểu** phần đã scaffold (đọc, chưa gõ gì) |
| 1 | [01-central-package-management.md](01-central-package-management.md) | Tạo `Directory.Packages.props` (CPM) |
| 2 | [02-directory-build-props.md](02-directory-build-props.md) | Tạo `Directory.Build.props` (build settings chung) |
| 3 | [03-shared-projects.md](03-shared-projects.md) | Tạo 2 project Shared + thêm vào solution |
| 4 | [04-don-template.md](04-don-template.md) | Dọn rác template (`Class1.cs`, weatherforecast) |
| 5 | [05-license-readme.md](05-license-readme.md) | Xác nhận LICENSE + sửa README badge |
| 6 | [06-commit-push.md](06-commit-push.md) | Build sạch → commit → push GitHub |

## Quy tắc kiểm chứng xuyên suốt

Sau **mỗi** bước thay đổi cấu trúc, chạy:

```bash
dotnet build EventHub.slnx
```

Phải thấy `Build succeeded`. Nền móng không build được thì mọi ngày sau xây trên cát. Đừng sang bước mới khi bước hiện tại còn đỏ.

## Định nghĩa "hoàn thành" Day 1

- [ ] `dotnet build EventHub.slnx` xanh, không warning.
- [ ] `dotnet run --project src/Bootstrap/EventHub.Api` lên được, có endpoint sức khỏe, không còn `/weatherforecast`.
- [ ] `Directory.Packages.props` + `Directory.Build.props` tồn tại ở gốc; các `.csproj` đã gọn (không lặp version/build settings).
- [ ] `EventHub.SharedKernel` + `EventHub.Contracts` xuất hiện trong solution, build cùng.
- [ ] Không còn `Class1.cs` nào.
- [ ] README badge/clone link trỏ repo thật; LICENSE đúng tên/năm.
- [ ] Đã push lên GitHub.
- [ ] **Bạn tự nói thành lời được**: vì sao modular monolith, vì sao 4 project mỗi module, khác nhau giữa SharedKernel và Contracts, CPM giải quyết vấn đề gì.

Xong Day 1, nhắn mentor **"review Day 1"** trước khi sang [Day 2](../README.md).
