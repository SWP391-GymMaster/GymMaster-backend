# -*- coding: utf-8 -*-
"""Sinh docs/06-Management/phan-cong.md — phan cong 5 nguoi theo lat cat doc (FE + BE).

Danh sach man hinh / endpoint lay tu out/inventory.csv, KHONG go tay.
Chay tu thu muc goc GymMaster-backend:
    uv run python .claude/skills/swp391-docs/scripts/gen_phancong.py
"""
import csv
import collections
import io
import sys

rows = list(csv.DictReader(open('out/inventory.csv', encoding='utf-8-sig')))
screens = {r['name']: r for r in rows if r['kind'] == 'Screen'}
apis = [r for r in rows if r['kind'] == 'API']

api_by_feat = collections.defaultdict(list)
for r in apis:
    api_by_feat[r['feature']].append(r)

NHOM = [
    dict(
        id='N1', nguoi='Như', git='BanhMiChao',
        ten='Xác thực, Tài khoản cá nhân & Quản trị tài khoản',
        be=['Auth', 'Account', 'Users'],
        specs=['001-auth-rbac', '002-member-management'],
        code_be=['Features/Auth/', 'Features/Account/', 'Features/Users/'],
        code_fe=['src/features/auth/', 'src/features/account/',
                 'src/features/member-profile/', 'src/app/(auth)/',
                 'src/features/member-management/ (chi 2 man /admin/users, /admin/staff)'],
        screens=['/login', '/signup', '/forgot-password', '/reset-password',
                 '/change-password',
                 '/admin/profile', '/staff/profile', '/pt/profile',
                 '/member/profile', '/member/profile/edit',
                 '/admin/users', '/admin/staff'],
        nonui=[],
        ghichu='Gom `Auth` + `Users` ve mot moi: dang nhap/dang ky va admin tao-khoa-reset '
               'tai khoan cung la mien **danh tinh & truy cap**, ai sua phan quyen chi dung '
               'mot cho. 5 man profile dung chung API `/users/me/*`.\n>\n'
               '> ⚠️ `/admin/staff` dung chung file `ManagementWorkspace.tsx` voi '
               '`/admin/members` va `/admin/trainers` cua **N2** — bao Quang Anh truoc khi sua. '
               'Rieng `/admin/users` co component rieng (`AdminUsersTemplateWorkspace.tsx`), '
               'sua thoai mai.',
    ),
    dict(
        id='N2', nguoi='Quang Anh', git='anhdaijka',
        ten='Hồ sơ Hội viên & PT',
        be=['Members', 'Trainers'],
        specs=['002-member-management', '006-progress-tracking'],
        code_be=['Features/Members/', 'Features/Trainers/'],
        code_fe=['src/features/member-management/', 'src/features/member-360/'],
        screens=['/admin/members', '/admin/members/[id]', '/admin/trainers',
                 '/staff/members', '/staff/members/[id]',
                 '/pt/members', '/pt/members/[id]'],
        nonui=[],
        ghichu='**Chu so huu `Features/Members/`** — bi 7 feature FE khac goi vao, '
               'nhieu nhat he thong. Ai can them/sua endpoint cua Members phai qua N2.',
    ),
    dict(
        id='N3', nguoi='Lộc', git='Loc-LX',
        ten='Gói tập, Membership & Thanh toán',
        be=['Billing'],
        specs=['003-membership-billing', '010-online-payment-vnpay'],
        code_be=['Features/Billing/'],
        code_fe=['src/features/billing/', 'src/features/staff-front-desk/'],
        screens=['/admin/packages', '/admin/memberships', '/admin/payments',
                 '/staff/sell-package', '/staff/renew-package', '/staff/payments',
                 '/member/membership', '/member/membership/vnpay-return'],
        nonui=['VNPay IPN callback (`/payments/vnpay/ipn`)',
               'Auto-cancel membership sau 30 phut (`MembershipLifecycle`)',
               'Lazy-expire membership khi truy van (`MembershipLifecycle`)'],
        ghichu='Nhan them **3 function khong co man hinh** — van tinh diem, dung quen '
               'ghi vao Project Tracking.',
    ),
    dict(
        id='N4', nguoi='Đam', git='vandam2005',
        ten='Tập luyện, Tiến độ & Check-in',
        be=['Training', 'CheckIns'],
        specs=['004-checkin', '005-pt-training', '006-progress-tracking'],
        code_be=['Features/Training/', 'Features/CheckIns/'],
        code_fe=['src/features/pt-training/', 'src/features/pt-assignment/',
                 'src/features/pt-dashboard/', 'src/features/member-progress-tracking/'],
        screens=['/admin/assignments', '/pt/check-in', '/staff/check-in',
                 '/pt/members/[id]/workout', '/pt/members/[id]/notes',
                 '/pt/members/[id]/progress',
                 '/member/workout', '/member/notes', '/member/progress'],
        nonui=[],
        ghichu='Man `/pt/members/[id]/*` va `/member/notes|workout` goi endpoint nam trong '
               '`Features/Members/` (chu so huu N2) — thong nhat truoc khi sua.',
    ),
    dict(
        id='N5', nguoi='Minh', git='Minhdicodedao',
        ten='Dinh dưỡng, Dashboard & Trang giới thiệu',
        be=['Nutrition', 'Dashboard'],
        specs=['007-nutrition-calorie', '008-dashboard-audit',
               '009-image-food-recognition'],
        code_be=['Features/Nutrition/', 'Features/Dashboard/'],
        code_fe=['src/features/member-nutrition/', 'src/features/admin-dashboard/'],
        screens=['/member/nutrition/meal-journal', '/member/nutrition/summary',
                 '/admin/dashboard', '/admin/audit-logs',
                 '/staff/dashboard', '/pt/dashboard', '/member/dashboard',
                 '/ (landing)', '/about', '/welcome'],
        nonui=[],
        ghichu='2 endpoint `food-items/barcode/{id}` va `food-items/online-search` moi co '
               'mock MSW, **BE chua implement** — lam not thi tinh them function.\n>\n'
               '> 3 trang tinh (`/`, `/about`, `/welcome`) khong goi API, khong dinh code ai '
               '— de o day vi day la nhom nhe nhat sau khi chuyen `Users` sang N1.',
    ),
]

