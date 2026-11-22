"""
allied_buildings.py - High-detail 3D hard-surface Allied structures modeled after original RA2 sprites.
Footprints in Unity meters (1 cell = 2.0m):
  GACNST: 4x4 (8x8m), GAPOWR: 2x2 (4x4m), GAREFN: 4x3 (8x6m), GAPILE: 2x2 (4x4m),
  GAWEAP: 5x3 (10x6m), GAAIRC: 3x2 (6x4m), GATECH: 3x2 (6x4m), GAPILL: 1x1 (2x2m), ATESLA: 1x1 (2x2m)
"""

from __future__ import annotations
import math
from common import PartCollector, create_pivot, create_root


def build_gacnst():
    """Allied Construction Yard (4x4 = 8m x 8m): Yellow crane + assembly rails + twin arched hangars + blue rear fins."""
    root = create_root("GACNST")
    pc = PartCollector(is_soviet=False)

    # Tiered industrial concrete slab
    pc.add_box((0.0, 0.12, 0.0), (7.5, 0.24, 7.5), "Mat_Concrete", bevel=0.06)
    pc.add_box((-2.2, 0.34, 0.8), (2.8, 0.22, 3.6), "Mat_Concrete", bevel=0.04)

    # Front-left assembly bay with yellow hazard perimeter & twin steel rails
    bay_x, bay_z = -1.0, -1.7
    pc.add_box((bay_x, 0.25, bay_z), (2.6, 0.04, 2.6), "Mat_DarkSteel", bevel=0.01)
    for dx in (-1.25, 1.25):
        pc.add_box((bay_x + dx, 0.27, bay_z), (0.12, 0.04, 2.6), "Mat_HazardYellow", bevel=0.005)
    for dz in (-1.25, 1.25):
        pc.add_box((bay_x, 0.27, bay_z + dz), (2.6, 0.04, 0.12), "Mat_HazardYellow", bevel=0.005)
    for rx in (-0.48, 0.48):
        pc.add_box((bay_x + rx, 0.29, bay_z), (0.14, 0.07, 2.3), "Mat_BrushedSteel", bevel=0.01)

    # Iconic Allied Yellow Crane Tower (left)
    cr_x, cr_z = -2.25, -0.15
    pc.add_cylinder((cr_x, 0.60, cr_z), 0.78, 0.68, 0.32, "Mat_TeamPaint", segments=16)
    pc.add_cylinder((cr_x, 1.25, cr_z), 0.50, 0.50, 1.05, "Mat_GlassDark", segments=16)
    pc.add_box((cr_x, 1.92, cr_z), (1.15, 0.42, 1.15), "Mat_HazardYellow", bevel=0.04)
    pc.add_cylinder((cr_x, 2.32, cr_z), 0.56, 0.52, 0.38, "Mat_TeamPaint", segments=16)
    pc.add_sphere((cr_x, 2.52, cr_z), 0.42, "Mat_GlassDark", scale=(1.0, 0.45, 1.0))

    # Crane Boom & Pincer Claw suspended over assembly bay
    pc.add_box((cr_x + 0.75, 2.02, cr_z - 0.78), (0.34, 0.30, 2.15), "Mat_HazardYellow", bevel=0.02, rot_deg=(14.0, 138.0, 0.0))
    claw_x, claw_z = bay_x - 0.15, bay_z + 0.25
    pc.add_cylinder((claw_x, 1.38, claw_z), 0.04, 0.04, 0.75, "Mat_DarkSteel", segments=8)
    pc.add_box((claw_x, 0.98, claw_z), (0.36, 0.22, 0.36), "Mat_HazardYellow", bevel=0.02)
    for ang in (0, 120, 240):
        rad = math.radians(ang)
        pc.add_box((claw_x + math.cos(rad) * 0.18, 0.72, claw_z + math.sin(rad) * 0.18), (0.08, 0.34, 0.08), "Mat_HazardYellow", bevel=0.01)

    # Twin Right-Side Arched Hangars with Skylights & Allied Shield
    pc.add_box((1.45, 0.58, -0.35), (2.7, 0.72, 3.6), "Mat_BrushedSteel", bevel=0.04)
    pc.add_quonset_arch((1.45, 0.92, -0.35), width=2.65, arch_height=1.25, length=3.55, mat_shell="Mat_BrushedSteel", mat_ribs="Mat_DarkSteel", rib_count=4)
    # Recessed front shutter door + Allied Blue emblem
    pc.add_box((1.45, 0.95, -2.14), (1.75, 1.35, 0.12), "Mat_DarkSteel", bevel=0.02)
    pc.add_box((1.45, 1.72, -2.18), (0.85, 0.42, 0.14), "Mat_TeamPaint", bevel=0.02, top_scale=(1.35, 1.0))
    # Blue side windows & roof skylights
    for wz in (-1.3, -0.4, 0.5):
        pc.add_box((2.80, 0.68, wz), (0.08, 0.38, 0.52), "Mat_TeamPaint", bevel=0.01)
        pc.add_box((1.05, 2.08, wz), (0.45, 0.12, 0.50), "Mat_TeamPaint", bevel=0.01)

    # Second offset rear-right hangar bay
    pc.add_box((2.15, 0.55, 1.65), (2.2, 0.65, 2.6), "Mat_BrushedSteel", bevel=0.04)
    pc.add_quonset_arch((2.15, 0.86, 1.65), width=2.15, arch_height=1.08, length=2.55, mat_shell="Mat_BrushedSteel", mat_ribs="Mat_TeamPaint", rib_count=3)

    # Twin Cylindrical Silos + Tall Swept Blue Pylon Fins in Rear
    for sx in (-0.55, 0.55):
        pc.add_cylinder((sx, 1.05, 1.45), 0.68, 0.64, 1.65, "Mat_BrushedSteel", segments=16)
        pc.add_cylinder((sx, 1.90, 1.45), 0.58, 0.45, 0.14, "Mat_DarkSteel", segments=16)
    for fx in (-1.35, -0.05):
        pc.add_box((fx, 1.85, 2.55), (0.28, 3.25, 0.95), "Mat_BrushedSteel", bevel=0.03, top_scale=(0.8, 0.55))
        pc.add_box((fx + 0.08, 1.80, 2.42), (0.22, 2.95, 0.72), "Mat_TeamPaint", bevel=0.02, top_scale=(0.8, 0.50))
    pc.add_cylinder((-0.70, 2.65, 2.55), 0.06, 0.06, 1.35, "Mat_BrushedSteel", segments=8, axis='X')

    # Props: Oil drums
    for ox, oz in ((0.4, -2.5), (0.72, -2.4), (0.55, -2.75), (2.9, -1.9), (3.15, -1.6)):
        pc.add_cylinder((ox, 0.44, oz), 0.14, 0.14, 0.40, "Mat_DarkSteel", segments=10)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_gapowr():
    """Allied Power Plant (2x2 = 4m x 4m): Curved blue shield walls + 3 brass-silver cooling towers + spinning core."""
    root = create_root("GAPOWR")
    pc = PartCollector(is_soviet=False)

    pc.add_cylinder((0.0, 0.12, 0.0), 1.88, 1.76, 0.24, "Mat_Concrete", segments=16)
    pc.add_cylinder((0.0, 0.72, 0.0), 1.38, 1.28, 0.98, "Mat_BrushedSteel", segments=16)

    # Twin curved Allied Blue outer armor shields
    pc.add_box((-0.92, 0.78, -0.68), (1.05, 0.95, 0.85), "Mat_TeamPaint", bevel=0.05, rot_deg=(0.0, 35.0, 0.0))
    pc.add_box(( 0.92, 0.78, -0.68), (1.05, 0.95, 0.85), "Mat_TeamPaint", bevel=0.05, rot_deg=(0.0, -35.0, 0.0))

    # Central glowing plasma turbine basin
    pc.add_cylinder((0.0, 1.26, -0.10), 0.68, 0.62, 0.22, "Mat_DarkSteel", segments=16)
    pc.add_cylinder((0.0, 1.36, -0.10), 0.48, 0.48, 0.08, "Mat_GlowCyan", segments=16)

    # Three staggered cooling/capacitor towers with gold/silver domes & blue collars
    towers = [
        (-0.82, 2.25,  0.15, 0.42),
        ( 0.08, 2.75,  0.82, 0.44),
        ( 0.84, 2.05, -0.05, 0.40),
    ]
    for tx, th, tz, tr in towers:
        pc.add_cylinder((tx, 0.24 + th * 0.45, tz), tr * 1.08, tr * 0.92, th * 0.9, "Mat_BrushedSteel", segments=16)
        pc.add_cylinder((tx, 0.24 + th * 0.62, tz), tr * 1.02, tr * 1.02, 0.28, "Mat_TeamPaint", segments=16)
        pc.add_sphere((tx, 0.24 + th * 0.90, tz), tr * 0.92, "Mat_GoldBrass", scale=(1.0, 0.65, 1.0), segments=14)
        for slot_ang in (0, 90, 180, 270):
            r_a = math.radians(slot_ang)
            pc.add_box((tx + math.cos(r_a) * tr * 0.92, 0.24 + th * 0.35, tz + math.sin(r_a) * tr * 0.92), (0.08, th * 0.42, 0.08), "Mat_DarkSteel", bevel=0.005)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    # Spinning turbine rotor inside central core
    spin = create_pivot("SpinPivot", (0.0, 1.42, -0.10), parent=root)
    spc = PartCollector(is_soviet=False)
    for b_ang in (0, 60, 120):
        spc.add_box((0.0, 0.0, 0.0), (0.82, 0.04, 0.14), "Mat_BrushedSteel", bevel=0.005, rot_deg=(0.0, b_ang, 0.0))
    spc.add_sphere((0.0, 0.02, 0.0), 0.14, "Mat_GlowCyan", segments=10)
    spc.build_mesh_object("TurbineBlades", (0.0, 1.42, -0.10), parent=spin)
    return root


