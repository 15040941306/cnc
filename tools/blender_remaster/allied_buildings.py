"""
allied_buildings.py - Westwood 3ds-Max-faithful Allied 3D structures with corrugated steel,
open lattice crane booms, spoked turbofans, handrails, shutter doors, and Allied Eagle crests.
"""

from __future__ import annotations
import math
from common import PartCollector, create_pivot, create_root


def build_gacnst():
    """Allied Construction Yard (8m x 8m) matched 1:1 to Sprites/GACNST.png."""
    root = create_root("GACNST")
    pc = PartCollector(is_soviet=False)

    # Paved concrete apron + elevated left crane deck + stairs & safety railings
    pc.add_box((0.0, 0.12, 0.0), (7.6, 0.24, 7.6), "Mat_Concrete", bevel=0.05)
    pc.add_box((-2.25, 0.36, 0.45), (2.75, 0.28, 3.85), "Mat_Concrete", bevel=0.04)
    for st in range(4):
        pc.add_box((-3.25 + st * 0.14, 0.18 + st * 0.06, -1.65), (0.55, 0.08, 0.28), "Mat_BrushedSteel", bevel=0.005)
    pc.add_railing((-3.52, 0.50, -1.35), (-3.52, 0.50, 2.25), height=0.28, posts=6)
    pc.add_railing((-3.52, 0.50,  2.25), (-1.05, 0.50, 2.25), height=0.28, posts=4)

    # Front-left recessed assembly bay with 45-deg hazard stripes & twin steel I-beam rails
    bay_x, bay_z = -0.85, -1.85
    pc.add_decal_panel((bay_x, 0.25, bay_z), 2.55, 2.55, 0.04, "Mat_HazardYellow", rot_deg=(90.0, 0.0, 0.0))
    pc.add_box((bay_x, 0.26, bay_z), (2.15, 0.04, 2.15), "Mat_DarkSteel", bevel=0.01)
    for rx in (-0.52, 0.52):
        pc.add_box((bay_x + rx, 0.30, bay_z), (0.14, 0.08, 2.25), "Mat_BrushedSteel", bevel=0.01)

    # Iconic Allied Crane: Geared Blue Base + Violet/Blue Energy Column + Open Yellow Lattice Boom + 2-Jaw Claw!
    cr_x, cr_z = -2.25, -0.25
    pc.add_cylinder((cr_x, 0.64, cr_z), 0.84, 0.72, 0.32, "Mat_TeamPaint", segments=16)
    for g in range(8):
        ga = math.radians(g * 45.0)
        pc.add_box((cr_x + math.cos(ga) * 0.82, 0.58, cr_z + math.sin(ga) * 0.82), (0.16, 0.18, 0.16), "Mat_TeamPaint", bevel=0.01)
    pc.add_cylinder((cr_x, 1.32, cr_z), 0.44, 0.44, 1.08, "Mat_GlowPlasma", segments=18)
    for post_a in (45, 135, 225, 315):
        pa = math.radians(post_a)
        pc.add_cylinder((cr_x + math.cos(pa) * 0.50, 1.32, cr_z + math.sin(pa) * 0.50), 0.05, 0.05, 1.08, "Mat_HazardYellow", segments=8)

    pc.add_box((cr_x, 1.98, cr_z), (1.18, 0.36, 1.18), "Mat_HazardYellow", bevel=0.03)
    pc.add_cylinder((cr_x, 2.34, cr_z), 0.58, 0.52, 0.36, "Mat_TeamPaint", segments=18)
    pc.add_sphere((cr_x, 2.54, cr_z), 0.44, "Mat_GlassDark", scale=(1.0, 0.45, 1.0))

    # Open 3D Lattice Crane Boom & Mechanical Grabber Claw!
    boom_tip = (bay_x - 0.05, 1.68, bay_z + 0.15)
    pc.add_lattice_boom((cr_x + 0.35, 2.04, cr_z - 0.30), boom_tip, width=0.36, height=0.32, bays=6, mat="Mat_HazardYellow")
    pc.add_cylinder((boom_tip[0], 1.32, boom_tip[2]), 0.03, 0.03, 0.68, "Mat_DarkSteel", segments=8)
    pc.add_box((boom_tip[0], 0.96, boom_tip[2]), (0.38, 0.20, 0.28), "Mat_HazardYellow", bevel=0.02)
    for jx in (-0.16, 0.16):
        pc.add_box((boom_tip[0] + jx, 0.72, boom_tip[2]), (0.08, 0.34, 0.14), "Mat_DarkSteel", bevel=0.01, rot_deg=(0.0, 0.0, -14.0 if jx < 0 else 14.0))

    # Twin Corrugated Silver-White Quonset Hangars + Shutter Door + Allied Eagle Crest!
    pc.add_box((1.45, 0.58, -0.45), (2.75, 0.70, 3.55), "Mat_BrushedSteel", bevel=0.03)
    pc.add_quonset_arch((1.45, 0.90, -0.45), width=2.72, arch_height=1.28, length=3.52, mat_shell="Mat_CorrugatedSteel", mat_ribs="Mat_BrushedSteel", rib_count=5)
    pc.add_decal_panel((1.45, 0.95, -2.23), 1.72, 1.35, 0.08, "Mat_ShutterDoor")
    pc.add_decal_panel((1.45, 1.78, -2.26), 0.92, 0.46, 0.10, "Mat_AlliedCrest")
    for wz in (-1.35, -0.50, 0.35):
        pc.add_box((2.83, 0.68, wz), (0.08, 0.42, 0.48), "Mat_GlassDark", bevel=0.01)
        pc.add_box((0.95, 2.08, wz), (0.48, 0.10, 0.48), "Mat_TeamPaint", bevel=0.01)

    pc.add_box((2.18, 0.55, 1.65), (2.25, 0.64, 2.55), "Mat_BrushedSteel", bevel=0.03)
    pc.add_quonset_arch((2.18, 0.85, 1.65), width=2.22, arch_height=1.12, length=2.52, mat_shell="Mat_CorrugatedSteel", mat_ribs="Mat_TeamPaint", rib_count=4)

    # Twin Spoked Rooftop Turbofan Silos + Swept Curved Blue/Concrete Deflector Sails!
    pc.add_spoked_fan((-0.45, 0.24, 1.25), radius=0.72, height=1.58, blades=7)
    pc.add_spoked_fan(( 0.72, 0.24, 1.65), radius=0.68, height=1.72, blades=7)

    for fx in (-1.35, -0.05):
        pc.add_box((fx, 1.88, 2.65), (0.30, 3.28, 0.92), "Mat_Concrete", bevel=0.03, top_scale=(0.82, 0.58))
        pc.add_box((fx + 0.07, 1.82, 2.48), (0.24, 2.98, 0.72), "Mat_TeamPaint", bevel=0.02, top_scale=(0.82, 0.52))
    pc.add_cylinder((-0.70, 2.72, 2.62), 0.055, 0.055, 1.32, "Mat_BrushedSteel", segments=8, axis='X')

    # Clustered Steel Fuel Drums
    for ox, oz in ((0.42, -2.52), (0.70, -2.42), (0.56, -2.78), (2.88, -1.95), (3.12, -1.65), (2.98, -1.35)):
        pc.add_cylinder((ox, 0.44, oz), 0.14, 0.14, 0.40, "Mat_DarkSteel", segments=12)
        pc.add_torus((ox, 0.44, oz), 0.142, 0.015, "Mat_BrushedSteel")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_gapowr():
    """Allied Power Plant (4m x 4m) matched 1:1 to Sprites/GAPOWR.png: Brass/gold base & towers + blue shields + green core."""
    root = create_root("GAPOWR")
    pc = PartCollector(is_soviet=False)

    # Scalloped warm concrete/brass apron with radiating blue cable conduits
    pc.add_cylinder((0.0, 0.11, 0.0), 1.90, 1.76, 0.22, "Mat_Concrete", segments=20)
    pc.add_cylinder((0.0, 0.28, 0.0), 1.56, 1.44, 0.16, "Mat_GoldBrass", segments=18)
    for ang in range(0, 360, 40):
        ra = math.radians(ang)
        pc.add_box((math.cos(ra) * 1.58, 0.22, math.sin(ra) * 1.58), (0.14, 0.12, 0.36), "Mat_TeamPaint", bevel=0.01, rot_deg=(0.0, -ang, 0.0))

    # Central turbine drum + Two large curved Allied Blue armor shields framed in Gold/Brass!
    pc.add_cylinder((0.0, 0.78, 0.0), 1.32, 1.24, 0.92, "Mat_DarkSteel", segments=20)
    for side, ang_y in ((-1, 38.0), (1, -38.0)):
        sx, sz = side * 0.92, -0.68
        pc.add_box((sx, 0.80, sz), (1.14, 0.98, 0.42), "Mat_GoldBrass", bevel=0.04, rot_deg=(0.0, ang_y, 0.0))
        pc.add_box((sx * 1.03, 0.80, sz * 1.03), (0.98, 0.76, 0.44), "Mat_TeamPaint", bevel=0.03, rot_deg=(0.0, ang_y, 0.0))

    # Central glowing green/teal plasma turbine basin
    pc.add_cylinder((0.0, 1.28, -0.08), 0.72, 0.66, 0.20, "Mat_BrushedSteel", segments=18)
    pc.add_cylinder((0.0, 1.38, -0.08), 0.54, 0.54, 0.08, "Mat_GlowGreen", segments=18)

    # Three staggered Brass/Gold cooling towers with vertical glowing green slots & Allied Blue side collars!
    towers = [
        (-0.84, 2.28,  0.18, 0.42),
        ( 0.06, 2.82,  0.84, 0.44),
        ( 0.86, 2.08, -0.04, 0.40),
    ]
    for tx, th, tz, tr in towers:
        pc.add_cylinder((tx, 0.30 + th * 0.45, tz), tr * 1.06, tr * 0.94, th * 0.90, "Mat_GoldBrass", segments=18)
        pc.add_box((tx + (0.14 if tx >= 0 else -0.14), 0.30 + th * 0.62, tz - 0.12), (tr * 1.55, 0.42, tr * 1.25), "Mat_TeamPaint", bevel=0.03)
        pc.add_sphere((tx, 0.30 + th * 0.90, tz), tr * 0.94, "Mat_GoldBrass", scale=(1.0, 0.68, 1.0), segments=16)
        for slot_ang in (0, 60, 120, 180, 240, 300):
            r_a = math.radians(slot_ang)
            pc.add_box((tx + math.cos(r_a) * tr * 0.96, 0.30 + th * 0.34, tz + math.sin(r_a) * tr * 0.96), (0.06, th * 0.44, 0.06), "Mat_DarkSteel", bevel=0.004)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    spin = create_pivot("SpinPivot", (0.0, 1.44, -0.08), parent=root)
    spc = PartCollector(is_soviet=False)
    for b_ang in (0, 45, 90, 135):
        spc.add_box((0.0, 0.0, 0.0), (0.92, 0.04, 0.12), "Mat_BrushedSteel", bevel=0.004, rot_deg=(0.0, b_ang, 15.0))
    spc.add_sphere((0.0, 0.02, 0.0), 0.16, "Mat_GlowGreen", segments=12)
    spc.build_mesh_object("TurbineBlades", (0.0, 1.44, -0.08), parent=spin)
    return root