# ---- KIEM TRA: khong sot, khong trung ----
assigned_s = [s for n in NHOM for s in n['screens']]
dup = [x for x, c in collections.Counter(assigned_s).items() if c > 1]
missing = sorted(set(screens) - set(assigned_s))
ghost = sorted(set(assigned_s) - set(screens))
assigned_f = [f for n in NHOM for f in n['be']]
all_f = sorted(api_by_feat)
err = []
if dup:
    err.append('Man bi chia 2 lan: %s' % dup)
if missing:
    err.append('Man chua ai nhan: %s' % missing)
if ghost:
    err.append('Man khong ton tai trong inventory: %s' % ghost)
if sorted(assigned_f) != all_f:
    err.append('Feature BE lech: thua/thieu %s'
               % (set(assigned_f) ^ set(all_f)))
if err:
    print('LOI — khong sinh file:')
    for e in err:
        print('  -', e)
    sys.exit(1)


def n_api(n):
    return sum(len(api_by_feat[f]) for f in n['be'])


o = io.StringIO()
w = o.write

w('# Phân công Feature — GymMaster (5 người)\n\n')
w('> Sinh tu dong tu `out/inventory.csv`.\n')
w('> Chay lai: `uv run python .claude/skills/swp391-docs/scripts/gen_phancong.py`\n')
w('> **Khong sua tay** — sua xong lan regenerate sau se mat. Sua trong script.\n\n')
w('Toan du an: **%d man hinh** · **%d endpoint** · **%d function tinh diem** '
  '(%d man + 3 non-UI).\n\n' % (len(screens), len(apis), len(screens) + 3, len(screens)))
w('Nguyen tac: **lat cat doc (vertical slice)** — moi nguoi nhan tron mot mien nghiep vu\n')
w('gom *ca frontend lan backend*, de khong ai phai cho nguoi khac lam xong API moi rap duoc UI.\n\n')

# ---- Bang tong ----
w('## Tổng quan\n\n')
w('| Nhóm | Người | Git | Miền nghiệp vụ | Màn FE | API BE | Tổng |\n')
w('|---|---|---|---|---:|---:|---:|\n')
for n in NHOM:
    tong = len(n['screens']) + n_api(n) + len(n['nonui'])
    w('| **%s** | **%s** | `%s` | %s | %d | %d | %d |\n'
      % (n['id'], n['nguoi'], n['git'], n['ten'], len(n['screens']), n_api(n), tong))
