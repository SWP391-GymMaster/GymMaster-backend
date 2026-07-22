# Phân công hoàn thiện SDS — GymMaster

- Tài liệu nền: `docs/GymMaster_SDS_v1.1.docx`
- Ngày chốt: 22/07/2026
- Phạm vi: 28 hình, gồm 4 sơ đồ tổng quan, 12 class diagram và 12 sequence diagram
- Trạng thái: Phân công chính thức

## 1. Phân công chính thức

| Nhóm | Người | Phần vẽ được giao | Tổng |
|---|---|---|---:|
| N1 | Như | Class + Sequence: Authentication and Session Management; Password Recovery and Account Profile; Admin Dashboard and Audit Logs | 6 |
| N2 | Quang Anh | Backend Package Diagram; Database Schema/ERD; Class + Sequence: User, Member and Trainer Management; Class: Progress Tracking and Member 360 | 5 |
| N3 | Lộc | Class + Sequence: Membership Packages and Membership Lifecycle; Payment and VnPay Integration | 4 |
| N4 | Đam | Class + Sequence: Member Check-in; PT Assignment; Workout Plans and Trainer Notes; Sequence: Progress Tracking and Member 360 | 7 |
| N5 | Minh | System Architecture; Frontend Package Diagram; Class + Sequence: Nutrition/Meal Journal; Gemini Food Recognition | 6 |

Tổng cộng: **28/28 hình đã có người chịu trách nhiệm**.

## 2. Hai phần giao nhau đã chốt rõ

- **User/Member/Trainer Management:** Quang Anh trực tiếp vẽ; Như review phần Users, trạng thái tài khoản và quản trị mật khẩu.
- **Progress Tracking and Member 360:** Quang Anh vẽ Class Diagram; Đam vẽ Sequence Diagram. Hai bạn chốt chung actor, lifeline `ProgressService` và `GetProfile360Async` trước khi xuất ảnh.
- **Database Schema/ERD:** Mỗi owner gửi danh sách bảng và khóa ngoại của domain cho Quang Anh; Quang Anh hợp nhất và chịu trách nhiệm bản ERD cuối.

## 3. Danh sách 28 hình và owner

| No. | Diagram | Owner |
|---:|---|---|
| 01 | System Architecture / Component | Minh |
| 02 | Frontend Package | Minh |
| 03 | Backend Package | Quang Anh |
| 04 | Database Schema / ERD | Quang Anh |
| 05 | Class — Authentication and Session | Như |
| 06 | Sequence — Authentication and Session | Như |
| 07 | Class — Password Recovery and Profile | Như |
| 08 | Sequence — Password Recovery and Profile | Như |
| 09 | Class — User/Member/Trainer | Quang Anh |
| 10 | Sequence — User/Member/Trainer | Quang Anh |
| 11 | Class — Membership Lifecycle | Lộc |
| 12 | Sequence — Membership Lifecycle | Lộc |
| 13 | Class — Payment and VnPay | Lộc |
| 14 | Sequence — Payment and VnPay | Lộc |
| 15 | Class — Member Check-in | Đam |
| 16 | Sequence — Member Check-in | Đam |
| 17 | Class — PT Assignment | Đam |
| 18 | Sequence — PT Assignment | Đam |
| 19 | Class — Workout Plans/Trainer Notes | Đam |
| 20 | Sequence — Workout Plans/Trainer Notes | Đam |
| 21 | Class — Progress Tracking/Member 360 | Quang Anh |
| 22 | Sequence — Progress Tracking/Member 360 | Đam |
| 23 | Class — Nutrition/Meal Journal | Minh |
| 24 | Sequence — Nutrition/Meal Journal | Minh |
| 25 | Class — Gemini Food Recognition | Minh |
| 26 | Sequence — Gemini Food Recognition | Minh |
| 27 | Class — Admin Dashboard/Audit | Như |
| 28 | Sequence — Admin Dashboard/Audit | Như |

