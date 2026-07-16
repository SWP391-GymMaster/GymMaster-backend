"""Liet ke TOAN BO endpoint (BE) + screen (FE) that -> nguon su that cho
sheet Product cua Template1_Project Tracking.

Khong doan: endpoint doc tu [Http*] + [Route] + [Authorize] trong Features/,
screen doc tu cay thu muc src/app cua Next.js.

Dung: python inventory.py [--fe <duong-dan-repo-frontend>] [--csv out.csv]
"""
import argparse
import csv
import re
import sys
from pathlib import Path

API = Path('backend/GymMaster.API/Features')

METHOD_RE = re.compile(r'^\s*public\s+(?:async\s+)?[\w<>,\s\[\]?]+?\s(\w+)\s*\(', re.M)
HTTP_ATTR_RE = re.compile(r'\[Http(Get|Post|Put|Delete|Patch)(?:\("([^"]*)"\))?\]')
ROUTE_RE = re.compile(r'\[Route\("([^"]+)"\)\]')
AUTH_ROLES_RE = re.compile(r'\[Authorize\(Roles\s*=\s*([^\]]+)\)\]')
AUTH_BARE_RE = re.compile(r'\[Authorize\s*\]')
ANON_RE = re.compile(r'\[AllowAnonymous\]')
ROLE_RE = re.compile(r'RoleNames\.(\w+)')
ATTR_LINE_RE = re.compile(r'^\s*(\[.*\]|//.*)?\s*$')


def attrs_above(src, pos):
    """Khoi attribute dung ngay tren mot khai bao method.

    Phai quet nguoc tung dong: [AllowAnonymous] co the nam TREN [HttpPost],
    va giua chung con co the co comment.
    """
    # Phai lui ve dau dong: neu pos nam giua dong ("public sealed |class X"),
    # phan con lai cua dong do khong phai attribute -> vong lap dung ngay va
    # mat sach [Authorize] muc class.
    pos = src.rfind('\n', 0, pos) + 1
    lines = src[:pos].splitlines()
    out = []
    for ln in reversed(lines):
        s = ln.strip()
        if s.startswith('['):
            out.append(s)
        elif s.startswith('//') or s == '':
            continue
        else:
            break
    return '\n'.join(reversed(out))


def norm_roles(expr):
    fix = {'Pt': 'PT'}
    roles = ROLE_RE.findall(expr)
    return '/'.join(fix.get(r, r) for r in roles) if roles else 'Authenticated'


def roles_from(attr_block, class_attrs):
    """Theo dung semantics ASP.NET, KHONG mac dinh 'Authenticated'.

    Khong co [Authorize] o ca action lan class => endpoint AN DANH.
    Mac dinh sai o day tung khien POST /auth/login bi ghi la 'Authenticated'.
    """
    if ANON_RE.search(attr_block):
        return 'Anonymous'
    m = AUTH_ROLES_RE.search(attr_block)
    if m:
        return norm_roles(m.group(1))
    if AUTH_BARE_RE.search(attr_block):
        return 'Authenticated'
    if ANON_RE.search(class_attrs):
        return 'Anonymous'
    m = AUTH_ROLES_RE.search(class_attrs)
    if m:
        return norm_roles(m.group(1))
    if AUTH_BARE_RE.search(class_attrs):
        return 'Authenticated'
    return 'Anonymous'


def join_route(base, sub):
    """Sub-route bat dau bang '/' la route TUYET DOI -> ghi de [Route] cua class."""
    if sub.startswith('/'):
        return sub
    return '/' + base + (('/' + sub) if sub else '')


def scan_backend():
    rows = []
    for f in sorted(API.rglob('*Controller.cs')):
        if 'obj' in f.parts or 'bin' in f.parts:
            continue
        src = f.read_text(encoding='utf-8-sig')
        base = ROUTE_RE.search(src)
        base = base.group(1) if base else '?'
        feature = f.parent.name

        # attribute mức class = mặc định cho mọi action
        ci = src.find('class ' + f.stem)
        class_attrs = attrs_above(src, ci) if ci > 0 else ''

        for m in METHOD_RE.finditer(src):
            name = m.group(1)
            block = attrs_above(src, m.start())
            hm = HTTP_ATTR_RE.search(block)
            if not hm:
                continue                      # method thuong, khong phai endpoint
            verb, sub = hm.group(1), hm.group(2) or ''
            rows.append({
                'kind': 'API',
                'feature': feature,
                'name': '{} {}'.format(verb.upper(), join_route(base, sub)),
                'actor': roles_from(block, class_attrs),
                'desc': '{}.{}()'.format(f.stem, name),
                'source': str(f).replace('\\', '/'),
            })
    return rows


def scan_frontend(fe_root):
    app = Path(fe_root) / 'src' / 'app'
    if not app.is_dir():
        print('  (bo qua FE: khong thay {})'.format(app), file=sys.stderr)
        return []
    rows = []
    for page in sorted(app.rglob('page.tsx')):
        rel = page.relative_to(app).parent
        parts = [p for p in rel.parts]
        # (admin) la route group -> khong nam trong URL, nhung cho biet vai tro
        group = next((p[1:-1] for p in parts if p.startswith('(') and p.endswith(')')), '')
        url = '/' + '/'.join(p for p in parts if not (p.startswith('(') and p.endswith(')')))
        rows.append({
            'kind': 'Screen',
            'feature': group or 'root',
            'name': url if url != '/' else '/ (landing)',
            'actor': {'admin': 'Admin', 'staff': 'Staff', 'pt': 'PT',
                      'member': 'Member', 'auth': 'Anonymous'}.get(group, '-'),
            'desc': str(page.relative_to(fe_root)).replace('\\', '/'),
            'source': str(page).replace('\\', '/'),
        })
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--fe', default='../GymMaster-frontend')
    ap.add_argument('--csv')
    a = ap.parse_args()

    be = scan_backend()
    fe = scan_frontend(a.fe)
    rows = fe + be

    print('SCREEN (frontend): {}'.format(len(fe)))
    print('API endpoint (backend): {}'.format(len(be)))
    print('TONG: {}\n'.format(len(rows)))

    from collections import Counter
    c = Counter(r['feature'] for r in be)
    print('Endpoint theo feature:')
    for k, v in c.most_common():
        print('  {:<14} {}'.format(k, v))

    if a.csv:
        with open(a.csv, 'w', newline='', encoding='utf-8-sig') as fh:
            w = csv.DictWriter(fh, fieldnames=['kind', 'feature', 'name', 'actor',
                                               'desc', 'source'])
            w.writeheader()
            w.writerows(rows)
        print('\n-> {}'.format(a.csv))


if __name__ == '__main__':
    main()
