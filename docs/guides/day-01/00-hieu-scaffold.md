# Bước A — Hiểu phần đã scaffold

> Bước này **chỉ đọc và hiểu**, chưa gõ gì. Mục tiêu: bạn không có "code mồ côi" — mọi file trong repo bạn đều biết nó là gì và vì sao tồn tại. Nếu bỏ qua bước này, các bước sau bạn sẽ làm như con vẹt.

Mở repo lên, vừa đọc vừa đối chiếu với cây thư mục thật.

---

## A1. "Modular Monolith" là gì và vì sao chọn nó?

Tưởng tượng 3 cách tổ chức một backend:

- **Monolith truyền thống (layered):** chia theo *tầng kỹ thuật* — tất cả Controller một chỗ, tất cả Service một chỗ, tất cả Repository một chỗ. Dễ bắt đầu, nhưng càng lớn càng rối: sửa tính năng vé lại lỡ tay đụng tính năng user, vì mọi thứ nằm chung. Người ta gọi đây là "big ball of mud".
- **Microservices:** mỗi nghiệp vụ là một service riêng, deploy riêng, database riêng, gọi nhau qua mạng. Ranh giới cực rõ, nhưng **trả giá đắt**: phải lo network, lỗi mạng, transaction phân tán, deploy chục service. Với một người làm project CV, đây là "dùng dao mổ trâu giết gà".
- **Modular Monolith (cái project này chọn):** vẫn deploy **một process duy nhất** (đơn giản như monolith), nhưng mã nguồn chia theo *nghiệp vụ* thành các **module** (Identity, Events, Ticketing) có ranh giới rõ ràng, và ranh giới đó được **ép buộc bằng test tự động**.

Điểm mấu chốt: nó cho bạn cái lợi "ranh giới rõ" của microservices mà **không** gánh chi phí vận hành phân tán. Nếu sau này một module thực sự cần tách thành microservice, ranh giới sẵn có giúp việc tách dễ hơn nhiều.

> **Góc kể khi phỏng vấn:** *"Tôi chọn modular monolith vì nó giữ ranh giới nghiệp vụ rõ như microservices nhưng tránh chi phí vận hành phân tán; và tôi ép ranh giới đó bằng architecture test để nó không trôi theo thời gian."* Đây là tư duy của middle, không phải junior.

## A2. Vì sao file solution là `.slnx` chứ không phải `.sln`?

`.sln` (định dạng cũ) là text với cú pháp GUID lằng nhằng, rất khó đọc và hay xung đột khi merge git. `.slnx` là **định dạng mới, dạng XML, người đọc được**, và **từ .NET 10 nó là mặc định** khi bạn chạy `dotnet new sln`.

Hãy mở [EventHub.slnx](../../../EventHub.slnx) ra xem — nó chỉ là vài thẻ `<Folder>` và `<Project>`. Bạn sẽ thấy nó liệt kê: folder `/src/Bootstrap/` chứa project host, folder `/src/Modules/Identity/` chứa 4 project. Đây chính là cây bạn thấy trong Solution Explorer.

Lát nữa ở [Bước 3](03-shared-projects.md) bạn sẽ thêm 2 project mới vào solution. Tin tốt: **không cần sửa file XML này bằng tay** — có lệnh CLI `dotnet sln add` lo việc đó (chi tiết ở Bước 3).

*Tham khảo:* [Giới thiệu .slnx trong .NET CLI](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/) · [dotnet new sln mặc định .slnx (.NET 10)](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default).

## A3. Vì sao mỗi module có tới 4 project?

Mở `src/Modules/Identity/` — bạn thấy 4 project: `EventHub.Identity.Domain`, `.Application`, `.Infrastructure`, `.Api`. Đây là **Clean Architecture thu nhỏ**. Quy tắc vàng: **mũi tên phụ thuộc chỉ đi vào trong**.

```
        Api  ──►  Application  ──►  Domain
                                      ▲
                  Infrastructure ─────┘
```

Đọc sơ đồ: Api phụ thuộc Application, Application phụ thuộc Domain; Infrastructure cũng phụ thuộc Application/Domain — nhưng **không ai phụ thuộc ngược vào Infrastructure** ngoài lúc ráp ở host.