def build_garefn():
    """Allied Ore Refinery (4x3 = 8m x 6m): Twin tall hyperboloid smokestacks + blue arch spine + arched vaults + ore bay."""
    root = create_root("GAREFN")
    pc = PartCollector(is_soviet=False)

    pc.add_box((0.0, 0.12, 0.0), (7.5, 0.24, 5.5), "Mat_Concrete", bevel=0.06)

    # Two tall tapered cooling/smelting towers on rear-left with blue collar rings
    for sx, sz, sh in ((-2.15, -0.35, 3.25), (-0.45, 1.45, 3.65)):
        pc.add_cylinder((sx, 0.46, sz), 1.15, 0.92, 0.45, "Mat_BrushedSteel", segments=18)
        pc.add_torus((sx, 0.72, sz), 0.76, 0.10, "Mat_TeamPaint")
        profile = [
            (0.72, 0.00),
            (0.54, sh * 0.35),
            (0.46, sh * 0.68),
            (0.58, sh * 0.88),
            (0.52, sh * 1.00),
        ]
        pc.add_lathe_profile((sx, 0.72, sz), profile, "Mat_BrushedSteel", segments=18)
        pc.add_cylinder((sx, 0.72 + sh - 0.05, sz), 0.38, 0.38, 0.12, "Mat_DarkSteel", segments=14)

    # Central Allied Blue spine arch with 3 silver pressure domes
    pc.add_box((1.25, 1.05, -0.15), (0.85, 1.65, 3.6), "Mat_TeamPaint", bevel=0.05, rot_deg=(0.0, 90.0, 0.0))
    for dx in (-0.1, 1.15, 2.4):
        pc.add_sphere((dx, 1.92, -0.15), 0.34, "Mat_BrushedSteel", scale=(1.0, 0.55, 1.0), segments=14)

    # Flanking ribbed silver arched vaults (front & rear of spine)
    pc.add_quonset_arch((1.15, 0.24, -1.35), width=2.5, arch_height=1.35, length=1.65, mat_shell="Mat_BrushedSteel", mat_ribs="Mat_TeamPaint", rib_count=3)
    pc.add_quonset_arch((1.65, 0.24,  1.05), width=2.4, arch_height=1.30, length=1.55, mat_shell="Mat_BrushedSteel", mat_ribs="Mat_DarkSteel", rib_count=3)

    # Front ore unloading ramp & hydraulic piston
    pc.add_box((1.15, 0.28, -2.35), (2.2, 0.12, 0.85), "Mat_HazardYellow", bevel=0.02)
    pc.add_box((1.15, 0.32, -2.35), (1.8, 0.10, 0.65), "Mat_DarkSteel", bevel=0.01)
    pc.add_cylinder((2.55, 0.85, -1.25), 0.16, 0.16, 1.2, "Mat_BrushedSteel", segments=10)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_gapile():
    """Allied Barracks (2x2 = 4m x 4m): Twin parallel Quonset arched halls + blue ribs + cyan portholes + watchtower."""
    root = create_root("GAPILE")
    pc = PartCollector(is_soviet=False)

    pc.add_box((0.0, 0.10, 0.0), (3.75, 0.20, 3.75), "Mat_Concrete", bevel=0.05)

    for bx, bz in ((-0.88, 0.18), (0.88, -0.18)):
        pc.add_quonset_arch((bx, 0.20, bz), width=1.42, arch_height=1.08, length=2.45, mat_shell="Mat_BrushedSteel", mat_ribs="Mat_TeamPaint", rib_count=3)
        # Front arched blast doorway + yellow hazard welcome mat
        pc.add_box((bx, 0.62, bz - 1.22), (0.68, 0.84, 0.10), "Mat_DarkSteel", bevel=0.02)
        pc.add_box((bx, 0.22, bz - 1.52), (0.82, 0.04, 0.42), "Mat_HazardYellow", bevel=0.005)
        # Glowing cyan side portholes
        for wz in (-0.65, 0.0, 0.65):
            pc.add_sphere((bx + (0.68 if bx > 0 else -0.68), 0.62, bz + wz), 0.11, "Mat_GlowCyan", segments=10)

    # Rear watchtower + tall antenna mast
    tx, tz = -0.88, 1.32
    pc.add_cylinder((tx, 1.35, tz), 0.36, 0.34, 1.9, "Mat_BrushedSteel", segments=12)
    pc.add_cylinder((tx, 2.32, tz), 0.48, 0.44, 0.26, "Mat_TeamPaint", segments=14)
    pc.add_cylinder((tx, 2.55, tz), 0.44, 0.46, 0.22, "Mat_GlassDark", segments=14)
    pc.add_sphere((tx, 2.70, tz), 0.46, "Mat_BrushedSteel", scale=(1.0, 0.45, 1.0), segments=14)
    pc.add_cylinder((tx, 3.45, tz), 0.035, 0.015, 1.35, "Mat_DarkSteel", segments=8)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_gaweap():
    """Allied War Factory (5x3 = 10m x 6m): Long ribbed arched hangar + 6 skylights + rollout bay + rear command dome."""
    root = create_root("GAWEAP")
    pc = PartCollector(is_soviet=False)

    pc.add_box((0.0, 0.12, 0.0), (9.5, 0.24, 5.5), "Mat_Concrete", bevel=0.06)

    # Main vehicle assembly hangar oriented along X/Z (front exit toward -Z!)
    pc.add_box((0.55, 0.55, -0.15), (4.6, 0.65, 4.4), "Mat_TeamPaint", bevel=0.04)
    pc.add_quonset_arch((0.55, 0.82, -0.15), width=4.4, arch_height=1.95, length=4.35, mat_shell="Mat_BrushedSteel", mat_ribs="Mat_DarkSteel", rib_count=6)

    # Roof skylight panels
    for sz in (-1.6, -0.9, -0.2, 0.5, 1.2):
        pc.add_box((0.95, 2.68, sz), (0.95, 0.14, 0.46), "Mat_GlassDark", bevel=0.02)

    # Front tank rollout bay portal & glowing yellow hazard floor
    pc.add_box((0.55, 1.15, -2.34), (2.85, 1.75, 0.18), "Mat_DarkSteel", bevel=0.02)
    pc.add_box((0.55, 0.26, -2.42), (2.65, 0.05, 0.75), "Mat_HazardYellow", bevel=0.01)

    # Rear-left Command Tower + Twin Allied Blue Shield Fins + Vat & Pipes
    cx, cz = -2.65, 1.15
    pc.add_cylinder((cx, 1.35, cz), 1.05, 0.98, 2.2, "Mat_BrushedSteel", segments=16)
    pc.add_torus((cx, 1.95, cz), 1.02, 0.09, "Mat_TeamPaint")
    pc.add_sphere((cx, 2.50, cz), 0.92, "Mat_BrushedSteel", scale=(1.0, 0.62, 1.0), segments=16)
    for fx, fz, rot in ((cx - 1.05, cz - 0.35, 15.0), (cx + 0.55, cz + 0.95, -75.0)):
        pc.add_box((fx, 1.75, fz), (0.24, 2.85, 1.15), "Mat_TeamPaint", bevel=0.03, rot_deg=(0.0, rot, 0.0), top_scale=(0.8, 0.65))

    # Chemical vat + pipeline along left side
    pc.add_cylinder((-2.55, 0.78, -0.85), 0.62, 0.62, 1.1, "Mat_BrushedSteel", segments=14)
    pc.add_cylinder((-2.55, 1.32, -0.85), 0.52, 0.52, 0.08, "Mat_DarkSteel", segments=14)
    pc.add_cylinder((-1.85, 0.65, -1.25), 0.11, 0.11, 2.4, "Mat_BrushedSteel", segments=10, axis='Z')

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_gaairc():
    """Allied Airforce Command HQ (3x2 = 6m x 4m): Twin antenna wing bastions + dome silo + rotating parabolic radar."""
    root = create_root("GAAIRC")
    pc = PartCollector(is_soviet=False)

    pc.add_box((0.0, 0.12, 0.0), (5.5, 0.24, 3.6), "Mat_Concrete", bevel=0.05)

    # Left & Right curved tiered radar bastions with triple whip antennas
    for side, bx, bz in ((-1, -1.65, -0.45), (1, 1.65, 0.45)):
        pc.add_cylinder((bx, 0.95, bz), 0.92, 0.86, 1.45, "Mat_BrushedSteel", segments=16)
        pc.add_cylinder((bx, 1.15, bz), 0.95, 0.95, 0.32, "Mat_TeamPaint", segments=16)
        pc.add_cylinder((bx, 1.45, bz), 0.94, 0.94, 0.24, "Mat_GlassDark", segments=16)
        for ai in (-0.38, 0.0, 0.38):
            pc.add_cylinder((bx + ai, 2.35, bz), 0.035, 0.015, 1.35, "Mat_DarkSteel", segments=8)

    # Front-center dome tower
    pc.add_cylinder((0.25, 0.95, -0.65), 0.65, 0.62, 1.45, "Mat_TeamPaint", segments=16)
    pc.add_sphere((0.25, 1.75, -0.65), 0.68, "Mat_BrushedSteel", scale=(1.0, 0.55, 1.0), segments=14)

    # Rear-center tall radar pedestal
    rx, rz = -0.35, 0.55
    pc.add_cylinder((rx, 1.35, rz), 0.95, 0.78, 2.25, "Mat_BrushedSteel", segments=18)
    pc.add_torus((rx, 2.15, rz), 0.84, 0.09, "Mat_TeamPaint")
    pc.add_cylinder((rx, 2.65, rz), 0.55, 0.55, 0.55, "Mat_GlassDark", segments=16)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    # Rotating swept parabolic radar dish!
    spin = create_pivot("SpinPivot", (rx, 3.05, rz), parent=root)
    rpc = PartCollector(is_soviet=False)
    rpc.add_cylinder((rx, 3.05, rz), 0.28, 0.22, 0.32, "Mat_DarkSteel", segments=12)
    rpc.add_box((rx, 3.38, rz), (1.85, 0.68, 0.22), "Mat_BrushedSteel", bevel=0.04, rot_deg=(-22.0, 0.0, 0.0), top_scale=(1.22, 1.0))
    rpc.add_box((rx, 3.36, rz - 0.08), (1.65, 0.52, 0.12), "Mat_TeamPaint", bevel=0.02, rot_deg=(-22.0, 0.0, 0.0))
    rpc.add_cylinder((rx, 3.55, rz - 0.45), 0.03, 0.02, 0.65, "Mat_GlowCyan", segments=8, axis='Z')
    rpc.build_mesh_object("RadarDish", (rx, 3.05, rz), parent=spin)
    return root