def build_garefn():
    """Allied Ore Refinery (8m x 6m) matched 1:1 to Sprites/GAREFN.png."""
    root = create_root("GAREFN")
    pc = PartCollector(is_soviet=False)

    pc.add_box((0.0, 0.12, 0.0), (7.6, 0.24, 5.6), "Mat_Concrete", bevel=0.05)

    # Two tall tapered hyperboloid cooling/smelting stacks with radiating base pipes & fluted crowns!
    for sx, sz, sh in ((-2.15, -0.45, 3.25), (-0.48, 1.38, 3.68)):
        pc.add_cylinder((sx, 0.46, sz), 1.22, 0.94, 0.44, "Mat_BrushedSteel", segments=20)
        for p_ang in range(0, 360, 45):
            pa = math.radians(p_ang)
            pc.add_pipe_run([
                (sx + math.cos(pa) * 0.85, 0.52, sz + math.sin(pa) * 0.85),
                (sx + math.cos(pa) * 1.32, 0.26, sz + math.sin(pa) * 1.32),
            ], radius=0.065, flanges=False)
        pc.add_torus((sx, 0.72, sz), 0.80, 0.10, "Mat_TeamPaint")
        profile = [
            (0.76, 0.00),
            (0.54, sh * 0.36),
            (0.46, sh * 0.68),
            (0.62, sh * 0.88),
            (0.56, sh * 1.00),
        ]
        pc.add_lathe_profile((sx, 0.72, sz), profile, "Mat_BrushedSteel", segments=24, flute_amp=0.025, flute_count=16)
        pc.add_cylinder((sx, 0.72 + sh - 0.04, sz), 0.42, 0.42, 0.12, "Mat_TreadRubber", segments=16)

    # Central Allied Blue spine arch with 3 silver pressure domes
    pc.add_box((1.25, 1.08, -0.15), (0.88, 1.72, 3.75), "Mat_TeamPaint", bevel=0.05, rot_deg=(0.0, 90.0, 0.0))
    for dx in (-0.15, 1.15, 2.45):
        pc.add_torus((dx, 1.94, -0.15), 0.36, 0.05, "Mat_DarkSteel")
        pc.add_sphere((dx, 1.98, -0.15), 0.34, "Mat_BrushedSteel", scale=(1.0, 0.55, 1.0), segments=14)

    # Flanking corrugated arched processing vaults + front hydraulic piston & hazard ore ramp
    pc.add_quonset_arch((1.15, 0.24, -1.35), width=2.55, arch_height=1.38, length=1.68, mat_shell="Mat_CorrugatedSteel", mat_ribs="Mat_TeamPaint", rib_count=4)
    pc.add_quonset_arch((1.65, 0.24,  1.05), width=2.45, arch_height=1.32, length=1.58, mat_shell="Mat_CorrugatedSteel", mat_ribs="Mat_BrushedSteel", rib_count=4)

    pc.add_decal_panel((1.15, 0.26, -2.38), 2.25, 0.88, 0.05, "Mat_HazardYellow", rot_deg=(90.0, 0.0, 0.0))
    pc.add_box((2.35, 0.92, -1.05), (0.48, 0.34, 0.65), "Mat_BrushedSteel", bevel=0.02)
    pc.add_cylinder((2.52, 0.58, -1.05), 0.14, 0.14, 0.75, "Mat_DarkSteel", segments=12)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_gapile():
    """Allied Barracks (4m x 4m) matched 1:1 to Sprites/GAPILE.png."""
    root = create_root("GAPILE")
    pc = PartCollector(is_soviet=False)

    pc.add_box((0.0, 0.10, 0.0), (3.8, 0.20, 3.8), "Mat_Concrete", bevel=0.04)

    for bx, bz in ((-0.88, 0.18), (0.88, -0.18)):
        pc.add_box((bx, 0.34, bz), (1.48, 0.28, 2.48), "Mat_TeamPaint", bevel=0.02)
        pc.add_quonset_arch((bx, 0.42, bz), width=1.44, arch_height=0.98, length=2.45, mat_shell="Mat_CorrugatedSteel", mat_ribs="Mat_TeamPaint", rib_count=4)
        pc.add_decal_panel((bx, 0.68, bz - 1.24), 0.68, 0.90, 0.08, "Mat_ShutterDoor")
        pc.add_decal_panel((bx, 0.21, bz - 1.54), 0.85, 0.42, 0.03, "Mat_HazardYellow", rot_deg=(90.0, 0.0, 0.0))
        for wz in (-0.68, 0.0, 0.68):
            s_out = 0.72 if bx > 0 else -0.72
            pc.add_torus((bx + s_out, 0.68, bz + wz), 0.13, 0.03, "Mat_BrushedSteel", rot_deg=(0.0, 90.0, 0.0))
            pc.add_sphere((bx + s_out, 0.68, bz + wz), 0.11, "Mat_GlowCyan", segments=10)

    # Rear watchtower + railings + radio mast
    tx, tz = -0.88, 1.32
    pc.add_cylinder((tx, 1.40, tz), 0.36, 0.34, 1.95, "Mat_BrushedSteel", segments=14)
    pc.add_cylinder((tx, 2.38, tz), 0.50, 0.44, 0.26, "Mat_TeamPaint", segments=16)
    pc.add_cylinder((tx, 2.60, tz), 0.44, 0.46, 0.24, "Mat_GlassDark", segments=16)
    pc.add_sphere((tx, 2.76, tz), 0.48, "Mat_BrushedSteel", scale=(1.0, 0.45, 1.0), segments=16)
    pc.add_cylinder((tx, 3.55, tz), 0.035, 0.012, 1.45, "Mat_DarkSteel", segments=8)

    for ox, oz in ((1.48, -1.52), (1.66, -1.32), (1.35, -1.68)):
        pc.add_cylinder((ox, 0.36, oz), 0.12, 0.12, 0.34, "Mat_BrushedSteel", segments=10)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_gaweap():
    """Allied War Factory (10m x 6m) matched 1:1 to Sprites/GAWEAP.png."""
    root = create_root("GAWEAP")
    pc = PartCollector(is_soviet=False)

    pc.add_box((0.0, 0.12, 0.0), (9.6, 0.24, 5.6), "Mat_Concrete", bevel=0.05)

    # Main corrugated tank assembly hall + blue side skirt + 6 skylights + shutter portal + hazard ramp!
    pc.add_box((0.65, 0.56, -0.15), (4.65, 0.66, 4.45), "Mat_TeamPaint", bevel=0.03)
    pc.add_quonset_arch((0.65, 0.82, -0.15), width=4.45, arch_height=1.98, length=4.42, mat_shell="Mat_CorrugatedSteel", mat_ribs="Mat_BrushedSteel", rib_count=7)
    for sz in (-1.65, -1.0, -0.35, 0.30, 0.95, 1.60):
        pc.add_box((1.05, 2.72, sz), (0.98, 0.12, 0.42), "Mat_TeamPaint", bevel=0.015)
        pc.add_box((1.05, 2.76, sz), (0.82, 0.10, 0.32), "Mat_GlowCyan", bevel=0.01)

    pc.add_decal_panel((0.65, 1.18, -2.38), 2.88, 1.82, 0.12, "Mat_ShutterDoor")
    pc.add_decal_panel((0.65, 2.28, -2.42), 1.08, 0.48, 0.10, "Mat_AlliedCrest")
    pc.add_decal_panel((0.65, 0.26, -2.52), 2.65, 0.82, 0.04, "Mat_HazardYellow", rot_deg=(90.0, 0.0, 0.0))

    # Rear-left Command Tower + Twin Allied Blue Shield Fins + Spoked Cooling Vat + Pipeline + Crates!
    cx, cz = -2.65, 1.15
    pc.add_cylinder((cx, 1.38, cz), 1.06, 0.96, 2.25, "Mat_BrushedSteel", segments=20)
    pc.add_torus((cx, 1.98, cz), 1.02, 0.09, "Mat_TeamPaint")
    pc.add_sphere((cx, 2.54, cz), 0.94, "Mat_BrushedSteel", scale=(1.0, 0.62, 1.0), segments=18)
    for fx, fz, rot in ((cx - 1.08, cz - 0.35, 15.0), (cx + 0.55, cz + 0.98, -75.0)):
        pc.add_box((fx, 1.78, fz), (0.26, 2.88, 1.18), "Mat_TeamPaint", bevel=0.03, rot_deg=(0.0, rot, 0.0), top_scale=(0.8, 0.65))

    pc.add_spoked_fan((-2.58, 0.24, -0.88), radius=0.66, height=1.10, blades=6)
    pc.add_pipe_run([(-2.58, 0.72, -0.22), (-1.75, 0.72, -0.22), (-1.75, 0.58, -2.15)], radius=0.10)
    for bx, bz in ((-2.45, -1.85), (-2.05, -2.15), (-2.65, -2.25)):
        pc.add_box((bx, 0.44, bz), (0.38, 0.38, 0.38), "Mat_BrushedSteel", bevel=0.02)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_gaairc():
    """Allied Airforce Command HQ (6m x 4m) matched 1:1 to Sprites/GAAIRC.png."""
    root = create_root("GAAIRC")
    pc = PartCollector(is_soviet=False)

    pc.add_box((0.0, 0.12, 0.0), (5.6, 0.24, 3.7), "Mat_Concrete", bevel=0.04)

    for bx, bz in ((-1.68, -0.45), (1.68, 0.45)):
        pc.add_cylinder((bx, 0.96, bz), 0.94, 0.86, 1.45, "Mat_BrushedSteel", segments=18)
        pc.add_cylinder((bx, 1.16, bz), 0.96, 0.96, 0.32, "Mat_TeamPaint", segments=18)
        pc.add_cylinder((bx, 1.46, bz), 0.95, 0.95, 0.24, "Mat_GlassDark", segments=18)
        pc.add_railing((bx - 0.65, 1.68, bz - 0.65), (bx + 0.65, 1.68, bz - 0.65), height=0.22, posts=4)
        for ai in (-0.38, 0.0, 0.38):
            pc.add_cylinder((bx + ai, 2.38, bz), 0.035, 0.012, 1.42, "Mat_DarkSteel", segments=8)

    pc.add_cylinder((0.25, 0.96, -0.68), 0.66, 0.62, 1.45, "Mat_TeamPaint", segments=18)
    pc.add_sphere((0.25, 1.76, -0.68), 0.68, "Mat_BrushedSteel", scale=(1.0, 0.55, 1.0), segments=16)

    rx, rz = -0.35, 0.55
    pc.add_cylinder((rx, 1.38, rz), 0.96, 0.78, 2.28, "Mat_BrushedSteel", segments=20)
    pc.add_torus((rx, 2.18, rz), 0.84, 0.09, "Mat_TeamPaint")
    pc.add_cylinder((rx, 2.68, rz), 0.56, 0.56, 0.55, "Mat_GlassDark", segments=18)
    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    spin = create_pivot("SpinPivot", (rx, 3.08, rz), parent=root)
    rpc = PartCollector(is_soviet=False)
    rpc.add_cylinder((rx, 3.08, rz), 0.28, 0.22, 0.32, "Mat_DarkSteel", segments=14)
    rpc.add_box((rx, 3.44, rz), (1.95, 0.74, 0.20), "Mat_BrushedSteel", bevel=0.04, rot_deg=(-22.0, 0.0, 0.0), top_scale=(1.25, 1.0))
    rpc.add_box((rx, 3.42, rz - 0.08), (1.72, 0.56, 0.12), "Mat_TeamPaint", bevel=0.02, rot_deg=(-22.0, 0.0, 0.0))
    rpc.add_lattice_boom((rx, 3.42, rz - 0.10), (rx, 3.68, rz - 0.75), width=0.14, height=0.14, bays=3, mat="Mat_BrushedSteel")
    rpc.add_sphere((rx, 3.68, rz - 0.78), 0.08, "Mat_GlowCyan", segments=10)
    rpc.build_mesh_object("RadarDish", (rx, 3.08, rz), parent=spin)
    return root


