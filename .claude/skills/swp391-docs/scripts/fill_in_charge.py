"""Dien cot In Charge cua Project Tracking theo CHU FEATURE (suy tu git).

Boi canh (user xac nhan 2026-07-17): ban dau chia BE cho 4 nguoi, anhdaijka lam
FE full; sau do spec khong on nen moi nguoi tu om mot feature lam xuyen suot.
=> 'In Charge' phai theo NGUOI CHU FEATURE, khong theo nguoi tao man hinh
   (anhdaijka tao 41/47 man trong tuan dau -> do kieu do thi 4 nguoi kia trang).

Phep do: trong moi thu muc feature, ai nhieu commit nhat = chu feature.
Loai commit tu 2026-07-12 (refactor cua Claude, thoi phong so lieu BanhMiChao).

DAY LA UOC LUONG, khong phai su that. Nhom phai ra soat lai.

Dung: python fill_in_charge.py <tracking.xlsx> <inventory.csv> [--add-backend]
"""
import argparse
import collections
import csv
import subprocess
import sys
from pathlib import Path

try:
    from openpyxl import load_workbook
except ImportError:
    sys.exit('Thieu openpyxl. Chay: uv run --with openpyxl python ' + __file__)

sys.path.insert(0, str(Path(__file__).parent))
from who_owns import ALIAS, who, FE_FEATURE, BE_FEATURE, pick
from fill_tracking import NAMES

FE = r'D:\GymMaster\GymMaster-frontend'
BE = r'D:\GymMaster\GymMaster-backend'
CUT = '2026-07-12'

BE_DESC = {
    'Auth': ('Authentication', 'API dang nhap, dang ky, quen mat khau (OTP), doi mat khau, '
                               'Google login, refresh/revoke token'),
    'Users': ('User Management', 'API quan ly tai khoan: tao, khoa/mo, phan quyen, reset mat khau'),
    'Members': ('Member Management', 'API ho so hoi vien: tao, tim kiem, cap nhat, Member 360'),
    'Trainers': ('Trainer Management', 'API ho so PT: tao, danh sach, cap nhat'),
    'Dashboard': ('Dashboard & Audit', 'API doanh thu, thong ke van hanh, audit log, notification'),
    'Billing': ('Membership & Billing', 'API goi tap, ban/gia han, thanh toan, VNPay (tao URL, IPN, return)'),
    'Nutrition': ('Nutrition', 'API bua an, muc tieu calo, danh muc mon, quet anh AI (Gemini)'),
    'Training': ('PT Training', 'API phan cong PT, giao an, ghi chu, tien do'),
    'CheckIns': ('Check-in', 'API check-in cua member/le tan/PT, gioi han 2 luot/ngay'),
    'Account': ('Account', 'API ho so ca nhan, doi avatar (Cloudinary)'),
}


def owner_of(repo, path):
    raw = subprocess.run(['git', 'log', '--format=%ae|%ad', '--date=short', '--', path],
                         cwd=repo, capture_output=True, text=True,
                         encoding='utf-8', errors='replace').stdout
    c = collections.Counter()
    for ln in raw.splitlines():
        if '|' not in ln:
            continue
        e, d = ln.split('|', 1)
        if d.strip() < CUT:
            c[who(e)] += 1
    return (c.most_common(1)[0][0] if c else ''), c


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('tracking')
    ap.add_argument('inventory')
    ap.add_argument('--add-backend', action='store_true')
    a = ap.parse_args()

    # chu feature FE
    fe_owner = {}
    for _, f in FE_FEATURE:
        if f not in fe_owner:
            fe_owner[f] = owner_of(FE, 'src/features/' + f)[0]

    # chu feature BE (--follow khong chay tren thu muc; nhung Features/<X> da co
    # commit refactor, con lich su goc nam o duong dan cu -> dung tung file)
    be_owner = {}
    for f in sorted(Path('backend/GymMaster.API/Features').iterdir()):
        if not f.is_dir():
            continue
        c = collections.Counter()
        for cs in f.glob('*.cs'):
            raw = subprocess.run(
                ['git', 'log', '--follow', '--format=%ae|%ad', '--date=short',
                 '--', str(cs).replace('\\', '/')],
                cwd=BE, capture_output=True, text=True,
                encoding='utf-8', errors='replace').stdout
            for ln in raw.splitlines():
                if '|' in ln:
                    e, d = ln.split('|', 1)
                    if d.strip() < CUT:
                        c[who(e)] += 1
        be_owner[f.name] = c.most_common(1)[0][0] if c else ''

    with open(a.inventory, encoding='utf-8-sig') as fh:
        inv = [r for r in csv.DictReader(fh) if r['kind'] == 'Screen']

    wb = load_workbook(a.tracking)
    sheet = next(s for s in wb.sheetnames if s.lower() in ('product', 'project'))
    ws = wb[sheet]
    hrow = next(r for r in range(1, 8)
                if any(str(c.value or '').lower().startswith('screen') for c in ws[r]))
    hdr = {str(c.value).strip(): i for i, c in enumerate(ws[hrow]) if c.value}

    def col(*names):
        for n in names:
            for k, i in hdr.items():
                if k.lower().startswith(n.lower()):
                    return i
        return None

    c_no, c_fn = col('#'), col('Screen/Function', 'Screen')
    c_ft, c_ac = col('Feature'), col('Actor')
    c_de, c_ic = col('Screen/Function Description', 'Description'), col('In Charge')
    c_st, c_acl = col('Status'), col('Actual')

    # ten man -> chu
    route_of = {NAMES.get(r['name'], r['name']): r['name'] for r in inv}
    stats = collections.Counter()
    last = hrow
    for row in ws.iter_rows(min_row=hrow + 1):
        nm = row[c_fn].value
        if not nm:
            continue
        last = row[0].row
        route = route_of.get(str(nm).strip())
        if not route:
            continue
        f = pick(route, FE_FEATURE)
        o = fe_owner.get(f) if f else ''
        if not o:
            bf = pick(route, BE_FEATURE)
            o = be_owner.get(bf, '') if bf else ''
        if o and c_ic is not None:
            row[c_ic].value = o
            stats[o] += 1

    if a.add_backend:
        n = last - hrow
        for name, owner in ((k, be_owner.get(k, '')) for k in sorted(BE_DESC)):
            n += 1
            last += 1
            feat, desc = BE_DESC[name]

            def put(ci, v):
                if ci is not None:
                    ws.cell(row=last, column=ci + 1, value=v)
            put(c_no, n)
            put(c_fn, '{} API'.format(name))
            put(c_ft, feat)
            put(c_ac, 'System')
            put(c_de, desc)
            put(c_ic, owner)
            put(c_st, 'Done')
            put(c_acl, 'iter1')
            stats[owner] += 1

    wb.save(a.tracking)
    print('Da dien In Charge -> {}'.format(a.tracking))
    print('\nSo dong moi nguoi phu trach (UOC LUONG tu git):')
    for k, v in stats.most_common():
        print('  {:<16} {:>2} dong'.format(k or '(khong ro)', v))
    print('\nChu feature FE :', {k: v for k, v in fe_owner.items() if v})
    print('Chu feature BE :', be_owner)
    print('\nLUU Y: day la UOC LUONG tu so commit, KHONG phai su that.')
    print('Nhom phai ra soat lai truoc khi nop — cot nay quyet dinh diem ca nhan 60%.')


if __name__ == '__main__':
    main()
