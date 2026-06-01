# 02 — PRODUCT SCOPE

**Status:** Approved | **Direction:** Core Gym Operation & Member Progress

## 1. Product Direction
GymMaster tập trung vào **vận hành cốt lõi của phòng gym + theo dõi tiến độ hội viên**, không phải nền tảng đa năng. Mục tiêu: 1 core flow chạy xuyên suốt, demo ổn định, dữ liệu thật từ workflow.

## 2. MVP (bắt buộc cho demo)
| ID | Feature | Mô tả |
|---|---|---|
| MVP-01 | Auth & Role | Đăng nhập JWT, 4 role Admin/Staff/PT/Member |
| MVP-02 | Member Management | Admin/Staff tạo, sửa, tìm hồ sơ hội viên |
| MVP-03 | Membership Package | Admin tạo/sửa gói tập mẫu |
| MVP-04 | Sell & Renew Membership | Bán/gia hạn gói, gán cho Member, set ngày hết hạn |
| MVP-05 | Payment (manual) | Ghi nhận thanh toán thủ công cho membership |
| MVP-06 | Check-in | Ghi nhận lượt check-in của Member |
| MVP-07 | PT Assignment | Admin phân công PT cho Member |
| MVP-08 | Workout Plan | PT tạo giáo án + bài tập cho Member |
| MVP-09 | Trainer Note | PT ghi chú luyện tập hằng ngày |
| MVP-10 | Progress Log | Member/PT ghi tiến độ (cân nặng, body metrics) |
| MVP-11 | Member 360 Profile | Member xem hồ sơ tổng hợp: gói, PT, giáo án, tiến độ |
| MVP-12 | Meal Journal (manual) | Member nhập bữa ăn từ food database |
| MVP-13 | Daily Calorie Summary | Hệ thống tính tổng calo/macro từ MealLog |
| MVP-14 | Dashboard | Doanh thu, trạng thái thanh toán, check-in |
| MVP-15 | Audit Log | Ghi log hành động quan trọng |

## 3. Secondary (làm nếu còn thời gian)
| ID | Feature |
|---|---|
| SEC-01 | Custom food do Member tự thêm |
| SEC-02 | Calorie target/macro goal cá nhân hóa |
| SEC-03 | Barcode lookup món ăn |
| SEC-04 | Lọc/báo cáo nâng cao trên dashboard |
| SEC-05 | Push notification (FCM) nhắc hết hạn gói |

## 4. Enhancement (sau secondary)
| ID | Feature | Ghi chú |
|---|---|---|
| ENH-01 | Image Food Recognition Assist | Dùng Google Cloud Vision đọc ảnh → **gợi ý tên món/nguyên liệu**, KHÔNG tự định lượng calo. User phải xác nhận món + khẩu phần trước khi lưu. |

## 5. Out of Scope (CHỐT — KHÔNG làm trong MVP)
- Multi-branch / chuỗi phòng gym.
- Tích hợp payment gateway tự động (VNPay/Momo/Stripe).
- Realtime dashboard / websocket.
- Đặt lịch lớp học, booking PT theo slot.
- Mobile app native (chỉ web responsive).
- E-commerce bán supplement / sản phẩm.
- Tự động định lượng calo từ ảnh (chỉ gợi ý tên món — xem ENH-01).

> **Lý do giữ Out of Scope:** giảm rủi ro external API, đảm bảo core flow demo ổn định, dễ chia việc 5 người trong timeline.