w('| | | | **Tổng** | **%d** | **%d** | **%d** |\n\n'
  % (sum(len(n['screens']) for n in NHOM),
     sum(n_api(n) for n in NHOM),
     sum(len(n['screens']) + n_api(n) + len(n['nonui']) for n in NHOM)))

# ---- Chi tiet tung nhom ----
for n in NHOM:
    w('---\n\n## %s — %s (`%s`)\n\n' % (n['id'], n['nguoi'], n['git']))
    w('### %s\n\n' % n['ten'])
    w('**Khối lượng:** %d màn FE + %d endpoint BE' % (len(n['screens']), n_api(n)))
    if n['nonui']:
        w(' + %d function non-UI' % len(n['nonui']))
    w('\n\n')
    w('**Spec phải đọc:** %s\n\n'
      % ' · '.join('`docs/03-Interface-Specs/feature-specs/%s/spec.md`' % s for s in n['specs']))
    w('**Code backend:** %s\n\n'
      % ' · '.join('`backend/GymMaster.API/%s`' % c for c in n['code_be']))
    w('**Code frontend:** %s\n\n' % ' · '.join('`%s`' % c for c in n['code_fe']))
    if n['ghichu']:
        w('> %s\n\n' % n['ghichu'])

    w('#### Màn hình frontend (%d)\n\n' % len(n['screens']))
    w('| Route | Actor |\n|---|---|\n')
    for s in n['screens']:
        w('| `%s` | %s |\n' % (s, screens[s]['actor']))
    w('\n')

    w('#### Endpoint backend (%d)\n\n' % n_api(n))
    w('| Feature | Method + Route | Quyền |\n|---|---|---|\n')
    for f in n['be']:
        for r in sorted(api_by_feat[f], key=lambda x: x['name']):
            w('| %s | `%s` | %s |\n' % (f, r['name'], r['actor']))
    w('\n')

    if n['nonui']:
        w('#### Function không có màn hình (%d)\n\n' % len(n['nonui']))
        for x in n['nonui']:
            w('- %s\n' % x)
        w('\n')

    w('#### Tài liệu phải nộp\n\n')
    w('- [ ] **RDS mục II** — mỗi use case 1 bảng 15 dòng + bảng Business Rules\n')
    w('- [ ] **RDS mục III** — bảng field UI + Database Access + câu LINQ thật\n')
    w('- [ ] **SDS mục II** — class diagram · đặc tả method · sequence diagram · DB queries\n')
    w('- [ ] **Project Tracking** — %d dòng function, cột In Charge ghi `%s`\n'
      % (len(n['screens']) + len(n['nonui']), n['nguoi']))
    w('- [ ] **Issues Report** — cột `Functions/Screens` khớp y hệt tên ở Project Tracking\n')
    w('- [ ] **AI Usage Report** — ghi theo tuần, cột *Validation* và *Risks* là chỗ ăn điểm\n\n')

# ---- Luat chung ----
w('---\n\n## Luật chung — đọc trước khi code\n\n')
w('### 1. Phần dùng chung, không thuộc riêng ai\n\n')
w('Sửa mấy chỗ này ảnh hưởng cả 5 người → **báo nhóm trước khi đụng**:\n\n')
w('| Thành phần | Đường dẫn |\n|---|---|\n')
for t, p in [
    ('Kiểu trả về của mọi service', '`backend/GymMaster.API/Common/ServiceResult.cs`'),
    ('Vỏ response API', '`backend/GymMaster.API/Common/ApiResponse.cs`'),
    ('Phân trang', '`backend/GymMaster.API/Common/PagedResult.cs`'),
    ('Validate thông tin cá nhân', '`backend/GymMaster.API/Common/PersonValidation.cs`'),
    ('Entity + DbContext', '`backend/GymMaster.API/Entities/` · `Data/GymMasterDbContext.cs`'),
    ('DI, middleware, CORS, JWT', '`backend/GymMaster.API/Program.cs`'),
    ('HTTP client', '`GymMaster-frontend/src/lib/api/http-client.ts`'),
    ('Session / auth store', '`GymMaster-frontend/src/features/auth/session/auth-session.ts`'),
    ('Component dùng chung', '`GymMaster-frontend/src/components/ui/` · `components/layout/`'),
    ('Mock MSW', '`GymMaster-frontend/src/mocks/handlers/`'),
]:
    w('| %s | %s |\n' % (t, p))

