"""Sinh docs/06-Management/phan-cong-rds.md — ai viet muc nao trong RDS.

Nhom chia nhau tu viet RDS, nhung RDS danh so II.n / III.n theo thu tu co dinh
(sinh tu inventory.csv). Neu khong noi ro so muc, 5 nguoi se dam nhau: hai
nguoi cung sua mot muc, hoac co muc khong ai nhan.

Nguon: bang NHOM trong gen_phancong.py (= phan cong nhom da chot).
  III.n  <- thu tu man hinh trong out/inventory.csv, chu man lay tu `screens`
  II.n   <- thu tu use case trong srs-use-cases.md, chu UC lay tu UC_OWNER

Dung: uv run --with openpyxl python gen_phancong_rds.py [--out docs/06-Management/phan-cong-rds.md]
"""
import argparse
import csv
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from fill_in_charge import bang_phan_cong
from fill_tracking import NAMES

# UC -> mien nghiep vu backend. Suy tu chinh noi dung UC, roi doi chieu voi
# `be` cua tung nhom de ra nguoi. Khong doan tu git.
UC_FEATURE = {
    'UC-01': 'Auth', 'UC-02': 'Auth',
    'UC-03': 'Users', 'UC-03A': 'Users',
    'UC-04': 'Members', 'UC-05': 'Trainers', 'UC-14': 'Members',
    'UC-06': 'Billing', 'UC-07': 'Billing', 'UC-08': 'Billing',
    'UC-27': 'Billing', 'UC-28': 'Billing',
    'UC-09': 'CheckIns',
    'UC-10': 'Training', 'UC-11': 'Training', 'UC-12': 'Training',
    'UC-13': 'Training', 'UC-15': 'Training',
    'UC-16': 'Nutrition', 'UC-17': 'Nutrition', 'UC-18': 'Nutrition',
    'UC-19': 'Nutrition', 'UC-20': 'Nutrition', 'UC-21': 'Nutrition',
    'UC-24': 'Nutrition', 'UC-26': 'Nutrition',
    'UC-22': 'Dashboard', 'UC-23': 'Dashboard',
    'UC-29': 'Account',
}

UC_MD = Path('docs/01-SRS-Requirements/use-cases/srs-use-cases.md')
ROW = re.compile(r'^\|\s*(UC-[0-9A-Z]+)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|')


