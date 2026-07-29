# ADR-0009: Dùng cột `Version` kiểu int do domain tự tăng làm concurrency token, không dùng `xmin`

## Trạng thái

Accepted — 2026-07-29

## Bối cảnh

Day 19 đưa số dư vào `Envelope`: thêm `Spent`, `Remaining` tính ra từ `Allocated - Spent`, và các method `Spend`, `Release`, `Fund`. Bất biến cần giữ là `0 <= Spent <= Allocated`, tức không ai chi vượt phần đã phân bổ.

Bất biến này không tự đứng được dưới truy cập đồng thời. Hai giao dịch cùng chi vào một phong bì gần cạn sẽ cùng đọc một giá trị `Spent`, cùng cộng phần của mình, rồi cùng ghi xuống. Cái ghi sau đè lên cái ghi trước và tiền của một trong hai bốc hơi khỏi sổ sách. Đây là lost update kinh điển, và nó xảy ra kể cả khi mỗi lần ghi nằm trong một transaction riêng, vì không transaction nào biết cái kia tồn tại.

Ràng buộc lúc quyết định:

- Số dư chỉ được ghi bởi Budgeting (single-writer, chốt từ Day 16). Ledger phát event rồi chờ, nó không có đường nào ghi thẳng vào bảng envelope.
- Hai module không được nằm chung một transaction, vì `Envelope` thuộc Budgeting còn `Transaction` thuộc Ledger và luật ranh giới cấm chúng thấy database của nhau. Bất biến phải được ép ở đúng chỗ ghi, phía Budgeting.
- Stack là EF Core 10 trên PostgreSQL 17 qua Npgsql, kèm Wolverine ở giữa. Cơ chế chọn phải hoạt động khi lệnh ghi đi qua một message handler chứ không chỉ qua một HTTP request.
- `docs/ROADMAP.md` viết "optimistic concurrency (rowversion)". Chữ "rowversion" ở đó mang nghĩa chung là token chống ghi đè, không phải chỉ định một API cụ thể.

## Các phương án đã cân nhắc