- **Domain** — trái tim nghiệp vụ: entity (vd `User`, `RefreshToken`), value object, luật nghiệp vụ thuần. **Không tham chiếu gì cả** — không EF Core, không ASP.NET. Nhờ vậy nó dễ test nhất.
- **Application** — điều phối *use case* (vd handler "Đăng nhập", "Đăng ký"). Nó *khai báo* các interface mà nó cần (vd "tôi cần một thứ lưu được user"), nhưng không tự hiện thực.
- **Infrastructure** — hiện thực các interface đó bằng công nghệ cụ thể: EF Core, `DbContext`, dịch vụ JWT, Redis. Đây là nơi "bẩn tay" với hạ tầng.
- **Api** — các endpoint Minimal API, cửa vào HTTP của module.

**Vì sao bận tâm chiều mũi tên?** Để **logic nghiệp vụ không bị công nghệ kéo theo**. Mai mốt đổi PostgreSQL sang SQL Server, chỉ Infrastructure đổi — Domain không đụng một dòng. Khái niệm đứng sau là **Dependency Inversion** (chữ D trong SOLID) — phần đáng giá nhất của kiến trúc này.

> **Việc cần làm ở bước này:** mở từng file `.csproj` trong `src/Modules/Identity/`, nhìn các thẻ `ProjectReference` (nếu có) xem ai tham chiếu ai. Nếu thấy `Domain` đang tham chiếu `Infrastructure` → đó là **sai chiều**, ghi chú lại để sửa sau. (Hiện scaffold mới tạo nên có thể chưa có reference nào — bình thường.)

## A4. Hai project Shared: `SharedKernel` và `Contracts` khác nhau thế nào?

Hai cái tên nghe giống nhau nhưng mục đích **khác hẳn** — đừng nhầm, đây là chỗ junior hay sai:

- **`EventHub.SharedKernel`** — các "viên gạch" dùng chung *bên trong* mọi module: kiểu `Result<T>` (biểu diễn thành công/thất bại mà không ném exception cho luồng nghiệp vụ), lớp cơ sở cho domain event, các guard clause (hàm kiểm tra đầu vào kiểu "không được null"). Coi như thư viện tiện ích nội bộ.
- **`EventHub.Contracts`** — **bề mặt công khai DUY NHẤT giữa các module**: các **integration event** như `TicketSoldEvent`, `EventCreatedEvent`. Khi module Ticketing muốn báo "vừa bán 1 vé", nó publish một event định nghĩa *ở đây* qua message bus Wolverine. Module khác lắng nghe event đó — chứ **không** được "thò tay" vào Domain của Ticketing.

> **Cạm bẫy chết người:** đừng nhét entity domain hay logic nghiệp vụ vào `Contracts`. Nó chỉ chứa các "hợp đồng" message — thường là `record` đơn giản, bất biến (immutable). Trộn lẫn = bạn làm rò rỉ nội bộ module ra ngoài, phá đúng cái ranh giới mà project muốn khoe.

Ở Day 1 ta chỉ **tạo khung rỗng** hai project này. Nội dung thật: `Result<T>` viết ở [Day 5](../README.md), các event viết ở Tuần 3.

## A5. `Class1.cs` và `weatherforecast` là gì? (rác cần dọn)

Khi chạy `dotnet new classlib`, .NET tự sinh một file `Class1.cs` rỗng làm ví dụ. Khi chạy `dotnet new web`/`webapi`, nó sinh demo `weatherforecast` trong `Program.cs`. Cả hai **vô giá trị** với project — chỉ là rác mẫu. Mở [Program.cs của host](../../../src/Bootstrap/EventHub.Api/Program.cs) sẽ thấy đoạn `WeatherForecast`. Ta sẽ dọn sạch ở [Bước 4](04-don-template.md).

---

## Tự kiểm tra trước khi đi tiếp

Nhắm mắt trả lời được 4 câu này thì sang [Bước 1](01-central-package-management.md):

1. Modular monolith khác microservices ở điểm nào, lợi gì?
2. Trong một module, `Domain` được phép tham chiếu `Infrastructure` không? Vì sao?
3. `SharedKernel` và `Contracts` — cái nào là bề mặt giao tiếp giữa các module?
4. `Class1.cs` để làm gì?
