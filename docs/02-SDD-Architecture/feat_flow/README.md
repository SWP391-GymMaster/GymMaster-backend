# feat_flow — Phân tích luồng feature

**Trạng thái:** 4 phân tích — chọn theo độ phức tạp, không làm đủ 10 feature để tránh trùng plan.md §6.

| File | Feature | Vì sao được chọn |
|---|---|---|
| [auth_feature_analysis.md](auth_feature_analysis.md) | 001 Auth | 9/10 feature phụ thuộc identity từ đây |
| [membership_billing_feature_analysis.md](membership_billing_feature_analysis.md) | 003 Billing | 7 feature phụ thuộc `MembershipLifecycle` |
| [member_360_feature_analysis.md](member_360_feature_analysis.md) | 006 Member 360° | gom dữ liệu từ **5 spec khác** |
| [vnpay_payment_feature_analysis.md](vnpay_payment_feature_analysis.md) | 010 VNPay | 2 đường callback, phải idempotent |

---

## Thư mục này để làm gì

Chứa tài liệu **"giải phẫu" một feature**: đi từ thao tác người dùng, qua từng file/function, tới câu query cuối cùng — kèm sequence diagram có nhãn file/hàm ở mỗi bước và trích đoạn code thật.

Tên file: `<tên_feature>_feature_analysis.md`

## Vì sao đang trống — tránh trùng với `plan.md`

Mỗi feature **đã có** mục **§6 Data Flow** trong `plan.md`, mô tả luồng ở mức đủ để hiểu và bảo trì. Sinh 10 file phân tích nữa sẽ là **bản sao thứ hai của cùng một thứ** — đúng cái bệnh mà đợt dọn tài liệu 2026-07-23 vừa xử lý (`06_FEATURE_SPECS.md` trùng với `spec.md`, đã đưa vào `archive/`).

| | `plan.md` §6 Data Flow | `feat_flow/*_feature_analysis.md` |
|---|---|---|
| Mức chi tiết | Luồng chính + các nhánh rẽ quyết định | Từng bước, kèm **số dòng** và trích code thật |
| Hình thức | Sơ đồ text (ASCII) | **Mermaid** sequence + component diagram |
| Dành cho | Người bảo trì feature | Người **mới vào dự án**, hoặc feature phức tạp cần đào sâu |
| Chi phí giữ đồng bộ | Thấp — đi cùng plan | **Cao** — trích code nên code đổi là lệch |

## Khi nào thì nên tạo file ở đây

Chỉ khi **cả hai** điều sau đúng:

1. Feature phức tạp tới mức `plan.md` §6 không đủ — luồng chạm nhiều slice hoặc có nhiều nhánh trạng thái.
2. Có người thật sự cần: onboarding thành viên mới, chuẩn bị bảo vệ đồ án, hoặc điều tra một bug xuyên feature.

**Ba ứng viên xứng đáng nhất** nếu cần làm:

| Feature | Vì sao |
|---|---|
| **006 — Member 360°** | Điểm tích hợp lớn nhất: gom dữ liệu từ **5 spec khác** trong một request |
| **003 — Membership lifecycle** | Vòng đời `PendingPayment → Active → Expired/Cancelled` + nối hạn + lazy expire, được **7 feature** phụ thuộc |
| **010 — VNPay** | Hai đường callback (IPN + Return) cùng kích hoạt được, phải idempotent — dễ sai nhất |

## Quy tắc khi viết

- **Chỉ dựa trên source code thật.** Đọc file trực tiếp; không suy đoán. Không xác định được thì ghi rõ *"Không tìm thấy trong source code"* thay vì đoán cho đủ.
- Trích code phải **copy chính xác**, kèm số dòng.
- Link file bằng **đường dẫn tương đối** từ gốc repo: `[MembershipLifecycle.cs](../../../backend/GymMaster.API/Features/Billing/MembershipLifecycle.cs)`.
- Nhãn Mermaid có ký tự đặc biệt (`.` `/` khoảng trắng) phải bọc ngoặc kép: `Slice("MembershipLifecycle.cs")`.
- Tối thiểu 2 diagram: 1 component/architecture + 1 sequence.

## Trước khi viết, kiểm xem đã có chưa

| Cần gì | Đã có ở đâu |
|---|---|
| Kiến trúc tổng thể, bản đồ phụ thuộc slice | [`../system-design/system-overview.md`](../system-design/system-overview.md) |
| Luồng dữ liệu của một feature | `plan.md` §6 của feature đó |
| Ai gọi ai giữa các feature | `system-overview.md` §3 + `plan.md` §7 Traceability |
| Kiến trúc triển khai | [`../../05-Deployment/deploy-diagram.md`](../../05-Deployment/deploy-diagram.md) |
| Schema + quan hệ bảng | [`../database-design/database-schema.md`](../database-design/database-schema.md) |
