# Bước 1 — Central Package Management (`Directory.Packages.props`)

> Mục tiêu: gom **toàn bộ version NuGet** của mọi project về **một file duy nhất** ở gốc repo.
>
> Lưu ý mentor: file này là XML, **mình không viết hộ**. Mình mô tả từng phần tử bằng lời + chỉ lệnh sinh khung sẵn; bạn tự điền. Đọc kỹ phần "Cấu trúc cần có".

---

## 1.1. Vấn đề CPM giải quyết (vì sao làm bước này)

Mặc định, mỗi file `.csproj` tự khai version của từng package nó dùng. Với một module 4 project × 3 module + shared + tests, bạn sẽ có chục project. Hậu quả khi version nằm rải rác:

- Project A dùng EF Core `10.0.1`, project B lỡ tay dùng `10.0.3` → lúc chạy sinh lỗi "method not found" rất khó truy.
- Nâng version một package phải sửa hàng chục file.

**Central Package Management (CPM)** sửa tận gốc: version khai **một nơi** (`Directory.Packages.props` ở gốc), các `.csproj` chỉ nói "tôi *cần* package này", không kèm số. Một chỗ sửa, cả solution theo.

> **Góc kể phỏng vấn:** "Tôi bật Central Package Management ngay từ đầu để mọi project dùng chung một bộ version, tránh lệch version âm thầm khi solution lớn dần."

## 1.2. Tạo file (cách nhanh nhất)

Tại thư mục gốc repo (`d:\projects\eventhub`, nơi có `EventHub.slnx`), chạy:

```bash
dotnet new packagesprops
```

Lệnh này sinh sẵn file `Directory.Packages.props` ở gốc với khung CPM cơ bản (đã bật cờ trung tâm). Nếu máy bạn báo không có template này, không sao — tự tạo file `Directory.Packages.props` ở gốc rồi điền theo mục 1.3.

> **Cạm bẫy vị trí:** file **phải ở gốc repo**, ngang hàng `EventHub.slnx`. MSBuild đi ngược cây thư mục từ mỗi project lên trên và dừng ở file đầu tiên gặp; đặt sai chỗ (vd trong `src/`) thì các project ngoài nhánh đó sẽ không "thấy".

## 1.3. Cấu trúc cần có (tự điền)

File `Directory.Packages.props` cần các thành phần sau (mô tả bằng lời — bạn tự gõ XML):

1. Thẻ gốc `Project` (không kèm thuộc tính `Sdk`).
2. Bên trong, một khối `PropertyGroup` chứa đúng một thuộc tính: `ManagePackageVersionsCentrally` đặt giá trị `true`. Đây là **công tắc bật CPM** — thiếu nó thì file vô tác dụng.
3. Một khối `ItemGroup` chứa các phần tử `PackageVersion`. Mỗi `PackageVersion` có hai thuộc tính: `Include` (tên package, vd `Microsoft.EntityFrameworkCore`) và `Version` (số version, vd `10.0.0`).

Hôm nay bạn **chưa cần** thêm package thật nào — cứ để `ItemGroup` rỗng (hoặc bỏ trống), sẽ thêm dần ở các ngày sau khi thực sự cần. Quan trọng nhất hôm nay là **cờ `ManagePackageVersionsCentrally` = true** đã bật.

*Tham khảo cấu trúc chính xác (có ví dụ XML đầy đủ):* [Central Package Management — Microsoft Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management).

## 1.4. Quy ước từ giờ về sau (ghi nhớ)

Kể từ khi CPM bật, mỗi khi cần dùng một package trong một project:

- Trong `.csproj`: thêm `PackageReference` với thuộc tính `Include` **nhưng KHÔNG kèm `Version`**.
- Trong `Directory.Packages.props`: thêm/cập nhật một `PackageVersion` tương ứng (có `Version`).

Nếu lỡ để cả `Version` trong `PackageReference` lẫn CPM bật, restore sẽ báo lỗi `NU1008` ("phải khai version tập trung"). **Lỗi này là tín hiệu tốt** — nó chứng minh CPM đang kiểm soát đúng.

> *Nâng cao (chỉ cần biết, chưa dùng hôm nay):* `GlobalPackageReference` để đưa một package vào *mọi* project (vd công cụ build); `VersionOverride` trên một `PackageReference` để một project dùng version khác biệt. Xem mục tương ứng trong [trang Microsoft Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management).

## 1.5. Kiểm chứng

Chạy:

```bash
dotnet build EventHub.slnx
```

- Phải thấy `Build succeeded`.
- Vì chưa có package nào, build không khác trước — đúng như mong đợi. Cái ta xác nhận là file mới **không làm hỏng** build.

## 1.6. Xong bước này khi

- [ ] File `Directory.Packages.props` tồn tại ở **gốc repo** (ngang `EventHub.slnx`).
- [ ] Trong đó `ManagePackageVersionsCentrally` = `true`.
- [ ] `dotnet build EventHub.slnx` vẫn xanh.

→ Sang [Bước 2 — Directory.Build.props](02-directory-build-props.md).
