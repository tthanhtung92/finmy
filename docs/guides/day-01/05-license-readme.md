# Bước 5 — LICENSE & README

> Mục tiêu: có `LICENSE` đúng loại MIT, và một `README` khung với link trỏ đúng repo thật của bạn.
>
> Đây là sửa/viết văn bản, không phải code — bạn tự làm, mình chỉ vào đâu cần xử lý.

---

## 5.1. Vì sao bận tâm LICENSE & README ở Day 1?

ROADMAP coi đây là một phần của "nền móng" và là tiêu chí Definition of Done của cả project. Quan trọng hơn: **README là thứ nhà tuyển dụng đọc đầu tiên**, trước cả code. Một repo có LICENSE rõ ràng + README chỉn chu tạo ấn tượng "người này làm việc nghiêm túc" ngay từ giây đầu.

## 5.2. LICENSE (MIT)

Mở file `LICENSE` ở gốc repo (nếu **chưa có**, tạo mới — chọn template "MIT License", vd qua nút "Add file → Create new file → LICENSE" trên GitHub sẽ gợi ý template, hoặc copy nội dung MIT chuẩn). Xác nhận:

- Đúng loại **MIT** (ROADMAP và README đều cam kết MIT).
- Dòng bản quyền có **đúng năm** và **đúng tên** bạn (chủ sở hữu).

> *Vì sao MIT:* đây là license cho phép người khác dùng lại tự do, phổ biến nhất cho project mã nguồn mở/CV. ROADMAP chọn MIT có chủ đích.

## 5.3. README khung

Mở `README.md` ở gốc. Nếu chưa có nội dung, dựng một khung tối thiểu gồm: tên + mô tả ngắn project, badge (CI/License/.NET), mục lục, tổng quan, tech stack, hướng dẫn chạy nhanh, cấu trúc thư mục, link ROADMAP. (Tham khảo cấu trúc trong [ROADMAP](../../ROADMAP.md) — viết bằng lời của bạn, đừng bịa số liệu chưa có.)

Quan trọng nhất hôm nay: **thay mọi placeholder trỏ về repo thật của bạn**. Quét cả file, tìm và thay hết các chuỗi giữ chỗ `<user>` / `<repo>`:

1. **Badge CI** (gần đầu file): URL chứa `github.com/<user>/<repo>/actions/...`. (Badge sẽ chưa xanh cho tới khi có GitHub Actions ở Tuần 4 — bình thường.)
2. **Lệnh clone** (mục "Bắt đầu nhanh"): `git clone https://github.com/<user>/<repo>.git` và dòng `cd <repo>`.

> **Chưa cần làm hôm nay:** các phần README có ghi chú `<!-- TODO -->` (ảnh demo, sơ đồ kiến trúc, thông tin tài khoản seed) là cho các tuần sau. Cứ để nguyên, đừng xóa ghi chú TODO — chúng là lời nhắc.

## 5.4. Kiểm chứng

- Tìm trong README: không còn chuỗi `<user>` hay `<repo>`.
- Mở thử link clone/badge bằng mắt xem đã trỏ đúng tài khoản/repo của bạn chưa.
- (Chưa push nên badge/CI chưa hoạt động — đó là việc của [Bước 6](06-commit-push.md).)

## 5.5. Xong bước này khi

- [ ] LICENSE là MIT, đúng tên & năm.
- [ ] README không còn placeholder `<user>/<repo>`; badge và lệnh clone trỏ repo thật.

→ Sang [Bước 6 — Commit & Push](06-commit-push.md).
