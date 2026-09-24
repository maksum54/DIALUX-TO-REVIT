"""TYPICAL DETAIL - CONNECTION EARTH BOX TO PIPE RACK (ISOMETRIC).

Style follows ME-F-EG-5001 (TYPICAL DETAIL GROUNDING INSTALLATION):
numbered balloons, cyan underlined title + SCALE : NTS, ITEM/QTY/UNIT/DESCRIPTION table.
Output: DXF (mm).
"""
import sys
from dxfkit import new_doc, Sheet

GREY = ((236, 236, 236), (205, 205, 205), (175, 175, 175))
STEEL = ((120, 160, 215), (60, 110, 190), (30, 80, 160))       # blue like pipe rack in model
COPPER = ((240, 190, 120), (215, 150, 80), (190, 120, 60))
BOX = ((225, 225, 225), (190, 190, 190), (160, 160, 160))

W, H = 27000, 16800        # frame of one detail cell (same proportion as ME-F-EG-5001)


def draw(doc, ox=0, oy=0):
    sh = Sheet(doc, ox, oy, th=250)
    s, o = 3.0, (0, 0)                  # isometric scale & origin inside cell

    def I(p):
        return sh.I(p, s, o)

    def ibox(a, b, shade=GREY, layer="TID-STRUCTURE", faces="tyx"):
        sh.ibox(a, b, layer, s, o, shade, faces)

    def ipl(pts, layer, width=0, closed=False):
        return sh.ipl(pts, layer, s, o, closed=closed, width=width)

    # ---------------- frame + table ----------------
    tbl_top = 3900
    sh.rect(0, 0, W, H + tbl_top, "TID-FRAME")
    sh.line((0, tbl_top), (W, tbl_top), "TID-FRAME")
    rows = [
        ("1", "EA", "COMPRESSION LUG TYPE 35mm², c/w BOLT, NUT, FLAT & LOCK WASHER (SS M10)"),
        ("AS REQ'D", "M", "GROUNDING CABLE / BONDING BC 35mm²"),
        ("1", "EA", "EARTHING BOX c/w COPPER BUSBAR 300x50x6mm & INSULATOR"),
        ("1", "EA", "GROUNDING BOSS / LUG PLATE 60x100x10mm WELDED TO COLUMN FLANGE"),
        ("AS REQ'D", "EA", "CABLE CLEAT / SADDLE c/w SCREW + PLUG @ 500mm"),
        ("AS REQ'D", "M", "GROUNDING CABLE BC 70mm² TO MAIN GROUNDING GRID c/w PVC SLEEVE"),
    ]
    # table sits below the frame (as in the reference sheets)
    sh.table(0, tbl_top, W, rows, blank=0)

    y_base = tbl_top            # drawing area above table
    o = (10000, y_base + 5600)

    # ---------------- isometric scene ----------------
    # floor slab
    fl = [(-2300, -900, 0), (1100, -900, 0), (1100, 1000, 0), (-2300, 1000, 0)]
    sh.fill([I(p) for p in fl], rgb=(245, 245, 245))
    ipl(fl, "TID-CONCRETE", closed=True)
    # back wall (brick) behind the column
    wall = [(-2300, 1000, 0), (1100, 1000, 0), (1100, 1000, 2400), (-2300, 1000, 2400)]
    sh.fill([I(p) for p in wall], rgb=(250, 250, 250))
    sh.fill([I(p) for p in wall], color=8, pattern="BRICK", scale=40, angle=0)
    ipl(wall, "TID-WALL", closed=True)
    ipl([(-2300, 1000, 0), (-2300, 1100, 0), (-2300, 1100, 2400), (-2300, 1000, 2400)], "TID-WALL")

    # concrete pedestal + base plate
    ibox((-350, -350, 0), (350, 350, 300), GREY, "TID-CONCRETE")
    ibox((-230, -230, 300), (230, 230, 325), STEEL, "TID-STRUCTURE")
    for bx, by in ((-180, -180), (180, -180), (-180, 180)):
        ibox((bx - 15, by - 15, 325), (bx + 15, by + 15, 360), GREY, "TID-STRUCTURE")

    # H-beam column 300x300 (flanges normal to X) - back to front painter order
    top = 2650
    ibox((135, -150, 325), (150, 150, top), STEEL, "TID-STRUCTURE")
    ibox((-135, -6, 325), (135, 6, top), STEEL, "TID-STRUCTURE")
    ibox((-150, -150, 325), (-135, 150, top), STEEL, "TID-STRUCTURE")
    # break line on top
    a, b = I((-150, -150, top)), I((-150, 150, top))
    m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    sh.pl([a, (m[0] - 60, m[1] + 120), (m[0] + 60, m[1] - 120), b], "TID-STRUCTURE")

    # grounding boss plate welded on flange outer face (x = -150)
    ibox((-160, -30, 500), (-150, 30, 620), COPPER, "ELC-GROUNDING")
    # compression lug 35 mm² bolted on boss
    ibox((-172, -16, 520), (-160, 16, 600), COPPER, "ELC-GROUNDING")
    c = I((-172, 0, 572))
    sh.circle(c, 22, "ELC-GROUNDING")
    ibox((-176, -12, 440), (-164, 12, 520), COPPER, "ELC-GROUNDING")   # lug barrel

    # earthing box on wall
    bx0, bx1, bz0, bz1 = -2050, -1550, 450, 800
    ibox((bx0, 860, bz0), (bx1, 1000, bz1), BOX, "TID-E-CONTROL BOX")
    # cover screws + nameplate
    for px, pz in ((bx0 + 30, bz0 + 30), (bx1 - 30, bz0 + 30), (bx0 + 30, bz1 - 30), (bx1 - 30, bz1 - 30)):
        sh.circle(I((px, 860, pz)), 12, "TID-E-CONTROL BOX")
    ipl([(bx0 + 150, 860, bz1 - 110), (bx1 - 150, 860, bz1 - 110), (bx1 - 150, 860, bz1 - 60),
         (bx0 + 150, 860, bz1 - 60)], "TID-E-CONTROL BOX", closed=True)
    # cable glands at bottom
    for gx in (bx1 - 110, bx0 + 110):
        ibox((gx - 18, 912, bz0 - 45), (gx + 18, 948, bz0), GREY, "TID-E-CONTROL BOX")

    # BC 35 mm² bonding: box -> floor -> column
    gx = bx1 - 110
    path35 = [(gx, 930, bz0 - 45), (gx, 930, 60), (gx, 930, 25), (gx, 0, 25), (-360, 0, 25),
              (-360, 0, 300), (-360, 0, 330), (-170, 0, 360), (-170, 0, 440)]
    ipl(path35, "ELC-GROUNDING", width=45)
    # cable cleats along the floor / pedestal
    cleats = [(gx, 600, 25), (gx, 200, 25), (-1200, 0, 25), (-800, 0, 25), (-360, 0, 180)]
    for p in cleats:
        x, y, z = p
        if y not in (0,):
            ibox((x - 25, y - 15, 0), (x + 25, y + 15, 45), GREY, "TID-E-SUPPORT")
        elif z > 50:
            ibox((x - 15, -25, z - 15), (x, 25, z + 15), GREY, "TID-E-SUPPORT")
        else:
            ibox((x - 15, -25, 0), (x + 15, 25, 45), GREY, "TID-E-SUPPORT")

    # BC 70 mm² from box to main grounding grid (through PVC sleeve in slab)
    gx2 = bx0 + 110
    ipl([(gx2, 930, bz0 - 45), (gx2, 930, 0)], "ELC-GROUNDING-70", width=60)
    ipl([(gx2, 930, 0), (gx2, 930, -600)], "TID-HIDDEN", width=0)
    ibox((gx2 - 30, 900, 0), (gx2 + 30, 960, 40), GREY, "TID-E-CONDUIT")

    # ---------------- annotation (iso scene) ----------------
    sh.label(I((150, -150, 2100)), (I((150, -150, 2100))[0] + 900, I((150, -150, 2100))[1] + 500),
             ["PIPE RACK COLUMN", "(EXISTING STRUCTURE)"], side=1)
    sh.label(I((-350, -350, 150)), (I((-350, -350, 150))[0] + 1500, I((-350, -350, 150))[1] - 1500),
             ["CONCRETE PEDESTAL"], side=1)
    sh.label(I((-2300, 1000, 2400)), (I((-2300, 1000, 2400))[0] - 300, I((-2300, 1000, 2400))[1] + 500),
             ["WALL"], side=-1)
    sh.label(I((-400, -900, 0)), (I((-400, -900, 0))[0] - 600, I((-400, -900, 0))[1] - 700),
             ["FFL"], side=-1)
    sh.label(I((gx2, 930, -500)), (I((gx2, 930, -500))[0] + 300, I((gx2, 930, -500))[1] - 900),
             ["TO MAIN", "GROUNDING GRID"], side=1)
    # balloons
    lug = I((-172, 0, 565))
    sh.balloon(lug, (lug[0] + 1500, lug[1] + 900), 1)
    cab = I((-1000, 0, 25))
    sh.balloon(cab, (cab[0] + 600, cab[1] - 1400), 2)
    ebx = I((bx0 + 60, 860, (bz0 + bz1) / 2))
    sh.balloon(ebx, (ebx[0] - 1300, ebx[1] + 300), 3)
    boss = I((-160, 25, 615))
    sh.balloon(boss, (boss[0] + 1500, boss[1] + 1700), 4)
    clt = I((gx, 200, 45))
    sh.balloon(clt, (clt[0] - 1200, clt[1] - 900), 5)
    b70 = I((gx2, 930, 200))
    sh.balloon(b70, (b70[0] - 1300, b70[1] - 300), 6)
    # detail-A marker circle around lug
    sh.circle(I((-165, 0, 540)), 420, "TID-E-TITLE")
    ctr = I((-165, 0, 540))
    sh.text("DETAIL-A", (ctr[0] - 2200, ctr[1] + 1500), layer="TID-E-TITLE")
    sh.line((ctr[0] - 1150, ctr[1] + 1450), (ctr[0] - 300, ctr[1] + 290), "TID-E-TITLE")

    sh.title("DETAIL (CONNECTION EARTH BOX TO PIPE RACK) - ISOMETRIC", (1200, y_base + 800))

    # ---------------- DETAIL-A : lug connection (side section, 2D) ----------------
    k, ax, az = 22.0, 22600, y_base + 11800      # scale factor, origin

    def A(x, z):
        return (ax + x * k, az + (z - 560) * k)

    # column flange (section)
    fl_pts = [A(0, 390), A(15, 390), A(15, 710), A(0, 710)]
    sh.fill(fl_pts, rgb=STEEL[1])
    sh.fill(fl_pts, color=7, pattern="ANSI31", scale=10)
    sh.pl(fl_pts, "TID-STRUCTURE", closed=True)
    sh.pl([A(15, 390), A(60, 390)], "TID-STRUCTURE")
    sh.pl([A(15, 710), A(60, 710)], "TID-STRUCTURE")
    # boss plate
    bp = [A(-10, 500), A(0, 500), A(0, 620), A(-10, 620)]
    sh.fill(bp, rgb=COPPER[1])
    sh.pl(bp, "ELC-GROUNDING", closed=True)
    for zz, sgn in ((500, -1), (620, 1)):           # fillet welds
        sh.fill([A(0, zz), A(-8, zz), A(0, zz + sgn * 8)], rgb=(40, 40, 40))
    # lug tongue + barrel
    lt = [A(-16, 520), A(-10, 520), A(-10, 600), A(-16, 600)]
    sh.fill(lt, rgb=COPPER[0])
    sh.pl(lt, "ELC-GROUNDING", closed=True)
    br = [A(-20, 430), A(-6, 430), A(-10, 520), A(-16, 520)]
    sh.fill(br, rgb=COPPER[0])
    sh.pl(br, "ELC-GROUNDING", closed=True)
    for zz in (455, 480, 505):                          # crimp marks
        sh.line(A(-19, zz), A(-7, zz), "ELC-GROUNDING")
    # cable
    sh.pl([A(-13, 430), A(-13, 410), A(-22, 395), A(-55, 390)], "ELC-GROUNDING", width=10 * k)
    # bolt / washers / nut
    sh.pl([A(-30, 566), A(15, 566), A(15, 578), A(-30, 578)], "TID-STRUCTURE", closed=True)  # tapped hole bolt
    for x0, x1, dz in ((-19, -16, 14), (-22, -19, 12), (-30, -22, 10)):
        sh.fill([A(x0, 572 - dz), A(x1, 572 - dz), A(x1, 572 + dz), A(x0, 572 + dz)], rgb=(170, 170, 170))
        sh.pl([A(x0, 572 - dz), A(x1, 572 - dz), A(x1, 572 + dz), A(x0, 572 + dz)], "TID-STRUCTURE", closed=True)
    sh.line(A(-40, 572), A(25, 572), "TID-HIDDEN")

    sh.label(A(-28, 582), (A(-28, 582)[0] - 1200, A(-28, 582)[1] + 300),
             ["BOLT M10 c/w NUT,", "FLAT & LOCK WASHER (SS)"], side=-1)
    sh.label(A(-13, 600), (A(-13, 600)[0] - 900, A(-13, 600)[1] + 1500),
             ["COMPRESSION LUG 35mm²"], side=-1)
    sh.label(A(-5, 615), (A(-5, 615)[0] + 900, A(-5, 615)[1] + 2600),
             ["GROUNDING BOSS 60x100x10mm", "WELDED ALL AROUND"], side=1)
    sh.label(A(-50, 392), (A(-50, 392)[0] - 300, A(-50, 392)[1] - 900),
             ["BC 35mm² TO EARTHING BOX"], side=-1)
    sh.label(A(15, 420), (A(15, 430)[0] + 700, A(15, 430)[1] - 600),
             ["PIPE RACK COLUMN", "(TOUCH UP PAINT AFTER WELD)"], side=1)
    sh.text("%%UDETAIL - A", (ax - 900, y_base + 16000), h=300)

    # ---------------- EARTHING BOX : front elevation (inside, cover removed) ----------------
    k2, ex, ez = 6.0, 18800, y_base + 2000
    E = lambda x, z: (ex + x * k2, ez + z * k2)      # noqa: E731
    sh.fill([E(0, 0), E(500, 0), E(500, 350), E(0, 350)], rgb=BOX[0])
    sh.rect(*E(0, 0), *E(500, 350), "TID-E-CONTROL BOX")
    sh.rect(*E(12, 12), *E(488, 338), "TID-E-CONTROL BOX")
    # busbar on 2 insulators
    for ix in (100, 400):
        sh.fill([E(ix - 18, 160), E(ix + 18, 160), E(ix + 18, 215), E(ix - 18, 215)], rgb=(210, 60, 60))
        sh.rect(*E(ix - 18, 160), *E(ix + 18, 215), "TID-E-CONTROL BOX")
    bb = [E(70, 215), E(430, 215), E(430, 265), E(70, 265)]
    sh.fill(bb, rgb=COPPER[1])
    sh.pl(bb, "ELC-GROUNDING", closed=True)
    for hx in range(110, 400, 45):
        sh.circle(E(hx, 240), 5 * k2, "ELC-GROUNDING")
    # connected lugs + cables down to glands
    for lx, lay, w in ((390, "ELC-GROUNDING", 12), (110, "ELC-GROUNDING-70", 16)):
        sh.fill([E(lx - 12, 180), E(lx + 12, 180), E(lx + 12, 240), E(lx - 12, 240)], rgb=COPPER[0])
        sh.rect(*E(lx - 12, 180), *E(lx + 12, 240), lay)
        sh.pl([E(lx, 180), E(lx, 0), E(lx, -45)], lay, width=w * k2 * 0.5)
        sh.rect(*E(lx - 20, -45), *E(lx + 20, 0), "TID-E-CONTROL BOX")
    sh.dim_h(ez + 350 * k2 + 250, ex, ex + 500 * k2, "500")
    sh.dim_v(ex - 350, ez, ez + 350 * k2, "350")
    sh.label(E(250, 262), (E(250, 262)[0] + 500, E(250, 262)[1] + 700), ["COPPER BUSBAR 300x50x6mm"], side=1)
    sh.label(E(418, 190), (E(418, 190)[0] + 1300, E(418, 190)[1] - 300), ["INSULATOR"], side=1)
    sh.label(E(390, -30), (E(390, -30)[0] + 600, E(390, -30)[1] - 700), ["BC 35mm² TO PIPE RACK"], side=1)
    sh.label(E(110, -30), (E(110, -30)[0] - 300, E(110, -30)[1] - 700), ["BC 70mm² TO GRID"], side=-1)
    sh.text("%%UEARTHING BOX (COVER REMOVED)", (ex + 300, ez - 1500), h=250)
    return sh


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "TYPICAL_DETAIL_EARTH_BOX_TO_PIPE_RACK.dxf"
    doc = new_doc()
    draw(doc)
    doc.saveas(out)
    print("saved", out)
