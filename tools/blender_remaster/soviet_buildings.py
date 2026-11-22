"""
soviet_buildings.py - High-detail 3D hard-surface Soviet structures modeled after original RA2 sprites.
Footprints in Unity meters (1 cell = 2.0m):
  NACNST: 4x4 (8x8m), NAPOWR: 2x2 (4x4m), NAREFN: 4x3 (8x6m), NAHAND: 2x2 (4x4m),
  NAWEAP: 5x3 (10x6m), NATECH: 3x2 (6x4m), TESLA: 1x1 (2x2m)
"""

from __future__ import annotations
import math
from common import PartCollector, create_pivot, create_root


def build_nacnst():
    """Soviet Construction Yard (4x4 = 8m x 8m): Sloped brick bunker + red vents + emblem door + Kremlin onion dome + yellow shed."""
    root = create_root("NACNST")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.12, 0.0), (7.5, 0.24, 7.5), "Mat_Concrete", bevel=0.06)

    # Front-center yellow/black hazard assembly pad
    pc.add_box((0.35, 0.25, -2.15), (2.2, 0.04, 1.9), "Mat_HazardYellow", bevel=0.01)
    pc.add_box((0.35, 0.27, -2.15), (1.85, 0.04, 1.55), "Mat_Concrete", bevel=0.01)

    # Heavy sloped Soviet brick-and-steel main bunker (left-front)
    bx, bz = -1.25, -0.45
    pc.add_sloped_hull((bx, 1.25, bz), width=3.6, height=2.05, length=3.8, front_slope=0.65, rear_slope=0.35, side_taper=0.76, mat="Mat_SovietBrick", bevel=0.04)
    # Recessed dark front blast gate with raised crimson Soviet star/emblem!
    pc.add_box((bx, 1.05, bz - 1.72), (2.15, 1.55, 0.22), "Mat_TreadRubber", bevel=0.02, rot_deg=(-14.0, 0.0, 0.0))
    pc.add_cylinder((bx, 1.12, bz - 1.84), 0.48, 0.48, 0.08, "Mat_TeamPaint", segments=5, axis='Z', rot_deg=(-14.0, 0.0, 0.0))
    # Crimson roof exhaust louver boxes
    for vx, vz in ((bx - 1.15, bz + 0.65), (bx - 0.15, bz + 1.35), (bx - 0.25, bz - 0.15)):
        pc.add_box((vx, 2.45, vz), (0.85, 0.55, 1.15), "Mat_TeamPaint", bevel=0.03, top_scale=(0.85, 0.75))
    # Side red fuel silo & steel pipes
    pc.add_cylinder((bx - 1.95, 0.92, bz - 0.95), 0.36, 0.32, 1.35, "Mat_TeamPaint", segments=12)
    pc.add_cylinder((bx - 1.35, 0.55, bz - 1.35), 0.24, 0.24, 1.15, "Mat_BrushedSteel", segments=12, axis='X')

    # Rear Kremlin Tower with Golden/Silver Fluted Onion Dome
    kx, kz = 0.05, 1.85
    pc.add_box((kx, 1.45, kz), (1.95, 2.45, 1.95), "Mat_DarkSteel", bevel=0.04, top_scale=(0.85, 0.85))
    pc.add_cylinder((kx, 2.85, kz), 0.68, 0.64, 0.55, "Mat_BrushedSteel", segments=16)
    pc.add_onion_dome((kx, 3.12, kz), base_radius=0.78, height=1.45, mat_dome="Mat_GoldBrass", mat_spire="Mat_GoldBrass")

    # Right-side auxiliary workshop with bright yellow sloped roof
    wx, wz = 2.15, -0.35
    pc.add_box((wx, 0.78, wz), (2.15, 1.10, 2.25), "Mat_DarkSteel", bevel=0.03)
    pc.add_box((wx, 1.42, wz), (2.35, 0.22, 2.45), "Mat_HazardYellow", bevel=0.03, rot_deg=(6.0, 0.0, 0.0))
    pc.add_box((wx - 0.35, 0.52, wz - 1.18), (0.95, 0.55, 0.35), "Mat_HazardYellow", bevel=0.02)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_napowr():
    """Soviet Tesla Reactor (2x2 = 4m x 4m): Twin brick/red end pylons + curved coolant pipes + glowing plasma sphere."""
    root = create_root("NAPOWR")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.10, 0.0), (3.75, 0.20, 3.55), "Mat_Concrete", bevel=0.04)
    pc.add_box((0.0, 0.24, 0.0), (1.95, 0.12, 1.95), "Mat_SovietBrick", bevel=0.02)

    # Twin left/right brick reactor pylons with crimson top plates & angled front control panels
    for sx in (-1.28, 1.28):
        pc.add_box((sx, 1.18, 0.25), (0.92, 1.95, 1.45), "Mat_SovietBrick", bevel=0.03)
        pc.add_box((sx, 2.20, 0.25), (0.86, 0.14, 1.35), "Mat_TeamPaint", bevel=0.02)
        # Angled front crimson control panel
        pc.add_box((sx, 0.62, -0.78), (0.84, 0.85, 0.65), "Mat_BrushedSteel", bevel=0.02, rot_deg=(-32.0, 0.0, 0.0))
        pc.add_box((sx, 0.68, -0.86), (0.72, 0.62, 0.12), "Mat_TeamPaint", bevel=0.01, rot_deg=(-32.0, 0.0, 0.0))
        # Coolant pipes into central plasma sphere
        for py in (0.95, 1.55):
            pc.add_cylinder((sx * 0.62, py, 0.25), 0.09, 0.09, 0.95, "Mat_BrushedSteel", segments=10, axis='X')

    # Central Glowing Plasma Containment Sphere!
    pc.add_cylinder((0.0, 0.45, 0.25), 0.62, 0.48, 0.45, "Mat_DarkSteel", segments=16)
    pc.add_sphere((0.0, 1.32, 0.25), 0.86, "Mat_GlowPlasma", segments=20)
    pc.add_torus((0.0, 1.32, 0.25), 0.89, 0.055, "Mat_BrushedSteel", rot_deg=(24.0, 0.0, 18.0))

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_narefn():
    """Soviet Ore Refinery (4x3 = 8m x 6m): Twin heavy riveted smelting furnaces + crimson crusher hall + conveyor & ore pit."""
    root = create_root("NAREFN")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.12, 0.0), (7.5, 0.24, 5.5), "Mat_Concrete", bevel=0.05)

    # Twin massive cylindrical smelting furnaces with conical roofs, smokestacks & exterior U-pipes
    for fx, fz, fh in ((-1.95, -0.55, 1.85), (1.55, 1.05, 2.15)):
        pc.add_cylinder((fx, 0.24 + fh * 0.42, fz), 1.25, 1.22, fh * 0.84, "Mat_DarkSteel", segments=18)
        pc.add_cylinder((fx, 0.24 + fh * 0.98, fz), 1.26, 0.48, fh * 0.38, "Mat_BrushedSteel", segments=18)
        pc.add_cylinder((fx, 0.24 + fh * 1.42, fz), 0.42, 0.46, fh * 0.58, "Mat_DarkSteel", segments=14)
        pc.add_torus((fx, 0.24 + fh * 1.68, fz), 0.45, 0.06, "Mat_BrushedSteel")
        for ang in (0, 60, 120, 180, 240, 300):
            rad = math.radians(ang)
            pc.add_cylinder((fx + math.cos(rad) * 1.28, 0.24 + fh * 0.36, fz + math.sin(rad) * 1.28), 0.08, 0.08, fh * 0.68, "Mat_BrushedSteel", segments=8)

    # Central Crimson Crusher Hall + Exhaust Vents
    pc.add_box((-0.15, 1.25, 0.25), (1.65, 2.05, 2.85), "Mat_TeamPaint", bevel=0.04)
    pc.add_box((-1.35, 1.65, 1.15), (0.95, 0.85, 1.35), "Mat_TeamPaint", bevel=0.03)

    # Front-right Circular Ore Pit & Angled Conveyor Truss
    pit_x, pit_z = 1.55, -1.45
    pc.add_torus((pit_x, 0.34, pit_z), 1.15, 0.12, "Mat_BrushedSteel")
    pc.add_cylinder((pit_x, 0.26, pit_z), 1.12, 1.12, 0.08, "Mat_GoldBrass", segments=18)
    pc.add_box((0.75, 1.05, -0.65), (0.48, 0.28, 2.35), "Mat_GoldBrass", bevel=0.02, rot_deg=(28.0, -32.0, 0.0))

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_nahand():
    """Soviet Barracks (2x2 = 4m x 4m): Sloped brick/red pedestal + towering 3D Soviet soldier statue with raised rifle!"""
    root = create_root("NAHAND")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.10, 0.0), (3.65, 0.20, 3.65), "Mat_Concrete", bevel=0.04)

    # Sloped brick-and-steel bunker pedestal
    pc.add_sloped_hull((0.0, 0.78, 0.0), width=2.85, height=1.18, length=2.85, front_slope=0.48, rear_slope=0.48, side_taper=0.68, mat="Mat_SovietBrick", bevel=0.04)
    pc.add_box((0.0, 1.42, 0.0), (2.25, 0.18, 2.25), "Mat_TeamPaint", bevel=0.03)
    pc.add_box((0.0, 1.54, 0.0), (1.85, 0.12, 1.85), "Mat_BrushedSteel", bevel=0.02)

    # Front & side recessed doorway plaques with crimson emblem
    for ang in (0.0, 90.0):
        rad = math.radians(ang)
        px, pz = -math.sin(rad) * 1.25, -math.cos(rad) * 1.25
        pc.add_box((px, 0.68, pz), (0.95, 0.92, 0.16), "Mat_DarkSteel", bevel=0.02, rot_deg=(-18.0, ang, 0.0))
        pc.add_cylinder((px, 0.72, pz - 0.06 * math.cos(rad)), 0.26, 0.26, 0.08, "Mat_TeamPaint", segments=5, axis='Z', rot_deg=(-18.0, ang, 0.0))

    # Heroic 3D Metallic Soviet Soldier Statue (boots -> flared greatcoat -> torso -> helmeted head -> raised rifle!)
    sy = 1.60
    for lx in (-0.22, 0.22):
        pc.add_cylinder((lx, sy + 0.35, 0.0), 0.14, 0.12, 0.70, "Mat_DarkSteel", segments=10)
    # Flared Soviet greatcoat skirt
    pc.add_cylinder((0.0, sy + 1.05, 0.0), 0.52, 0.34, 0.95, "Mat_BrushedSteel", segments=14)
    # Muscular chest & shoulders
    pc.add_box((0.0, sy + 1.72, 0.0), (0.82, 0.62, 0.44), "Mat_BrushedSteel", bevel=0.06, top_scale=(1.15, 1.0))
    # Head + Soviet steel helmet
    pc.add_sphere((0.0, sy + 2.22, 0.0), 0.22, "Mat_BrushedSteel", segments=12)
    pc.add_sphere((0.0, sy + 2.30, 0.02), 0.26, "Mat_DarkSteel", scale=(1.05, 0.72, 1.12), segments=12)
    # Right arm across chest, Left arm holding tall vertical AK/Mosin rifle skyward!
    pc.add_box((-0.38, sy + 1.68, -0.18), (0.52, 0.20, 0.22), "Mat_BrushedSteel", bevel=0.03, rot_deg=(0.0, 22.0, -12.0))
    pc.add_box(( 0.46, sy + 1.78, -0.08), (0.22, 0.58, 0.22), "Mat_BrushedSteel", bevel=0.03, rot_deg=(-15.0, 0.0, 18.0))
    pc.add_cylinder((0.52, sy + 2.35, -0.12), 0.055, 0.035, 1.85, "Mat_DarkSteel", segments=8)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_naweap():
    """Soviet War Factory (5x3 = 10m x 6m): Brick portal + skylight hall + 3 red buttresses/spheres + rear Kremlin onion dome."""
    root = create_root("NAWEAP")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.12, 0.0), (9.5, 0.24, 5.5), "Mat_Concrete", bevel=0.06)

    # Main factory assembly hall with glass skylight roof
    pc.add_box((0.55, 1.15, -0.25), (3.8, 1.85, 4.5), "Mat_SovietBrick", bevel=0.04)
    pc.add_box((0.55, 2.15, -0.25), (2.6, 0.32, 4.2), "Mat_BrushedSteel", bevel=0.03, top_scale=(0.65, 0.98))
    for sz in (-1.6, -0.6, 0.4, 1.4):
        pc.add_box((-0.15, 2.22, sz), (0.95, 0.18, 0.72), "Mat_GlassDark", bevel=0.02, rot_deg=(0.0, 0.0, 22.0))

    # Sloped front brick portal with crimson Soviet star gate
    pc.add_sloped_hull((0.55, 1.22, -2.35), width=3.95, height=2.05, length=0.85, front_slope=0.35, rear_slope=0.05, side_taper=0.86, mat="Mat_SovietBrick", bevel=0.03)
    pc.add_box((0.55, 1.05, -2.68), (2.55, 1.65, 0.18), "Mat_TreadRubber", bevel=0.02)
    pc.add_cylinder((0.55, 1.08, -2.78), 0.52, 0.52, 0.08, "Mat_TeamPaint", segments=5, axis='Z')

    # Left side: 3 Crimson Vertical Buttress Towers + 3 Silver Gas Spheres on Red Pedestals!
    for bz in (-1.65, -0.25, 1.15):
        pc.add_box((-1.55, 1.45, bz), (0.68, 2.45, 0.72), "Mat_TeamPaint", bevel=0.03, top_scale=(0.85, 0.75))
        pc.add_box((-2.25, 0.48, bz), (0.85, 0.48, 0.85), "Mat_TeamPaint", bevel=0.03, top_scale=(0.82, 0.82))
        pc.add_sphere((-2.25, 0.98, bz), 0.36, "Mat_BrushedSteel", segments=12)

    # Right side: 3 Crimson Exhaust Louver Hoods
    for rz in (-1.45, -0.15, 1.15):
        pc.add_box((2.55, 1.95, rz), (0.75, 0.95, 0.82), "Mat_TeamPaint", bevel=0.03, top_scale=(0.75, 0.85))

    # Rear Kremlin Tower with Golden/Silver Onion Dome & Pipes
    kx, kz = -0.85, 1.95
    pc.add_box((kx, 1.85, kz), (2.15, 3.25, 1.65), "Mat_SovietBrick", bevel=0.04, top_scale=(0.82, 0.82))
    pc.add_cylinder((kx, 3.65, kz), 0.72, 0.68, 0.52, "Mat_DarkSteel", segments=16)
    pc.add_onion_dome((kx, 3.90, kz), base_radius=0.82, height=1.55, mat_dome="Mat_GoldBrass", mat_spire="Mat_GoldBrass")
    pc.add_cylinder((-2.55, 0.75, 1.85), 0.78, 0.74, 1.05, "Mat_BrushedSteel", segments=14)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_natech():
    """Soviet Battle Lab (3x2 = 6m x 4m): 4-corner fortress + concentric crimson circular tiers + grand golden onion dome."""
    root = create_root("NATECH")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.12, 0.0), (5.5, 0.24, 3.7), "Mat_Concrete", bevel=0.05)

    # Symmetrical fortress base with crimson trim & 4 corner mini-turret spires
    pc.add_box((0.0, 0.88, 0.0), (4.4, 1.32, 3.1), "Mat_SovietBrick", bevel=0.04)
    pc.add_box((0.0, 1.58, 0.0), (4.55, 0.14, 3.25), "Mat_TeamPaint", bevel=0.02)
    for cx in (-2.05, 2.05):
        for cz in (-1.38, 1.38):
            pc.add_box((cx, 1.25, cz), (0.56, 2.05, 0.56), "Mat_BrushedSteel", bevel=0.02)
            pc.add_box((cx, 2.32, cz), (0.64, 0.16, 0.64), "Mat_TeamPaint", bevel=0.01)
            pc.add_cylinder((cx, 2.75, cz), 0.18, 0.02, 0.72, "Mat_GoldBrass", segments=10)

    # Two concentric circular crimson-rimmed galleries
    pc.add_cylinder((0.0, 1.98, 0.0), 1.48, 1.42, 0.68, "Mat_DarkSteel", segments=18)
    pc.add_torus((0.0, 2.32, 0.0), 1.46, 0.10, "Mat_TeamPaint")
    pc.add_cylinder((0.0, 2.68, 0.0), 1.02, 0.96, 0.65, "Mat_BrushedSteel", segments=18)
    pc.add_torus((0.0, 3.02, 0.0), 1.00, 0.09, "Mat_TeamPaint")

    # Grand central Golden Onion Dome
    pc.add_cylinder((0.0, 3.32, 0.0), 0.58, 0.54, 0.52, "Mat_DarkSteel", segments=16)
    pc.add_onion_dome((0.0, 3.58, 0.0), base_radius=0.76, height=1.55, mat_dome="Mat_GoldBrass", mat_spire="Mat_GoldBrass")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_tesla():
    """Soviet Tesla Coil (1x1 = 2m x 2m): 3 red base capacitor pods + insulated column + 3 chrome toroid rings + plasma sphere."""
    root = create_root("TESLA")
    pc = PartCollector(is_soviet=True)

    pc.add_cylinder((0.0, 0.14, 0.0), 0.92, 0.84, 0.28, "Mat_DarkSteel", segments=14)
    # 3 Crimson high-voltage capacitor pods around base
    for ang in (0, 120, 240):
        rad = math.radians(ang)
        px, pz = math.sin(rad) * 0.58, math.cos(rad) * 0.58
        pc.add_cylinder((px, 0.68, pz), 0.26, 0.22, 0.85, "Mat_TeamPaint", segments=12)
        pc.add_sphere((px, 1.12, pz), 0.21, "Mat_BrushedSteel", scale=(1.0, 0.6, 1.0), segments=10)

    # Central insulated coil mast + 3 chrome toroid corona rings
    pc.add_cylinder((0.0, 1.48, 0.0), 0.22, 0.14, 2.45, "Mat_BrushedSteel", segments=14)
    for ry, rr in ((1.52, 0.62), (1.92, 0.50), (2.30, 0.38)):
        pc.add_torus((0.0, ry, 0.0), rr, 0.075, "Mat_BrushedSteel")
        pc.add_torus((0.0, ry, 0.0), rr * 0.65, 0.035, "Mat_GlowPlasma")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    # Discharge Sphere Head (TurretPivot)
    tur = create_pivot("TurretPivot", (0.0, 2.75, 0.0), parent=root)
    tpc = PartCollector(is_soviet=True)
    tpc.add_sphere((0.0, 2.98, 0.0), 0.42, "Mat_BrushedSteel", segments=18)
    tpc.add_torus((0.0, 2.98, 0.0), 0.44, 0.04, "Mat_GlowPlasma")
    tpc.build_mesh_object("TeslaSphere", (0.0, 2.75, 0.0), parent=tur)
    return root


SOVIET_BUILDINGS = {
    "NACNST": (build_nacnst, 8.8),
    "NAPOWR": (build_napowr, 4.8),
    "NAREFN": (build_narefn, 8.2),
    "NAHAND": (build_nahand, 6.4),
    "NAWEAP": (build_naweap, 9.6),
    "NATECH": (build_natech, 6.8),
    "TESLA":  (build_tesla,  4.2),
}