def build_gatech():
    """Allied Battle Lab (3x2 = 6m x 4m): Central blue spine + twin cantilevered silver lab pods + twin coil masts."""
    root = create_root("GATECH")
    pc = PartCollector(is_soviet=False)

    pc.add_box((0.0, 0.12, 0.0), (5.4, 0.24, 3.6), "Mat_Concrete", bevel=0.05)

    # Central tower core & Allied Blue monolith spine
    pc.add_cylinder((0.0, 1.55, 0.0), 1.05, 0.92, 2.65, "Mat_DarkSteel", segments=16)
    pc.add_box((0.0, 1.65, -0.65), (1.15, 2.85, 0.95), "Mat_TeamPaint", bevel=0.05, top_scale=(0.75, 0.8))
    # Glowing Allied triangular emblem shield on front
    pc.add_box((0.0, 1.35, -1.22), (0.95, 0.68, 0.16), "Mat_BrushedSteel", bevel=0.02, top_scale=(1.4, 1.0))
    pc.add_box((0.0, 1.35, -1.28), (0.68, 0.44, 0.12), "Mat_GlowCyan", bevel=0.01, top_scale=(1.4, 1.0))

    # Twin cantilevered upper laboratory pods (left & right)
    for sx in (-1.35, 1.35):
        pc.add_box((sx, 2.05, 0.05), (1.65, 1.05, 2.15), "Mat_BrushedSteel", bevel=0.08)
        pc.add_box((sx, 2.05, 0.05), (1.70, 0.28, 2.20), "Mat_GlassDark", bevel=0.02)
        pc.add_box((sx * 0.78, 2.82, 0.10), (0.95, 0.58, 1.25), "Mat_BrushedSteel", bevel=0.06)
        pc.add_sphere((sx * 0.78, 3.15, 0.10), 0.42, "Mat_BrushedSteel", scale=(1.0, 0.5, 1.0), segments=12)

    # Twin sky-piercing lightning/prism spires with 3 glowing blue toroid coils
    for mx, mz in ((-0.42, -0.15), (0.42, 0.25)):
        pc.add_cylinder((mx, 4.15, mz), 0.06, 0.02, 2.9, "Mat_DarkSteel", segments=10)
        pc.add_sphere((mx, 5.62, mz), 0.09, "Mat_GlowCyan", segments=10)
        for cy in (3.25, 3.48, 3.71):
            pc.add_torus((mx, cy, mz), 0.22, 0.055, "Mat_TeamPaint")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_gapill():
    """Allied Pillbox (1x1 = 2m x 2m): Reinforced octagonal concrete bunker + 360 firing slits + blue armor roof."""
    root = create_root("GAPILL")
    pc = PartCollector(is_soviet=False)

    pc.add_cylinder((0.0, 0.12, 0.0), 0.96, 0.90, 0.24, "Mat_Concrete", segments=8)
    # Recessed dark firing slit ring
    pc.add_cylinder((0.0, 0.46, 0.0), 0.72, 0.72, 0.28, "Mat_TreadRubber", segments=8)
    # Corner armor pillars between slits
    for ang in range(0, 360, 45):
        rad = math.radians(ang)
        pc.add_box((math.cos(rad) * 0.72, 0.46, math.sin(rad) * 0.72), (0.14, 0.30, 0.14), "Mat_Concrete", bevel=0.01)
    # Upper sloped concrete dome + Allied Blue hatch
    pc.add_cylinder((0.0, 0.74, 0.0), 0.84, 0.64, 0.28, "Mat_BrushedSteel", segments=12)
    pc.add_cylinder((0.0, 0.90, 0.0), 0.44, 0.40, 0.10, "Mat_TeamPaint", segments=12)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_atesla():
    """Allied Prism Tower (1x1 = 2m x 2m): Octagonal pedestal + 4 chrome struts + rotating faceted prism mirror crown."""
    root = create_root("ATESLA")
    pc = PartCollector(is_soviet=False)

    pc.add_cylinder((0.0, 0.20, 0.0), 0.86, 0.64, 0.40, "Mat_DarkSteel", segments=12)
    pc.add_cylinder((0.0, 0.46, 0.0), 0.66, 0.58, 0.16, "Mat_TeamPaint", segments=12)

    # Central glowing conduit + 4 chrome support struts
    pc.add_cylinder((0.0, 1.35, 0.0), 0.16, 0.16, 1.65, "Mat_GoldBrass", segments=12)
    for ang in (45, 135, 225, 315):
        rad = math.radians(ang)
        pc.add_cylinder((math.cos(rad) * 0.36, 1.32, math.sin(rad) * 0.36), 0.055, 0.055, 1.60, "Mat_BrushedSteel", segments=8)

    pc.add_cylinder((0.0, 2.22, 0.0), 0.54, 0.44, 0.24, "Mat_BrushedSteel", segments=14)
    pc.add_torus((0.0, 2.12, 0.0), 0.48, 0.06, "Mat_TeamPaint")
    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    # Rotating Prism Emitter Head (TurretPivot so it aims toward target!)
    tur = create_pivot("TurretPivot", (0.0, 2.38, 0.0), parent=root)
    tpc = PartCollector(is_soviet=False)
    tpc.add_cylinder((0.0, 2.52, 0.0), 0.26, 0.32, 0.28, "Mat_GoldBrass", segments=12)
    tpc.add_sphere((0.0, 2.95, 0.0), 0.28, "Mat_GlowCyan", scale=(1.0, 1.35, 1.0), segments=14)
    # 6 angled reflective prism mirror panels around crystal
    for i in range(6):
        ang = i * 60.0
        rad = math.radians(ang)
        px = math.sin(rad) * 0.42
        pz = math.cos(rad) * 0.42
        tpc.add_box((px, 2.98, pz), (0.32, 0.72, 0.08), "Mat_BrushedSteel", bevel=0.015, rot_deg=(-10.0, ang, 0.0))

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
