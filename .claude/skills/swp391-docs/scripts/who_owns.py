"""Suy 'In Charge' cho tung man hinh: ai commit nhieu nhat vao file cua man do.

Gop cac danh tinh git cua cung mot nguoi (nhieu may -> nhieu git config).

Dung: python who_owns.py <inventory.csv> [--fe <path>] [--csv out.csv]
"""
import argparse
import collections
import csv
import subprocess
import sys
from pathlib import Path

# Mot nguoi co the co nhieu email (dung nhieu may / commit tu web GitHub).
# Da xac nhan voi user 2026-07-17.
ALIAS = {
    'nhubui2905@gmail.com': 'BanhMiChao',
    '160343989+banhmichao1811@users.noreply.github.com': 'BanhMiChao',
    'anhdaijka@gmail.com': 'anhdaijka',
    '84665790+anhdaijka@users.noreply.github.com': 'anhdaijka',
    'locdoncare2004@gmail.com': 'Loc-LX',
    '118116720+minhdicodedao@users.noreply.github.com': 'Minhdicodedao',
    'minhbaoca@gmail.com': 'Minhdicodedao',
    'vandam281105@gmail.com': 'vandam2005',
}


def who(email):
    return ALIAS.get(email.lower(), email)


def authors_of(repo, *paths):
    """Dem commit theo tac gia cho mot hoac nhieu duong dan.

    KHONG dung --diff-filter=A hay --follow: cai dau bo sot file them qua merge
    commit, cai sau lan nham sang file khac (da gap luc lam fill_iterations).
    """
    raw = subprocess.run(['git', 'log', '--format=%ae', '--'] + list(paths),
                         cwd=repo, capture_output=True, text=True,
                         encoding='utf-8', errors='replace').stdout
    return collections.Counter(who(e.strip()) for e in raw.splitlines() if e.strip())


# Trong Next.js App Router, page.tsx chi la VO MONG import component that tu
# features/. Dem commit tren page.tsx => do nham: ai tao vo an het cong, con
# nguoi viet ruot (va nguoi viet backend) thi 0 man. Phai dem ca thu muc feature.
FE_FEATURE = [
    ('/admin/assignments', 'pt-assignment'), ('/admin/audit-logs', 'admin-dashboard'),
    ('/admin/dashboard', 'admin-dashboard'), ('/admin/notifications', 'notifications'),
    ('/admin/users', 'member-management'), ('/admin/staff', 'member-management'),
    ('/admin/members', 'member-management'), ('/admin/trainers', 'member-management'),
    ('/admin/packages', 'billing'), ('/admin/memberships', 'billing'),
    ('/admin/payments', 'billing'), ('/admin/profile', 'account'),
    ('/staff/sell-package', 'billing'), ('/staff/renew-package', 'billing'),
    ('/staff/payments', 'billing'), ('/staff/check-in', 'staff-front-desk'),
    ('/staff/members', 'staff-front-desk'), ('/staff/dashboard', 'staff-front-desk'),
    ('/staff/profile', 'account'),
    ('/pt/dashboard', 'pt-dashboard'), ('/pt/check-in', 'pt-training'),
    ('/pt/members', 'pt-training'), ('/pt/profile', 'account'),
    ('/member/membership', 'billing'), ('/member/workout', 'pt-training'),
    ('/member/notes', 'pt-training'), ('/member/progress', 'member-progress-tracking'),
    ('/member/nutrition', 'member-nutrition'), ('/member/profile', 'member-profile'),
    ('/member/dashboard', 'member-360'),
    ('/login', 'auth'), ('/signup', 'auth'), ('/forgot-password', 'auth'),
    ('/reset-password', 'auth'), ('/change-password', 'auth'),
    ('/members/[id]', 'member-360'),
]

BE_FEATURE = [
    ('assignment', 'Training'), ('workout', 'Training'), ('notes', 'Training'),
    ('progress', 'Training'), ('check-in', 'CheckIns'),
    ('package', 'Billing'), ('membership', 'Billing'), ('payment', 'Billing'),
    ('sell-', 'Billing'), ('renew-', 'Billing'),
    ('nutrition', 'Nutrition'), ('dashboard', 'Dashboard'), ('audit', 'Dashboard'),
    ('notification', 'Dashboard'), ('users', 'Users'), ('staff', 'Users'),
    ('trainers', 'Trainers'), ('members', 'Members'), ('profile', 'Account'),
    ('login', 'Auth'), ('signup', 'Auth'), ('password', 'Auth'),
]


def pick(route, table):
    hit = None
    for k, v in table:
        if k in route and (hit is None or len(k) > hit[0]):
            hit = (len(k), v)
    return hit[1] if hit else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('inventory')
    ap.add_argument('--fe', default=r'D:\GymMaster\GymMaster-frontend')
    ap.add_argument('--csv')
    a = ap.parse_args()

    with open(a.inventory, encoding='utf-8-sig') as fh:
        inv = [r for r in csv.DictReader(fh) if r['kind'] == 'Screen']

    be_root = Path(__file__).resolve().parents[3]
    out = []
    for r in inv:
        route = r['name']
        paths = [r['desc']]                       # page.tsx
        fef = pick(route, FE_FEATURE)
        if fef:
            paths.append('src/features/{}'.format(fef))
        c = authors_of(a.fe, *paths)

        bef = pick(route, BE_FEATURE)
        if bef:
            c += authors_of(str(be_root), 'backend/GymMaster.API/Features/{}'.format(bef))

        top = c.most_common(1)[0] if c else ('(khong ro)', 0)
        out.append({'screen': route, 'in_charge': top[0], 'commits': top[1],
                    'all': '; '.join('{}={}'.format(k, v) for k, v in c.most_common(4))})

    tot = collections.Counter(r['in_charge'] for r in out)
    print('So man hinh moi nguoi lam chinh (theo git):')
    for k, v in tot.most_common():
        print('  {:<16} {:>2} man'.format(k, v))
    print()
    print('{:<34} {:<14} {}'.format('MAN HINH', 'IN CHARGE', 'CHI TIET'))
    for r in out:
        print('{:<34} {:<14} {}'.format(r['screen'][:33], r['in_charge'][:13],
                                        r['all'][:44]))

    if a.csv:
        with open(a.csv, 'w', newline='', encoding='utf-8-sig') as fh:
            w = csv.DictWriter(fh, fieldnames=['screen', 'in_charge', 'commits', 'all'])
            w.writeheader()
            w.writerows(out)
        print('\n-> ' + a.csv)


if __name__ == '__main__':
    main()
