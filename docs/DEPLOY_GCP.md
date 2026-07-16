# Deploy GymMaster Backend → Google Cloud (Cloud Run + Cloud SQL for SQL Server)

> **Cả frontend lẫn backend đều deploy trên Google Cloud Run** (FE service `gymmaster-os`, BE service `gymmaster-api`). Tài liệu này tập trung deploy **backend** (.NET 10 API); FE cũng dùng Cloud Run (Dockerfile node:22-alpine, xem repo frontend).
> Phương án: **Cloud Run** (container serverless) + **Cloud SQL for SQL Server Express**.
> Vùng dùng `asia-southeast1` (Singapore) — gần VN nhất.

Đặt biến cho dễ copy (đổi `YOUR_PROJECT_ID` và mật khẩu):

```bash
export PROJECT_ID="YOUR_PROJECT_ID"
export REGION="asia-southeast1"
export SQL_INSTANCE="gymmaster-sql"
export DB_NAME="GymMasterDb"
export DB_PASS="ĐỔI_MẬT_KHẨU_MẠNH_Ở_ĐÂY"     # mật khẩu user 'sqlserver'
export SERVICE="gymmaster-api"
```

---

## 0. Chuẩn bị (chỉ làm 1 lần)

Cách nhanh nhất: mở **Cloud Shell** tại https://console.cloud.google.com (đã có sẵn `gcloud`).
Hoặc cài gcloud CLI ở máy: https://cloud.google.com/sdk/docs/install

```bash
gcloud auth login
gcloud config set project "$PROJECT_ID"

# Bật các API cần dùng
gcloud services enable \
  run.googleapis.com \
  sqladmin.googleapis.com \
  cloudbuild.googleapis.com \
  artifactregistry.googleapis.com
```

---

## 1. Tạo Cloud SQL for SQL Server (Express — license miễn phí)

```bash
gcloud sql instances create "$SQL_INSTANCE" \
  --database-version=SQLSERVER_2022_EXPRESS \
  --tier=db-custom-1-3840 \
  --region="$REGION" \
  --root-password="$DB_PASS" \
  --storage-size=10GB \
  --storage-type=SSD \
  --no-backup

# Tạo database rỗng
gcloud sql databases create "$DB_NAME" --instance="$SQL_INSTANCE"
```

> 💡 **Tiết kiệm tiền**: khi không demo, tắt instance để khỏi tốn tiền compute
> (chỉ còn phí storage ~$2/tháng), bật lại trước khi demo:
> ```bash
> gcloud sql instances patch "$SQL_INSTANCE" --activation-policy=NEVER   # TẮT
> gcloud sql instances patch "$SQL_INSTANCE" --activation-policy=ALWAYS  # BẬT
> ```

---

## 2. Nạp schema + dữ liệu vào DB

Cloud SQL (SQL Server) **không** import trực tiếp file `.sql` qua gcloud (chỉ nhận `.bak`),
nên ta kết nối trực tiếp bằng công cụ rồi chạy script trong thư mục `database/`.

1. Bật public IP + cho phép IP máy bạn truy cập:
   ```bash
   # Lấy IP public hiện tại của bạn
   MY_IP=$(curl -s https://api.ipify.org)
   gcloud sql instances patch "$SQL_INSTANCE" \
     --assign-ip \
     --authorized-networks="$MY_IP/32"

   # Xem IP public của instance
   gcloud sql instances describe "$SQL_INSTANCE" \
     --format="value(ipAddresses[0].ipAddress)"
   ```
2. Dùng **Azure Data Studio** / **SSMS** / `sqlcmd` kết nối:
   - Server: `<PUBLIC_IP>,1433`  · User: `sqlserver`  · Password: `$DB_PASS`
   - Encrypt: true, Trust server certificate: true
3. Chọn database `GymMasterDb` rồi chạy lần lượt các script trong `database/`
   (chạy `GymMaster_SQLServer_Final.sql` trước, rồi các file `004…007` nếu cần).

---

## 3. Deploy backend lên Cloud Run (build trên cloud, không cần Docker ở máy)

Connection string trỏ tới public IP của Cloud SQL:

```bash
SQL_IP=$(gcloud sql instances describe "$SQL_INSTANCE" \
  --format="value(ipAddresses[0].ipAddress)")

CONN="Server=${SQL_IP},1433;Database=${DB_NAME};User Id=sqlserver;Password=${DB_PASS};Encrypt=True;TrustServerCertificate=True;"

# Deploy từ source (Cloud Build đọc backend/Dockerfile)
gcloud run deploy "$SERVICE" \
  --source ./backend \
  --region "$REGION" \
  --allow-unauthenticated \
  --port 8080 \
  --set-env-vars "ASPNETCORE_ENVIRONMENT=Production" \
  --set-env-vars "ConnectionStrings__DefaultConnection=${CONN}" \
  --set-env-vars "Jwt__SecretKey=ĐỔI_MỘT_CHUỖI_BÍ_MẬT_DÀI_>=32_KÝ_TỰ" \
  --set-env-vars "Google__ClientId=GOOGLE_OAUTH_CLIENT_ID" \
  --set-env-vars "VnPay__TmnCode=VNPAY_TMNCODE" \
  --set-env-vars "VnPay__HashSecret=VNPAY_HASHSECRET"
```

Sau khi deploy, lấy URL service:
```bash
gcloud run services describe "$SERVICE" --region "$REGION" --format="value(status.url)"
# vd: https://gymmaster-api-xxxxx-as.a.run.app
```

> ⚠️ **Bảo mật DB**: cách trên cho Cloud Run kết nối qua public IP. Vì Cloud Run
> không có IP tĩnh, để chắc chắn kết nối được bạn có thể tạm thời thêm
> `--authorized-networks=0.0.0.0/0` (vẫn cần đúng mật khẩu). Đây là mức chấp nhận
> được cho demo 2 tháng; muốn an toàn hơn thì dùng Private IP + Serverless VPC
> connector (phức tạp hơn, không bắt buộc cho đồ án).

### Các biến môi trường cấu hình khác (nếu dùng)
- `VnPay__ReturnUrl` → trỏ về URL Cloud Run: `https://<service-url>/api/v1/payments/vnpay/return`
- Email OTP (MailKit): `Email__Host`, `Email__Port`, `Email__User`, `Email__Password`, `Email__From`
  (tên section đúng theo `Options/EmailOptions.cs`).

---

## 4. Nối frontend (Cloud Run) ↔ backend

1. FE (Cloud Run `gymmaster-os`) đặt `NEXT_PUBLIC_API_BASE_URL` = URL Cloud Run backend ở trên
   (bake ở build-time qua Dockerfile ARG), rồi redeploy.
2. CORS: hiện backend mở `AllowAnyOrigin` nên FE gọi được ngay. Muốn siết chặt,
   sửa policy `"Frontend"` trong `Program.cs` thành `.WithOrigins("https://<app>.run.app")`.

---

## 5. Cập nhật code sau này
Mỗi lần sửa code, chỉ cần chạy lại lệnh `gcloud run deploy ... --source ./backend`.

## Dọn dẹp (khi xong đồ án, tránh tốn credit)
```bash
gcloud run services delete "$SERVICE" --region "$REGION"
gcloud sql instances delete "$SQL_INSTANCE"
```
