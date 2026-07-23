# 14 — Prompt Library

> Thư viện prompt mẫu cho dự án GymMaster. Luôn kèm ngữ cảnh: stack (ASP.NET Core 10 + SQL Server + EF Core 10), spec ref, và CONSTITUTION.

---

# 1. Pattern nền tảng (sách Ch.14)

### P-01 Clarification-First
```
Đây là spec [dán UC/FR]. TRƯỚC khi viết code, liệt kê mọi điểm chưa rõ,
giả định ngầm, và edge case còn thiếu. KHÔNG code cho tới khi tôi trả lời.
```

### P-02 Plan-then-Execute
```
Task: [mô tả]. Stack: ASP.NET Core 10, EF Core 10, SQL Server, layered
(Controller→Service→Repository). Trình bày plan: file sẽ tạo/sửa, cách tiếp cận,
ảnh hưởng migration. Đợi tôi duyệt rồi mới code.
```

### P-03 Spec-Compliance (chống over-engineering)
```
Chỉ implement đúng các mệnh đề SHALL trong spec sau, không thêm tính năng.
Mọi thứ trong "Out of Scope" KHÔNG được làm. [dán feature spec từ 06]
```

### P-04 Shadowing / Self-Review
```
Đóng vai senior reviewer. Rà đoạn code sau theo CONSTITUTION.md và acceptance
criteria của spec. Liệt kê vi phạm (security, kiến trúc, thiếu validate, lệch spec)
kèm cách sửa. [dán code]
```

---

# 2. Prompt theo vai trò

### BA / Spec
```
Viết spec feature "[tên]" theo cấu trúc 8 thành phần (Context, Actors,
Functional dạng EARS, Non-functional, Data, Error Handling, Acceptance Criteria
Given-When-Then, Out of Scope). Domain: phòng gym, role Admin/Staff/PT/Member.
Bảo đảm 40–60% nội dung là xử lý lỗi/edge case.
```

### Database
```
Thiết kế bảng SQL Server cho [thực thể] dùng EF Core 10 Code-First. Nêu cột, kiểu,
khóa, index, ràng buộc, soft-delete (IsDeleted), audit (CreatedAt/UpdatedAt).
Viết entity class C# + cấu hình Fluent API.
```

### Test
```
Sinh unit test (xUnit) cho service [tên] theo acceptance criteria sau [dán].
Bao gồm happy path + mọi error case trong Error Handling. Mock repository.
Mục tiêu coverage ≥80%.
```

### Code Review
```
Review PR theo checklist: kiến trúc layered, response {success,data,error,meta},
userId lấy từ JWT claim, không secret hardcode, có validate + audit log,
có test. Báo cáo pass/fail từng mục.
```

### Debug
```
Lỗi: [dán log/stack]. Ngữ cảnh: [endpoint/flow]. Đề xuất nguyên nhân gốc theo
thứ tự khả năng, cách kiểm chứng từng giả thuyết. KHÔNG sửa lung tung — chỉ ra
1 fix tối thiểu khớp spec.
```

### Presentation / Báo cáo
```
Tóm tắt module [tên] cho slide demo SWP391: vấn đề, giải pháp, luồng chính,
công nghệ (ASP.NET Core 10 + SQL Server + Next.js), điểm nổi bật về spec-driven + AI.
Ngắn gọn, gạch đầu dòng.
```

---

# 3. Quy tắc dùng prompt
- Luôn dán spec ref thật, không để AI tự đoán business rule.
- Không dán secret/connection string thật.
- Sau khi AI trả code → chạy Validation Gate (09 §6) trước khi tin.
- Lưu lại prompt hữu ích + tinh chỉnh ở Retrospective (13 §5).
