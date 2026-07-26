# Bài nói — Sequence Diagram: Khôi phục mật khẩu & Hồ sơ cá nhân

**Người trình bày:** Như (N1) · **Thời lượng:** ~4 phút
**Hình:** *Figure 8. Sequence Diagram — Password Recovery and Account Profile* (SDS mục 2.c)

**Màn liên quan:** `/forgot-password` · `/reset-password` · `/change-password` · `/admin|staff|pt|member/profile` · `/member/profile/edit`

> Bài nói **gọi đúng tên các cột trên hình**. Tên dưới đây lấy từ **Class Specifications của SDS mục 2.b** — mở hình ra đối chiếu, cột nào ghi khác thì sửa lại tên cho khớp trước khi thuyết trình.
> Không đọc số dòng code, không kể tên hàm không có trên hình.

---

## Câu mở — giới thiệu các cột (30 giây)

> "Hình này gộp hai nhóm chức năng liên quan tới tài khoản cá nhân: **khôi phục mật khẩu** khi người dùng quên, và **tự quản lý hồ sơ** của mình.
>
> Các cột tham gia, từ trái sang:
>
> **User** — người dùng, ở đây có thể là khách chưa đăng nhập.
> **AuthController** và **AccountController** — hai cửa nhận yêu cầu: một cho mật khẩu, một cho hồ sơ cá nhân.
> **AuthService** — chứa logic khôi phục và đổi mật khẩu.
> **AccountService** — chứa logic hồ sơ và ảnh đại diện.
> **GymMasterDbContext** — tầng truy cập cơ sở dữ liệu.
> **BCrypt** — thư viện băm.
>
> Điểm khác so với sơ đồ đăng nhập lúc nãy: ở đây có **hai hệ thống bên ngoài** tham gia — **EmailSender** gửi thư qua SMTP, và **CloudinaryAvatarStorage** lưu ảnh. Đó là hai cột ngoài cùng bên phải."

*(Chỉ vào 2 cột ngoài cùng bên phải khi nói câu cuối)*

---

## Cảnh 1 — Quên mật khẩu: `AuthService` sinh mã và `EmailSender` gửi đi

> "Người dùng quên mật khẩu, vào màn Quên mật khẩu và **chỉ nhập email**. `AuthController` nhận rồi chuyển cho `AuthService`.
>
> `AuthService` làm ba việc.
>
> **Thứ nhất — chống spam:** nó hỏi `GymMasterDbContext` xem có vừa gửi mã cho email này chưa. Nếu vừa gửi trong vòng một phút thì **không gửi lại**, tránh bị lợi dụng gửi thư rác làm phiền người khác.
>
> **Thứ hai — sinh mã:** `AuthService` tạo một **mã sáu chữ số ngẫu nhiên**, hiệu lực **ba mươi phút**. Chú ý mũi tên sang cột `BCrypt`: mã này **được băm trước khi lưu**, y hệt cách xử lý mật khẩu. Nghĩa là kể cả quản trị viên cũng không đọc được mã của người dùng. `GymMasterDbContext` chỉ lưu bản băm.
>
> **Thứ ba — gửi mã:** `AuthService` nhờ `EmailSender` gửi mã qua email."

**Điểm nhấn — nói chậm:**

> "Xin phép nhấn mạnh mũi tên trả về: **dù email đó có tồn tại trong hệ thống hay không, phản hồi trả về là hoàn toàn giống nhau** — luôn báo 'nếu email tồn tại, hệ thống sẽ gửi mã'.
>
> Lý do giống với màn đăng nhập: nếu báo rõ 'email này chưa đăng ký' thì người ta có thể thử hàng loạt địa chỉ để **dò ra danh sách hội viên**. Ngay cả trường hợp bị chặn vì chưa đủ một phút, phản hồi **cũng vẫn là câu đó** — nói khác đi là lộ luôn việc email có tồn tại."

**Ý cần đọng lại:** mã được **băm như mật khẩu** · phản hồi **luôn giống nhau** để chống dò danh sách.

---

## Cảnh 2 — Đặt lại mật khẩu: `AuthService` kiểm mã

> "Người dùng mở email, lấy mã, quay lại hệ thống nhập **email, mã và mật khẩu mới**.
>
> `AuthService` lấy bản ghi mã từ `GymMasterDbContext` và kiểm tra ba điều kiện: mã **còn hạn** không, mã **đã dùng rồi** chưa, và **số lần nhập sai**.
>
> Rồi nó nhờ `BCrypt` đối chiếu mã vừa nhập với bản băm đã lưu — cùng cách làm với mật khẩu ở sơ đồ trước.
>
> Nhập sai thì `AuthService` **tăng bộ đếm** rồi lưu lại. **Sai quá ba lần là mã bị vô hiệu hoàn toàn**, phải xin mã mới — mà xin mã mới lại vướng giới hạn một phút.
>
> Đây là lý do mã chỉ cần sáu chữ số vẫn an toàn: sáu chữ số là một triệu khả năng, nghe thì ít, nhưng vì **mỗi lượt chỉ được thử ba lần và mỗi phút mới xin lại được**, nên dò trúng trong ba mươi phút là gần như không thể."

