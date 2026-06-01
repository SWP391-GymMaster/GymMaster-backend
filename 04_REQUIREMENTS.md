# 04 — REQUIREMENTS (EARS Notation)

**Status:** Approved
**Quy ước:** Dùng EARS — `WHEN/WHILE/WHERE … THE system SHALL …`. `SHALL` = bắt buộc.
5 mẫu: Ubiquitous · Event-driven · State-driven · Optional · Unwanted (xử lý lỗi).

---

## A. FUNCTIONAL REQUIREMENTS

### A1. Authentication & Authorization
- **FR-AUTH-01 (Ubiquitous):** THE system SHALL hash password bằng BCrypt cost ≥ 12.
- **FR-AUTH-02 (Event):** WHEN user submit đúng email + password, THE system SHALL cấp access token (15') + refresh token (7d).
- **FR-AUTH-03 (Unwanted):** WHERE đăng nhập sai 5 lần liên tiếp trong 15 phút, THE system SHALL khoá tài khoản tạm 15 phút và ghi security event.
- **FR-AUTH-04 (Unwanted):** WHERE credentials sai, THE system SHALL trả lỗi GIỐNG NHAU cho "sai email" và "sai password" (chống user enumeration).
- **FR-AUTH-05 (State):** WHILE token hết hạn/không hợp lệ, THE system SHALL trả 401 và yêu cầu đăng nhập lại.

### A2. Member Management
- **FR-MEM-01 (Event):** WHEN Admin/Staff tạo Member với dữ liệu hợp lệ, THE system SHALL tạo User (role Member) + hồ sơ và trả 201.
- **FR-MEM-02 (Unwanted):** WHERE email/phone đã tồn tại, THE system SHALL trả 409 Conflict.
- **FR-MEM-03 (Event):** WHEN Admin/Staff tìm Member theo tên/SĐT/mã, THE system SHALL trả danh sách khớp trong < 1s.

### A3. Membership & Payment
- **FR-PKG-01 (Event):** WHEN Admin tạo gói tập, THE system SHALL lưu gói với tên, giá, thời hạn (ngày).
- **FR-MS-01 (Event):** WHEN Staff bán gói cho Member, THE system SHALL tạo Membership status `PendingPayment`, tính ngày hết hạn = ngày bắt đầu + thời hạn.
- **FR-MS-02 (Event):** WHEN Payment được ghi nhận cho Membership, THE system SHALL chuyển status sang `Active`.
- **FR-MS-03 (Event):** WHEN gia hạn cho Member đang có gói active, THE system SHALL nối tiếp ngày hết hạn cũ.
- **FR-MS-04 (State):** WHILE ngày hiện tại > ngày hết hạn, THE system SHALL coi Membership là `Expired`.
- **FR-PAY-01 (Unwanted):** WHERE ghi Payment trùng cho cùng Membership+kỳ, THE system SHALL trả 409.

### A4. Check-in
- **FR-CHK-01 (Event):** WHEN Member check-in và có Membership Active còn hạn, THE system SHALL tạo CheckIn (timestamp UTC).
- **FR-CHK-02 (Unwanted):** WHERE Member không có Membership Active còn hạn, THE system SHALL từ chối check-in và hiển thị nhắc gia hạn.

### A5. PT Assignment & Workout
- **FR-PT-01 (Event):** WHEN Admin phân công PT cho Member, THE system SHALL tạo TrainerAssignment active.
- **FR-PT-02 (Unwanted):** WHERE Member đã có PT active, THE system SHALL từ chối tạo assignment mới (422) cho đến khi assignment cũ kết thúc.
- **FR-WP-01 (Event):** WHEN PT tạo WorkoutPlan cho Member được phân công, THE system SHALL lưu plan + danh sách WorkoutExercise.
- **FR-WP-02 (Unwanted):** WHERE PT thao tác trên Member KHÔNG được phân công, THE system SHALL trả 403.
- **FR-NOTE-01 (Event):** WHEN PT ghi TrainerNote cho ngày, THE system SHALL lưu note gắn Member + ngày.

### A6. Progress & Meal Journal
- **FR-PROG-01 (Event):** WHEN Member/PT ghi ProgressLog, THE system SHALL lưu cân nặng/metric + ngày đo.
- **FR-MEAL-01 (Event):** WHEN Member thêm món vào MealLog với khẩu phần > 0, THE system SHALL lưu MealLogItem và tính lại Daily Calorie Summary.
- **FR-MEAL-02 (Unwanted):** WHERE khẩu phần ≤ 0 hoặc FoodItem không tồn tại, THE system SHALL trả 422/404.
- **FR-CAL-01 (Ubiquitous):** THE system SHALL tính Daily Calorie Summary = tổng (calo × khẩu phần) của các MealLogItem trong ngày.

### A7. Dashboard & Audit
- **FR-DASH-01 (Event):** WHEN Admin mở Dashboard, THE system SHALL trả doanh thu, số membership active/expired, check-in theo ngày từ dữ liệu thật.
- **FR-AUD-01 (Ubiquitous):** THE system SHALL ghi AuditLog (actor, action, entity, entityId, timestamp) cho mọi hành động mutating quan trọng.

### A8. Authorization (cross-cutting)
- **FR-RBAC-01 (State):** WHILE người dùng là Member, THE system SHALL chỉ cho truy cập dữ liệu của chính họ.
- **FR-RBAC-02 (State):** WHILE người dùng là PT, THE system SHALL chỉ cho truy cập Member được phân công.

---

## B. NON-FUNCTIONAL REQUIREMENTS
| ID | Loại | Yêu cầu (đo được) |
|---|---|---|
| NFR-PERF-01 | Performance | API thường < 300ms; tìm kiếm Member < 1s (p95) |
| NFR-PERF-02 | Performance | Dashboard load < 2s với ~1000 hội viên |
| NFR-SCALE-01 | Scalability | Chịu ~50 concurrent users, 100–300 check-in/ngày |
| NFR-SEC-01 | Security | Password BCrypt; JWT; HTTPS only; CORS whitelist FE domain |
| NFR-SEC-02 | Security | Không log PII thô (mask email/phone); không lộ stack trace |
| NFR-AVAIL-01 | Availability | Demo uptime ổn định; có seed data |
| NFR-MAINT-01 | Maintainability | Layered architecture; coverage ≥ 80% business logic |
| NFR-USAB-01 | Usability | UI tiếng Việt, responsive, thao tác check-in ≤ 3 click |
| NFR-AUDIT-01 | Compliance | Audit trail truy được cho mọi tranh chấp gói/payment/PT |