w('\n### 2. `Features/Members/` do N2 (Quang Anh) làm chủ\n\n')
w('Nó bị **7 feature frontend** khác gọi vào — nhiều nhất hệ thống:\n')
w('`billing` · `member-360` · `member-management` · `member-nutrition` · `member-profile` ·\n')
w('`member-progress-tracking` · `pt-training` · `staff-front-desk`.\n\n')
w('Ai cần thêm hoặc đổi endpoint của `Members` thì báo N2, đừng tự sửa — đổi một chỗ\n')
w('là 4 người khác vỡ.\n\n')

w('### 3. `ManagementWorkspace.tsx` bị 2 người dùng chung\n\n')
w('| Màn | Component | Chủ |\n|---|---|---|\n')
w('| `/admin/users` | `AdminUsersTemplateWorkspace.tsx` | N1 — riêng, sửa thoải mái |\n')
w('| `/admin/staff` | `ManagementWorkspace.tsx` | **N1** |\n')
w('| `/admin/members` | `ManagementWorkspace.tsx` | **N2** |\n')
w('| `/admin/trainers` | `ManagementWorkspace.tsx` | **N2** |\n\n')
w('Ba màn cuối chung một file. N1 và N2 báo nhau trước khi sửa `ManagementWorkspace.tsx`.\n\n')

w('### 4. Đặt tên function phải khớp nhau\n\n')
w('Tên function ghi trong **Project Tracking**, **Issues Report** và **RDS/SDS** phải\n')
w('giống hệt nhau. Lệch tên là thầy không dò được đóng góp cá nhân.\n\n')

w('### 5. Điểm cá nhân\n\n')
w('Individual Results = `LOC × Quality` theo function — 60 (đơn giản) / 120 (trung bình) /\n')
w('240 (phức tạp), Quality 100% / 75% / 50%. Cần **≥720 cả dự án** để đạt tối đa.\n\n')
w('Ước lượng độ nặng sau khi chia (**không phải điểm thật** — mức LOC và Quality do thầy\n')
w('duyệt, bảng này chỉ để so tương đối giữa 5 người):\n\n')
w('| Hạng | Nhóm | Người | Ước lượng | Nặng nhất ở đâu |\n|---:|---|---|---:|---|\n')
for hang, (nh, ng, diem, vi) in enumerate([
    ('N3', 'Lộc', '~1740', '2 wizard bán/gia hạn gói + VNPay + 3 function ngầm'),
    ('N2', 'Quang Anh', '~1440', '5 màn CRUD/chi tiết hội viên & PT, 20 endpoint'),
    ('N4', 'Đam', '~1380', 'Workout plan builder + check-in terminal + assign PT'),
    ('N1', 'Như', '~1320', '`/admin/users` CRUD + 5 form auth'),
    ('N5', 'Minh', '~1260', 'Meal journal + admin dashboard'),
], start=1):
    w('| %d | %s | %s | %s | %s |\n' % (hang, nh, ng, diem, vi))
w('\n**Cả 5 người đều vượt ngưỡng ≥720 rất xa** — thấp nhất ~1260, gần gấp đôi. Nên chênh\n')
w('lệch giữa các nhóm ảnh hưởng cảm giác công bằng nhiều hơn là ảnh hưởng điểm thật.\n')
w('Lộc đang nặng nhất; muốn cân thêm thì gỡ bớt của Lộc (vd `/admin/payments`,\n')
w('`/staff/payments`) chứ không phải của người nhẹ nhất.\n\n')
w('**Git history là bằng chứng.** Ai nhận nhóm nào thì commit thật vào nhóm đó —\n')
w('phân công trên giấy mà git không ghi nhận thì không tính được.\n')

open('docs/06-Management/phan-cong.md', 'w', encoding='utf-8').write(o.getvalue())
print('OK -> docs/06-Management/phan-cong.md')
print('   %d man / %d endpoint / %d nhom — khong sot, khong trung.'
      % (len(screens), len(apis), len(NHOM)))
for n in NHOM:
    print('   %s %-10s %-40s %2d man + %2d api%s'
          % (n['id'], n['nguoi'], n['ten'], len(n['screens']), n_api(n),
             ' + %d non-UI' % len(n['nonui']) if n['nonui'] else ''))
