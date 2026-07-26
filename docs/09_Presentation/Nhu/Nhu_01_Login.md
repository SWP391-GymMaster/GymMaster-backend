# Bài nói — Sequence Diagram: Đăng nhập

**Người trình bày:** Như (N1) · **Thời lượng:** ~3–4 phút
**Hình:** *Figure 6. Sequence Diagram — Authentication and Session Management (Login)* (SDS mục 1.c)

> Bài nói **gọi đúng tên các cột trên hình** (`LoginPage`, `AuthController`, `AuthService`, `GymMasterDbContext`, `BCrypt`) — vì thầy nhìn thấy các tên đó trên slide, nói chung chung là lệch.
> Nhưng **không đọc số dòng code, không kể tên hàm không có trên hình** — phần đó để dành cho hỏi đáp.

---

## Câu mở — giới thiệu 6 cột (30 giây)

> "Đây là sơ đồ tuần tự của chức năng đăng nhập. Sáu cột dọc là sáu thành phần tham gia, em xin giới thiệu nhanh từ trái sang phải:
>
> **User** — người dùng.
> **LoginPage** — trang đăng nhập phía giao diện, viết bằng Next.js.
> **AuthController** — cửa nhận yêu cầu của máy chủ; nó chỉ nhận và chuyển tiếp, **không chứa nghiệp vụ**.
> **AuthService** — nơi chứa **toàn bộ logic xác thực**; đây là cột hoạt động nhiều nhất trên hình.
> **GymMasterDbContext** — tầng truy cập cơ sở dữ liệu, dùng Entity Framework Core.
> **BCrypt** — thư viện băm mật khẩu.
>
> Các mũi tên đọc từ trên xuống theo thời gian: sang phải là **nhờ làm giúp**, quay về trái là **trả kết quả**."

*(Lướt tay qua 6 cột khi đọc tên, rồi lướt từ trên xuống)*

---

## Cảnh 1 — Yêu cầu đi từ người dùng vào tới `AuthService`

**Chỉ vào:** mũi tên **1 → 2 → 3**

> "**Mũi tên 1:** người dùng nhập email và mật khẩu rồi bấm đăng nhập. `LoginPage` kiểm tra sơ bộ ngay tại chỗ — thiếu ô nào hoặc email sai định dạng thì báo đỏ luôn, **chưa gửi gì lên máy chủ**. Đây là để người dùng biết lỗi ngay, không phải chờ mạng.
>
> **Mũi tên 2:** qua được bước đó, `LoginPage` gửi yêu cầu tới địa chỉ `/api/v1/auth/login` của máy chủ.
>
> **Mũi tên 3:** `AuthController` nhận yêu cầu rồi **chuyển thẳng cho `AuthService`**. Em xin nhấn mạnh: `AuthController` không tự quyết định gì cả — nó chỉ nhận yêu cầu, gọi service, rồi đóng gói kết quả trả về. Thiết kế như vậy để sau này nếu đổi cách gọi, ví dụ thêm ứng dụng di động, thì chỉ viết cửa nhận mới, còn `AuthService` giữ nguyên."

**Ý cần đọng lại:** `LoginPage` chỉ thu thập · `AuthController` chỉ chuyển tiếp · **nghiệp vụ nằm ở `AuthService`**.

---

## Cảnh 2 — `AuthService` tra cứu tài khoản trong `GymMasterDbContext`

**Chỉ vào:** mũi tên **4 → 5 → 6 → 7**