> "Khi mã đúng, `AuthService` làm bốn việc cùng lúc, đều qua `GymMasterDbContext`: nhờ `BCrypt` băm mật khẩu mới rồi lưu, đánh dấu mã **đã dùng** để không dùng lại được, **mở khoá tài khoản** nếu đang bị khoá vì đăng nhập sai nhiều lần, và **thu hồi toàn bộ vé gia hạn** — nghĩa là mọi thiết bị khác đang đăng nhập đều bị đăng xuất."

**Ý cần đọng lại:** mã dùng **một lần** · đặt lại mật khẩu **mở khoá luôn tài khoản** · **mọi thiết bị khác bị đăng xuất**.

---

## Cảnh 3 — Đổi mật khẩu khi đang đăng nhập

*(Nhánh ngắn — nói nhanh. Nếu bị cắt thời lượng thì bỏ cảnh này trước.)*

> "Trường hợp người dùng **vẫn nhớ** mật khẩu và chỉ muốn đổi thì ngắn hơn nhiều: không cần mã OTP, không cần `EmailSender`. Người dùng nhập **mật khẩu hiện tại** và mật khẩu mới, `AuthService` nhờ `BCrypt` xác minh mật khẩu cũ rồi thay bằng mật khẩu mới.
>
> Nhưng bước cuối vẫn giống hệt: **thu hồi toàn bộ vé gia hạn**. Vì thường người ta đổi mật khẩu chính là do nghi tài khoản bị lộ."

---

## Cảnh 4 — `AccountController` xử lý hồ sơ cá nhân

> "Nhóm chức năng thứ hai trên hình là hồ sơ cá nhân, đi qua `AccountController` chứ không phải `AuthController`. Cả bốn vai trò — quản trị viên, lễ tân, huấn luyện viên, hội viên — đều dùng **chung một luồng** này.
>
> Điểm thiết kế đáng chú ý: `AccountController` **không nhận mã người dùng từ phía giao diện**. Nó lấy danh tính **từ tấm vé đăng nhập**, rồi `AccountService` tự tìm đúng bảng hồ sơ tương ứng với vai trò — hội viên có bảng riêng, lễ tân có bảng riêng, huấn luyện viên có bảng riêng.
>
> Nhờ vậy **không ai sửa được hồ sơ của người khác qua đường này**, kể cả khi họ cố tình chỉnh sửa yêu cầu gửi lên. Việc quản trị viên sửa hồ sơ người khác là một chức năng riêng, ở sơ đồ khác, có kiểm tra quyền riêng."

**Ý cần đọng lại:** danh tính lấy **từ vé đăng nhập**, không nhận từ giao diện — đây là cách chống leo quyền.

---

## Cảnh 5 — `CloudinaryAvatarStorage` xử lý ảnh đại diện

> "Cuối cùng là ảnh đại diện. Người dùng chọn ảnh, `AccountService` kiểm tra **định dạng và dung lượng** — giới hạn năm megabyte, và chỉ nhận đúng vài định dạng ảnh.
>
> Ảnh **không lưu trong cơ sở dữ liệu** mà `AccountService` đẩy sang `CloudinaryAvatarStorage`. Dịch vụ đó tự cắt ảnh về kích thước chuẩn **hai trăm năm mươi sáu nhân hai trăm năm mươi sáu**, và cắt **tự động lấy khuôn mặt làm trung tâm**, rồi trả về đường dẫn ảnh. `GymMasterDbContext` chỉ lưu lại **đường dẫn** đó.
>
> Lý do không lưu ảnh trong cơ sở dữ liệu: ảnh rất nặng, lưu vào sẽ làm cơ sở dữ liệu phình to và sao lưu chậm. Dịch vụ chuyên dụng còn tự tối ưu và phân phối ảnh nhanh hơn."

**Ý cần đọng lại:** ảnh để ở **dịch vụ ngoài**, cơ sở dữ liệu chỉ giữ **đường dẫn**.

---

## Câu chốt (15 giây)

