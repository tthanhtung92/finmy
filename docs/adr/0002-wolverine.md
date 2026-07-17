# ADR-0002: Dùng Wolverine làm mediator + message bus + transactional outbox

## Trạng thái

Accepted — 2026-07-12

## Bối cảnh

Finmy cần ba thứ hạ tầng ứng dụng: một mediator để tách endpoint mỏng khỏi handler xử lý, một message bus in-process để publish integration event giữa các module, và một transactional outbox để ghi DB và publish event thành một thao tác atomic. Cái cuối là mấu chốt: bài toán "đinh" của project là anti-overselling ở module Ticketing — đặt vé phải ghi Order và phát `TicketSoldEvent` mà không có khe nào cho hai việc lệch nhau khi crash giữa chừng.

Ràng buộc song song: nhiều thư viện .NET quen thuộc đã chuyển sang license thương mại trong làn sóng 2025, nên lựa chọn phải cân cả tính năng lẫn license (xem thêm [ADR-0003](0003-tranh-thu-vien-thuong-mai.md)).

## Các phương án đã cân nhắc

- **MediatR** — mediator phổ biến nhất, nhưng đã chuyển license thương mại 2025, và bản thân nó *chỉ* là mediator: vẫn phải ghép thêm một message bus và tự lo outbox. Hai vấn đề trong một.

- **MassTransit** — bus trưởng thành, có outbox, nhưng cũng chuyển sang mô hình thương mại 2025. Loại vì cùng lý do license, và nặng hơn nhu cầu in-process của một monolith.

- **Tự viết mediator + outbox** — miễn license, kiểm soát hoàn toàn, nhưng tốn công đúng vào phần không phải trọng tâm; outbox tự viết cho đúng (atomic, retry, idempotent) là một dự án con, dễ sai ở chính chỗ cần chắc nhất.

- **Wolverine** — MIT, gộp mediator + message bus + transactional outbox native trong một thư viện. Đúng ba nhu cầu bằng một phụ thuộc.

## Quyết định

Chúng tôi chọn **Wolverine** làm mediator, message bus in-process, và transactional outbox. Endpoint mỏng gửi command/query qua Wolverine tới handler; integration event trong `Finmy.Contracts` publish qua bus; luồng đặt vé Ticketing dùng outbox native của Wolverine để ghi Order và phát event atomic.

Về codegen: hiện dùng **Dynamic mode** (compile handler bằng Roslyn lúc startup) cho tiện khi dev. Định hướng (chưa làm, dự kiến ở Tuần 4 lúc dựng Docker production image): chuyển sang **Static codegen** để tránh recompile mỗi cold start và tiết kiệm phần RAM Roslyn. Đây mới là dự định, chưa phải quyết định đã chốt.

## Hệ quả

- Một phụ thuộc phủ cả ba nhu cầu — ít bề mặt tích hợp hơn ghép ba thư viện rời, và outbox không phải tự viết đúng vào chỗ nhạy nhất.
- Outbox native phục vụ thẳng bài toán anti-overselling ở Tuần 3: ghi Order + publish `TicketSoldEvent` atomic là nền cho optimistic concurrency + idempotent consumer.
- Đánh đổi: buộc vào một thư viện codegen-heavy — hiểu sai codegen mode có thể làm cold start chậm hoặc image phình. Nên codegen mode là chỗ cần cân nhắc có chủ đích khi sang production, không để mặc định.
