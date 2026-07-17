"""Dien RDS phan III (Design Specifications) — XOA MAU cua thay roi thay bang
noi dung that cua GymMaster.

Nguon:
  Mockup UI      <- ../GymMaster-frontend/docs/design/screenshots/*.png (43 anh)
  Bang field     <- Zod schema trong ../GymMaster-frontend/src/features/*/schemas/
  Database Access<- _dbContext.<Bang> trong Features/<Feature>/*Service.cs

CANH BAO: template co san muc mau cua du an GAMS (User Login, Setting List,
Setting Details, anh co chu "Teacher"). Khong xoa la nop nham du an nguoi khac.

Dung: python fill_rds_design.py <rds.docx> <inventory.csv> [--fe <path>]
"""
import argparse
import csv
import re
import sys
from pathlib import Path

try:
    import docx
    from docx.oxml.ns import qn
    from docx.shared import Pt, Inches
except ImportError:
    sys.exit('Thieu python-docx. Chay: uv run --with python-docx python ' + __file__)

sys.path.insert(0, str(Path(__file__).parent))
from fill_tracking import NAMES

API = Path('backend/GymMaster.API/Features')

# ten anh <- route (khop cach dat ten trong visual-screenshots.spec.ts)
SHOT = {
    '/': 'landing', '/ (landing)': 'landing', '/about': 'about', '/login': 'login',
    '/signup': 'signup', '/forgot-password': 'forgot-password',
    '/reset-password': 'reset-password', '/welcome': 'welcome',
    '/admin/dashboard': 'admin-dashboard', '/admin/users': 'admin-users',
    '/admin/staff': 'admin-staff', '/admin/trainers': 'admin-trainers',
    '/admin/members': 'admin-members', '/admin/assignments': 'admin-assignments',
    '/admin/audit-logs': 'admin-audit-logs', '/admin/packages': 'admin-packages',
    '/admin/memberships': 'admin-memberships', '/admin/payments': 'admin-payments',
    '/admin/notifications': 'admin-notifications', '/admin/profile': 'admin-profile',
    '/staff/dashboard': 'staff-dashboard', '/staff/members': 'staff-members',
    '/staff/check-in': 'staff-check-in', '/staff/sell-package': 'staff-sell-package',
    '/staff/renew-package': 'staff-renew-package', '/staff/payments': 'staff-payments',
    '/staff/profile': 'staff-profile',
    '/pt/dashboard': 'pt-dashboard', '/pt/members': 'pt-members',
    '/pt/members/[id]': 'pt-member-360', '/pt/members/[id]/workout': 'pt-workout-planner',
    '/pt/members/[id]/notes': 'pt-trainer-notes',
    '/pt/members/[id]/progress': 'pt-member-progress',
    '/pt/check-in': 'pt-check-in', '/pt/profile': 'pt-profile',
    '/member/dashboard': 'member-dashboard', '/member/workout': 'member-workout',
    '/member/nutrition/meal-journal': 'member-meal-journal',
    '/member/nutrition/summary': 'member-nutrition-summary',
    '/member/progress': 'member-progress', '/member/membership': 'member-membership',
    '/member/notes': 'member-notes', '/member/profile': 'member-profile',
    '/member/profile/edit': 'member-profile-edit',
}

# route -> feature backend (de tra bang DB)
FEAT = {
    'login': 'Auth', 'signup': 'Auth', 'forgot-password': 'Auth',
    'reset-password': 'Auth', 'change-password': 'Auth',
    'users': 'Users', 'staff': 'Users', 'members': 'Members', 'trainers': 'Trainers',
    'packages': 'Billing', 'memberships': 'Billing', 'payments': 'Billing',
    'membership': 'Billing', 'sell-package': 'Billing', 'renew-package': 'Billing',
    'check-in': 'CheckIns', 'assignments': 'Training', 'workout': 'Training',
    'notes': 'Training', 'progress': 'Training',
    'nutrition': 'Nutrition', 'meal-journal': 'Nutrition', 'summary': 'Nutrition',
    'dashboard': 'Dashboard', 'audit-logs': 'Dashboard', 'notifications': 'Dashboard',
    'profile': 'Account',
}

