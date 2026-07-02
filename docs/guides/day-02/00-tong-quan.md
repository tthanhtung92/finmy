# Bước A — Hiểu hạ tầng + pipeline EF + pattern module

> Bước này **chỉ đọc và hiểu**, chưa gõ gì. Mục tiêu: trước khi chạm bàn phím, bạn hình dung được hôm nay mình dựng cái gì và *vì sao*. Bỏ qua bước này, các bước sau bạn làm như con vẹt.

Day 2 có ba mảnh ghép, nhìn rời nhau nhưng phục vụ chung một mục tiêu "`docker compose up` là chạy":

1. **Hạ tầng** — ba dịch vụ ngoài (Postgres, Redis, MinIO) chạy trong container.
2. **EF Core pipeline** — nối ứng dụng .NET vào Postgres và sinh được migration.
3. **Pattern module** — cách host nạp từng module một cách gọn gàng, có kỷ luật.

---

## A1. Vì sao chạy hạ tầng bằng Docker Compose, không cài tay?

Một backend thật cần Postgres (database), Redis (cache), MinIO (lưu file). Bạn **có thể** tải từng cái về cài lên máy — nhưng đó là cái bẫy:

- Mỗi máy (của bạn, của đồng nghiệp, của CI) cài một version khác nhau → "trên máy tôi chạy mà".
- Gỡ đi cài lại bẩn máy; mỗi project lại một bộ dịch vụ khác nhau.

**Docker Compose** mô tả toàn bộ hạ tầng trong **một file YAML** (`docker/docker-compose.yml`): cần service nào, version nào, mở port nào, lưu dữ liệu ở đâu. Ai clone repo về cũng `up` ra **đúng một** môi trường. Đây chính là lời hứa số 3 trong [ROADMAP](../../ROADMAP.md) mục 1: *"`docker compose up` là chạy"*.

> **Góc kể phỏng vấn:** *"Tôi đóng gói toàn bộ hạ tầng dev (Postgres/Redis/MinIO) trong một docker-compose, kèm healthcheck và named volume, để môi trường tái lập được trên mọi máy và CI — không phụ thuộc 'máy tôi'."*

> TODO mentor: bổ sung phần phân biệt **container vs image vs volume** ở mức một câu mỗi khái niệm (đủ cho junior chưa từng dùng Docker), và vì sao dev dùng Compose chứ không Kubernetes ở giai đoạn này.

## A2. Ba service làm gì trong project này?

| Service | Image (tham khảo) | Vai trò trong EventHub | Dùng từ ngày nào |
|---------|-------------------|------------------------|------------------|
| **PostgreSQL 17** | `postgres:17` | Database chính — lưu User, Event, Order... | Day 2 trở đi (EF Core) |
| **Redis 8** | `redis:8` | Cache L2 phân tán cho HybridCache; pub/sub | Tuần 2 (caching) |
| **MinIO** | `minio/minio` | Object storage tương thích S3 — lưu poster sự kiện, giả lập CDN origin cục bộ | Tuần 2 (CDN) |

Hôm nay **chỉ Postgres được dùng thật** (cho migration). Redis và MinIO **dựng sẵn nhưng chưa code tới** — ta đưa vào Compose từ đầu để sau này khỏi quay lại sửa hạ tầng.

Tag image dùng trong [Bước 1](01-docker-compose.md): `postgres:17`, `redis:8`, `minio/minio` (khớp bảng tech stack [ROADMAP mục 2](../../ROADMAP.md)). Bạn có thể chọn biến thể `-alpine` của Postgres/Redis cho image nhẹ hơn — nhớ giữ **đồng nhất** giữa các máy.

> **Lưu ý license:** MinIO theo **AGPL** — trong project này nó chỉ đóng vai origin S3 *cục bộ cho dev*, không nhúng vào sản phẩm phân phối, nên không vướng. Đây cũng là một điểm "biết license" đáng kể khi phỏng vấn (xem ghi chú thương mại hóa thư viện ở ROADMAP mục 2).

## A3. EF Core, DbContext, migration, `__EFMigrationsHistory` — là gì?

Đây là pipeline biến class C# thành bảng trong Postgres:

