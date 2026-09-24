"""Small helpers to draw isometric / 2D typical details into DXF (ezdxf)."""
import math
import ezdxf
from ezdxf.enums import TextEntityAlignment

C30, S30 = math.cos(math.radians(30)), math.sin(math.radians(30))

LAYERS = {
    "TID-FRAME": 7, "TID-ELC TEXT": 15, "TID-E-TITLE": 4, "TID-ELC DIMENTION": 7,
    "ELC-GROUNDING": 172, "ELC-GROUNDING-70": 6, "TID-E-CONTROL BOX": 170,
    "TID-STRUCTURE": 8, "TID-CONCRETE": 9, "TID-HATCH": 250, "TID-WALL": 15,
    "TID-E-LADDER": 170, "TID-E-SUPPORT": 30, "TID-E-LIGHTING": 150,
    "TID-E-CONDUIT": 3, "TID-HIDDEN": 8,
}


def prepare(doc):
    """Make an existing drawing ready to receive a detail (layers + text style)."""
    if "TX-AN" not in doc.styles:
        doc.styles.add("TX-AN", font="ARIALN.TTF")
    if "DASHED" not in doc.linetypes:
        doc.linetypes.add("DASHED", pattern=[0.6, 0.5, -0.1])
    for name, color in LAYERS.items():
        if name not in doc.layers:
            doc.layers.add(name, color=color)
    doc.layers.get("TID-HIDDEN").dxf.linetype = "DASHED"
    return doc


def new_doc():
    doc = ezdxf.new("R2010", setup=True)
    doc.units = ezdxf.units.MM
    doc.styles.add("TX-AN", font="ARIALN.TTF")
    for name, color in LAYERS.items():
        doc.layers.add(name, color=color)
    doc.layers.get("TID-HIDDEN").dxf.linetype = "DASHED"
    return doc


def iso(p):
    x, y, z = p
    return ((x - y) * C30, (x + y) * S30 + z)


