# 06 — FEATURE SPECS

**Status:** Approved
Mỗi feature theo cấu trúc **8 thành phần** (Ch.5 sách): Context · Actors · Functional (EARS) · Non-functional · Data · Error Handling · Acceptance Criteria · Out of Scope.

---

## F1 — Sell & Renew Membership (CORE, Risk: High → Full Spec)

**1. Context & Goal:** Cho phép Staff bán/gia hạn gói cho hội viên, là nguồn doanh thu chính và đầu vào cho dashboard. Sai = sai doanh thu + tranh chấp.

**2. Actors:** Admin, Staff (thực hiện); System (tính ngày hết hạn, set status).

**3. Functional (EARS):**
- WHEN Staff bán gói hợp lệ, THE system SHALL tạo Membership `PendingPayment` + tính `EndDate = StartDate + Package.DurationDays`.
- WHEN Payment ghi nhận, THE system SHALL chuyển Membership → `Active` và ghi AuditLog.
- WHEN gia hạn cho Member có gói active, THE system SHALL set `StartDate = EndDate cũ`, nối tiếp thời hạn.
- WHERE Member chưa thanh toán, THE system SHALL KHÔNG cho check-in.

**4. Non-functional:** Thao tác bán gói < 500ms; mọi bước có audit.

**5. Data:** Memberships, Payments, MembershipPackages, Members (xem `05`).

**6. Error Handling (Unwanted):**
- Gói không tồn tại → 404. · Member không tồn tại → 404.
- Ghi payment trùng kỳ → 409. · StartDate quá khứ xa/không hợp lệ → 422.

**7. Acceptance Criteria (Given-When-Then):**
- [ ] Bán gói hợp lệ → Membership `PendingPayment`, EndDate đúng.
- [ ] Ghi payment → status `Active`, AuditLog `SELL_MEMBERSHIP`.
- [ ] Gia hạn khi đang active → EndDate nối tiếp, không trùng lặp ngày.
- [ ] Member chưa thanh toán → check-in bị từ chối.

**8. Out of Scope:** Payment gateway tự động; hoá đơn điện tử; trả góp.

---

## F2 — Check-in (CORE, Risk: Medium → Detailed)

**1. Context:** Ghi nhận lượt đến phòng, đầu vào cho dashboard & xác thực gói còn hạn.
**2. Actors:** Staff (quầy), Member (self).
**3. Functional (EARS):**
- WHEN Member có Membership Active còn hạn check-in, THE system SHALL tạo CheckIn (timestamp UTC).
- WHERE membership hết hạn/không active, THE system SHALL từ chối + hiện nhắc gia hạn.
- (Optional) WHERE cấu hình giới hạn 1 lần/ngày bật, THE system SHALL chặn check-in thứ 2 trong ngày.
**4. Non-functional:** Check-in < 300ms, ≤ 3 click.
**5. Data:** CheckIns, Memberships, Members.
**6. Error Handling:** Member không tồn tại → 404; không có gói active → 403/422 kèm message gia hạn.
**7. Acceptance:** [ ] Member còn hạn → tạo CheckIn. [ ] Member hết hạn → từ chối + cảnh báo. [ ] Check-in hiện trong dashboard hôm nay.
**8. Out of Scope:** Quét vân tay/thẻ từ phần cứng (MVP dùng tìm theo mã/SĐT).

---

## F3 — PT Assignment & Workout Plan (CORE, Risk: Medium → Detailed)

**1. Context:** Admin phân công PT; PT lập giáo án cho Member của mình.
**2. Actors:** Admin (phân công), PT (giáo án/note).
**3. Functional (EARS):**
- WHEN Admin phân công PT cho Member chưa có PT active, THE system SHALL tạo TrainerAssignment active.
- WHERE Member đã có PT active, THE system SHALL trả 422.
- WHEN PT tạo WorkoutPlan cho Member được phân công, THE system SHALL lưu plan + exercises.
- WHERE PT thao tác trên Member không thuộc mình, THE system SHALL trả 403.
**4. Non-functional:** Lưu giáo án < 500ms.
**5. Data:** TrainerAssignments, WorkoutPlans, WorkoutExercises, TrainerNotes.
**6. Error Handling:** Member/PT không tồn tại → 404; trùng PT active → 422; vượt quyền → 403.
**7. Acceptance:** [ ] Phân công thành công khi Member chưa có PT. [ ] PT chỉ thấy Member của mình. [ ] Tạo plan + ≥1 exercise. [ ] PT lạ bị 403.
**8. Out of Scope:** Lịch buổi tập theo slot, video bài tập.

---

## F4 — Meal Journal & Calorie Summary (CORE, Risk: Low-Med → Detailed)

**1. Context:** Member ghi bữa ăn từ food database, hệ thống tính calo/macro ngày.
**2. Actors:** Member (nhập), System (tính tổng).
**3. Functional (EARS):**
- WHEN Member thêm món với khẩu phần > 0, THE system SHALL tạo MealLogItem + tính lại Daily Calorie Summary.
- WHERE khẩu phần ≤ 0 → 422; FoodItem không tồn tại → 404.
- THE system SHALL tính Summary = Σ(calo × khẩu phần) trong ngày.
**4. Non-functional:** Cập nhật summary < 500ms sau khi thêm món.
**5. Data:** FoodItems, MealLogs, MealLogItems.
**6. Error Handling:** Khẩu phần âm/0 → 422; món lạ → 404; trùng món → cộng dồn khẩu phần.
**7. Acceptance:** [ ] Thêm món hợp lệ → summary cập nhật đúng. [ ] Khẩu phần 0 → 422. [ ] Tổng calo = đúng công thức.
**8. Out of Scope:** Tự định lượng calo từ ảnh (chỉ gợi ý tên — ENH-01); gợi ý thực đơn AI.

---

## F5 — Dashboard (CORE, Risk: Low → Sketch+Detailed)

**1. Context:** Admin xem doanh thu, membership active/expired, check-in.
**2. Actors:** Admin.
**3. Functional (EARS):** WHEN Admin mở dashboard, THE system SHALL trả số liệu từ dữ liệu thật (không mock): tổng doanh thu kỳ, #active/#expired, check-in theo ngày.
**4. Non-functional:** Load < 2s với ~1000 hội viên.
**5. Data:** Payments, Memberships, CheckIns (aggregate query).
**6. Error Handling:** Không có dữ liệu kỳ → trả 0, không lỗi.
**7. Acceptance:** [ ] Số liệu khớp DB. [ ] Lọc theo khoảng ngày. [ ] Member/PT không truy cập được (403).
**8. Out of Scope:** Realtime, export báo cáo nâng cao (Secondary).