- **EF Core** — ORM (Object-Relational Mapper): ánh xạ entity (class) ↔ bảng (table), để bạn viết LINQ thay vì SQL tay.
- **DbContext** — "cửa ngõ" tới database trong EF Core: nó khai báo có những `DbSet` (bảng) nào và cấu hình ánh xạ. Mỗi module sẽ có DbContext riêng (ranh giới dữ liệu theo module).
- **Provider** — phần dịch EF sang phương ngữ SQL của một DB cụ thể. Với Postgres là **Npgsql** (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Migration** — một file C# mô tả *thay đổi schema* (tạo bảng, thêm cột...). Sinh bằng `dotnet ef migrations add <Tên>`; áp vào DB bằng `dotnet ef database update`.
- **`__EFMigrationsHistory`** — bảng EF tự tạo trong DB để ghi *đã áp những migration nào*, nhờ đó không áp trùng.

> **Vì sao dùng migration thay vì tự gõ SQL `CREATE TABLE`?**
>
> TODO mentor: giải thích — schema được **version hóa cùng code** trong git; lên môi trường mới chỉ cần `database update`; review được diff schema qua PR. Nhấn mạnh đây là điểm "kể được khi phỏng vấn".

> **Ranh giới với Day 3:** Hôm nay DbContext **chỉ cần tối thiểu** để pipeline chạy thông — đừng model `User`/`Role`/`RefreshToken` ở đây, đó là việc Day 3.

Về "migration tối thiểu": một DbContext **chưa có `DbSet` nào vẫn tạo được migration** — EF không thấy thay đổi schema nên sinh ra **migration rỗng** (hợp lệ), và khi `database update` thì EF **vẫn tạo bảng `__EFMigrationsHistory`**. Như vậy đủ chứng minh pipeline chạy thông mà không cần đẻ entity rác. Chi tiết hai hướng làm (migration rỗng vs entity placeholder) ở [Bước 3](03-dbcontext-migration.md).

## A4. Pattern `AddModules()` / `UseModules()` giải quyết vấn đề gì?

Nhớ từ [Day 1](../day-01/00-kien-truc-tong-quan.md): module Api là **class library** chỉ chứa định nghĩa endpoint, và host `Bootstrap/EventHub.Api` là **composition root duy nhất** nạp chúng vào.

Câu hỏi: host nạp **bằng cách nào**? Hai lựa chọn:

- **Wire tay:** trong `Program.cs` của host, gọi thẳng `AddIdentityServices()`, `MapIdentityEndpoints()`, rồi `AddEventsServices()`... Mỗi lần thêm module phải sửa `Program.cs`. Host phải *biết tên* từng module → coupling chặt.
- **Pattern `IModule`:** định nghĩa một interface chung (vd `IModule`) với hai việc: "đăng ký service của tôi" và "map endpoint của tôi". Mỗi module hiện thực interface này. Host chỉ cần **quét và gọi tất cả** `IModule` qua `AddModules()` (lúc cấu hình DI) và `UseModules()` (lúc cấu hình pipeline). Thêm module mới → host **không phải sửa**.

Đây là một dạng **Plugin / Convention over configuration**: host không biết tên module cụ thể, chỉ biết "ai hiện thực `IModule` thì được nạp".

> TODO mentor: chốt **nơi đặt `IModule`** — `EventHub.SharedKernel` (vì là viên gạch dùng chung nội bộ) hay một project nền tảng riêng? Và cách host **tìm** các `IModule`: quét assembly bằng reflection, hay đăng ký tường minh danh sách module? Cân nhắc đánh đổi (reflection tiện nhưng khó trace; tường minh rõ ràng nhưng thủ công). Ghi lại lựa chọn + lý do.

> **Góc kể phỏng vấn:** *"Host của tôi không wire tay từng module. Mỗi module hiện thực `IModule` tự đăng ký service và endpoint; host quét và gọi qua `AddModules`/`UseModules`. Thêm module mới không phải đụng composition root."*

---

## Tự kiểm tra trước khi đi tiếp

Nhắm mắt trả lời được 5 câu này thì sang [Bước 1](01-docker-compose.md):

1. Vì sao dùng Docker Compose cho hạ tầng dev thay vì cài Postgres/Redis tay?
2. Trong ba service, hôm nay cái nào thực sự được code tới? Hai cái còn lại dựng sẵn để làm gì?
3. `DbContext` và `provider` (Npgsql) khác vai trò nhau thế nào?
4. Bảng `__EFMigrationsHistory` để làm gì?
5. Pattern `IModule` + `AddModules()` hơn cách wire tay trong `Program.cs` ở điểm nào?

<details>
<summary>Đáp án (tự trả lời trước rồi mới mở)</summary>

1. Để **môi trường tái lập được** trên mọi máy/CI: một file YAML mô tả đúng version, port, volume; ai `up` cũng ra cùng một hạ tầng — tránh "trên máy tôi chạy".
2. **Postgres** được dùng thật (cho migration EF). **Redis** (cache L2, Tuần 2) và **MinIO** (object storage/CDN, Tuần 2) dựng sẵn để khỏi quay lại sửa hạ tầng sau.
3. `DbContext` là cửa ngõ tới DB phía .NET (khai `DbSet`, cấu hình ánh xạ, không gắn DB cụ thể); **provider Npgsql** là phần dịch EF sang SQL của Postgres. Đổi DB thì đổi provider, DbContext giữ nguyên phần lớn.
4. EF tự tạo bảng này để ghi **đã áp những migration nào**, nhờ đó `database update` không áp trùng và biết cần áp tiếp cái nào.
5. Wire tay khiến host **biết tên** từng module và phải **sửa `Program.cs`** mỗi lần thêm module (coupling chặt). `IModule` đảo ngược: mỗi module tự đăng ký, host chỉ gọi chung `AddModules`/`UseModules` → thêm module mới không đụng composition root.

</details>