> "**Mũi tên 4** là mũi tên vòng lại chính nó — `AuthService` **tự kiểm tra lại** email và mật khẩu có rỗng không. Trống thì trả lỗi ngay, không đi tiếp.
>
> Có thể thầy sẽ hỏi vì sao `LoginPage` kiểm rồi mà còn kiểm lại. Lý do: kiểm ở giao diện **có thể bị bỏ qua** nếu ai đó gọi thẳng vào API bằng công cụ khác. **Kiểm ở giao diện là để thuận tiện, kiểm ở máy chủ mới là để an toàn.**
>
> **Mũi tên 5:** `AuthService` nhờ `GymMasterDbContext` tìm tài khoản theo email.
>
> **Mũi tên 6:** `GymMasterDbContext` trả về tài khoản **kèm luôn vai trò** — lấy trong cùng một lần truy vấn, không phải hỏi cơ sở dữ liệu hai lần. Nếu không có thì trả về rỗng.
>
> **Mũi tên 7** là nhánh rẽ sớm: nếu **không tìm thấy tài khoản**, hoặc **tài khoản đang bị khoá**, `AuthService` trả lỗi thẳng về `LoginPage` — chưa hề đụng tới `BCrypt`."

**Điểm nhấn — nói chậm:**

> "Xin phép nhấn mạnh dòng ghi chú ngay trên mũi tên 7: *same message — no account enumeration*.
>
> Nghĩa là **hai trường hợp khác nhau nhưng trả về cùng một thông báo giống hệt** — chỉ nói 'email hoặc mật khẩu không đúng'. Nhìn thì như thiếu thân thiện, nhưng nếu báo rõ 'email này chưa đăng ký' thì người ta có thể thử hàng loạt địa chỉ để **dò ra danh sách hội viên** của phòng gym. Đây là lỗ hổng có tên trong OWASP, gọi là *user enumeration*."

**Ý cần đọng lại:** máy chủ **kiểm lại** dù giao diện đã kiểm · thông báo mập mờ là **quyết định bảo mật** · tài khoản khoá thì **chưa cần kiểm mật khẩu**.

---

## Cảnh 3 — `AuthService` nhờ `BCrypt` kiểm mật khẩu

**Chỉ vào:** mũi tên **8 → 9 → 10 → 11**, và cột `:BCrypt`

> "**Mũi tên 8:** tìm thấy tài khoản và tài khoản không bị khoá, `AuthService` mới nhờ `BCrypt` kiểm tra mật khẩu.
>
> Xin lưu ý cột `BCrypt` này — **hệ thống không lưu mật khẩu của người dùng**. Trong cơ sở dữ liệu chỉ có bản băm, tức một chuỗi **không thể đảo ngược** lại thành mật khẩu gốc.
>
> Vì vậy `AuthService` không so sánh hai mật khẩu với nhau. Nó đưa cho `BCrypt` mật khẩu vừa nhập cùng bản băm đã lưu; `BCrypt` **băm lại rồi đối chiếu**, và ở **mũi tên 9** chỉ trả về đúng hoặc sai. Kể cả quản trị viên mở cơ sở dữ liệu ra cũng không đọc được mật khẩu của bất kỳ ai.
>
> **Mũi tên 10** là nhánh sai — lại là mũi tên vòng lại `AuthService`: nó **đếm số lần đăng nhập sai** trong một khoảng thời gian. Sai quá năm lần trong mười lăm phút thì tài khoản bị **khoá tạm mười lăm phút**. Đây là cơ chế chống dò mật khẩu tự động.
>
> **Mũi tên 11** là nhánh đúng: `AuthService` **xoá bộ đếm sai** và ghi lại thời điểm đăng nhập gần nhất."

**Ý cần đọng lại:** mật khẩu **không bao giờ lưu nguyên bản** · `BCrypt` chỉ trả đúng/sai · sai quá nhiều lần thì khoá tạm.

---

## Cảnh 4 — Cấp phiên đăng nhập và chuyển trang

**Chỉ vào:** mũi tên **12 → 13 → 14 → 15 → 16**