> "Điểm chung của cả nhóm chức năng này: **hệ thống không bao giờ lưu thông tin nhạy cảm ở dạng đọc được** — mật khẩu băm, mã OTP cũng băm, đều qua `BCrypt`. Và mọi thao tác liên quan tới mật khẩu đều kết thúc bằng việc **thu hồi các phiên đăng nhập cũ**, để nếu tài khoản từng bị lộ thì kẻ khác cũng bị đẩy ra ngoài."

---

# Phòng khi bị hỏi thêm

| Câu hỏi | Trả lời gọn |
|---|---|
| Sao email không tồn tại vẫn báo thành công? | Chống dò danh sách người dùng — lỗi *user enumeration* trong OWASP. |
| Mã OTP chỉ 6 số, không yếu à? | Yếu nếu cho dò thoải mái. Nhưng bị bó bởi ba lớp: **3 lần thử**, **30 phút hết hạn**, **60 giây mới xin lại được**. Quan trọng không phải độ dài mã mà là **tốc độ dò bị giới hạn**. |
| Mã OTP lưu thế nào? | Qua `BCrypt`, **băm** giống mật khẩu. Mở cơ sở dữ liệu ra cũng không đọc được. |
| Sao không lưu mã dạng thường cho dễ so sánh? | Cơ sở dữ liệu bị lộ là kẻ tấn công đọc được mã của mọi người đang đặt lại mật khẩu. |
| Nhập sai lần thứ ba thì sao? | Mã bị vô hiệu hoàn toàn, phải xin mã mới. |
| Đang bị khoá tài khoản, đặt lại mật khẩu có vào được không? | Được. Đặt lại mật khẩu **mở khoá luôn** tài khoản, không phải chờ hết 15 phút. |
| Xin mã liên tục để phá người khác được không? | Không. Có giới hạn **60 giây** giữa hai lần gửi, và phản hồi vẫn là câu chung. |
| Người dùng sửa hồ sơ của người khác được không? | Không. `AccountController` lấy danh tính **từ vé đăng nhập**, không nhận mã người dùng từ giao diện. |
| Sao có tới ba bảng hồ sơ? | Mỗi vai trò cần thông tin khác nhau — hội viên có ngày tham gia, huấn luyện viên có chuyên môn và kinh nghiệm. |
| Ảnh lưu ở đâu? | `CloudinaryAvatarStorage` — dịch vụ ngoài; cơ sở dữ liệu chỉ lưu đường dẫn. |

---

# Ghi chú kỹ thuật — đọc trước khi lên nói

### Tên trên hình ↔ cách gọi khi nói

Lần đầu nói **cả hai**, từ lần sau chỉ gọi tên trên hình.

| Trên hình | Gọi là |
|---|---|
| `:AuthController` | cửa nhận yêu cầu về mật khẩu |
| `:AuthService` | logic khôi phục và đổi mật khẩu |
| `:AccountController` | cửa nhận yêu cầu về hồ sơ cá nhân |
| `:AccountService` | logic hồ sơ và ảnh đại diện |
| `:GymMasterDbContext` | tầng truy cập cơ sở dữ liệu |
| `:BCrypt` | thư viện băm |
| `:EmailSender` (hoặc `SMTP`) | dịch vụ gửi thư |
| `:CloudinaryAvatarStorage` | dịch vụ lưu ảnh bên ngoài |

⚠️ **Mở `Figure 8` đối chiếu trước khi nói.** Danh sách trên lấy từ Class Specifications của SDS mục 2.b — nếu hình vẽ gộp hoặc đặt tên khác thì sửa lại cho khớp, đừng gọi tên không có trên slide.

### Sáu con số phải thuộc

| Thứ | Giá trị |
|---|---|
| Độ dài mã OTP | **6 chữ số** |
| Hạn mã OTP | **30 phút** |
| Số lần nhập sai tối đa | **3 lần** |
| Giãn cách giữa 2 lần xin mã | **60 giây** |
| Dung lượng ảnh tối đa | **5 MB** |
| Kích thước ảnh sau khi cắt | **256 × 256**, lấy khuôn mặt làm tâm |

### Những thứ **KHÔNG** nên nói

- Số dòng code, tên file
- Tên hàm **không có trên hình**
- Tên bảng trong cơ sở dữ liệu
- Chi tiết bộ sinh số ngẫu nhiên mã hoá
- Chế độ dự phòng trả mã về ngay trong phản hồi khi chưa cấu hình email — **chỉ chạy ở môi trường phát triển**. Nếu bị hỏi thì nói rõ là có chặn theo môi trường, bản chạy thật không bao giờ trả mã ra.

### Tài liệu chi tiết nếu cần đào sâu

`docs/08-Auth-Flow/jwt-auth-flow.md` — chặng "Thu hồi" giải thích vì sao đổi mật khẩu lại đăng xuất mọi thiết bị.