- **Cột hệ thống `xmin` của Postgres, map qua `IsRowVersion()`.** Mỗi dòng Postgres mang sẵn `xmin` chứa id transaction đã ghi ra phiên bản đó, tự đổi mỗi lần dòng bị sửa; Npgsql map nó vào một property `uint` ([Npgsql, Concurrency Tokens](https://www.npgsql.org/efcore/modeling/concurrency.html)). Ưu điểm thật và đáng kể: database tự bump nên không ai quên được. Ba điểm trừ. EF sinh ra một `AddColumn<uint>(name: "xmin", ...)` phải xóa tay, không xóa thì `database update` chết với `column name "xmin" conflicts with a system column name` ([efcore.pg#3270](https://github.com/npgsql/efcore.pg/issues/3270), [#145](https://github.com/npgsql/efcore.pg/issues/145), cả hai chưa có câu trả lời của maintainer). Chính tài liệu PostgreSQL khuyến cáo không dựa vào tính duy nhất của transaction id về lâu dài ([System Columns](https://www.postgresql.org/docs/17/ddl-system-columns.html)), và giá trị đó không sống sót qua dump-restore hay logical replica. Và không hệ ORM lớn nào đi đường này: Hibernate có `@Version`, Rails có `lock_version`, [Marten có `mt_version`](https://martendb.io/documents/concurrency), tất cả đều là cột tường minh. Marten là dẫn chứng nặng nhất vì nó cùng nhà JasperFx với Wolverine và chỉ chạy trên Postgres, tức là tác giả biết `xmin` tồn tại và vẫn tự dựng cột riêng.

- **Token kiểu `Guid`, sinh giá trị mới mỗi lần ghi.** Cách Marten làm. Có lợi trong hệ phân tán vì không cần đồng bộ một bộ đếm toàn cục. Với một monolith một node thì nó không mua thêm gì, lại khó đọc hơn khi soi bằng `psql`.

- **Bỏ token, ghi bằng một câu `UPDATE` có điều kiện.** EF Core 7 trở lên có `ExecuteUpdateAsync`, viết thẳng "cộng `amount` vào `Spent` ở dòng có `Id` này, với điều kiện `Allocated - Spent >= amount`", rồi đọc số dòng bị ảnh hưởng: 1 là đủ tiền, 0 là không. Đây là phương án nhanh nhất trong tất cả: một round trip, không đọc trước, không bao giờ conflict, không bao giờ phải retry. Cái giá là bất biến rời khỏi domain model và biến thành một mệnh đề `WHERE`; đọc `Envelope.Spend` trong C# sẽ không còn thấy luật chống chi vượt ở đâu.

- **Cột `int Version` do domain tự tăng, khai `IsConcurrencyToken()`.** Luật nằm nguyên trong domain, cơ chế hoạt động giống nhau bất kể lệnh ghi tới từ HTTP hay từ message handler, và migration sinh ra sạch không phải sửa tay. Đổi lại việc bump là trách nhiệm của người viết code.

## Quyết định

Chọn cột `Version` kiểu `int` trên `Envelope`, do domain tự tăng, khai `IsConcurrencyToken()` trong `BudgetingDbContext.OnModelCreating`.

Hai điểm định hình cách làm:

- **Bump ở mọi method thay đổi trạng thái, kể cả `Update` vốn chỉ sửa tên và mô tả.** EF Core đưa concurrency token vào mệnh đề `WHERE` của mọi câu `UPDATE` bất kể sửa cột nào, nên `Update` mà không bump thì hai người cùng đổi tên sẽ ghi đè nhau im lặng. Vẫn là lost update, chỉ đổi chỗ từ cột tiền sang cột tên. Hibernate và Rails cũng bump ở mọi lần lưu chứ không chọn lọc theo cột.

- **Retry policy cho `DbUpdateConcurrencyException` đặt ở composition root, không đặt trên handler chain.** Kiểu exception đó thuộc `Microsoft.EntityFrameworkCore`, mà đặt policy trên handler nghĩa là `Finmy.Budgeting.Application` phải tham chiếu EF Core, đúng thứ port `IEnvelopeRepository` sinh ra để tránh. Luật rút ra để dùng lại: policy về nghiệp vụ của một message thì đặt trên handler, policy về hạ tầng thì đặt ở host.

## Hệ quả

- Quên `Version++` trong một method mutate mới là mất lớp bảo vệ, mà build vẫn xanh và không có gì báo đỏ. Đây là cái giá trực tiếp của việc không để database tự bump. Luật bù lại: mỗi method mutate mới phải kèm một unit test khẳng định version có tăng.

- Xung đột không đến từ Postgres. `UPDATE` khớp 0 dòng là kết quả hợp lệ dưới database, chính EF Core đếm số dòng bị ảnh hưởng rồi tự dựng `DbUpdateConcurrencyException` ([EF Core, Handling Concurrency Conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)). Ai đi tìm dấu vết xung đột trong log Postgres sẽ không thấy gì.

- Cơ chế chỉ phủ `UPDATE` và `DELETE`, không phủ `INSERT`, vì thêm entity mới thì không có giá trị cũ nào để so. Trùng khóa lúc chèn là bài toán khác, giải bằng ràng buộc unique.

- `PUT /envelopes/{id}` từ nay có thể hỏng khi hai người sửa cùng lúc. `EnvelopeService.UpdateAsync` gọi `SaveChangesAsync` trần nên exception bay lên `GlobalExceptionHandler` và thành 500, trong khi đúng ra phải là 409 kèm ProblemDetails. Đây là khoản nợ có ý thức: đường đi qua message bus đã có retry, đường đi qua HTTP thì chưa.

- Bump ở mọi lần lưu làm tăng tỉ lệ xung đột cho luồng chỉ sửa metadata. Hai người cùng đổi mô tả một phong bì sẽ có một người thua, dù về nghiệp vụ hai thao tác đó chẳng đụng nhau. Chấp nhận, vì phương án thay thế là bump chọn lọc theo cột và nó mở lại đúng lỗ hổng lost update ở trên.

- `Version` là một số nguyên tăng đơn điệu nên sau này dùng lại được làm số revision cho HTTP `ETag`, nếu có lúc cần optimistic concurrency ở tầng API.

- Nếu về sau luồng chi tiêu chạm mức tải mà retry trở thành nút thắt, phương án `ExecuteUpdateAsync` vẫn nằm đó. Đổi sang nó là đổi quyết định, nên lúc đó viết ADR mới và đánh dấu ADR này Superseded, không sửa đè file này.