def doc_uc():
    """Danh sach UC theo dung thu tu RDS phan II (da loai UC ~~Removed~~)."""
    t = UC_MD.read_text(encoding='utf-8')
    blk = t[t.index('# 2. Use Case Overview'):t.index('# 3.')]
    out = []
    for ln in blk.splitlines():
        m = ROW.match(ln.strip())
        if m:
            uid, ten, _, prio = (g.strip() for g in m.groups())
            if 'removed' in prio.lower():
                continue
            out.append((uid, re.sub(r'\s*\(.*?\)\s*$', '', ten)))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default='docs/06-Management/phan-cong-rds.md')
    a = ap.parse_args()

    man_owner, be_owner, _ = bang_phan_cong()
    inv = [r for r in csv.DictReader(open('out/inventory.csv', encoding='utf-8-sig'))
           if r['kind'] == 'Screen']

    # III.n theo dung thu tu fill_rds_design duyet inventory
    phan_iii = {}
    for n, r in enumerate(inv, 1):
        o = man_owner.get(r['name'], '')
        phan_iii.setdefault(o, []).append(
            (n, NAMES.get(r['name'], r['name']), r['name']))

    # II.n theo dung thu tu fill_rds_usecases duyet bang UC
    phan_ii, thieu_uc = {}, []
    for n, (uid, ten) in enumerate(doc_uc(), 1):
        feat = UC_FEATURE.get(uid)
        o = be_owner.get(feat, '') if feat else ''
        if not o:
            thieu_uc.append(uid)
        phan_ii.setdefault(o, []).append((n, uid, ten))

    nguoi = sorted(set(list(phan_ii) + list(phan_iii)) - {''},
                   key=lambda k: -(len(phan_ii.get(k, [])) + len(phan_iii.get(k, []))))

    L = []
    w = L.append
    w('# Phân công viết RDS — GymMaster\n\n')
    w('> Sinh tự động từ `gen_phancong_rds.py`. **Không sửa tay** — sửa bảng `NHOM` '
      'trong `gen_phancong.py` rồi chạy lại.\n')
    w('> Số mục khớp đúng file `out/docs/GYM_RDS.docx` do skill sinh ra.\n\n')
    w('RDS đánh số cố định: **II.n** = bảng use case (phần II), **III.n** = thiết kế '
      'màn hình (phần III). Mỗi người chỉ sửa mục của mình để không đè lên nhau.\n\n')

    w('## Tổng quan\n\n')
    w('| Người | Git | Mục II (use case) | Mục III (màn hình) | Tổng |\n')
    w('|---|---|---:|---:|---:|\n')
    for k in nguoi:
        ii, iii = len(phan_ii.get(k, [])), len(phan_iii.get(k, []))
        w('| | `{}` | {} | {} | **{}** |\n'.format(k, ii, iii, ii + iii))
    w('| | | **{}** | **{}** | **{}** |\n\n'.format(
        sum(len(v) for k, v in phan_ii.items() if k),
        sum(len(v) for k, v in phan_iii.items() if k),
        sum(len(v) for k, v in phan_ii.items() if k) +
        sum(len(v) for k, v in phan_iii.items() if k)))

    w('---\n\n')
    for k in nguoi:
        w('## `{}`\n\n'.format(k))
        ii = phan_ii.get(k, [])
        if ii:
            w('### Phần II — bảng use case ({} mục)\n\n'.format(len(ii)))
            w('| Mục | UC | Tên |\n|---|---|---|\n')
            for n, uid, ten in ii:
                w('| **II.{}** | {} | {} |\n'.format(n, uid, ten))
            w('\n')
        iii = phan_iii.get(k, [])
        if iii:
            w('### Phần III — thiết kế màn hình ({} mục)\n\n'.format(len(iii)))
            w('| Mục | Màn hình | Route |\n|---|---|---|\n')
            for n, ten, route in iii:
                w('| **III.{}** | {} | `{}` |\n'.format(n, ten, route))
            w('\n')
        w('---\n\n')

    w('## Lưu ý khi sửa\n\n')
    w('- File `out/docs/GYM_RDS.docx` **được sinh tự động** — sửa tay vào đó thì lần '
      'chạy lại skill sau sẽ mất. Nội dung gốc nằm ở `docs/01-SRS-Requirements/use-cases/srs-use-cases.md` '
      '(phần II) và code frontend (phần III).\n')
    w('- Chỗ còn `[CAN BO SUNG]`: UC-24 Barcode (chưa implement) và 3 trang tĩnh '
      'About/Welcome/Landing (không gọi API nên không có SQL).\n')
    w('- Business Rules đang liệt kê **đủ** FR của spec kèm nhãn `[CAN CAT BOT]` — '
      'mỗi người tự cắt FR không thuộc use case của mình.\n')
    if thieu_uc:
        w('- ⚠️ **UC chưa có chủ:** {} — bổ sung vào `UC_FEATURE` trong '
          '`gen_phancong_rds.py`.\n'.format(', '.join(thieu_uc)))

    Path(a.out).write_text(''.join(L), encoding='utf-8')
    print('-> {}'.format(a.out))
    for k in nguoi:
        print('  {:<16} II:{:>2}  III:{:>2}'.format(
            k, len(phan_ii.get(k, [])), len(phan_iii.get(k, []))))
    if thieu_uc:
        print('  CANH BAO: UC chua co chu:', ', '.join(thieu_uc))


if __name__ == '__main__':
    main()