ZOD_FIELD = re.compile(r'^\s*(\w+)\s*:\s*z\.(\w+)\(\s*(?:"([^"]*)")?', re.M)
ZOD_OBJ = re.compile(r'export const (\w+Schema)\s*=\s*z\.object\(\{(.*?)^\}\)', re.M | re.S)


def feature_of(route):
    for k, v in FEAT.items():
        if k in route:
            return v
    return None


def tables_of(feature):
    d = API / feature
    if not d.is_dir():
        return []
    seen = set()
    for f in d.glob('*Service.cs'):
        src = f.read_text(encoding='utf-8-sig')
        seen |= set(re.findall(r'_dbContext\.(\w+)\s*\.', src))
    return sorted(seen)


def crud_of(feature):
    d = API / feature
    ops = set()
    for f in d.glob('*Service.cs') if d.is_dir() else []:
        s = f.read_text(encoding='utf-8-sig')
        if re.search(r'\.Add\(|\.AddAsync\(', s):
            ops.add('C')
        if re.search(r'\.(First|Single|Any|Where|ToList|Count|Select)', s):
            ops.add('R')
        if re.search(r'\.Update\(|SaveChangesAsync', s):
            ops.add('U')
        if re.search(r'\.Remove\(|\.RemoveRange\(', s):
            ops.add('D')
    return ''.join(o for o in 'CRUD' if o in ops) or 'R'


def zod_fields(fe_root, route):
    """Field cua man hinh <- Zod schema. Thong bao loi tieng Viet lam mo ta."""
    feat_dir = None
    for cand in Path(fe_root, 'src/features').iterdir() if Path(fe_root, 'src/features').is_dir() else []:
        key = cand.name.replace('member-', '').replace('-', '')
        if key and key in route.replace('/', '').replace('-', ''):
            feat_dir = cand
            break
    if not feat_dir:
        return []
    out = []
    for sf in feat_dir.rglob('*.ts'):
        if 'schema' not in sf.name:
            continue
        src = sf.read_text(encoding='utf-8')
        for m in ZOD_OBJ.finditer(src):
            for f in ZOD_FIELD.finditer(m.group(2)):
                name, typ, msg = f.group(1), f.group(2), f.group(3) or ''
                out.append((name, {'string': 'Text Box', 'email': 'Text Box (email)',
                                   'number': 'Number Box', 'boolean': 'Checkbox',
                                   'enum': 'Combo Box', 'date': 'Date Picker',
                                   'coerce': 'Number Box'}.get(typ, typ),
                            msg or 'Truong {} cua form'.format(name)))
    seen, uniq = set(), []
    for f in out:
        if f[0] not in seen:
            seen.add(f[0])
            uniq.append(f)
    return uniq[:14]


def set_cell(cell, text):
    cell.text = ''
    p = cell.paragraphs[0]
    r = p.add_run(str(text))
    r.font.size = Pt(9)


