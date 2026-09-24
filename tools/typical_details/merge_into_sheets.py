"""Merge the new typical details into the existing sheets (DWG converted to DXF with LibreDWG dwg2dxf).

  python3 merge_into_sheets.py <grounding_5002.dxf> <cable_tray_5001.dxf> <out_dir>
The new detail is placed to the right of the existing drawing extents in model space.
"""
import os
import sys
import ezdxf  # noqa: F401
from ezdxf import recover
from ezdxf import bbox
from dxfkit import prepare
import earth_box_pipe_rack
import trunking_highbay


def merge(src, out, draw, gap_ratio=0.05, align_top=True, height=None):
    doc, _ = recover.readfile(src)
    for m in ("ByLayer", "ByBlock", "Global"):   # LibreDWG output may lack default materials
        if not hasattr(doc.materials.get(m), "dxf"):
            try:
                doc.materials.remove(m)
            except Exception:
                pass
            doc.materials.new(m)
    prepare(doc)
    ext = bbox.extents(doc.modelspace(), fast=True)
    gap = (ext.extmax.x - ext.extmin.x) * gap_ratio
    ox = ext.extmax.x + gap
    oy = ext.extmax.y - height if (align_top and height) else ext.extmin.y
    draw(doc, ox, oy)
    doc.saveas(out)
    print("saved", out, "at", round(ox), round(oy))


if __name__ == "__main__":
    g, c, od = sys.argv[1:4]
    merge(g, os.path.join(od, "ME-F-EG-5002_R0_TYPICAL_DETAIL_GROUNDING_INSTALLATION.dxf"),
          earth_box_pipe_rack.draw, height=earth_box_pipe_rack.H + 3900)
    merge(c, os.path.join(od, "ME-F-EP-5001_R0_TYPICAL_DETAIL_CABLE_TRAY_INSTALLATION.dxf"),
          trunking_highbay.draw, height=trunking_highbay.CH)
