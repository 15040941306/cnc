"""
soviet_buildings.py - Westwood 3ds-Max-faithful Soviet 3D structures with weathered masonry,
Hammer & Sickle blast gates, 3D louvered exhaust stacks, flanged coolant pipes, and fluted Kremlin domes.
"""

from __future__ import annotations
import math
from common import PartCollector, create_pivot, create_root


def build_nacnst():
    """Soviet Construction Yard (8m x 8m) matched 1:1 to Sprites/NACNST.png."""
    root = create_root("NACNST")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.12, 0.0), (7.6, 0.24, 7.6), "Mat_Concrete", bevel=0.05)

    # Front-center yellow/black diagonal hazard-striped assembly pad
    pc.add_decal_panel((0.35, 0.25, -2.18), 2.25, 1.95, 0.04, "Mat_HazardYellow", rot_deg=(90.0, 0.0, 0.0))
    pc.add_box((0.35, 0.27, -2.18), (1.82, 0.04, 1.52), "Mat_Concrete", bevel=0.01)

    # Battered brick-and-concrete main bunker + steel buttress ribs + Hammer & Sickle Blast Gate!
    bx, bz = -1.25, -0.45
    pc.add_sloped_hull((bx, 1.25, bz), width=3.65, height=2.08, length=3.85, front_slope=0.65, rear_slope=0.35, side_taper=0.76, mat="Mat_SovietBrick", bevel=0.04)
    # Recessed Hammer & Sickle blast gate!
    pc.add_decal_panel((bx, 0.98, bz - 1.82), 2.15, 1.52, 0.14, "Mat_SovietGate", rot_deg=(18.0, 0.0, 0.0))
    # Exposed industrial steel pipes & buttresses along left wall (just like Sprites/NACNST.png!)
    for pz in (bz - 1.05, bz - 0.35, bz + 0.35):
        pc.add_pipe_run([(bx - 1.88, 0.32, pz), (bx - 1.72, 1.45, pz), (bx - 1.32, 1.95, pz)], radius=0.075)
    # Crimson 3D louvered exhaust vent stacks on roof
    pc.add_louver_stack((bx - 1.15, 2.48, bz + 0.65), (0.85, 0.62, 1.10), slats=4)
    pc.add_louver_stack((bx - 0.15, 2.48, bz + 1.35), (0.85, 0.62, 1.10), slats=4)
    pc.add_box((bx - 0.25, 2.34, bz - 0.18), (1.25, 0.22, 0.95), "Mat_TeamPaint", bevel=0.02)

    # Side red fuel silo + horizontal white/silver pressure tank
    pc.add_cylinder((bx - 2.02, 0.92, bz - 1.05), 0.34, 0.30, 1.38, "Mat_TeamPaint", segments=14)
    pc.add_cylinder((bx - 1.52, 0.56, bz - 1.48), 0.24, 0.24, 1.18, "Mat_BrushedSteel", segments=14, axis='X')

    # Rear multi-tiered Kremlin Tower with 32-segment Fluted Golden Onion Dome!
    kx, kz = 0.05, 1.85
    pc.add_box((kx, 1.12, kz), (2.15, 1.75, 2.15), "Mat_DarkSteel", bevel=0.04, top_scale=(0.88, 0.88))
    pc.add_box((kx, 2.28, kz), (1.65, 0.72, 1.65), "Mat_BrushedSteel", bevel=0.03)
    pc.add_onion_dome((kx, 2.64, kz), base_radius=0.80, height=1.75, mat_dome="Mat_GoldBrass", mat_spire="Mat_GoldBrass")

    # Right-side auxiliary workshop with corrugated yellow roof & hazard bumper
    wx, wz = 2.15, -0.35
    pc.add_box((wx, 0.78, wz), (2.15, 1.10, 2.25), "Mat_DarkSteel", bevel=0.03)
    pc.add_box((wx, 1.42, wz), (2.38, 0.18, 2.48), "Mat_GoldBrass", bevel=0.02, rot_deg=(6.0, 0.0, 0.0))
    pc.add_decal_panel((wx - 0.35, 0.48, wz - 1.18), 1.05, 0.52, 0.18, "Mat_HazardYellow")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_napowr():
    """Soviet Tesla Reactor (4m x 4m) matched 1:1 to Sprites/NAPOWR.png."""
    root = create_root("NAPOWR")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.10, 0.0), (3.8, 0.20, 3.6), "Mat_Concrete", bevel=0.04)
    pc.add_box((0.0, 0.24, 0.10), (1.98, 0.12, 1.98), "Mat_DarkSteel", bevel=0.02)

    for sx in (-1.28, 1.28):
        pc.add_box((sx, 1.20, 0.25), (0.94, 1.98, 1.48), "Mat_SovietBrick", bevel=0.03)
        pc.add_box((sx, 2.22, 0.25), (0.86, 0.14, 1.36), "Mat_TeamPaint", bevel=0.02)
        # Angled front crimson control desk with 6 silver dials + exterior vertical conduits!
        pc.add_box((sx, 0.62, -0.78), (0.86, 0.86, 0.66), "Mat_BrushedSteel", bevel=0.02, rot_deg=(-32.0, 0.0, 0.0))
        pc.add_box((sx, 0.68, -0.86), (0.74, 0.62, 0.12), "Mat_TeamPaint", bevel=0.01, rot_deg=(-32.0, 0.0, 0.0))
        for dx in (-0.20, 0.0, 0.20):
            for dy in (0.56, 0.78):
                pc.add_sphere((sx + dx, dy, -0.94), 0.045, "Mat_BrushedSteel", segments=8)
        for pz in (-0.15, 0.25):
            pc.add_pipe_run([(sx + (0.52 if sx > 0 else -0.52), 0.30, pz), (sx + (0.52 if sx > 0 else -0.52), 1.85, pz)], radius=0.065)
        for py in (0.95, 1.58):
            pc.add_pipe_run([(sx * 0.85, py, 0.25), (sx * 0.35, py, 0.25)], radius=0.085)

    pc.add_cylinder((0.0, 0.45, 0.25), 0.64, 0.48, 0.45, "Mat_DarkSteel", segments=18)
    pc.add_sphere((0.0, 1.34, 0.25), 0.88, "Mat_GlowPlasma", segments=24)
    pc.add_torus((0.0, 1.34, 0.25), 0.91, 0.05, "Mat_BrushedSteel", rot_deg=(24.0, 0.0, 18.0))
    pc.add_torus((0.0, 1.34, 0.25), 0.91, 0.04, "Mat_DarkSteel", rot_deg=(-28.0, 45.0, 0.0))

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_narefn():
    """Soviet Ore Refinery (8m x 6m) matched 1:1 to Sprites/NAREFN.png."""
    root = create_root("NAREFN")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.12, 0.0), (7.6, 0.24, 5.6), "Mat_Concrete", bevel=0.05)

    for fx, fz, fh in ((-1.95, -0.55, 1.88), (1.55, 1.05, 2.18)):
        pc.add_cylinder((fx, 0.24 + fh * 0.42, fz), 1.26, 1.22, fh * 0.84, "Mat_DarkSteel", segments=20)
        pc.add_cylinder((fx, 0.24 + fh * 0.98, fz), 1.28, 0.48, fh * 0.38, "Mat_BrushedSteel", segments=20)
        pc.add_cylinder((fx, 0.24 + fh * 1.42, fz), 0.44, 0.48, fh * 0.58, "Mat_DarkSteel", segments=16)
        pc.add_torus((fx, 0.24 + fh * 1.68, fz), 0.47, 0.06, "Mat_BrushedSteel")
        for ang in range(0, 360, 60):
            rad = math.radians(ang)
            ux, uz = fx + math.cos(rad) * 1.30, fz + math.sin(rad) * 1.30
            pc.add_pipe_run([(ux, 0.26, uz), (ux, 0.24 + fh * 0.72, uz), (fx + math.cos(rad) * 1.05, 0.24 + fh * 0.82, fz + math.sin(rad) * 1.05)], radius=0.07)

    # Central Crimson Crusher Hall + Louver Vents + Open 3D Lattice Ore Conveyor Truss!
    pc.add_box((-0.15, 1.25, 0.25), (1.68, 2.05, 2.88), "Mat_TeamPaint", bevel=0.04)
    pc.add_louver_stack((-1.35, 1.85, 1.15), (0.92, 0.78, 1.25), slats=4)

    pit_x, pit_z = 1.55, -1.48
    pc.add_torus((pit_x, 0.34, pit_z), 1.18, 0.12, "Mat_BrushedSteel")
    pc.add_cylinder((pit_x, 0.28, pit_z), 1.14, 1.14, 0.10, "Mat_GoldBrass", segments=20)
    pc.add_lattice_boom((-0.05, 1.68, 0.15), (pit_x, 0.42, pit_z), width=0.46, height=0.30, bays=6, mat="Mat_GoldBrass")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_nahand():
    """Soviet Barracks (4m x 4m) matched 1:1 to Sprites/NAHAND.png."""
    root = create_root("NAHAND")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.10, 0.0), (3.7, 0.20, 3.7), "Mat_Concrete", bevel=0.04)
    pc.add_sloped_hull((0.0, 0.78, 0.0), width=2.90, height=1.18, length=2.90, front_slope=0.48, rear_slope=0.48, side_taper=0.68, mat="Mat_SovietBrick", bevel=0.04)
    pc.add_box((0.0, 1.42, 0.0), (2.28, 0.18, 2.28), "Mat_TeamPaint", bevel=0.03)
    pc.add_box((0.0, 1.54, 0.0), (1.88, 0.12, 1.88), "Mat_BrushedSteel", bevel=0.02)

    # Hammer & Sickle doorway plaques on Front & Left slopes!
    pc.add_decal_panel((0.0, 0.68, -1.32), 0.96, 0.88, 0.12, "Mat_SovietGate", rot_deg=(22.0, 0.0, 0.0))
    pc.add_decal_panel((-1.32, 0.68, 0.0), 0.96, 0.88, 0.12, "Mat_SovietGate", rot_deg=(22.0, 90.0, 0.0))

    # Sculpted 3D Metallic Soviet Soldier Statue
    sy = 1.60
    for lx in (-0.20, 0.20):
        pc.add_box((lx, sy + 0.12, -0.04), (0.16, 0.22, 0.28), "Mat_DarkSteel", bevel=0.03)
        pc.add_cylinder((lx, sy + 0.42, 0.0), 0.13, 0.12, 0.56, "Mat_DarkSteel", segments=12)
    pc.add_lathe_profile((0.0, sy + 0.62, 0.0), [(0.52, 0.0), (0.42, 0.48), (0.32, 0.86)], "Mat_BrushedSteel", segments=18, flute_amp=0.03, flute_count=8)
    pc.add_torus((0.0, sy + 1.42, 0.0), 0.32, 0.045, "Mat_DarkSteel")
    pc.add_sloped_hull((0.0, sy + 1.72, 0.0), width=0.80, height=0.58, length=0.44, front_slope=0.06, rear_slope=0.06, side_taper=1.12, mat="Mat_BrushedSteel", bevel=0.05)
    pc.add_sphere((0.0, sy + 2.22, 0.0), 0.21, "Mat_BrushedSteel", segments=14)
    pc.add_sphere((0.0, sy + 2.30, 0.02), 0.25, "Mat_DarkSteel", scale=(1.06, 0.74, 1.12), segments=14)
    # Right arm across chest, Left arm holding tall rifle skyward
    pc.add_pipe_run([(-0.42, sy + 1.88, 0.0), (-0.46, sy + 1.62, -0.20), (-0.08, sy + 1.62, -0.24)], radius=0.095, mat="Mat_BrushedSteel", flanges=False)
    pc.add_pipe_run([( 0.42, sy + 1.88, 0.0), ( 0.52, sy + 1.68, -0.14)], radius=0.095, mat="Mat_BrushedSteel", flanges=False)
    pc.add_cylinder((0.52, sy + 2.40, -0.14), 0.048, 0.028, 1.95, "Mat_DarkSteel", segments=10)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_naweap():
    """Soviet War Factory (10m x 6m) matched 1:1 to Sprites/NAWEAP.png."""
    root = create_root("NAWEAP")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.12, 0.0), (9.6, 0.24, 5.6), "Mat_Concrete", bevel=0.05)

    pc.add_box((0.55, 1.15, -0.25), (3.85, 1.85, 4.55), "Mat_SovietBrick", bevel=0.04)
    pc.add_box((0.55, 2.16, -0.25), (2.65, 0.32, 4.25), "Mat_BrushedSteel", bevel=0.03, top_scale=(0.65, 0.98))
    for sz in (-1.6, -0.6, 0.4, 1.4):
        pc.add_box((-0.15, 2.22, sz), (0.96, 0.18, 0.72), "Mat_GlassDark", bevel=0.02, rot_deg=(0.0, 0.0, 22.0))

    # Sloped front brick portal with Hammer & Sickle Blast Gate!
    pc.add_sloped_hull((0.55, 1.22, -2.35), width=3.98, height=2.06, length=0.86, front_slope=0.35, rear_slope=0.05, side_taper=0.86, mat="Mat_SovietBrick", bevel=0.03)
    pc.add_decal_panel((0.55, 1.05, -2.72), 2.58, 1.68, 0.14, "Mat_SovietGate")

    # Left side: 3 Crimson Vertical Buttress Towers + 3 Silver Gas Spheres on Red Pedestals + Pipes!
    for bz in (-1.65, -0.25, 1.15):
        pc.add_box((-1.55, 1.46, bz), (0.68, 2.46, 0.72), "Mat_TeamPaint", bevel=0.03, top_scale=(0.85, 0.75))
        pc.add_box((-2.28, 0.48, bz), (0.86, 0.48, 0.86), "Mat_TeamPaint", bevel=0.03, top_scale=(0.82, 0.82))
        pc.add_sphere((-2.28, 1.00, bz), 0.38, "Mat_BrushedSteel", segments=16)
        pc.add_pipe_run([(-2.28, 1.35, bz), (-1.62, 1.58, bz)], radius=0.065)

    for rz in (-1.45, -0.15, 1.15):
        pc.add_louver_stack((2.58, 1.96, rz), (0.76, 0.96, 0.84), slats=4, rot_y_deg=-90.0)

    kx, kz = -0.85, 1.95
    pc.add_box((kx, 1.85, kz), (2.15, 3.25, 1.68), "Mat_SovietBrick", bevel=0.04, top_scale=(0.82, 0.82))
    pc.add_onion_dome((kx, 3.48, kz), base_radius=0.84, height=1.78, mat_dome="Mat_GoldBrass", mat_spire="Mat_GoldBrass")
    pc.add_spoked_fan((-2.62, 0.24, 1.85), radius=0.78, height=1.05, blades=6)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_natech():
    """Soviet Battle Lab (6m x 4m) matched 1:1 to Sprites/NATECH.png."""
    root = create_root("NATECH")
    pc = PartCollector(is_soviet=True)

    pc.add_box((0.0, 0.12, 0.0), (5.6, 0.24, 3.8), "Mat_Concrete", bevel=0.04)
    pc.add_box((0.0, 0.88, 0.0), (4.45, 1.32, 3.15), "Mat_SovietBrick", bevel=0.04)
    pc.add_box((0.0, 1.58, 0.0), (4.60, 0.14, 3.30), "Mat_TeamPaint", bevel=0.02)

    for cx in (-2.08, 2.08):
        for cz in (-1.40, 1.40):
            pc.add_box((cx, 1.25, cz), (0.56, 2.05, 0.56), "Mat_BrushedSteel", bevel=0.02)
            pc.add_box((cx, 2.32, cz), (0.64, 0.16, 0.64), "Mat_TeamPaint", bevel=0.01)
            pc.add_onion_dome((cx, 2.40, cz), base_radius=0.22, height=0.68, mat_dome="Mat_GoldBrass", mat_spire="Mat_GoldBrass")

    pc.add_cylinder((0.0, 1.98, 0.0), 1.50, 1.44, 0.68, "Mat_DarkSteel", segments=22)
    pc.add_torus((0.0, 2.32, 0.0), 1.48, 0.10, "Mat_TeamPaint")
    pc.add_cylinder((0.0, 2.68, 0.0), 1.04, 0.98, 0.65, "Mat_BrushedSteel", segments=22)
    pc.add_torus((0.0, 3.02, 0.0), 1.02, 0.09, "Mat_TeamPaint")
    for ang in range(0, 360, 45):
        ra = math.radians(ang)
        pc.add_pipe_run([(math.cos(ra) * 1.48, 1.65, math.sin(ra) * 1.48), (math.cos(ra) * 1.04, 2.95, math.sin(ra) * 1.04)], radius=0.055)

    pc.add_onion_dome((0.0, 3.04, 0.0), base_radius=0.78, height=1.82, mat_dome="Mat_GoldBrass", mat_spire="Mat_GoldBrass")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_tesla():
    """Soviet Tesla Coil (2m x 2m) matched 1:1 to Cameos/TESLA.png."""
    root = create_root("TESLA")
    pc = PartCollector(is_soviet=True)

    pc.add_cylinder((0.0, 0.14, 0.0), 0.94, 0.84, 0.28, "Mat_DarkSteel", segments=16)
    for ang in (0, 120, 240):
        rad = math.radians(ang)
        px, pz = math.sin(rad) * 0.60, math.cos(rad) * 0.60
        pc.add_cylinder((px, 0.68, pz), 0.26, 0.22, 0.86, "Mat_TeamPaint", segments=14)
        pc.add_sphere((px, 1.12, pz), 0.21, "Mat_BrushedSteel", scale=(1.0, 0.6, 1.0), segments=12)
        pc.add_pipe_run([(px * 0.8, 0.95, pz * 0.8), (0.0, 1.28, 0.0)], radius=0.045)

    pc.add_cylinder((0.0, 1.48, 0.0), 0.20, 0.12, 2.45, "Mat_BrushedSteel", segments=16)
    pc.add_lattice_boom((0.0, 0.35, 0.0), (0.0, 2.55, 0.0), width=0.34, height=0.34, bays=5, mat="Mat_DarkSteel")
    for ry, rr in ((1.52, 0.64), (1.92, 0.52), (2.32, 0.38)):
        pc.add_torus((0.0, ry, 0.0), rr, 0.075, "Mat_BrushedSteel")
        pc.add_torus((0.0, ry, 0.0), rr * 0.68, 0.035, "Mat_GlowPlasma")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur = create_pivot("TurretPivot", (0.0, 2.75, 0.0), parent=root)
    tpc = PartCollector(is_soviet=True)
    tpc.add_sphere((0.0, 2.98, 0.0), 0.44, "Mat_BrushedSteel", segments=22)
    tpc.add_torus((0.0, 2.98, 0.0), 0.46, 0.038, "Mat_GlowPlasma")
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
