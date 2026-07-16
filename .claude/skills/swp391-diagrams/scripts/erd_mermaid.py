"""Sinh ERD (Mermaid) tu cac entity C# that trong backend/GymMaster.API/Entities/.

Doc bang AST tho (regex tren khai bao property) — khong can build, khong can DB.
Quan he suy ra tu navigation property + khoa ngoai <Ten>Id.

Dung: python erd_mermaid.py <out.mmd> [--tables t1,t2,...]
"""
import re
import sys
from pathlib import Path

ENT_DIR = Path('backend/GymMaster.API/Entities')

CLASS_RE = re.compile(r'public\s+(?:sealed\s+)?class\s+([A-Z]\w+)')
PROP_RE = re.compile(
    r'^\s*public\s+(?:virtual\s+)?([\w<>?\[\],\s]+?)\s+(\w+)\s*\{\s*get;', re.M)

SCALARS = {
    'int': 'int', 'long': 'bigint', 'string': 'nvarchar', 'bool': 'bit',
    'decimal': 'decimal', 'double': 'float', 'DateTime': 'datetime2',
    'DateOnly': 'date', 'TimeOnly': 'time', 'byte': 'tinyint', 'Guid': 'uniqueidentifier',
}

# Khoa ngoai khong phai luc nao cung trung ten entity: MemberId -> MemberProfile,
# TrainerId -> TrainerProfile, CreatedBy -> User. Khong co bang nay thi FK bi bo sot.
FK_ALIAS = {
    'Member': 'MemberProfile',
    'Trainer': 'TrainerProfile',
    'Staff': 'StaffProfile',
    'CreatedBy': 'User',
    'UpdatedBy': 'User',
    'Package': 'MembershipPackage',
    'Food': 'FoodItem',
}


def fk_target(pname, names):
    """Ten property -> entity ma no tro toi, hoac None."""
    if pname == 'Id':
        return None
    if pname in FK_ALIAS and FK_ALIAS[pname] in names:
        return FK_ALIAS[pname]
    if pname.endswith('Id'):
        stem = pname[:-2]
        if stem in names:
            return stem
        if stem in FK_ALIAS and FK_ALIAS[stem] in names:
            return FK_ALIAS[stem]
    return None


def norm(t):
    return t.replace('?', '').strip()


def parse_entities():
    ents = {}
    for f in sorted(ENT_DIR.glob('*.cs')):
        src = f.read_text(encoding='utf-8-sig')
        m = CLASS_RE.search(src)
        if not m:
            continue
        name = m.group(1)
        props = []
        for pm in PROP_RE.finditer(src):
            ptype, pname = norm(pm.group(1)), pm.group(2)
            props.append((ptype, pname))
        ents[name] = {'file': f.name, 'props': props}
    return ents


def build(ents, only=None):
    names = set(ents)
    lines = ['erDiagram']
    rels = []

    for ename, e in sorted(ents.items()):
        if only and ename not in only:
            continue
        attrs = []
        for ptype, pname in e['props']:
            base = ptype.replace('ICollection<', '').replace('List<', '').replace('>', '')
            # navigation 1-n
            if ptype.startswith(('ICollection<', 'List<')) and base in names:
                if not only or base in only:
                    rels.append('  {} ||--o{{ {} : "has"'.format(ename, base))
                continue
            # navigation n-1
            if base in names and base != ename:
                if not only or base in only:
                    rels.append('  {} }}o--|| {} : "refers"'.format(ename, base))
                continue
            sql = SCALARS.get(base, base)
            key = ''
            if pname == 'Id':
                key = ' PK'
            else:
                tgt = fk_target(pname, names)
                if tgt:
                    key = ' FK'
                    # entity khong co navigation property van phai co quan he
                    if not only or tgt in only:
                        rels.append('  {} }}o--|| {} : "{}"'.format(ename, tgt, pname))
            attrs.append('    {} {}{}'.format(sql, pname, key))
        lines.append('  {} {{'.format(ename))
        lines.extend(attrs)
        lines.append('  }')

    seen = set()
    for r in rels:
        if r not in seen:
            seen.add(r)
            lines.append(r)
    return '\n'.join(lines)


def main():
    out = sys.argv[1]
    only = None
    if '--tables' in sys.argv:
        only = set(sys.argv[sys.argv.index('--tables') + 1].split(','))

    ents = parse_entities()
    print('doc duoc {} entity tu {}'.format(len(ents), ENT_DIR))
    mmd = build(ents, only)
    Path(out).write_text(mmd, encoding='utf-8')

    n_ent = mmd.count('{\n') if '{\n' in mmd else len([l for l in mmd.splitlines() if l.endswith(' {')])
    n_rel = len([l for l in mmd.splitlines() if '--' in l])
    print('-> {}: {} bang, {} quan he'.format(out, n_ent, n_rel))
    print('\nDan vao draw.io: Arrange > Insert > Advanced > Mermaid')


if __name__ == '__main__':
    main()