def build_gatech():
    """Allied Battle Lab (6m x 4m) matched 1:1 to Sprites/GATECH.png."""
    root = create_root("GATECH")
    pc = PartCollector(is_soviet=False)

    pc.add_box((0.0, 0.12, 0.0), (5.5, 0.24, 3.7), "Mat_Concrete", bevel=0.04)
    pc.add_torus((0.0, 0.26, 0.0), 1.45, 0.06, "Mat_TeamPaint")

    pc.add_cylinder((0.0, 1.58, 0.0), 1.08, 0.94, 2.68, "Mat_DarkSteel", segments=20)
    pc.add_box((0.0, 1.68, -0.68), (1.18, 2.88, 0.96), "Mat_TeamPaint", bevel=0.05, top_scale=(0.75, 0.8))
    for hy in (0.48, 0.76):
        pc.add_box((0.0, hy, -1.16), (1.05, 0.18, 0.28), "Mat_TeamPaint", bevel=0.02)
    pc.add_decal_panel((0.0, 1.38, -1.26), 0.96, 0.68, 0.12, "Mat_AlliedCrest")

    # Twin cantilevered silver-white high-tech pods with glass observation ribs
    for sx in (-1.38, 1.38):
        pc.add_box((sx, 2.08, 0.05), (1.68, 1.08, 2.18), "Mat_CorrugatedSteel", bevel=0.08)
        pc.add_box((sx, 2.08, 0.05), (1.74, 0.28, 2.24), "Mat_GlassDark", bevel=0.02)
        pc.add_box((sx * 0.78, 2.86, 0.10), (0.98, 0.58, 1.28), "Mat_BrushedSteel", bevel=0.05)
        pc.add_sphere((sx * 0.78, 3.18, 0.10), 0.44, "Mat_BrushedSteel", scale=(1.0, 0.5, 1.0), segments=14)

    for mx, mz in ((-0.42, -0.15), (0.42, 0.25)):
        pc.add_cylinder((mx, 4.18, mz), 0.06, 0.02, 2.95, "Mat_DarkSteel", segments=10)
        pc.add_sphere((mx, 5.66, mz), 0.09, "Mat_GlowCyan", segments=10)
        for cy in (3.28, 3.52, 3.76):
            pc.add_torus((mx, cy, mz), 0.22, 0.055, "Mat_TeamPaint")
            pc.add_torus((mx, cy, mz), 0.14, 0.030, "Mat_GlowCyan")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_gapill():
    """Allied Pillbox (2m x 2m) matched 1:1 to Sprites/GAPILL.png."""
    root = create_root("GAPILL")
    pc = PartCollector(is_soviet=False)

    pc.add_cylinder((0.0, 0.12, 0.0), 0.96, 0.90, 0.24, "Mat_Concrete", segments=12)
    for ang in range(0, 360, 30):
        ra = math.radians(ang)
        pc.add_box((math.cos(ra) * 0.88, 0.26, math.sin(ra) * 0.88), (0.28, 0.18, 0.18), "Mat_Concrete", bevel=0.04, rot_deg=(0.0, -ang, 0.0))

    pc.add_cylinder((0.0, 0.48, 0.0), 0.74, 0.74, 0.28, "Mat_TreadRubber", segments=12)
    for ang in range(0, 360, 45):
        rad = math.radians(ang)
        pc.add_box((math.cos(rad) * 0.74, 0.48, math.sin(rad) * 0.74), (0.14, 0.30, 0.14), "Mat_BrushedSteel", bevel=0.01)

    pc.add_cylinder((0.0, 0.76, 0.0), 0.84, 0.64, 0.28, "Mat_BrushedSteel", segments=16)
    pc.add_cylinder((0.0, 0.92, 0.0), 0.46, 0.42, 0.10, "Mat_TeamPaint", segments=16)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_atesla():
    """Allied Prism Tower (2m x 2m) matched 1:1 to Cameos/ATESLA.png."""
    root = create_root("ATESLA")
    pc = PartCollector(is_soviet=False)

    pc.add_cylinder((0.0, 0.20, 0.0), 0.88, 0.64, 0.40, "Mat_DarkSteel", segments=16)
    pc.add_cylinder((0.0, 0.46, 0.0), 0.68, 0.58, 0.16, "Mat_TeamPaint", segments=16)
    pc.add_cylinder((0.0, 1.35, 0.0), 0.16, 0.16, 1.65, "Mat_GoldBrass", segments=14)
    pc.add_lattice_boom((0.0, 0.52, 0.0), (0.0, 2.15, 0.0), width=0.52, height=0.52, bays=4, mat="Mat_BrushedSteel")
    pc.add_cylinder((0.0, 2.22, 0.0), 0.56, 0.44, 0.24, "Mat_BrushedSteel", segments=16)
    pc.add_torus((0.0, 2.12, 0.0), 0.50, 0.06, "Mat_TeamPaint")
    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur = create_pivot("TurretPivot", (0.0, 2.38, 0.0), parent=root)
    tpc = PartCollector(is_soviet=False)
    tpc.add_cylinder((0.0, 2.52, 0.0), 0.28, 0.34, 0.28, "Mat_GoldBrass", segments=14)
    tpc.add_sphere((0.0, 2.96, 0.0), 0.30, "Mat_GlowCyan", scale=(1.0, 1.35, 1.0), segments=16)
    for i in range(6):
        ang = i * 60.0
        rad = math.radians(ang)
        px, pz = math.sin(rad) * 0.44, math.cos(rad) * 0.44
        tpc.add_box((px, 2.98, pz), (0.34, 0.74, 0.08), "Mat_BrushedSteel", bevel=0.015, rot_deg=(-10.0, ang, 0.0))
    tpc.build_mesh_object("PrismHead", (0.0, 2.38, 0.0), parent=tur)
    return root


ALLIED_BUILDINGS = {
    "GACNST": (build_gacnst, 8.8),
    "GAPOWR": (build_gapowr, 4.8),
    "GAREFN": (build_garefn, 8.2),
    "GAPILE": (build_gapile, 4.8),
    "GAWEAP": (build_gaweap, 9.6),
    "GAAIRC": (build_gaairc, 6.2),
    "GATECH": (build_gatech, 6.8),
    "GAPILL": (build_gapill, 2.6),
    "ATESLA": (build_atesla, 4.2),
}
