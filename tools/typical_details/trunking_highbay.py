"""TYPICAL DETAIL - CABLE TRAY / TRUNKING FOR HIGHBAY LIGHTING (PURLIN SUPPORT).

Style follows ME-F-EP-5001 (TYPICAL DETAIL CABLE TRAY INSTALLATION):
leader labels, cyan underlined title + SCALE : NTS, framed cells.
Output: DXF (mm).
"""
import math
import sys
from dxfkit import new_doc, Sheet, C30

STEEL = ((235, 235, 235), (205, 205, 205), (175, 175, 175))
TRUNK = ((190, 210, 240), (120, 160, 220), (80, 125, 200))
STRUT = ((255, 205, 140), (245, 165, 70), (220, 135, 40))
LAMP = (90, 90, 90)
CW, CH = 5200, 4200          # one detail cell


def section(sh, ox, oy):
    """FRONT VIEW / SECTION across the trunking."""
    X = lambda x, y: (ox + x, oy + y)       # noqa: E731
    yr = 2750                                # underside of purlin
    # roof sheet + C purlin 150x50x20x2.3
    sh.pl([X(-900, yr + 260), X(900, yr + 260)], "TID-STRUCTURE")
    sh.pl([X(-900, yr + 275), X(900, yr + 275)], "TID-STRUCTURE")
    cp = [X(-25, yr + 150 - 20), X(-25, yr + 150), X(25, yr + 150), X(25, yr), X(-25, yr), X(-25, yr + 20)]
    sh.pl([X(25, yr + 150), X(25, yr + 260)], "TID-STRUCTURE")
    sh.fill([X(-25, yr), X(25, yr), X(25, yr + 150), X(-25, yr + 150)], rgb=STEEL[1])
    sh.pl(cp, "TID-STRUCTURE")
    sh.pl([X(-25, yr), X(-25, yr + 150)], "TID-STRUCTURE")
    # purlin clamp + rod
    sh.fill([X(-45, yr - 45), X(45, yr - 45), X(45, yr + 8), X(-45, yr + 8)], rgb=STRUT[1])
    sh.rect(-45 + ox, yr - 45 + oy, 45 + ox, yr + 8 + oy, "TID-E-SUPPORT")
    # threaded rod M10 down to unistrut
    yu = 2250                                 # top of unistrut
    sh.rect(ox - 5, oy + yu - 60, ox + 5, oy + yr - 45, "TID-E-SUPPORT")
    for yy in (yr - 70, yu + 10, yu - 58):
        sh.rect(ox - 14, oy + yy, ox + 14, oy + yy + 16, "TID-E-SUPPORT")
    # unistrut 41x41 (side view, long 400)
    us = [X(-200, yu - 41), X(200, yu - 41), X(200, yu), X(-200, yu)]
    sh.fill(us, rgb=STRUT[1])
    sh.pl(us, "TID-E-SUPPORT", closed=True)
    sh.line(X(-200, yu - 30), X(200, yu - 30), "TID-E-SUPPORT")
    # trunking 200x100 c/w cover hung under unistrut with 2 rods (trapeze)
    # -> trunking sits on a lower unistrut trapeze
    yt = 1650
    for sx in (-150, 150):
        sh.rect(ox + sx - 5, oy + yt - 90, ox + sx + 5, oy + yu - 41, "TID-E-SUPPORT")
        sh.rect(ox + sx - 14, oy + yt - 75, ox + sx + 14, oy + yt - 59, "TID-E-SUPPORT")
    u2 = [X(-200, yt - 41), X(200, yt - 41), X(200, yt), X(-200, yt)]
    sh.fill(u2, rgb=STRUT[1])
    sh.pl(u2, "TID-E-SUPPORT", closed=True)
    tr = [X(-100, yt), X(100, yt), X(100, yt + 100), X(-100, yt + 100)]
    sh.fill(tr, rgb=TRUNK[0])
    sh.pl(tr, "TID-E-LADDER", closed=True)
    sh.pl([X(-106, yt + 92), X(-106, yt + 106), X(106, yt + 106), X(106, yt + 92)], "TID-E-LADDER", width=4)
    for i, cx in enumerate((-70, -40, -10, 20, 50, 75)):
        sh.circle(X(cx, yt + 16), 12, "TID-E-LIGHTING")
        if i < 3:
            sh.circle(X(cx + 15, yt + 40), 12, "TID-E-LIGHTING")
    # hanger for highbay: eye bolt + safety chain from lower unistrut
    yh = 1150                                  # top of highbay driver
    sh.circle(X(0, yt - 60), 12, "TID-E-SUPPORT")
    sh.rect(ox - 5, oy + yt - 50, ox + 5, oy + yt - 41, "TID-E-SUPPORT")
    for k in range(int((yt - 72 - yh - 40) // 36)):
        yy = yt - 72 - k * 36
        if k % 2:
            sh.rect(ox - 6, oy + yy - 36, ox + 6, oy + yy, "TID-E-SUPPORT")
        else:
            sh.msp.add_ellipse(X(0, yy - 18), major_axis=(0, 20), ratio=0.45,
                               dxfattribs={"layer": "TID-E-SUPPORT"})
    # highbay luminaire (UFO type 150W) side view
    sh.pl([X(-12, yh + 40), X(-12, yh), X(12, yh), X(12, yh + 40)], "TID-E-LIGHTING")   # hook
    dr = [X(-110, yh - 90), X(110, yh - 90), X(110, yh), X(-110, yh)]
    sh.fill(dr, rgb=(150, 150, 150))
    sh.pl(dr, "TID-E-LIGHTING", closed=True)
    body_t, body_b = yh - 90, yh - 190
    sh.fill([X(-200, body_b), X(200, body_b), X(200, body_t), X(-200, body_t)], rgb=LAMP)
    for fx in range(-190, 200, 20):
        sh.line(X(fx, body_b + 5), X(fx, body_t), "TID-E-LIGHTING")
    sh.rect(ox - 200, oy + body_b, ox + 200, oy + body_t, "TID-E-LIGHTING")
    lens = [X(-190, body_b), X(190, body_b), X(170, body_b - 25), X(-170, body_b - 25)]
    sh.fill(lens, rgb=(250, 240, 180))
    sh.pl(lens, "TID-E-LIGHTING", closed=True)
    # light rays
    for dx in (-150, -60, 60, 150):
        sh.line(X(dx, body_b - 50), X(dx * 1.8, body_b - 330), "TID-HIDDEN")
    # flexible conduit trunking -> driver box
    fc = [X(100, yt + 50), X(170, yt + 50), X(260, yt), X(300, yt - 150), X(290, yh - 20),
          X(230, yh - 45), X(110, yh - 45)]
    sh.pl(fc, "TID-E-CONDUIT", width=22)
    sh.rect(ox + 100, oy + yt + 38, ox + 125, oy + yt + 62, "TID-E-CONDUIT")
    sh.rect(ox + 98, oy + yh - 57, ox + 110, oy + yh - 33, "TID-E-CONDUIT")

    # ---- dimensions ----
    sh.dim_v(ox - 560, oy + yt, oy + yt + 100, "100")
    sh.dim_h(oy + yt + 180, ox - 100, ox + 100, "200")
    sh.dim_v(ox - 560, oy + yu, oy + yr, "VARIES")
    sh.dim_v(ox - 560, oy + body_b - 25, oy + yt - 41, "± 450")
    sh.line(X(-600, yr), X(-30, yr), "TID-ELC DIMENTION")
    sh.line(X(-600, yu), X(-210, yu), "TID-ELC DIMENTION")
    sh.line(X(-600, yt + 100), X(-110, yt + 100), "TID-ELC DIMENTION")
    sh.line(X(-600, yt), X(-210, yt), "TID-ELC DIMENTION")
    sh.line(X(-600, yt - 41), X(-210, yt - 41), "TID-ELC DIMENTION")
    sh.line(X(-600, body_b - 25), X(-210, body_b - 25), "TID-ELC DIMENTION")
    # FFL
    sh.pl([X(-900, 0), X(900, 0)], "TID-STRUCTURE", width=6)
    sh.fill([X(-900, -60), X(900, -60), X(900, 0), X(-900, 0)], color=8, pattern="AR-CONC", scale=0.4)
    sh.text("FFL", X(-880, 20), h=45)
    sh.line(X(-700, 0), X(-700, body_b - 25), "TID-ELC DIMENTION")
    sh.text("MOUNTING HEIGHT REFER TO LAYOUT", X(-735, 150), h=40, rot=90)

    # ---- labels (same wording style as ME-F-EP-5001) ----
    L = sh.label
    L(X(0, yr + 270), X(260, yr + 470), ["ROOF SHEET"], 1)
    L(X(25, yr + 90), X(300, yr + 170), ["'C' PURLIN 150x50x20x2.3mm"], 1)
    L(X(45, yr - 25), X(300, yr - 170), ["PURLIN CLAMP C/W BOLT M10"], 1)
    L(X(5, yr - 250), X(300, yr - 300), ["THREADED ROD M10 C/W NUT,", "LOCK & FLAT WASHER"], 1)
    L(X(200, yu - 20), X(330, yu + 30), ["UNISTRUT CHANNEL 41x41x2.5mm"], 1)
    L(X(-100, yt + 70), X(-450, yt + 330), ["CABLE TRUNKING 200x100mm", "C/W COVER (HDG)"], -1)
    L(X(-60, yt + 16), X(-400, yt - 150), ["NYY/NYM CABLE", "FOR HIGHBAY"], -1)
    L(X(200, yt - 20), X(420, yt - 100), ["UNISTRUT CHANNEL 41x41x2.5mm"], 1)
    L(X(300, yt - 150), X(420, yt - 330), ["FLEXIBLE CONDUIT Ø20mm", "C/W CONNECTOR & GLAND"], 1)
    L(X(-6, yt - 280), X(-400, yt - 320), ["EYE BOLT + SAFETY CHAIN"], -1)
    L(X(-110, yh - 50), X(-400, yh - 20), ["DRIVER BOX"], -1)
    L(X(200, body_t - 50), X(420, yh - 130), ["HIGHBAY LED 150W", "(UFO TYPE) IP65"], 1)
    sh.title("SECTION - CABLE TRUNKING FOR HIGHBAY (PURLIN TYPE)", (ox - 1000, oy - 450))


def isometric(sh, ox, oy):
    """ISOMETRIC VIEW: trunking run hung from purlins with highbay luminaires."""
    s, o = 0.72, (ox, oy)

    def I(p):
        return sh.I(p, s, o)

    def box(a, b, shade, layer):
        sh.ibox(a, b, layer, s, o, shade)

    zr = 2600
    L = 3300
    purl_x = (400, 1900, 3400)
    # purlins (along Y, drawn back to front)
    for px in reversed(purl_x):
        box((px - 25, -700, zr), (px + 25, 900, zr + 150), STEEL, "TID-STRUCTURE")
    # rods + upper strut + trapeze per purlin
    zu, zt = 2150, 1750
    for px in reversed(purl_x):
        sh.ipl([(px, 100, zr), (px, 100, zu)], "TID-E-SUPPORT", s, o, width=10)
        box((px - 20, -100, zu - 41), (px + 20, 300, zu), STRUT, "TID-E-SUPPORT")
        for yy in (250, -50):
            sh.ipl([(px, yy, zu - 41), (px, yy, zt)], "TID-E-SUPPORT", s, o, width=10)
        box((px - 20, -100, zt - 41), (px + 20, 300, zt), STRUT, "TID-E-SUPPORT")
    # trunking along X  (y 0..200)
    box((0, 0, zt), (L, 200, zt + 100), TRUNK, "TID-E-LADDER")
    for xx in range(300, L, 600):                       # cover joints
        sh.iline((xx, 0, zt + 100), (xx, 200, zt + 100), "TID-E-LADDER", s, o)
    # re-draw front parts of struts over trunking
    for px in purl_x:
        box((px - 20, -100, zt - 41), (px + 20, 0, zt), STRUT, "TID-E-SUPPORT")
        sh.ipl([(px, -50, zu - 41), (px, -50, zt)], "TID-E-SUPPORT", s, o, width=10)
    # highbays between supports
    zb = 1250
    for hx in (1150, 2650):
        sh.ipl([(hx, 100, zt - 41), (hx, 100, zb)], "TID-E-SUPPORT", s, o, width=8)
        for k in range(9):
            z = zt - 60 - k * 50
            sh.msp.add_ellipse(sh.P(I((hx, 100, z))), major_axis=(0, 12), ratio=0.5,
                               dxfattribs={"layer": "TID-E-SUPPORT"})
        c_top, c_mid, c_bot = I((hx, 100, zb - 90)), I((hx, 100, zb - 190)), I((hx, 100, zb - 215))
        r = 200 * s * 1.2247
        box((hx - 80, 20, zb - 90), (hx + 80, 180, zb), ((170, 170, 170), (140, 140, 140), (110, 110, 110)),
            "TID-E-LIGHTING")
        sh.fill([(c_top[0] - r, c_top[1]), (c_top[0] + r, c_top[1]), (c_mid[0] + r, c_mid[1]),
                 (c_mid[0] - r, c_mid[1])], rgb=LAMP)
        for cc in (c_top, c_mid):
            sh.msp.add_ellipse(sh.P(cc), major_axis=(r, 0), ratio=0.577, dxfattribs={"layer": "TID-E-LIGHTING"})
        sh.msp.add_ellipse(sh.P(c_bot), major_axis=(r * 0.93, 0), ratio=0.577,
                           dxfattribs={"layer": "TID-E-LIGHTING"})
        for side in (-1, 1):
            sh.line((c_top[0] + side * r, c_top[1]), (c_mid[0] + side * r, c_mid[1]), "TID-E-LIGHTING")
        for t in range(0, 360, 20):                     # fins
            a = math.radians(t)
            dx, dy = r * math.cos(a), r * 0.577 * math.sin(a)
            if dy < 0:
                sh.line((c_top[0] + dx, c_top[1] + dy), (c_mid[0] + dx, c_mid[1] + dy), "TID-E-LIGHTING")
        # flexible conduit from trunking side to driver
        fc = [(hx + 150, 0, zt + 50), (hx + 150, -80, zt + 30), (hx + 150, -80, zb - 20),
              (hx + 80, 0, zb - 45)]
        sh.ipl(fc, "TID-E-CONDUIT", s, o, width=12)

    lab = sh.label
    p = I((purl_x[0], 900, zr + 150))
    lab(p, (p[0] - 250, p[1] + 150), ["'C' PURLIN"], -1)
    p = I((purl_x[2], -50, zt - 250))
    lab(p, (p[0] + 350, p[1] + 150), ["THREADED ROD M10"], 1)
    p = I((purl_x[2] - 20, 100, zu - 20))
    lab(p, (p[0] - 200, p[1] + 500), ["UNISTRUT CHANNEL"], -1)
    p = I((L, 100, zt + 100))
    lab(p, (p[0] + 250, p[1] + 150), ["CABLE TRUNKING", "200x100mm C/W COVER"], 1)
    p = I((2650 + 200, 100, zb - 150))
    lab(p, (p[0] + 350, p[1] - 150), ["HIGHBAY LED 150W"], 1)
    p = I((1150 + 150, -80, 1400))
    lab(p, (p[0] - 450, p[1] - 450), ["FLEXIBLE CONDUIT Ø20mm"], -1)
    p = I((1150, 100, 1500))
    lab(p, (p[0] - 450, p[1] + 350), ["SAFETY CHAIN"], -1)
    # support spacing
    a, b = I((purl_x[0], 0, zt + 100)), I((purl_x[1], 0, zt + 100))
    sh.text("SUPPORT @ 1500mm MAX.", ((a[0] + b[0]) / 2 - 450, (a[1] + b[1]) / 2 + 180), rot=30, h=42)
    sh.title("ISOMETRIC - CABLE TRUNKING FOR HIGHBAY", (ox - 200, 300))


def draw(doc, ox=0, oy=0):
    sh = Sheet(doc, ox, oy, th=42)
    sh.rect(0, 0, CW * 2, CH, "TID-FRAME")
    sh.line((CW, 0), (CW, CH), "TID-FRAME")
    section(sh, 2300, 750)
    isometric(sh, CW + 1900, 380)
    # general notes
    nx, ny = CW + 1700, 1150
    notes = ["NOTES :",
             "1. ALL SUPPORT MATERIAL SHALL BE HOT DIP GALVANIZED (HDG).",
             "2. MAX. SUPPORT SPACING 1500mm, AND AT EVERY BEND / TEE.",
             "3. HIGHBAY SHALL BE HUNG WITH SAFETY CHAIN, INDEPENDENT FROM TRUNKING.",
             "4. TRUNKING SHALL BE BONDED TO EARTH (BC 16mm²) AT EVERY 30m & END."]
    for i, n in enumerate(notes):
        sh.text(n, (nx, ny - i * 95), h=50)
    return sh


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "TYPICAL_DETAIL_CABLE_TRUNKING_HIGHBAY.dxf"
    doc = new_doc()
    draw(doc)
    doc.saveas(out)
    print("saved", out)
