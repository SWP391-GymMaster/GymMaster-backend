"""POC: sinh file .drawio (mxGraph XML) chua use case diagram cho tung actor,
doc truc tiep tu docs/init/03_SRS_USE_CASES.md."""
import re
import sys
from pathlib import Path
from xml.sax.saxutils import escape

SRC = Path('docs/init/03_SRS_USE_CASES.md')

ROW = re.compile(r'^\|\s*(UC-[0-9A-Z]+)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|')

ACTORS = ['Admin', 'Staff', 'PT', 'Member', 'System']


def parse():
    text = SRC.read_text(encoding='utf-8')
    # chi lay bang trong muc "2. Use Case Overview"
    start = text.index('# 2. Use Case Overview')
    end = text.index('# 3.', start)
    rows = []
    for line in text[start:end].splitlines():
        m = ROW.match(line.strip())
        if m:
            uid, name, actor, prio = (g.strip() for g in m.groups())
            # UC da go khoi pham vi thi khong ve — diagram phai ta he thong THAT
            # da giao, khong phai y dinh ban dau. Cot uu tien danh dau '~~Removed~~'.
            if 'removed' in prio.lower():
                continue
            rows.append({'id': uid, 'name': name, 'actor': actor, 'prio': prio})
    return rows


def actors_of(raw):
    """'Admin/Staff' -> [Admin, Staff]; 'All' -> tat ca tru System."""
    if raw.lower() == 'all':
        return ['Admin', 'Staff', 'PT', 'Member']
    out = []
    for part in re.split(r'[/,]', raw):
        p = part.strip()
        for a in ACTORS:
            if p.lower() == a.lower():
                out.append(a)
    return out or []


def diagram_for(actor, ucs, idx):
    UC_W, UC_H, GAP = 190, 44, 18
    TOP, LEFT = 60, 300
    h = max(len(ucs) * (UC_H + GAP) + 60, 240)

    cells = []
    cells.append(
        '<mxCell id="sys{i}" value="GymMaster" '
        'style="rounded=0;whiteSpace=wrap;html=1;fillColor=none;verticalAlign=top;'
        'fontStyle=1;fontSize=14;" vertex="1" parent="1">'
        '<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>'
        .format(i=idx, x=LEFT - 40, y=TOP - 40, w=UC_W + 80, h=h))

    ay = TOP + h / 2 - 70
    cells.append(
        '<mxCell id="act{i}" value="{a}" '
        'style="shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;'
        'outlineConnect=0;fontStyle=1;" vertex="1" parent="1">'
        '<mxGeometry x="90" y="{y}" width="40" height="80" as="geometry"/></mxCell>'
        .format(i=idx, a=escape(actor), y=int(ay)))

    for k, uc in enumerate(ucs):
        uid = 'uc{}_{}'.format(idx, k)
        label = '{}\n{}'.format(uc['id'], uc['name'])
        y = TOP + k * (UC_H + GAP)
        cells.append(
            '<mxCell id="{u}" value="{v}" '
            'style="ellipse;whiteSpace=wrap;html=1;fontSize=11;" vertex="1" parent="1">'
            '<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>'
            .format(u=uid, v=escape(label), x=LEFT, y=y, w=UC_W, h=UC_H))
        cells.append(
            '<mxCell id="e{u}" style="endArrow=none;html=1;strokeWidth=1;" '
            'edge="1" parent="1" source="act{i}" target="{u}">'
            '<mxGeometry relative="1" as="geometry"/></mxCell>'.format(u=uid, i=idx))

    return (
        '<diagram id="d{i}" name="UC - {a}">'
        '<mxGraphModel dx="900" dy="700" grid="1" gridSize="10" guides="1" tooltips="1" '
        'connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="850" '
        'pageHeight="1100" math="0" shadow="0"><root>'
        '<mxCell id="0"/><mxCell id="1" parent="0"/>{c}</root></mxGraphModel></diagram>'
        .format(i=idx, a=escape(actor), c=''.join(cells)))


def main(out):
    rows = parse()
    print('doc duoc {} use case tu {}'.format(len(rows), SRC))

    diagrams = []
    for i, actor in enumerate(ACTORS):
        ucs = [r for r in rows if actor in actors_of(r['actor'])]
        if not ucs:
            continue
        diagrams.append(diagram_for(actor, ucs, i))
        print('  {:<8} {:>2} use case'.format(actor, len(ucs)))

    xml = ('<mxfile host="app.diagrams.net" type="device">'
           + ''.join(diagrams) + '</mxfile>')
    Path(out).write_text(xml, encoding='utf-8')
    print('\n-> {} ({} tab, {} KB)'.format(out, len(diagrams), len(xml) // 1024))


if __name__ == '__main__':
    main(sys.argv[1])
