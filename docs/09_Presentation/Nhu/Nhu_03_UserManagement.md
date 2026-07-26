# Bài nói — Sequence Diagram: Quản trị tài khoản

**Người trình bày:** Như (N1) · **Thời lượng:** ~3 phút
**Hình:** *Figure 10. Sequence Diagram — User, Member and Trainer Management* (SDS mục 3.c)

**Màn của Như:** `/admin/users` · `/admin/staff`

---

## ⚠️ Đọc trước: hình này bạn chỉ nói MỘT PHẦN

`Figure 10` gộp **ba nhóm quản lý** vào một sơ đồ:

| Phần trong hình | Cột chính | Màn | Người trình bày |
|---|---|---|---|
| **Quản lý tài khoản** | `UsersController` · `UserService` | `/admin/users`, `/admin/staff` | **Như — N1** ✅ |
| Quản lý hội viên | `MemberService` | `/admin/members`, `/staff/members` | Quang Anh — N2 |
| Quản lý huấn luyện viên | `TrainerService` | `/admin/trainers` | Quang Anh — N2 |

**Thống nhất với Quang Anh trước khi thuyết trình:** ai nói phần nào. Hai người cùng đứng lên giải thích một hình là mất điểm phối hợp.

Bài dưới đây viết cho **phần `UsersController` / `UserService`** — câu mở đã khoanh vùng sẵn.

---

## Câu mở — giới thiệu cột và khoanh vùng (30 giây)

> "Sơ đồ này mô tả các màn hình quản lý trong hệ thống. Nó gồm ba nhóm dùng chung một khuôn xử lý — nhìn trên hình sẽ thấy ba service song song: `UserService` quản lý tài khoản, `MemberService` quản lý hội viên, `TrainerService` quản lý huấn luyện viên.
>
> Em xin trình bày nhánh **`UsersController` và `UserService`** — tức là màn Quản lý tài khoản và màn Quản lý nhân viên. Hai nhánh còn lại bạn Quang Anh sẽ trình bày.
>
> Các cột trong nhánh của em: **Admin** là người dùng; **ManagementWorkspace** là màn quản lý phía giao diện; **UsersController** là cửa nhận yêu cầu; **UserService** chứa nghiệp vụ; **GymMasterDbContext** truy cập cơ sở dữ liệu; **BCrypt** băm mật khẩu; và **AuditService** ghi nhật ký hệ thống."

*(Chỉ vào nhánh `UsersController` khi nói câu thứ hai)*

---

## Cảnh 1 — Mở màn quản lý và tìm kiếm

> "Quản trị viên mở màn quản lý tài khoản. `ManagementWorkspace` gửi lên các điều kiện lọc: từ khoá tìm kiếm, vai trò, trạng thái và **số trang**.
>
> `UsersController` nhận yêu cầu — và đây là điểm đầu tiên đáng nói: **toàn bộ `UsersController` chỉ quản trị viên mới gọi được**. Việc kiểm tra quyền diễn ra ở lớp ngoài, **trước khi chạm tới `UsersController`**. Nếu người gọi là lễ tân thì bị chặn ngay từ đó, `UserService` **không chạy một dòng nào**.
>
> Qua được, `UsersController` chuyển cho `UserService`. `UserService` dựng câu truy vấn có lọc, nhờ `GymMasterDbContext` **nối bảng tài khoản với bảng vai trò** rồi trả về **từng trang** kết quả.
>
> Vì sao phải phân trang: với khoảng một nghìn hội viên, trả hết một lần sẽ chậm và tốn băng thông vô ích."

> "Màn Quản lý nhân viên thực chất **dùng chung đúng `UsersController` này**, chỉ khác một bộ lọc vai trò là Lễ tân. Chúng em không viết trùng chức năng hai lần."

**Ý cần đọng lại:** quyền chặn **trước khi vào controller** · phân trang · hai màn dùng chung một controller.

---

## Cảnh 2 — Tạo tài khoản mới

> "Quản trị viên nhập thông tin và **chọn vai trò**. `UserService` kiểm ba việc trước khi tạo.
>
> **Một:** nhờ `GymMasterDbContext` kiểm tra **email và số điện thoại không trùng** với tài khoản đang hoạt động.
>
> **Hai:** nếu quản trị viên **để trống mật khẩu**, `UserService` **tự sinh một mật khẩu tạm** rồi nhờ `BCrypt` băm — vì trong thực tế lễ tân tạo tài khoản hộ hội viên tại quầy, không thể bắt khách tự nghĩ mật khẩu ngay lúc đó.
>
> **Ba:** tuỳ vai trò được chọn, `UserService` **tạo kèm hồ sơ tương ứng** — tạo tài khoản Lễ tân thì tạo kèm hồ sơ nhân sự.
>
> Xin lưu ý phần đóng khung trên hình: tài khoản, ánh xạ vai trò và hồ sơ được ghi trong **cùng một giao dịch**. Hoặc thành công cả ba, hoặc không tạo gì cả — **không bao giờ có tài khoản thiếu hồ sơ**.
>
> Cuối cùng, `UserService` gọi `AuditService` **ghi nhật ký** việc tạo tài khoản."

**Ý cần đọng lại:** không trùng danh tính · tự sinh mật khẩu tạm · tài khoản và hồ sơ tạo trong **cùng một giao dịch**.

---

## Cảnh 3 — Sửa, khoá và đặt lại mật khẩu

