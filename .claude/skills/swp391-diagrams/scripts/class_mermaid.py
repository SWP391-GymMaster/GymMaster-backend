"""Sinh class diagram / package diagram (Mermaid) tu code C# that.

class:   python class_mermaid.py class out/cls_billing.mmd --feature Billing
package: python class_mermaid.py package out/packages.mmd

Doc Controller + Service + interface trong Features/<Ten>/, lay method public
va quan he ke thua / implement / phu thuoc (constructor injection).
"""
import re
import sys
from pathlib import Path

API = Path('backend/GymMaster.API')
FEATURES = API / 'Features'

CLASS_RE = re.compile(
    r'public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*'
    r'(class|interface|record)\s+(\w+)(?:<[^>]*>)?\s*(\([^)]*\))?\s*(?::\s*([^{\r\n;]+))?')
# Class: chi lay method PUBLIC (private khong thuoc ve class diagram).
PUB_METHOD_RE = re.compile(
    r'^[ \t]*public\s+(?:async\s+)?([\w<>,\[\]?\.]+(?:<[^>]*>)?)\s+(\w+)\s*\(([^)]*)\)\s*[;{]', re.M)
# Interface: thanh vien C# KHONG viet 'public' -> khong doi tu khoa do,
# neu khong interface se hien rong tren class diagram.
IFACE_METHOD_RE = re.compile(
    r'^[ \t]*(?:public\s+)?([\w<>,\[\]?\.]+(?:<[^>]*>)?)\s+(\w+)\s*\(([^)]*)\)\s*;', re.M)
INJECT_RE = re.compile(r'private\s+readonly\s+([\w<>]+)\s+(\w+)\s*;')
# DTO kieu class dung PROPERTY chu khong phai record positional — khong bat
# cai nay thi CreateCheckInRequest hien ra rong tren class diagram.
PROP_RE = re.compile(
    r'^[ \t]*public\s+([\w<>,\[\]?\.]+(?:<[^>]*>)?)\s+(\w+)\s*\{\s*get;', re.M)

NOT_METHOD = {'if', 'for', 'foreach', 'while', 'switch', 'catch', 'using', 'lock', 'return'}


def clean(t):
    t = re.sub(r'\s+', ' ', t or '').strip()
    return t.replace('<', '~').replace('>', '~')      # Mermaid ky tu dac biet


def split_params(s):
    """Tach tham so o cap ngoac ngoai cung: 'Task<A,B> x, int y' -> 2 phan.

    split(',') binh thuong se cat nham trong generic.
    """
    out, depth, cur = [], 0, ''
    for ch in s:
        if ch in '<([':
            depth += 1
        elif ch in '>)]':
            depth -= 1
        if ch == ',' and depth == 0:
            out.append(cur)
            cur = ''
        else:
            cur += ch
    if cur.strip():
        out.append(cur)
    return [p.strip() for p in out if p.strip()]


def body_after(src, pos):
    """Than cua type bat dau tu pos (khop ngoac nhon). Record 1 dong -> rong."""
    i = src.find('{', pos)
    semi = src.find(';', pos)
    if i < 0 or (0 <= semi < i):
        return ''
    depth = 0
    for j in range(i, len(src)):
        if src[j] == '{':
            depth += 1
        elif src[j] == '}':
            depth -= 1
            if depth == 0:
                return src[i:j + 1]
    return ''


def scan(files):
    """Bat MOI type trong file, khong chi cai dau tien.

    File DTO co nhieu record trong cung mot file — chi lay cai dau se ra
    class sai kem theo cac record khac bi parse nham thanh method.
    """
    types = {}
    for f in files:
        src = f.read_text(encoding='utf-8-sig')
        for m in CLASS_RE.finditer(src):
            kind, name, positional, bases = m.groups()
            body = body_after(src, m.end())
            methods = []
            if positional:
                # record CreateX(string A, int B = null) -> tham so la thuoc tinh.
                # Phai cat gia tri mac dinh truoc, khong thi "string? X = null"
                # cho ra ten thuoc tinh la "null".
                for p in split_params(positional.strip('()')):
                    p = p.split('=')[0].strip()
                    bits = p.split()
                    if len(bits) >= 2:
                        methods.append(('+' + bits[-1], -1, clean(' '.join(bits[:-1]))))
            for pm in PROP_RE.finditer(body):
                methods.append(('+' + pm.group(2), -1, clean(pm.group(1))))
            rx = IFACE_METHOD_RE if kind == 'interface' else PUB_METHOD_RE
            seen_m = set()
            for mm in rx.finditer(body):
                ret, mname, args = mm.group(1).strip(), mm.group(2), mm.group(3)
                if mname == name or mname in NOT_METHOD or ret in NOT_METHOD:
                    continue
                if mname in seen_m:
                    continue
                seen_m.add(mname)
                nargs = len([a for a in split_params(args) if a.strip()])
                methods.append((mname, nargs, clean(ret)))
            types[name] = {
                'kind': kind,
                'bases': [b.strip() for b in (bases or '').split(',') if b.strip()],
                'methods': methods,
                'injects': [t for t, _ in INJECT_RE.findall(body)],
                'file': f,
            }
    return types