def uniq_cells(row):
    out, seen = [], set()
    for c in row.cells:
        if id(c._tc) not in seen:
            seen.add(id(c._tc))
            out.append(c)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('rds')
    ap.add_argument('inventory')
    ap.add_argument('--fe', default='../GymMaster-frontend')
    a = ap.parse_args()

    shots = Path(a.fe) / 'docs/design/screenshots'
    d = docx.Document(a.rds)
    body = d.element.body
    kids = list(body.iterchildren())

    # --- 1. XOA MAU: tu sau 'III. Design Specifications' den truoc 'IV. Appendix'
    i_start = i_end = None
    for i, c in enumerate(kids):
        if c.tag == qn('w:p'):
            t = ''.join(n.text or '' for n in c.iter(qn('w:t'))).strip()
            if t.startswith('III. Design Specifications'):
                i_start = i
            elif t.startswith('IV. Appendix') and i_start is not None:
                i_end = i
                break
    if i_start is None or i_end is None:
        sys.exit('Khong tim thay moc phan III / IV')

    removed = 0
    for c in kids[i_start + 1:i_end]:
        body.remove(c)
        removed += 1
    print('Da xoa {} phan tu mau cua thay (User Login / Setting List / Setting '
          'Details + anh "Teacher")'.format(removed))

    tpl_field = d.tables[74] if len(d.tables) > 74 else None
    tpl_db = d.tables[75] if len(d.tables) > 75 else None

    with open(a.inventory, encoding='utf-8-sig') as fh:
        inv = [r for r in csv.DictReader(fh) if r['kind'] == 'Screen']

    anchor = kids[i_end]           # chen truoc 'IV. Appendix'
    made = no_shot = no_field = 0

    for n, r in enumerate(inv, 1):
        route = r['name']
        name = NAMES.get(route, route)
        feat = feature_of(route)

        def add(p_el):
            anchor.addprevious(p_el)

        h = d.add_paragraph(style='Heading 3')
        h.add_run('III.{} {}'.format(n, name))
        add(h._p)

        h = d.add_paragraph(style='Heading 5')
        h.add_run('UI Design')
        add(h._p)

        png = shots / (SHOT.get(route, '') + '.png') if SHOT.get(route) else None
        p = d.add_paragraph()
        if png and png.exists():
            p.add_run().add_picture(str(png), width=Inches(5.6))
        else:
            p.add_run('[CAN BO SUNG] Chua co anh chup cho man {}'.format(route))
            no_shot += 1
        add(p._p)

        flds = zod_fields(a.fe, route)
        if tpl_field is not None:
            import copy
            t = copy.deepcopy(tpl_field._tbl)
            add(t)
            tb = docx.table.Table(t, d)
            rows = flds or [('[CAN BO SUNG]', '-', 'Khong tim thay Zod schema cho man nay')]
            if not flds:
                no_field += 1
            while len(tb.rows) - 1 < len(rows):
                tb.add_row()
            while len(tb.rows) - 1 > len(rows):
                tb._tbl.remove(tb.rows[-1]._tr)
            for k, vals in enumerate(rows, 1):
                cs = uniq_cells(tb.rows[k])
                for j, v in enumerate(vals):
                    if j < len(cs):
                        set_cell(cs[j], v)

        h = d.add_paragraph(style='Heading 5')
        h.add_run('Database Access')
        add(h._p)

        tbls = tables_of(feat) if feat else []
        if tpl_db is not None:
            import copy
            t = copy.deepcopy(tpl_db._tbl)
            add(t)
            tb = docx.table.Table(t, d)
            crud = crud_of(feat) if feat else 'R'
            rows = [(x, crud, 'Truy cap qua {}Service (Features/{})'.format(feat, feat))
                    for x in tbls] or [('[CAN BO SUNG]', '-', 'Chua map duoc feature backend')]
            while len(tb.rows) - 1 < len(rows):
                tb.add_row()
            while len(tb.rows) - 1 > len(rows):
                tb._tbl.remove(tb.rows[-1]._tr)
            for k, vals in enumerate(rows, 1):
                cs = uniq_cells(tb.rows[k])
                for j, v in enumerate(vals):
                    if j < len(cs):
                        set_cell(cs[j], v)

        p = d.add_paragraph()
        p.add_run('SQL Commands: ').bold = True
        p.add_run('[CAN BO SUNG] Backend dung EF Core LINQ, khong co SQL tho. '
                  'Lay SQL that do EF sinh ra bang cach bat log '
                  '(Microsoft.EntityFrameworkCore.Database.Command) roi goi endpoint.')
        add(p._p)
        made += 1

    d.save(a.rds)
    print('Da sinh {} muc III.n vao {}'.format(made, a.rds))
    print('  man co anh mockup     : {}/{}'.format(made - no_shot, made))
    print('  man co bang field Zod : {}/{}'.format(made - no_field, made))


if __name__ == '__main__':
    main()
