# 05 — Deployment

Triển khai GymMaster lên **Google Cloud Run + Cloud SQL for SQL Server**, region `asia-southeast1` (Singapore).

| File | Đọc khi nào |
|---|---|
| [deploy-gcp.md](deploy-gcp.md) | **Dựng hạ tầng lần đầu** — tạo Cloud SQL, nạp schema, deploy, nối FE↔BE, setup Workload Identity Federation |
| [docker.md](docker.md) | Hiểu `backend/Dockerfile`, build/chạy container ở máy để debug |
| [ci-cd.md](ci-cd.md) | Hai workflow GitHub Actions — `ci.yml` và `deploy.yml` |
| [deploy-diagram.md](deploy-diagram.md) | Nhìn tổng thể: sơ đồ kiến trúc, bảng env vars, khác biệt Local ↔ Cloud Run |

## Tóm tắt nhanh

| | |
|---|---|
| **Backend** | Cloud Run `gymmaster-api` · ASP.NET Core 10 · cổng 8080 |
| **Frontend** | Cloud Run `gymmaster-os` · Next.js (repo riêng `GymMaster-frontend`) |
| **Database** | Cloud SQL `gymmaster-sql` · SQL Server 2022 Express |
| **Build** | Cloud Build đọc [`backend/Dockerfile`](../../backend/Dockerfile) — **không cần Docker ở máy** |
| **CD** | Bấm tay: Actions → *Deploy to Cloud Run* → Run workflow |
| **Xác thực CD** | Workload Identity Federation — không có file key JSON |
| **Project** | `gymmaster-500004` |

## Deploy trong 1 phút (khi hạ tầng đã dựng)

```text
Actions → Deploy to Cloud Run → Run workflow → chọn main → Run
```

CI chạy trước; CI xanh mới deploy; deploy xong tự smoke test `curl /`. URL hiện ở job summary.

## Ba việc CD **không** làm, phải chạy tay

1. **Thêm/đổi biến môi trường** → `gcloud run deploy ... --env-vars-file env.yaml` (giữ đủ mọi key, thiếu key nào là xoá key đó).
2. **Đổi schema DB** → chạy `database/*.sql` lên Cloud SQL **trước** khi deploy, không thì EF 500.
3. **Đổi `NEXT_PUBLIC_*` của FE** → sửa `ARG` trong Dockerfile repo frontend rồi rebuild (giá trị inline lúc build).

## Bẫy hay gặp

| Triệu chứng | Nguyên nhân |
|---|---|
| `/openapi/v1.json` trả **404** trên cloud | Đúng như thiết kế — `MapOpenApi()` bọc trong `IsDevelopment()`. Không phải deploy hỏng. |
| Smoke test khác 200 | Container không lên: thường thiếu env var ở revision mới, hoặc sai cổng |
| API `/auth/forgot-password` trả 200 mà **không có mail** | Thiếu `Email__SenderEmail`/`Email__AppPassword` → `EmailSender` bỏ qua im lặng |
| Link trong mail chết | `Email__FrontendBaseUrl` còn là `http://localhost:3000` |
| Biến môi trường "không ăn" | Thiếu **hai** gạch dưới: phải là `Jwt__SecretKey`, không phải `Jwt_SecretKey` |
| Tốn credit khi không demo | `gcloud sql instances patch gymmaster-sql --activation-policy=NEVER` |