> "Ngoài tạo mới, `UsersController` còn ba thao tác: **sửa thông tin**, **khoá hoặc mở khoá** tài khoản, và **đặt lại mật khẩu** cho người dùng khi họ không tự làm được. Cả ba đều kết thúc bằng một mũi tên sang `AuditService`.
>
> Có một luật nghiệp vụ chúng em cố ý đặt ra, xin phép nêu vì nó hay bị hỏi: **vai trò được gán một lần lúc tạo và không đổi được về sau**. `UserService` từ chối mọi yêu cầu đổi vai trò. Muốn đổi thì phải tạo tài khoản mới rồi khoá tài khoản cũ.
>
> Lý do nằm ở chính cột `AuditService`: hệ thống lưu nhật ký mọi thao tác. Nếu một tài khoản hôm nay là Hội viên, mai thành Quản trị viên, thì các bản ghi nhật ký cũ trở nên khó truy — không biết lúc thao tác đó người này đang ở vai trò nào. **Giữ vai trò cố định thì lịch sử luôn đọc được rõ ràng.**"

**Ý cần đọng lại:** **vai trò không đổi được** — và lý do gắn thẳng với `AuditService`.

---

## Cảnh 4 — Xoá tài khoản: xoá mềm

> "Thao tác cuối là xoá. Nhưng `UserService` **không xoá thật khỏi cơ sở dữ liệu** — nó chỉ nhờ `GymMasterDbContext` đánh dấu là đã xoá. Cách này gọi là **xoá mềm**.
>
> Vì sao: một hội viên đã có lịch sử thanh toán, lịch sử tập luyện, lịch sử điểm danh. Xoá thật thì các bản ghi đó **mất liên kết**, báo cáo doanh thu của tháng trước sẽ sai theo.
>
> Xoá mềm giữ nguyên toàn bộ lịch sử, đồng thời **email và số điện thoại của tài khoản đã xoá được dùng lại** cho người mới — vì lúc kiểm trùng ở cảnh hai, `UserService` chỉ xét các tài khoản chưa bị xoá.
>
> Và mũi tên cuối cùng: `AuditService` ghi lại cả việc xoá. Năm thao tác — tạo, sửa, khoá, đặt lại mật khẩu, xoá — **đều được ghi nhật ký**: ai làm, làm gì, lúc nào."

**Ý cần đọng lại:** xoá mềm để **giữ lịch sử** · email dùng lại được · **cả năm thao tác đều có nhật ký**.

---

## Câu chốt (10 giây)

> "Tóm lại, nhánh `UserService` được thiết kế quanh hai nguyên tắc: **không mất dữ liệu lịch sử** — nên xoá là xoá mềm và vai trò không đổi; và **mọi thao tác đều truy vết được** — nên tất cả đều đi qua `AuditService`."

---

# Phòng khi bị hỏi thêm

| Câu hỏi | Trả lời gọn |
|---|---|
| Sao không xoá thật? | Giữ lịch sử thanh toán, điểm danh, tập luyện. Xoá thật là mất liên kết, báo cáo cũ sai theo. |
| Xoá mềm rồi email dùng lại được không? | Được — hệ thống chỉ kiểm trùng trên các tài khoản **chưa bị xoá**. |
| Sao không cho đổi vai trò? | Để nhật ký truy vết không mập mờ. Muốn đổi thì tạo tài khoản mới, khoá tài khoản cũ. |
| Bỏ trống mật khẩu khi tạo thì sao? | `UserService` tự sinh mật khẩu tạm; người dùng đổi sau qua chức năng quên mật khẩu. |
| Lễ tân có gọi được `UsersController` không? | Không. Toàn bộ controller này **chỉ Quản trị viên**, chặn ở lớp ngoài. |
| Hai màn Quản lý tài khoản và Quản lý nhân viên khác nhau chỗ nào? | Cùng `UsersController`, khác **bộ lọc vai trò**. Không viết trùng hai lần. |
| Vì sao phải dùng giao dịch khi tạo? | Để không bao giờ có tài khoản mà thiếu hồ sơ, hoặc hồ sơ mà không có tài khoản. |
| `AuditService` là của nhóm nào? | Thuộc nhóm Nhật ký hệ thống — bạn Minh. Nhánh của em **gọi vào** chứ không sở hữu. |

---

# Ghi chú kỹ thuật — đọc trước khi lên nói

### Tên trên hình ↔ cách gọi khi nói

| Trên hình | Gọi là |
|---|---|
| `:ManagementWorkspace` | màn quản lý phía giao diện |
| `:UsersController` | cửa nhận yêu cầu quản lý tài khoản |
| `:UserService` | nghiệp vụ quản lý tài khoản |
| `:GymMasterDbContext` | tầng truy cập cơ sở dữ liệu |
| `:BCrypt` | thư viện băm mật khẩu |
| `:AuditService` | ghi nhật ký hệ thống |

⚠️ **Mở `Figure 10` đối chiếu trước khi nói.** Danh sách trên lấy từ Class Specifications của SDS mục 3.b — nếu hình đặt tên khác thì sửa lại cho khớp, đừng gọi tên không có trên slide.

### Năm thao tác được ghi nhật ký

Tạo tài khoản · Sửa thông tin · Đổi trạng thái khoá/mở · Đặt lại mật khẩu · Xoá mềm.

### Những thứ **KHÔNG** nên nói

- Số dòng code, tên file
- Tên hàm **không có trên hình**
- Mã lỗi cụ thể — nếu bị hỏi *"đổi vai trò thì báo gì"* thì chỉ nói: hệ thống từ chối và báo không được phép đổi vai trò
- Chi tiết tham số của `BCrypt`
- Chuyện `/admin/staff` và `/admin/members` dùng chung một tệp giao diện — đó là chuyện nội bộ nhóm, không phải nội dung thiết kế

### Phối hợp

Trước buổi thuyết trình, thống nhất với **Quang Anh (N2)** về `Figure 10`: bạn nói nhánh `UserService`, Quang Anh nói nhánh `MemberService` và `TrainerService`. Câu mở đã khoanh vùng sẵn để người nghe không nhầm.
