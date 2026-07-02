# Bước 1 — Docker Compose: Postgres + Redis + MinIO

> Mục tiêu: tạo `docker/docker-compose.yml` mô tả ba dịch vụ hạ tầng, sao cho một lệnh `up` là dựng xong, dữ liệu bền qua restart.
>
> Lưu ý mentor: file này là YAML cấu hình, **mình không viết hộ**. Mình mô tả từng phần cần có bằng lời; bạn tự gõ. Đọc kỹ phần "Cấu trúc cần có".

---

## 1.1. Cái gì

Tạo thư mục `docker/` ở gốc repo và file `docker/docker-compose.yml` định nghĩa **3 service**: `postgres`, `redis`, `minio`. Mỗi service khai: image, port mở ra host, biến môi trường (mật khẩu...), named volume để giữ dữ liệu, và healthcheck.

## 1.2. Vì sao

> TODO mentor: giải thích vì sao **named volume** (không phải bind mount) cho dữ liệu DB; vì sao **healthcheck** quan trọng (để service phụ thuộc chờ DB *thật sự sẵn sàng*, không chỉ "container đã start"); vì sao tách `docker-compose.yml` (cấu hình chung) khỏi `docker-compose.override.yml` (cấu hình dev) như ROADMAP mục 3 phác. Đây là phần điểm cộng — điền kỹ.

## 1.3. Các bước làm

Mô tả bằng lời (bạn tự gõ YAML):

1. Tạo thư mục `docker/` ở **gốc repo** (ngang `EventHub.slnx`).
2. Trong `docker/docker-compose.yml`, khai một khối `services` gồm ba mục con (các tên biến/đường dẫn/healthcheck dưới đây đã đối chiếu tài liệu chính thức — xem Link):
   - **postgres** — image `postgres:17`; biến môi trường `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`; map port `5432`; gắn named volume vào **`/var/lib/postgresql/data`** (thư mục dữ liệu của Postgres) để **không mất dữ liệu** khi container bị xóa; healthcheck dạng `["CMD-SHELL", "pg_isready -U <user> -d <db>"]` (kèm `interval`, `timeout`, `retries`, `start_period`).
   - **redis** — image `redis:8`; map port `6379`; (tùy chọn) volume `/data` cho persistence; healthcheck `["CMD", "redis-cli", "ping"]` (phản hồi `PONG` khi khỏe).
   - **minio** — image `minio/minio`; chạy lệnh `server /data --console-address ":9001"`; biến môi trường `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`; map **hai** port: `9000` (API S3) và `9001` (console UI); volume vào **`/data`**; healthcheck gọi endpoint **`/minio/health/live`** (vd `["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]`).
3. Khai khối `volumes` ở cấp trên cùng để đăng ký các named volume đã dùng.
4. (Khuyến nghị) Đặt **mật khẩu/secret** qua file `.env` cạnh compose thay vì hardcode; thêm `.env` vào `.gitignore` nếu chứa secret thật.

> **Lưu ý healthcheck MinIO:** endpoint `/minio/health/live` là chính thức, nhưng image `minio/minio` **có thể không kèm sẵn `curl`**. Nếu healthcheck báo "unhealthy" dù service chạy, kiểm tra bằng `docker compose exec minio sh -c "command -v curl"`; nếu thiếu, dùng công cụ có sẵn trong image (vd `mc ready local`) — xác nhận lại trong [MinIO docs](https://min.io/docs/minio/container/operations/checklists/healthcheck.html) cho đúng tag bạn dùng.

## 1.4. Kiểm chứng

Tại gốc repo:

```bash
docker compose -f docker/docker-compose.yml up -d
docker compose -f docker/docker-compose.yml ps
```

- Cả ba service phải lên; cột trạng thái chuyển sang `healthy` sau vài giây (chờ healthcheck).
- Mở MinIO console trên trình duyệt (port console đã map) đăng nhập bằng root user/password — vào được là MinIO ổn.
- Kiểm tra dữ liệu bền:

```bash
docker compose -f docker/docker-compose.yml down
docker compose -f docker/docker-compose.yml up -d
```

`down` (không kèm `-v`) rồi `up` lại — dữ liệu vẫn còn nhờ named volume.

## 1.5. Cạm bẫy thường gặp

- **Trùng port:** nếu máy đã có Postgres/Redis chạy sẵn ở `5432`/`6379`, `up` sẽ lỗi "port is already allocated". Đổi port phía host (vd map `5433:5432`) hoặc tắt service đang chiếm.
- **`down -v` xóa sạch dữ liệu:** cờ `-v` xóa luôn named volume. Chỉ dùng khi cố ý reset.
- **Healthcheck sai → service "mãi không healthy":** sai lệnh hoặc thiếu công cụ trong image khiến healthcheck luôn fail. Kiểm bằng `docker compose logs <service>`.
- **Secret commit nhầm lên git:** đừng hardcode mật khẩu thật rồi push. Dùng `.env` + `.gitignore`.

## 1.6. Góc kể khi phỏng vấn

> TODO mentor: điền — gợi ý xoay quanh "healthcheck + depends_on để thứ tự khởi động đúng", "named volume cho tính bền dữ liệu", "một lệnh tái lập toàn bộ hạ tầng".

## 1.7. Link tài liệu chính thức

- [Compose file reference — Docker Docs](https://docs.docker.com/reference/compose-file/)
- [Healthcheck trong Compose](https://docs.docker.com/reference/compose-file/services/#healthcheck)
- [Postgres image — Docker Hub](https://hub.docker.com/_/postgres) · [Redis image](https://hub.docker.com/_/redis) · [MinIO docs](https://min.io/docs/minio/container/index.html)

## 1.8. Xong bước này khi

- [x] `docker/docker-compose.yml` tồn tại, khai đủ 3 service + khối `volumes`.
- [x] `up -d` → cả 3 service `healthy`.
- [x] `down` rồi `up` lại không mất dữ liệu.

→ Sang [Bước 2 — EF Core packages](02-ef-core-packages.md).
