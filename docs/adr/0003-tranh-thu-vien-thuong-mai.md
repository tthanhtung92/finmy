# ADR-0003: Tránh thư viện đã thương mại hóa, chọn Mapster / NSubstitute / Shouldly

## Trạng thái

Accepted — 2026-07-12

## Bối cảnh

Từ tháng 4/2025 trở đi, một loạt thư viện .NET quen thuộc chuyển sang license thương mại: MediatR, AutoMapper, MassTransit, Moq, FluentAssertions. Finmy là repo public; ai clone về cũng phải build và chạy được mà không vướng ràng buộc license hay phí.

MediatR và MassTransit đã được xử lý ở [ADR-0002](0002-wolverine.md) (Wolverine thay cả hai). ADR này chốt phần còn lại: mapping, mocking, assertion.

## Các phương án đã cân nhắc

- **Giữ AutoMapper / Moq / FluentAssertions** — quen tay, tài liệu nhiều, nhưng nay cần license thương mại cho dùng thật. Với repo public thì vướng ràng buộc license và không dùng miễn phí được.

- **Mapster hoặc mapping thủ công** thay AutoMapper — Mapster (MIT) nhanh, hỗ trợ source generator; mapping thủ công thì zero phụ thuộc và tường minh. Đổi lại mất phần convention-based magic của AutoMapper.

- **NSubstitute** thay Moq — MIT, cú pháp substitute gọn, đủ cho unit test handler/domain.

- **Shouldly** thay FluentAssertions — MIT/BSD, thông báo lỗi assertion đọc dễ, cú pháp `ShouldBe` thay cho `Should().Be()`.

## Quyết định

Chúng tôi **không** đưa MediatR, AutoMapper, MassTransit, Moq, FluentAssertions vào project. Thay bằng bộ MIT/Apache/BSD: mapping dùng **Mapster** (viết tay cho những map quá đơn giản, không cần config); mock dùng **NSubstitute**; assertion dùng **Shouldly**. (Wolverine đã thay MediatR/MassTransit ở ADR-0002.)

## Hệ quả

- Repo build và chạy được cho bất kỳ ai clone, không vướng license — đúng yêu cầu repo public.
- Việc phát sinh: không còn convention-based mapping của AutoMapper, phải khai mapping qua Mapster config (hoặc viết tay). Đổi lại mapping rõ ràng hơn, ít magic ẩn.
- Đường học ngắn cho cú pháp mới: `Substitute.For<T>()` thay `new Mock<T>()`, `ShouldBe` thay `Should().Be()`. Chi phí một lần.
