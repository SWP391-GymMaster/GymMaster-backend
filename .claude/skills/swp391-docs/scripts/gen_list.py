# -*- coding: utf-8 -*-
"""Sinh docs/06-Management/danh-sach-feature.md tu out/inventory.csv.

Chi LIET KE, khong tu chia nhom — de nguoi doc tu cat.
Chay tu thu muc goc GymMaster-backend:
    uv run python <file nay>
"""
import csv
import collections
import io
import pathlib
import re

FE_ROOT = pathlib.Path('../GymMaster-frontend/src/features')
# KHONG loai '$' khoi lop ky tu: duong dan hay viet dang template literal
# `/api/v1/members/${id}/notes` — loai '$' la cat cut thanh '/api/v1/members/'
# roi khop nham. Giu nguyen rồi thay ${...} -> {id} o buoc sau.
EP_PAT = re.compile(r'["`](/api/v1/[^"`?]*)')

SPEC_OF = {
    'Auth':      ['001-auth-rbac'],
    'Account':   ['001-auth-rbac'],
    'Users':     ['002-member-management'],
    'Members':   ['002-member-management', '006-progress-tracking'],
    'Trainers':  ['002-member-management'],
    'Billing':   ['003-membership-billing', '010-online-payment-vnpay'],
    'CheckIns':  ['004-checkin'],
    'Training':  ['005-pt-training', '006-progress-tracking'],
    'Nutrition': ['007-nutrition-calorie', '009-image-food-recognition'],
    'Dashboard': ['008-dashboard-audit'],
}

rows = list(csv.DictReader(open('out/inventory.csv', encoding='utf-8-sig')))
screens = [r for r in rows if r['kind'] == 'Screen']
apis = [r for r in rows if r['kind'] == 'API']

api_by_feat = collections.defaultdict(list)
for r in apis:
    api_by_feat[r['feature']].append(r)

scr_by_grp = collections.defaultdict(list)
for r in screens:
    scr_by_grp[r['feature']].append(r)

GRP_LABEL = {
    'admin': 'Admin (quản trị)',
    'staff': 'Staff (lễ tân)',
    'pt': 'PT (huấn luyện viên)',
    'member': 'Member (hội viên)',
    'auth': 'Auth (đăng nhập / đăng ký)',
    'root': 'Trang tĩnh',
}

o = io.StringIO()
w = o.write

w('# Danh sách Feature — GymMaster\n\n')
w('> Sinh tu dong tu `out/inventory.csv`. Chay lai: `uv run python scripts/gen_list.py`\n')
w('> **Khong sua tay** — sua xong lan sau regenerate se mat.\n\n')
w('Toan du an: **%d man hinh frontend** · **%d endpoint backend** · '
  '**%d feature backend** · **%d nhom route frontend**.\n\n'
  % (len(screens), len(apis), len(api_by_feat), len(scr_by_grp)))

# ---------- Tom tat ----------
w('## Tom tat so luong\n\n')
w('### Backend — theo feature\n\n')
w('| Feature | Endpoint | Spec | Thu muc code |\n|---|---:|---|---|\n')
for f in sorted(api_by_feat, key=lambda x: -len(api_by_feat[x])):
    specs = ' · '.join('`docs/03-Interface-Specs/feature-specs/%s/`' % s for s in SPEC_OF.get(f, []))
    w('| **%s** | %d | %s | `Features/%s/` |\n'
      % (f, len(api_by_feat[f]), specs or '—', f))
w('| | **%d** | | |\n\n' % len(apis))

w('### Frontend — theo nhom route\n\n')
w('| Nhom route | So man | Actor chinh |\n|---|---:|---|\n')
for g in sorted(scr_by_grp, key=lambda x: -len(scr_by_grp[x])):
    actors = sorted(set(r['actor'] for r in scr_by_grp[g]))
    w('| **%s** | %d | %s |\n' % (GRP_LABEL.get(g, g), len(scr_by_grp[g]), ', '.join(actors)))
w('| | **%d** | |\n\n' % len(screens))

