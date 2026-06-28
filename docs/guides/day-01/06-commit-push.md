# Bước 6 — Build sạch → Commit → Push

> Mục tiêu: đóng gói công sức Day 1 thành lịch sử git sạch và đẩy lên GitHub (repo public).
>
> Toàn lệnh CLI — chạy theo. Quan trọng là **thứ tự** và **cách tách commit**.

---

## 6.1. Đảm bảo có `.gitignore` (tránh đẩy rác build)

Nếu repo **chưa có** file `.gitignore`, tạo một bản chuẩn cho .NET tại gốc repo:

```bash
dotnet new gitignore
```

File này chặn các thư mục build `bin/`, `obj/` và file cấu hình máy cá nhân khỏi bị commit. Nếu đã có `.gitignore` rồi thì bỏ qua.

## 6.2. Build & chạy lần cuối (cổng chất lượng)

Trước khi commit, đảm bảo mọi thứ xanh:

```bash
dotnet build EventHub.slnx
```

Phải `Build succeeded`, **không warning** (nếu bạn đã bật `TreatWarningsAsErrors` thì warning = lỗi, càng yên tâm). Chạy thử host một lần nữa cho chắc:

```bash
dotnet run --project src/Bootstrap/EventHub.Api
```

Gọi endpoint sức khỏe thấy phản hồi → `Ctrl+C` dừng. **Đừng commit khi build còn đỏ.**

## 6.3. Xem lại những gì sắp commit

```bash
git status
```

Đọc danh sách. Bạn nên thấy: `EventHub.slnx`, `Directory.Packages.props`, `Directory.Build.props`, các `.csproj`, `Program.cs` của host, các project Identity + Shared, README/LICENSE.

> **Kiểm tra quan trọng:** trong danh sách **không được** có thư mục `bin/`, `obj/`, hay file cấu hình máy cá nhân. Nếu thấy chúng, nghĩa là `.gitignore` chưa chặn — dừng lại, kiểm tra `.gitignore` trước khi đi tiếp (đừng đẩy rác build lên repo public).

## 6.4. Tách thành vài commit nhỏ (kể chuyện rõ hơn)

Day 1 gồm nhiều việc khác loại. Thay vì một commit khổng lồ, tách 2–3 commit theo nhóm việc — lịch sử git sẽ kể câu chuyện rõ ràng cho người đọc (gồm cả nhà tuyển dụng đọc `git log`). Gợi ý nhóm:

1. Scaffold solution: `EventHub.slnx` + các project (host, Identity, Shared).
2. Nền tảng build: `Directory.Packages.props` + `Directory.Build.props` + dọn `.csproj`.
3. Dọn template + LICENSE/README.

Với mỗi nhóm: chọn file (`git add <đường-dẫn cụ thể>`) rồi commit. Trước khi add, luôn xem `git status` để biết chính xác tên file cần add.

> **Tránh dùng `git add src/**/*.csproj`:** ký tự `**` **không** được shell (bash/PowerShell) bung đệ quy theo mặc định, nên dễ sót project lồng sâu. Cách an toàn: add từng đường dẫn cụ thể bạn thấy trong `git status`, hoặc nếu muốn add tất cả những gì đã kiểm thì dùng `git add -A` (an toàn vì `.gitignore` đã chặn `bin/`/`obj/`).

> Nếu thấy việc tách phức tạp, **một commit gọn cũng chấp nhận được** ở Day 1 — đừng để việc này chặn bạn. Tách commit là kỹ năng "nice to have", build xanh + push được mới là bắt buộc.

## 6.5. Quy ước commit (Conventional Commits, tiếng Việt)

Project dùng [Conventional Commits](../../conventional-commits.md), **viết tiếng Việt, mệnh lệnh**. Cú pháp: `<type>(<scope>): mô tả ngắn`. Một số `type` hợp cho Day 1:

- `feat:` — thêm cấu trúc/khung mới (vd khởi tạo solution + project).
- `build:` — thay đổi hệ thống build / dependencies (đúng cho CPM, Directory.Build.props).
- `chore:` — việc lặt vặt (dọn template, dọn `Class1.cs`).
- `docs:` — thay đổi tài liệu (README).

Ví dụ tốt: `feat: khởi tạo solution và cấu trúc module`, `build: thêm Central Package Management và build settings dùng chung`, `chore: dọn code template và Class1.cs`.

## 6.6. Push lên GitHub

Nếu repo đã có remote (kiểm tra bằng `git remote -v` thấy `origin`), đẩy lên:

```bash
git push
```

(Lần đầu trên một nhánh mới có thể cần `git push -u origin <tên-nhánh>` — terminal sẽ gợi ý đúng lệnh nếu cần.)

Nếu **chưa** có remote: tạo một repo **public** rỗng trên GitHub (đừng cho GitHub tự thêm README/LICENSE để tránh xung đột), rồi làm theo hướng dẫn "push an existing repository" mà GitHub hiển thị (thường là `git remote add origin <url>` rồi `git push -u origin main`).

## 6.7. Kiểm chứng cuối

- Mở repo trên GitHub bằng trình duyệt: thấy các file mới, thấy commit Day 1 trong lịch sử.
- Repo ở chế độ **Public**.
- (Chưa có GitHub Actions nên chưa có badge CI xanh — đúng tiến độ, Tuần 4 mới làm.)

## 6.8. Hoàn thành Day 1 🎉

Đối chiếu lại [checklist Definition of Done ở trang chỉ mục Day 1](README.md). Nếu đủ hết, nhắn mentor **"review Day 1"** — mình sẽ rà `EventHub.slnx`, `Directory.Packages.props`, `Directory.Build.props`, các `.csproj`, `Program.cs`, rồi mới mở [Day 2](../README.md).

> **Tự vấn nhanh (quan trọng hơn code):** bạn có giải thích trôi chảy cho người khác nghe được không — CPM giải quyết vấn đề gì, Directory.Build.props nạp lúc nào, vì sao module Api là library chứ không phải host, và vì sao module không được tham chiếu trực tiếp Domain của module khác? Nếu chưa, đọc lại [Bước A](00-kien-truc-tong-quan.md). Project này ăn điểm ở chỗ *giải thích được*, không chỉ *làm được*.
