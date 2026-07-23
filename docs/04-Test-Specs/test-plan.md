# 09 — Test Plan

# GymMaster — Test Strategy

---

# 1. Test Objectives
- Đảm bảo core workflow chạy được end-to-end.
- Đảm bảo business rules không bị vi phạm.
- Đảm bảo role-based access đúng.
- Đảm bảo dashboard lấy dữ liệu thật từ workflow.
- Đảm bảo demo final ổn định.

# 2. Test Types
| Type | Description |
|---|---|
| Manual Test | Test UI flow bằng tay. |
| API Test | Postman/Thunder Client cho endpoint. |
| Unit Test | Logic business ở Service layer (mục tiêu ≥80% coverage). |
| Integration Test | Flow nhiều module + DB test (SQL Server / LocalDB hoặc Docker mssql). |
| UAT | Kịch bản người dùng cuối. |
| Security Test | Auth, RBAC, input validation, secret leak. |

---

# 3. Core Test Scenarios

## Authentication
| TC ID | Scenario | Expected | Spec |
|---|---|---|---|
| TC-001 | Login valid account | Success + token | FR-AUTH-02 |
| TC-002 | Login wrong password | Lỗi chung (chống enumeration) | FR-AUTH-04 |
| TC-003 | Locked user login | Access denied | FR-AUTH-? |
| TC-004 | Logout | Session ended | UC-02 |
| TC-005 | Sai >5 lần/15 phút | Khóa tạm 15 phút | FR-AUTH-03 |

## Membership
| TC ID | Scenario | Expected | Spec |
|---|---|---|---|
| TC-201 | Sell package to member | Payment + membership created | F1 |
| TC-202 | Sell inactive package | Rejected | FR-PKG-01 |
| TC-203 | Renew membership | EndDate nối tiếp | FR-MS-03 |
| TC-204 | Payment pending | Membership PendingPayment | FR-MS-01 |
| TC-205 | Ghi payment trùng | 409 Conflict | FR-PAY-01 |

## Check-in
| TC ID | Scenario | Expected | Spec |
|---|---|---|---|
| TC-301 | Active member check-in | Check-in created | FR-CHK-01 |
| TC-302 | Expired member check-in | Rejected + nhắc gia hạn | FR-CHK-02 |
| TC-303 | Invalid QR/card | Rejected | UC-09 |
| TC-304 | Check-in shown in dashboard | Dashboard updated | F5 |

## PT Assignment
| TC ID | Scenario | Expected | Spec |
|---|---|---|---|
| TC-401 | Assign PT to member | Assignment created | FR-PT-01 |
| TC-402 | Assign PT khi đã có active | 422 / đóng cũ rồi tạo mới | FR-PT-02 |
| TC-403 | PT views assigned member | Data shown | FR-RBAC-02 |
| TC-404 | PT views unassigned member | 403 Access denied | FR-WP-02 |

## Calorie Tracking
| TC ID | Scenario | Expected | Spec |
|---|---|---|---|
| TC-601 | Set calorie target | Target saved | UC-16 |
| TC-602 | Add meal log from food item | Saved + summary updated | FR-MEAL-01 |
| TC-603 | Add custom food | Saved | S-01 |
| TC-604 | Invalid quantity (≤0) | 422 Validation error | FR-MEAL-02 |
| TC-605 | View daily summary | Calo consumed/remaining đúng | FR-CAL-01 |
| TC-606 | PT views member calorie history | Chỉ assigned member | FR-RBAC-02 |
| TC-607 | Image recognition gợi ý tên món | Hiện gợi ý để xác nhận | UC-26 |
| TC-608 | User sửa kết quả nhận diện sai | Lưu món đã sửa | UC-26 |
| TC-609 | Recognition service fails | Manual fallback hoạt động | UC-26 |

## Security (bổ sung theo sách Ch.2.5 / 8)
| TC ID | Scenario | Expected |
|---|---|---|
| TC-701 | userId lấy từ body thay vì JWT | Bị từ chối (chống privilege escalation) |
| TC-702 | SQL injection ở ô tìm kiếm | Bị chặn (parameterized) |
| TC-703 | Response chứa stack trace | Không có; chỉ {code,message,requestId} |
| TC-704 | Password lưu plaintext | Không; phải BCrypt |
| TC-705 | Secret trong repo (gitleaks) | Không có secret |

---

# 4. UAT Demo Script
1. Admin login → 2. tạo package → 3. tạo/tìm member → 4. bán package → 5. Member login → 6. check-in → 7. Admin assign PT → 8. PT login → 9. tạo workout plan → 10. ghi trainer note → 11. Member xem 360° profile → 12. add meal log → 13. xem daily calorie summary → 14. Admin xem dashboard → 15. Admin xem audit log.

# 5. Defect Log Template
| Defect ID | Feature | Description | Severity | Owner | Status | Fixed In |
|---|---|---|---|---|---|---|
| BUG-001 |  |  | Low/Medium/High/Critical | TBD | Open |  |

---

# 6. (Bổ sung theo sách Ch.13) Validation Gate — 4 lớp trước khi merge
> "Đừng tin AI khi nó nói 'I have finished'. Tin khi Unit Test xanh và Spec Checklist được tick."

| Lớp | Kiểm tra | Công cụ | Fail = ? |
|---|---|---|---|
| L1 Automated | `dotnet test` pass; lint 0 error; build clean; gitleaks no secret | CI | Block merge |
| L2 Spec compliance | Mỗi `SHALL` trong 04 có code + tag `// FR-...`; không có code ngoài Out of Scope | Manual + AI trace | Task chưa done |
| L3 Constitution | Security/Arch/Eng rules (CONSTITUTION.md) | CI + self-check | Block + escalate |
| L4 Acceptance | Demo từng acceptance criteria của UC/Feature | Manual + demo | Quay lại implement |

# 7. Coverage Target & Exit Criteria
- Unit coverage ≥ **80%** business logic; integration cho mọi endpoint (happy + ≥1 error).
- Exit để demo (v1.0): 0 defect Critical/High mở; tất cả TC core Pass; Validation Gate L1–L4 pass.