# ---------- Chi tiet BE ----------
w('---\n\n## Endpoint backend (%d)\n\n' % len(apis))
for f in sorted(api_by_feat):
    w('### %s — %d endpoint\n\n' % (f, len(api_by_feat[f])))
    specs = SPEC_OF.get(f, [])
    if specs:
        w('Spec: %s\n\n' % ' · '.join('`docs/03-Interface-Specs/feature-specs/%s/spec.md`' % s for s in specs))
    w('| Method + Route | Quyen |\n|---|---|\n')
    for r in sorted(api_by_feat[f], key=lambda x: x['name']):
        w('| `%s` | %s |\n' % (r['name'], r['actor']))
    w('\n')

# ---------- Chi tiet FE ----------
w('---\n\n## Man hinh frontend (%d)\n\n' % len(screens))
for g in sorted(scr_by_grp):
    w('### %s — %d man\n\n' % (GRP_LABEL.get(g, g), len(scr_by_grp[g])))
    w('| Route | Actor | File nguon |\n|---|---|---|\n')
    for r in sorted(scr_by_grp[g], key=lambda x: x['name']):
        w('| `%s` | %s | `%s` |\n' % (r['name'], r['actor'], r['desc']))
    w('\n')

# ---------- Anh xa FE feature <-> BE endpoint ----------
# Doc thang tu src/features/<ten>/**/*.ts(x), khong phai tu route group,
# vi route group chia theo VAI TRO con src/features chia theo NGHIEP VU.
be_route = {}
for r in apis:
    path = r['name'].split(' ', 1)[1] if ' ' in r['name'] else r['name']
    be_route.setdefault(re.sub(r'\{[^}]*\}', '{id}', path).rstrip('/'), r['feature'])

fe_map = {}
if FE_ROOT.exists():
    for feat in sorted(p for p in FE_ROOT.iterdir() if p.is_dir()):
        eps = set()
        for pat in ('*.ts', '*.tsx'):
            for f in feat.rglob(pat):
                if 'test' in f.name:
                    continue
                txt = f.read_text(encoding='utf-8', errors='ignore')
                for m in EP_PAT.finditer(txt):
                    # '\$?' la bat buoc: template literal la '${id}', neu chi thay
                    # '{id}' thi con lai dau '$' -> '/members/${id}/notes' khong bao
                    # gio khop route BE '/members/{id}/notes'.
                    p = re.sub(r'\$?\{[^}]*\}', '{id}', m.group(1))
                    # '${qs ? ... : ""}' co dau '?' nen regex cat giua chung, con
                    # lai duoi '$' cut -> bo tu dau '$' tro di.
                    p = p.split('$')[0].rstrip('/')
                    if p:
                        eps.add(p)
        fe_map[feat.name] = sorted(eps)


def resolve(path):
    """Doi 1 duong dan FE ve feature BE. Tra None neu BE khong co route do."""
    if path in be_route:
        return be_route[path]
    # '/payments/vnpay/return${search}' -> '...return{id}': '{id}' o day la query
    # string chu khong phai path param, bo di roi thu lai.
    if path.endswith('{id}'):
        alt = path[:-4].rstrip('/')
        if alt in be_route:
            return be_route[alt]
    return None

