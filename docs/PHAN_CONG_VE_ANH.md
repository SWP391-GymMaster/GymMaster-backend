# Phân công vẽ ảnh tài liệu GymMaster

> Phạm vi: 60 hình bàn giao cho bộ tài liệu, gồm 42 mockup giao diện và 18 sơ đồ.
> Tài liệu này bổ sung cho `docs/PHAN_CONG.md`; không thay thế bảng phân công feature được sinh tự động.

Theo phân công feature và độ nặng công việc hiện tại, không nên chia đều đúng 12 ảnh/người. N3 và N2 đang có phần code nặng nhất nên nhận ít ảnh hơn; N5 nhẹ nhất nên nhận thêm các sơ đồ tổng quan dùng chung.

## Tổng quan phân công

| Nhóm | Người | Mockup giao diện | Sơ đồ | Tổng ảnh |
|---|---|---:|---:|---:|
| N1 | Như | 11 | 2 | **13** |
| N2 | Quang Anh | 5 | 4 | **9** |
| N3 | Lộc | 8 | 2 | **10** |
| N4 | Đam | 9 | 3 | **12** |
| N5 | Minh | 9 | 7 | **16** |
| **Tổng** |  | **42** | **18** | **60** |

---

# N1 — Như

**Miền nghiệp vụ:** Auth, tài khoản cá nhân và quản trị tài khoản

## Sơ đồ — 2 ảnh

1. **Use Case Diagram for Guest**
2. **Guest/Authentication Screen Flow**

## Mockup — 11 ảnh

3. **SCR-AUTH-02 — Login**
4. **SCR-AUTH-03 — Sign Up**
5. **SCR-AUTH-04 — Forgot Password**
6. **SCR-AUTH-05 — Reset Password**
7. **SCR-AUTH-06 — Change Password**
8. **SCR-ADM-02 — User Accounts**
9. **SCR-ADM-03 — Staff Management**
10. **SCR-ADM-12 — Admin Profile**
11. **SCR-STF-07 — Staff Profile**
12. **SCR-PT-07 — PT Profile**
13. **SCR-MEM-09 — Member Profile**

**Lý do:** toàn bộ đều thuộc Auth, Account, Users và các trang profile dùng chung API `/users/me/*`.

---

# N2 — Quang Anh

**Miền nghiệp vụ:** Hồ sơ Hội viên và PT

## Sơ đồ — 4 ảnh

1. **Use Case Diagram for Member**
2. **Member Screen Flow**
3. **GymMaster Database Schema / ERD**
4. **Backend Package Diagram**

## Mockup — 5 ảnh

5. **SCR-ADM-04 — Member Management**
6. **SCR-ADM-05 — Member 360**
7. **SCR-ADM-06 — Trainer Management**
8. **SCR-STF-02 — Member Search and Details**
9. **SCR-PT-02 — Assigned Members and Member 360**

**Lý do:** N2 sở hữu `Features/Members` và `Features/Trainers`, là trung tâm dữ liệu được nhiều module khác sử dụng. Vì ERD và Backend Package Diagram khá nặng nên tổng số ảnh của N2 thấp hơn các nhóm khác.

---

# N3 — Lộc

**Miền nghiệp vụ:** Gói tập, Membership và Thanh toán

## Sơ đồ — 2 ảnh

1. **Staff Screen Flow**
2. **Membership and Payment Flow**

## Mockup — 8 ảnh

3. **SCR-ADM-07 — Package Management**
4. **SCR-ADM-08 — Membership Management**
5. **SCR-ADM-09 — Payment Management**
6. **SCR-STF-03 — Sell Package**
7. **SCR-STF-04 — Renew Package**
8. **SCR-STF-06 — Staff Payments**
9. **SCR-MEM-02 — Membership and VNPay**
10. **SCR-MEM-03 — VNPay Return**

**Lý do:** N3 đang nặng nhất về code do wizard bán/gia hạn gói, VNPay và ba chức năng non-UI, nên chỉ nhận các hình trực tiếp thuộc nghiệp vụ của mình.

> Trong Staff Screen Flow, Lộc chỉ cần tập trung phần bán gói, gia hạn và thanh toán. Phần check-in cần hỏi Đam để nối flow đúng.

---

# N4 — Đam

**Miền nghiệp vụ:** Tập luyện, Tiến độ và Check-in

## Sơ đồ — 3 ảnh

1. **Use Case Diagram for Staff**
2. **Use Case Diagram for PT**
3. **PT Screen Flow**

## Mockup — 9 ảnh

4. **SCR-ADM-10 — PT Assignment**
5. **SCR-STF-05 — Check-in Terminal**
6. **SCR-PT-03 — Workout Plan Workspace**
7. **SCR-PT-04 — Trainer Notes Workspace**
8. **SCR-PT-05 — Member Progress**
9. **SCR-PT-06 — PT Check-in**
10. **SCR-MEM-04 — Workout Plans**
11. **SCR-MEM-05 — Trainer Notes**
12. **SCR-MEM-06 — Progress**

**Lý do:** các sơ đồ Staff/PT cần thể hiện rõ check-in, assignment, workout plan, trainer note và progress — đúng miền nghiệp vụ của N4.

> Use Case Staff cần lấy phần bán/gia hạn/thanh toán từ Lộc và phần tạo/tìm hội viên từ Quang Anh, nhưng Đam là người ghép sơ đồ hoàn chỉnh.

---