def emit_class(types, out):
    names = set(types)
    lines = ['classDiagram']
    for name, t in sorted(types.items()):
        stereo = ' {\n    <<interface>>' if t['kind'] == 'interface' else ' {'
        lines.append('  class {}{}'.format(name, stereo))
        for mname, nargs, ret in t['methods'][:12]:
            if nargs < 0:                       # thuoc tinh cua record, khong phai method
                lines.append('    {} {}'.format(mname, ret))
            else:
                lines.append('    +{}({}) {}'.format(mname, '...' if nargs else '', ret))
        lines.append('  }')
    for name, t in sorted(types.items()):
        for b in t['bases']:
            b = b.split('<')[0].strip()
            if b in names:
                arrow = '..|>' if types[b]['kind'] == 'interface' else '--|>'
                lines.append('  {} {} {}'.format(name, arrow, b))
        for inj in t['injects']:
            inj = inj.split('<')[0].strip()
            if inj in names and inj != name:
                lines.append('  {} ..> {} : uses'.format(name, inj))
    Path(out).parent.mkdir(parents=True, exist_ok=True)
    Path(out).write_text('\n'.join(lines), encoding='utf-8')
    return len(types), sum(len(t['methods']) for t in types.values())


def emit_package(out):
    """Package diagram: 1 namespace / feature + cac tang dung chung."""
    lines = ['classDiagram']
    groups = []
    for d in sorted(FEATURES.iterdir()):
        if d.is_dir():
            groups.append(('Features.' + d.name, d))
    for extra in ('Common', 'Infrastructure', 'Entities', 'Data', 'Options'):
        p = API / extra
        if p.is_dir():
            groups.append((extra, p))

    for ns, d in groups:
        files = [f for f in d.glob('*.cs')]
        lines.append('  namespace {} {{'.format(ns.replace('.', '_')))
        shown = 0
        for f in files:
            src = f.read_text(encoding='utf-8-sig')
            m = CLASS_RE.search(src)
            if not m:
                continue
            lines.append('    class {}'.format(m.group(2)))
            shown += 1
            if shown >= 6:                       # nhieu qua thi hinh khong doc noi
                break
        if shown == 0:
            lines.append('    class {}_empty'.format(ns.replace('.', '_')))
        lines.append('  }')

    Path(out).parent.mkdir(parents=True, exist_ok=True)
    Path(out).write_text('\n'.join(lines), encoding='utf-8')
    return len(groups)


def main():
    mode, out = sys.argv[1], sys.argv[2]
    if mode == 'package':
        n = emit_package(out)
        print('-> {}: {} package'.format(out, n))
        print('Luu y: Mermaid khong co notation package that -> dung namespace gia lap.')
        return

    feature = None
    if '--feature' in sys.argv:
        feature = sys.argv[sys.argv.index('--feature') + 1]
    root = FEATURES / feature if feature else FEATURES
    if not root.is_dir():
        sys.exit('Khong thay {}. Cac feature: {}'.format(
            root, ', '.join(sorted(d.name for d in FEATURES.iterdir() if d.is_dir()))))

    files = [f for f in sorted(root.rglob('*.cs'))
             if 'obj' not in f.parts and 'bin' not in f.parts]
    types = scan(files)
    nc, nm = emit_class(types, out)
    print('-> {}: {} class/interface, {} method (tu {})'.format(out, nc, nm, root))
    print('Dan vao draw.io: Arrange > Insert > Advanced > Mermaid')


if __name__ == '__main__':
    main()