> "Xác thực xong, `AuthService` cấp cho người dùng **hai tấm vé**.
>
> Vé thứ nhất là **vé ra vào**, hạn ngắn — mười lăm phút. Mỗi lần gọi chức năng nào đó, vé này được đính kèm để máy chủ biết người gọi là ai. **Trong vé có ghi sẵn vai trò** của người dùng.
>
> Vé thứ hai là **vé gia hạn**, sống bảy ngày, dùng để xin vé mới khi vé thứ nhất hết hạn — để người dùng không phải đăng nhập lại mỗi mười lăm phút.
>
> **Mũi tên 12:** `AuthService` lại nhờ `BCrypt` — lần này **băm vé gia hạn**. Đây là chi tiết em muốn nhấn mạnh: vé gia hạn được đối xử **y như mật khẩu**, băm rồi mới lưu. Cơ sở dữ liệu bị lộ thì kẻ tấn công cũng không dùng được vé của ai.
>
> **Mũi tên 13:** `AuthService` nhờ `GymMasterDbContext` lưu bản băm đó xuống cơ sở dữ liệu. Bản gốc chỉ trả về cho người dùng, **máy chủ không giữ lại**.
>
> **Mũi tên 14:** `AuthService` trả kết quả về `AuthController` — gồm hai tấm vé, **vai trò**, và **đường dẫn cần chuyển tới**.
>
> **Mũi tên 15:** `AuthController` đóng gói thành phản hồi chuẩn của hệ thống rồi gửi về `LoginPage`. Toàn bộ tám mươi lăm chức năng của hệ thống đều trả về **cùng một khuôn phản hồi** như vậy, nên giao diện chỉ cần một chỗ xử lý chung.
>
> **Mũi tên 16:** `LoginPage` lưu phiên đăng nhập rồi đưa người dùng vào đúng khu làm việc của mình — quản trị viên vào trang quản trị, hội viên vào trang hội viên."

**Ý cần đọng lại:** **vai trò do máy chủ quyết định** · vé gia hạn **cũng được băm** · màn đăng nhập không có nút chọn vai trò.

---

## Câu chốt (15 giây)

> "Nhìn lại cả sơ đồ: `LoginPage` chỉ thu thập và hiển thị, `AuthController` chỉ chuyển tiếp, `GymMasterDbContext` chỉ lấy dữ liệu, `BCrypt` chỉ trả đúng hay sai — còn **mọi quyết định đều nằm ở `AuthService`**: tài khoản có tồn tại không, có bị khoá không, mật khẩu đúng không, vai trò là gì. Đó là lý do người dùng không thể tự nâng quyền của mình từ phía trình duyệt."

---

# Phòng khi bị hỏi thêm

Trả lời **ngắn**, chỉ mở rộng khi thầy hỏi tiếp.

| Câu hỏi | Trả lời gọn |
|---|---|
| Sao `AuthController` mỏng vậy, để làm gì? | Tách nghiệp vụ khỏi cách gọi. Mai thêm ứng dụng di động thì viết cửa nhận mới, `AuthService` giữ nguyên. |
| Sao kiểm hai lần, cả `LoginPage` lẫn `AuthService`? | Kiểm ở giao diện để **báo lỗi nhanh**; kiểm ở máy chủ để **an toàn**, vì có thể gọi thẳng API bỏ qua giao diện. |
| Sao không báo rõ "email không tồn tại"? | Chống dò danh sách người dùng — lỗi *user enumeration* trong OWASP. |
| Sao lấy vai trò ngay ở mũi tên 6? | Để chỉ truy vấn cơ sở dữ liệu **một lần**. Không thì lấy tài khoản xong phải hỏi thêm lần nữa cho vai trò. |
| Mật khẩu lưu thế nào? | `BCrypt` băm, **không mã hoá**, không đảo ngược được. Mỗi mật khẩu có chuỗi muối riêng nên hai người cùng mật khẩu vẫn ra hai bản băm khác nhau. |
| Quên mật khẩu thì lấy lại kiểu gì? | **Không lấy lại được** — chỉ đặt mật khẩu mới qua mã OTP gửi về email. Web nào gửi lại mật khẩu cũ là đang lưu nguyên bản, sai nghiêm trọng. |
| Vì sao vé gia hạn cũng phải băm? | Để cơ sở dữ liệu bị lộ thì vé đó cũng vô dụng — cùng lý do với mật khẩu. |
| Vé ra vào bị lấy cắp thì sao? | Hạn chỉ 15 phút nên thiệt hại có giới hạn. Vé gia hạn thì **thu hồi được** — đổi mật khẩu là mọi thiết bị khác bị đăng xuất. |
| Người dùng tự sửa vai trò được không? | Không. Vai trò nằm trong vé đã được máy chủ **ký**. Sửa một ký tự là chữ ký sai, máy chủ từ chối ngay. |
| Sai bao nhiêu lần thì khoá? | **Quá 5 lần sai** trong cửa sổ 15 phút → khoá tạm 15 phút. *(Xem ghi chú bên dưới.)* |

