# Secondary Features — Backlog (CHƯA spec)

> Trạng thái: **Deferred**. Đây KHÔNG phải spec đầy đủ — chỉ là backlog để theo dõi. Chỉ viết spec 9 thành phần (như `docs/03-Interface-Specs/feature-specs/NNN-*/spec.md`) khi core (001–008) đã ổn định và còn thời gian. Thứ tự ưu tiên có thể điều chỉnh.

| ID | Feature | Mô tả | Phụ thuộc | Ưu tiên |
|---|---|---|---|---|
| ~~SEC-02~~ | ~~Basic In-app Notification~~ | **Đóng 2026-07-17 — gỡ khỏi phạm vi.** Nhóm quyết định không làm thông báo. Phần vỏ rỗng đã có (nút chuông + `NotificationsController` trả rỗng) đã bị xoá hẳn: BE `c61cadc`, FE `54475f0`. Xem `docs/01-SRS-Requirements/use-cases/srs-use-cases.md` UC-25. | 003 (Membership) | ~~Medium~~ |
| SEC-03 | PT Online Booking | Member đặt lịch buổi tập với PT | 005 (PT Assignment) | Low |
| SEC-04 | Basic Group Classes | Quản lý lớp nhóm (Yoga, Zumba, HIIT) ở mức đơn giản | — | Low |
| SEC-05 | Combo Packages | Quản lý gói combo / nhiều dịch vụ | 003 (Package) | Low |
| SEC-06 | Basic PT KPI | Theo dõi số member, số buổi, hiệu suất cơ bản của PT | 005, 004 | Low |
| SEC-07 | Room / Training Space Booking | Đặt phòng/khu vực tập (nếu còn thời gian) | — | Lowest |

## Quy tắc xử lý (theo SDD / Spec Kit)
- Khi quyết định làm một secondary feature → tạo `docs/03-Interface-Specs/feature-specs/NNN-<name>/spec.md` đủ 9 thành phần + EARS, đặt số tiếp theo (010, 011, …).
- Không implement secondary nào khi chưa có spec Approved (ARCH-05).
- Mọi quyết định đưa secondary vào/ra scope → ghi `docs/06-Management/decision-log.md`.

> Liên quan: feature enhancement `009-image-food-recognition` đã có spec riêng (mức enhancement, ngoài MVP).