if fe_map:
    w('---\n\n## Anh xa: feature frontend goi feature backend nao\n\n')
    w('`src/features/` (chia theo **nghiep vu + vai tro**) khong trung ten voi\n')
    w('`Features/` cua backend (chia theo **nghiep vu**). Bang duoi doc thang tu code:\n')
    w('gom moi chuoi `/api/v1/...` trong tung thu muc feature FE roi doi chieu voi\n')
    w('danh sach endpoint that cua BE.\n\n')
    w('| Feature frontend | Endpoint goi | Cham vao feature backend |\n|---|---:|---|\n')
    orphans = []
    for fe, eps in fe_map.items():
        hits = collections.Counter()
        miss = []
        for e in eps:
            f = resolve(e)
            if f:
                hits[f] += 1
            else:
                miss.append(e)
        parts = ['%s (%d)' % (k, v) for k, v in hits.most_common()]
        if miss:
            parts.append('**%d khong khop**' % len(miss))
            orphans += [(fe, e) for e in miss]
        w('| `%s` | %d | %s |\n' % (fe, len(eps), ', '.join(parts) or '—'))
    w('\n')
    if orphans:
        w('**Chi tiet %d duong dan khong khop endpoint BE nao:**\n\n' % len(orphans))
        w('| Feature FE | Duong dan | Ly do |\n|---|---|---|\n')
        for fe, e in orphans:
            if e.count('/') <= 3:
                why = 'Hang base path, khong phai endpoint that — bo qua'
            else:
                why = '**BE chua implement** (chi co mock MSW)'
            w('| `%s` | `%s` | %s |\n' % (fe, e, why))
        w('\n')
    # Chieu nguoc: BE feature bi bao nhieu FE feature dung chung
    rev = collections.defaultdict(set)
    for fe, eps in fe_map.items():
        for e in eps:
            f = resolve(e)
            if f:
                rev[f].add(fe)
    w('**Chieu nguoc — feature backend nao bi nhieu ben dung chung** (cang nhieu\n')
    w('cang de dung tay nhau khi chia viec):\n\n')
    w('| Feature backend | So feature FE dung | Ai dung |\n|---|---:|---|\n')
    for f in sorted(rev, key=lambda x: -len(rev[x])):
        w('| **%s** | %d | %s |\n'
          % (f, len(rev[f]), ', '.join('`%s`' % x for x in sorted(rev[f]))))
    w('\n')

# ---------- Dung chung ----------
w('---\n\n## Phan dung chung — khong thuoc rieng feature nao\n\n')
w('Sua may cho nay anh huong toan bo he thong, nen bao nhom truoc khi dung:\n\n')
w('| Thanh phan | Duong dan |\n|---|---|\n')
for t, p in [
    ('Kieu tra ve cua moi service', '`backend/GymMaster.API/Common/ServiceResult.cs`'),
    ('Vo response API', '`backend/GymMaster.API/Common/ApiResponse.cs`'),
    ('Phan trang', '`backend/GymMaster.API/Common/PagedResult.cs`'),
    ('Validate thong tin ca nhan', '`backend/GymMaster.API/Common/PersonValidation.cs`'),
    ('Entity + DbContext', '`backend/GymMaster.API/Entities/` · `Data/GymMasterDbContext.cs`'),
    ('DI, middleware, CORS, JWT', '`backend/GymMaster.API/Program.cs`'),
    ('HTTP client', '`../GymMaster-frontend/src/lib/api/http-client.ts`'),
    ('Session / auth store', '`../GymMaster-frontend/src/features/auth/session/auth-session.ts`'),
    ('Component dung chung', '`../GymMaster-frontend/src/components/ui/` · `components/layout/`'),
    ('Mock MSW', '`../GymMaster-frontend/src/mocks/handlers/`'),
]:
    w('| %s | %s |\n' % (t, p))

w('\n---\n\n## Ghi chu\n\n')
w('- Con **3 function khong co man hinh** (non-UI), dung quen khi chia: VNPay IPN,\n')
w('  auto-cancel membership sau 30 phut, lazy-expire membership (`MembershipLifecycle`).\n')
w('  Tong don vi tinh diem = %d man + 3 = %d function.\n' % (len(screens), len(screens) + 3))
w('- Diem ca nhan SWP391 = `LOC x Quality` theo function (60 don gian / 120 trung binh /\n')
w('  240 phuc tap), can **>=720 ca du an** de dat toi da.\n')
w('- Ten function khi ghi vao Project Tracking / Issues Report phai khop nhau y het.\n')

open('docs/06-Management/danh-sach-feature.md', 'w', encoding='utf-8').write(o.getvalue())
print('OK -> docs/06-Management/danh-sach-feature.md')
print('   %d man hinh, %d endpoint, %d feature BE, %d nhom route FE'
      % (len(screens), len(apis), len(api_by_feat), len(scr_by_grp)))