---

# Ghi chú kỹ thuật — đọc trước khi lên nói

### Bản đồ mũi tên → cảnh

| Mũi tên | Đi từ → đến | Cảnh |
|---|---|---|
| 1 | User → LoginPage | 1 |
| 2 | LoginPage → AuthController | 1 |
| 3 | AuthController → AuthService | 1 |
| 4 | AuthService → chính nó (kiểm rỗng) | 2 |
| 5–6 | AuthService ↔ GymMasterDbContext | 2 |
| 7 | AuthService → LoginPage (lỗi, rẽ sớm) | 2 |
| 8–9 | AuthService ↔ BCrypt (kiểm mật khẩu) | 3 |
| 10–11 | AuthService → chính nó (đếm sai / xoá bộ đếm) | 3 |
| 12 | AuthService → BCrypt (băm vé gia hạn) | 4 |
| 13 | AuthService → GymMasterDbContext (lưu) | 4 |
| 14–15 | AuthService → AuthController → LoginPage | 4 |
| 16 | LoginPage → chính nó (lưu phiên, chuyển trang) | 4 |

### ⚠️ Con số trên hình lệch nhẹ so với code

Hình ghi *"5 attempts / 15 min → LockedUntil"*. Code thực tế khoá khi số lần sai **vượt quá** 5, tức **lần sai thứ 6** mới bị khoá — sai đúng 5 lần thì chưa khoá.

**Cách xử lý khi nói:** dùng *"sai **quá** năm lần"* thay vì *"sai năm lần"*. Nếu thầy đếm thật và bắt bẻ thì nhận, nói rõ ngưỡng thật là "vượt quá 5".

### Ba con số nên thuộc

| Thứ | Giá trị |
|---|---|
| Hạn vé ra vào (access token) | **15 phút** |
| Hạn vé gia hạn (refresh token) | **7 ngày** |
| Khoá tạm khi sai mật khẩu | **15 phút** |

### Tên trên hình ↔ cách gọi khi nói

Lần đầu nói **cả hai** ("`AuthService` — nơi chứa toàn bộ logic xác thực"), từ lần sau chỉ gọi tên trên hình.

| Trên hình | Gọi là |
|---|---|
| `:LoginPage (Next.js)` | trang đăng nhập phía giao diện |
| `:AuthController` | cửa nhận yêu cầu |
| `:AuthService` | nơi chứa toàn bộ logic xác thực |
| `:GymMasterDbContext (EF Core)` | tầng truy cập cơ sở dữ liệu |
| `:BCrypt` | thư viện băm mật khẩu |

### Những thứ **KHÔNG** nên nói khi thuyết trình

Để dành cho hỏi đáp — nói ra ngay sẽ loãng:

- Số dòng code, tên file
- Tên hàm **không có trên hình** (hàm nào hình có ghi thì đọc theo hình)
- Thuật toán ký vé (HS256, HMAC-SHA256) và cấu trúc ba phần của token
- Tham số `cost 12` của `BCrypt`
- Cơ chế xoay vòng vé gia hạn

### Tài liệu chi tiết nếu cần đào sâu

`docs/08-Auth-Flow/jwt-auth-flow.md` — vòng đời token đầy đủ, 5 chặng, kèm bản đồ `file:dòng`.