class Sheet:
    def __init__(self, doc, ox=0.0, oy=0.0, th=250.0):
        self.doc, self.msp, self.ox, self.oy, self.th = doc, doc.modelspace(), ox, oy, th

    # ---------- 2D primitives (local coordinates) ----------
    def P(self, p):
        return (p[0] + self.ox, p[1] + self.oy)

    def line(self, a, b, layer="0", **kw):
        self.msp.add_line(self.P(a), self.P(b), dxfattribs={"layer": layer, **kw})

    def pl(self, pts, layer="0", closed=False, width=0, **kw):
        e = self.msp.add_lwpolyline([self.P(p) for p in pts], close=closed,
                                    dxfattribs={"layer": layer, **kw})
        if width:
            e.dxf.const_width = width
        return e

    def fill(self, pts, rgb=None, color=None, layer="TID-HATCH", pattern=None, scale=1.0, angle=0):
        h = self.msp.add_hatch(color=color or 7, dxfattribs={"layer": layer})
        if pattern:
            h.set_pattern_fill(pattern, scale=scale, angle=angle)
        elif rgb:
            h.set_solid_fill(rgb=rgb)
        h.paths.add_polyline_path([self.P(p) for p in pts], is_closed=True)
        return h

    def circle(self, c, r, layer="0", **kw):
        self.msp.add_circle(self.P(c), r, dxfattribs={"layer": layer, **kw})

    def arc(self, c, r, a0, a1, layer="0"):
        self.msp.add_arc(self.P(c), r, a0, a1, dxfattribs={"layer": layer})

    def text(self, s, p, h=None, layer="TID-ELC TEXT", align="LEFT", rot=0):
        t = self.msp.add_text(s, height=h or self.th, rotation=rot,
                              dxfattribs={"layer": layer, "style": "TX-AN", "width": 0.9})
        t.set_placement(self.P(p), align=TextEntityAlignment[align])
        return t

    def arrow(self, tip, frm, size=None, layer="TID-ELC TEXT"):
        size = size or self.th * 0.6
        ang = math.atan2(frm[1] - tip[1], frm[0] - tip[0])
        a = (tip[0] + size * math.cos(ang + 0.25), tip[1] + size * math.sin(ang + 0.25))
        b = (tip[0] + size * math.cos(ang - 0.25), tip[1] + size * math.sin(ang - 0.25))
        self.msp.add_solid([self.P(tip), self.P(a), self.P(b)], dxfattribs={"layer": layer})

    def label(self, tip, knee, lines, side=1, layer="TID-ELC TEXT"):
        """Leader: arrow at tip -> knee -> horizontal shoulder with text above/below."""
        th = self.th
        w = max(len(s) for s in lines) * th * 0.55 + th
        end = (knee[0] + side * w, knee[1])
        self.line(tip, knee, layer)
        self.line(knee, end, layer)
        self.arrow(tip, knee, layer=layer)
        x = knee[0] + (th * 0.4 if side > 0 else -w + th * 0.2)
        y = knee[1] + th * 0.35
        self.text(lines[0], (x, y), layer=layer)
        for i, s in enumerate(lines[1:], 1):
            self.text(s, (x, knee[1] - th * 1.5 * i), layer=layer)

    def balloon(self, tip, c, n, r=None, layer="TID-ELC TEXT"):
        r = r or self.th * 0.9
        self.circle(c, r, layer)
        self.text(str(n), c, align="MIDDLE_CENTER", layer=layer)
        ang = math.atan2(tip[1] - c[1], tip[0] - c[0])
        st = (c[0] + r * math.cos(ang), c[1] + r * math.sin(ang))
        self.line(st, tip, layer)
        self.arrow(tip, st, layer=layer)

    def title(self, s, p, sub="SCALE  :  NTS"):
        h = self.th * 1.2
        self.text(s, p, h=h, layer="TID-E-TITLE")
        w = len(s) * h * 0.62
        self.line((p[0], p[1] - h * 0.3), (p[0] + w, p[1] - h * 0.3), "TID-E-TITLE")
        self.text(sub, (p[0], p[1] - h * 1.2), h=self.th * 0.6)

    def dim_v(self, x, y0, y1, txt, off=0, layer="TID-ELC DIMENTION"):
        th = self.th * 0.8
        self.line((x, y0), (x, y1), layer)
        for y in (y0, y1):
            self.line((x - th * 0.4, y - th * 0.4), (x + th * 0.4, y + th * 0.4), layer)
            if off:
                self.line((x + off, y), (x - th * 0.5 * (1 if off > 0 else -1), y), layer)
        self.text(txt, (x - th * 0.3, (y0 + y1) / 2), h=th, align="BOTTOM_CENTER", rot=90, layer=layer)

    def dim_h(self, y, x0, x1, txt, layer="TID-ELC DIMENTION"):
        th = self.th * 0.8
        self.line((x0, y), (x1, y), layer)
        for x in (x0, x1):
            self.line((x - th * 0.4, y - th * 0.4), (x + th * 0.4, y + th * 0.4), layer)
        self.text(txt, ((x0 + x1) / 2, y + th * 0.3), h=th, align="BOTTOM_CENTER", layer=layer)

    def rect(self, x0, y0, x1, y1, layer="0", **kw):
        return self.pl([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], layer, closed=True, **kw)

    def table(self, x0, y_top, width, rows, blank=2):
        """Bill of material table ITEM / QTY / UNIT / DESCRIPTION like ME-F-EG-5001."""
        th = self.th
        cols = [x0, x0 + 900, x0 + 2900, x0 + 4400, x0 + width]
        hh, rh = 1.4 * th * 2.5, 2.0 * th
        n = len(rows) + blank
        y_bot = y_top - hh - n * rh
        self.rect(x0, y_bot, x0 + width, y_top, "TID-FRAME")
        for x in cols[1:-1]:
            self.line((x, y_bot), (x, y_top), "TID-FRAME")
        self.line((x0, y_top - hh), (x0 + width, y_top - hh), "TID-FRAME")
        for i in range(1, n):
            y = y_top - hh - i * rh
            self.line((x0, y), (x0 + width, y), "TID-FRAME")
        for x_a, x_b, s in zip(cols, cols[1:], ["ITEM", "QTY", "UNIT", "DESCRIPTION"]):
            self.text(s, ((x_a + x_b) / 2, y_top - hh / 2), h=th * 1.2, align="MIDDLE_CENTER")
        for i, (qty, unit, desc) in enumerate(rows):
            y = y_top - hh - (i + 0.5) * rh
            self.text(str(i + 1), ((cols[0] + cols[1]) / 2, y), align="MIDDLE_CENTER")
            self.text(qty, ((cols[1] + cols[2]) / 2, y), align="MIDDLE_CENTER")
            self.text(unit, ((cols[2] + cols[3]) / 2, y), align="MIDDLE_CENTER")
            self.text(desc, (cols[3] + th * 0.8, y), align="MIDDLE_LEFT")
        return y_bot

    # ---------- isometric primitives (3D local mm -> 2D) ----------
    def I(self, p, s=1.0, o=(0, 0)):
        x, y = iso(p)
        return (x * s + o[0], y * s + o[1])

    def iline(self, a, b, layer="0", s=1.0, o=(0, 0), **kw):
        self.line(self.I(a, s, o), self.I(b, s, o), layer, **kw)

    def ipl(self, pts, layer="0", s=1.0, o=(0, 0), closed=False, width=0, **kw):
        return self.pl([self.I(p, s, o) for p in pts], layer, closed=closed, width=width, **kw)

    def ibox(self, p0, p1, layer="0", s=1.0, o=(0, 0), shade=((235, 235, 235), (200, 200, 200), (170, 170, 170)),
             faces="tyx"):
        """Axis aligned box. Draws visible faces (top, -y front, -x side) filled + outlined."""
        (x0, y0, z0), (x1, y1, z1) = p0, p1
        F = {
            "t": [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)],
            "y": [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)],
            "x": [(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)],
        }
        for k, rgb in zip("tyx", shade):
            if k not in faces:
                continue
            pts = [self.I(p, s, o) for p in F[k]]
            if rgb:
                self.fill(pts, rgb=rgb)
            self.pl(pts, layer, closed=True)
