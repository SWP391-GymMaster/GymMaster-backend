"""Sinh sequence diagram (Mermaid) cho MOT endpoint, doc tu code that.

Lan theo: Controller action -> Service method -> cac bang DbContext duoc dung
-> ket qua tra ve. Khong doan, chi lay nhung gi co trong code.

Dung: python sequence_mermaid.py <ControllerName> <ActionName> <out.mmd>
Vi du: python sequence_mermaid.py CheckInsController Create out/seq_checkin.mmd
"""
import re
import sys
from pathlib import Path

API = Path('backend/GymMaster.API')


def find_file(stem):
    hits = [p for p in API.rglob(stem + '.cs')
            if 'obj' not in p.parts and 'bin' not in p.parts]
    return hits[0] if hits else None


def method_body(src, name):
    """Cat than cua mot method theo dau ngoac nhon can bang."""
    m = re.search(r'\b' + re.escape(name) + r'\s*\([^)]*\)[^{;]*\{', src, re.S)
    if not m:
        return None
    i = src.index('{', m.start())
    depth = 0
    for j in range(i, len(src)):
        if src[j] == '{':
            depth += 1
        elif src[j] == '}':
            depth -= 1
            if depth == 0:
                return src[i:j + 1]
    return None


def tables_in(body):
    return set(re.findall(r'_(?:db|context|dbContext)\.(\w+)\s*\.', body))


def walk_service(ssrc, meth, depth=2, seen=None):
    """Bang ma method dung, KE CA qua cac helper private trong cung class.

    Khong lam viec nay thi sequence diagram bo sot het buoc validate — vi du
    CheckInService.CreateAsync chi 'thay' bang CheckIns, con MemberProfiles /
    Memberships nam trong ValidateMembershipAsync.
    """
    if seen is None:
        seen = set()
    if meth in seen or depth < 0:
        return set(), []
    seen.add(meth)

    body = method_body(ssrc, meth)
    if not body:
        return set(), []

    tables = tables_in(body)
    steps = []
    # helper private cung class duoc goi truc tiep (khong co dau '.' phia truoc)
    for m in re.finditer(r'(?<![.\w])(\w+Async)\s*\(', body):
        h = m.group(1)
        if h == meth or h in seen:
            continue
        if re.search(r'\bprivate\s[^;{]*\b' + re.escape(h) + r'\s*\(', ssrc):
            t2, s2 = walk_service(ssrc, h, depth - 1, seen)
            if t2 or s2:
                steps.append(h)
            tables |= t2
            steps.extend(s2)
    return tables, steps


def main():
    ctrl_name, action, out = sys.argv[1], sys.argv[2], sys.argv[3]

    cf = find_file(ctrl_name)
    if not cf:
        sys.exit('Khong tim thay ' + ctrl_name + '.cs')
    csrc = cf.read_text(encoding='utf-8-sig')

    route = re.search(r'\[Route\("([^"]+)"\)\]', csrc)
    verb = re.search(r'\[Http(Get|Post|Put|Delete|Patch)(?:\("([^"]*)"\))?\]\s*'
                     r'(?:\[[^\]]+\]\s*)*public[^{]*?\b' + re.escape(action) + r'\s*\(',
                     csrc, re.S)
    http = verb.group(1).upper() if verb else '?'
    sub = (verb.group(2) or '') if verb else ''
    path = '/' + (route.group(1) if route else '?') + (('/' + sub) if sub else '')

    body = method_body(csrc, action)
    if not body:
        sys.exit('Khong tim thay action ' + action + ' trong ' + cf.name)

    # service field -> interface type
    fields = dict((n, t) for t, n in re.findall(
        r'private\s+readonly\s+(I\w+)\s+(\w+)\s*;', csrc))

    called = []
    for fld, svc in ((f, t) for f, t in fields.items()):
        for m in re.finditer(re.escape(fld) + r'\.(\w+)\s*\(', body):
            called.append((svc, m.group(1)))
    # bo trung, giu thu tu
    seen = set()
    calls = [c for c in called if not (c in seen or seen.add(c))]

    lines = ['sequenceDiagram', '    autonumber',
             '    actor U as Client',
             '    participant C as {}'.format(ctrl_name)]
    impls = {}
    for i, (svc, meth) in enumerate(calls):
        impl = svc[1:]  # IAuthService -> AuthService
        if impl not in impls:
            impls[impl] = 'S{}'.format(i)
            lines.append('    participant {} as {}'.format(impls[impl], impl))
    lines.append('    participant DB as GymMasterDbContext')

    lines.append('    U->>C: {} {}'.format(http, path))
    for svc, meth in calls:
        impl = svc[1:]
        alias = impls[impl]
        lines.append('    C->>{}: {}(...)'.format(alias, meth))
        sf = find_file(impl)
        tables, helpers = set(), []
        if sf:
            ssrc = sf.read_text(encoding='utf-8-sig')
            tables, helpers = walk_service(ssrc, meth)
        for h in helpers:
            lines.append('    {}->>{}: {}()'.format(alias, alias, h))
        for t in sorted(tables)[:8]:
            lines.append('    {}->>DB: {}'.format(alias, t))
            lines.append('    DB-->>{}: rows'.format(alias))
        if not tables:
            lines.append('    Note over {}: (khong truy cap DB truc tiep)'.format(alias))
        lines.append('    {}-->>C: ServiceResult<T>'.format(alias))
    lines.append('    C-->>U: ApiResponse<T> (HTTP 200 / loi)')

    Path(out).parent.mkdir(parents=True, exist_ok=True)
    Path(out).write_text('\n'.join(lines), encoding='utf-8')
    print('{} {} -> {}'.format(http, path, out))
    print('  service goi: {}'.format(', '.join('{}.{}'.format(s[1:], m) for s, m in calls) or '(khong co)'))


if __name__ == '__main__':
    main()
