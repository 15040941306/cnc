"""
units_and_infantry.py - High-detail Westwood-faithful 3D Allied & Soviet vehicles & multi-joint infantry.
All units face +Z (Unity Vector3.forward) with independent TurretPivot & BarrelPivot nodes.
"""

from __future__ import annotations
import math
from common import PartCollector, create_pivot, create_root


def build_mtnk():
    """Allied Grizzly Battle Tank matched to Cameos/MTNK.png."""
    root = create_root("MTNK")
    pc = PartCollector(is_soviet=False)

    for sx in (-0.66, 0.66):
        pc.add_track_pod((sx, 0.0, 0.0), width=0.35, height=0.44, length=2.30, wheel_count=6, has_skirt=True, skirt_mat="Mat_TeamPaint")

    pc.add_sloped_hull((0.0, 0.42, 0.0), width=1.08, height=0.42, length=2.32, front_slope=0.55, rear_slope=0.25, side_taper=0.90, mat="Mat_BrushedSteel", bevel=0.025)
    pc.add_box((0.0, 0.56, 0.68), (0.34, 0.10, 0.24), "Mat_DarkSteel", bevel=0.015)  # Driver hatch & periscope
    pc.add_box((0.0, 0.64, -0.62), (0.94, 0.08, 0.82), "Mat_DarkSteel", bevel=0.01)  # Louvered engine deck
    for hx in (-0.42, 0.42):
        pc.add_sphere((hx, 0.49, 1.08), 0.055, "Mat_GlowCyan", segments=10)
        pc.add_box((hx, 0.30, 1.14), (0.08, 0.10, 0.10), "Mat_DarkSteel", bevel=0.01)  # Tow shackles
        pc.add_box((hx * 0.9, 0.48, -1.16), (0.24, 0.14, 0.12), "Mat_DarkSteel", bevel=0.01)  # Exhausts
    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur_piv = (0.0, 0.66, 0.06)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=False)
    tpc.add_sloped_hull((0.0, 0.85, 0.02), width=1.02, height=0.36, length=1.26, front_slope=0.38, rear_slope=0.18, side_taper=0.78, mat="Mat_TeamPaint", bevel=0.03)
    tpc.add_box((0.0, 0.88, -0.66), (0.78, 0.24, 0.30), "Mat_DarkSteel", bevel=0.02)
    tpc.add_railing((-0.38, 1.00, -0.78), (0.38, 1.00, -0.78), height=0.12, posts=3)
    # Smoke grenade discharger clusters (3 tubes per cheek!)
    for side in (-1, 1):
        for s_idx in range(3):
            tpc.add_cylinder((side * 0.50, 0.84 + s_idx * 0.05, 0.22 + s_idx * 0.06), 0.032, 0.032, 0.18, "Mat_BrushedSteel", segments=8, axis='Z', rot_deg=(-18.0, side * 20.0, 0.0))
    # Commander cupola + pintle .50cal MG
    tpc.add_cylinder((0.24, 1.06, -0.08), 0.18, 0.16, 0.12, "Mat_BrushedSteel", segments=14)
    tpc.add_box((0.24, 1.16, 0.04), (0.08, 0.09, 0.22), "Mat_DarkSteel", bevel=0.008)
    tpc.add_cylinder((0.24, 1.17, 0.28), 0.022, 0.018, 0.38, "Mat_BrushedSteel", segments=8, axis='Z')
    tpc.build_mesh_object("TurretMesh", tur_piv, parent=tur)

    bar_piv = (0.0, 0.85, 0.54)
    bar = create_pivot("BarrelPivot", bar_piv, parent=tur)
    bpc = PartCollector(is_soviet=False)
    bpc.add_box((0.0, 0.85, 0.58), (0.32, 0.22, 0.26), "Mat_DarkSteel", bevel=0.02)
    bpc.add_cylinder((0.0, 0.85, 1.22), 0.068, 0.056, 1.32, "Mat_BrushedSteel", segments=16, axis='Z')
    bpc.add_cylinder((0.0, 0.85, 1.08), 0.095, 0.090, 0.36, "Mat_TeamPaint", segments=16, axis='Z')
    bpc.add_cylinder((0.0, 0.85, 1.88), 0.078, 0.078, 0.14, "Mat_DarkSteel", segments=16, axis='Z')
    bpc.add_cylinder((0.0, 0.85, 1.95), 0.042, 0.042, 0.04, "Mat_TreadRubber", segments=12, axis='Z')
    bpc.build_mesh_object("BarrelMesh", bar_piv, parent=bar)
    return root


def build_htnk():
    """Soviet Rhino Heavy Tank matched to Cameos/HTNK.png."""
    root = create_root("HTNK")
    pc = PartCollector(is_soviet=True)

    for sx in (-0.72, 0.72):
        pc.add_track_pod((sx, 0.0, 0.0), width=0.38, height=0.46, length=2.45, wheel_count=6, has_skirt=True, skirt_mat="Mat_TeamPaint")

    pc.add_sloped_hull((0.0, 0.44, 0.0), width=1.20, height=0.44, length=2.48, front_slope=0.60, rear_slope=0.28, side_taper=0.88, mat="Mat_DarkSteel", bevel=0.03)
    # Glacis reactive armor (ERA) bricks
    for ex in (-0.34, 0.0, 0.34):
        pc.add_box((ex, 0.53, 0.82), (0.28, 0.08, 0.38), "Mat_TeamPaint", bevel=0.01, rot_deg=(20.0, 0.0, 0.0))
    # Twin external Soviet cylindrical fuel drums + straps
    for fx in (-0.36, 0.36):
        pc.add_cylinder((fx, 0.60, -1.26), 0.16, 0.16, 0.52, "Mat_BrushedSteel", segments=16, axis='X')
        pc.add_torus((fx - 0.14, 0.60, -1.26), 0.165, 0.018, "Mat_DarkSteel", rot_deg=(0.0, 90.0, 0.0))
        pc.add_torus((fx + 0.14, 0.60, -1.26), 0.165, 0.018, "Mat_DarkSteel", rot_deg=(0.0, 90.0, 0.0))
    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur_piv = (0.0, 0.68, 0.10)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=True)
    tpc.add_cylinder((0.0, 0.85, 0.06), 0.64, 0.48, 0.36, "Mat_TeamPaint", segments=20)
    tpc.add_sloped_hull((0.0, 0.85, 0.14), width=1.06, height=0.34, length=1.22, front_slope=0.42, rear_slope=0.25, side_taper=0.72, mat="Mat_TeamPaint", bevel=0.04)
    # Cheek ERA blocks + Infrared searchlight + twin antennas
    for side in (-1, 1):
        tpc.add_box((side * 0.46, 0.86, 0.36), (0.14, 0.18, 0.34), "Mat_BrushedSteel", bevel=0.01, rot_deg=(0.0, side * -28.0, 0.0))
    tpc.add_cylinder((0.24, 0.96, 0.52), 0.08, 0.08, 0.12, "Mat_DarkSteel", segments=12, axis='Z')
    tpc.add_sphere((0.24, 0.96, 0.58), 0.065, "Mat_GlowRed", segments=10)
    tpc.add_cylinder((-0.24, 1.07, -0.05), 0.18, 0.16, 0.12, "Mat_DarkSteel", segments=14)
    for ax in (-0.36, 0.32):
        tpc.add_cylinder((ax, 1.40, -0.32), 0.014, 0.006, 0.72, "Mat_BrushedSteel", segments=6)
    tpc.build_mesh_object("TurretMesh", tur_piv, parent=tur)

    bar_piv = (0.0, 0.86, 0.58)
    bar = create_pivot("BarrelPivot", bar_piv, parent=tur)
    bpc = PartCollector(is_soviet=True)
    bpc.add_box((0.0, 0.86, 0.62), (0.36, 0.22, 0.28), "Mat_DarkSteel", bevel=0.02)
    bpc.add_cylinder((0.0, 0.86, 1.35), 0.082, 0.070, 1.46, "Mat_BrushedSteel", segments=16, axis='Z')
    bpc.add_cylinder((0.0, 0.86, 1.18), 0.110, 0.106, 0.38, "Mat_DarkSteel", segments=16, axis='Z')
    bpc.add_box((0.0, 0.86, 2.08), (0.20, 0.16, 0.24), "Mat_DarkSteel", bevel=0.02)
    bpc.add_cylinder((0.0, 0.86, 2.20), 0.050, 0.050, 0.04, "Mat_TreadRubber", segments=12, axis='Z')
    bpc.build_mesh_object("BarrelMesh", bar_piv, parent=bar)
    return root


def build_apoc():
    """Soviet Apocalypse Tank matched to Cameos/APOC.png."""
    root = create_root("APOC")
    pc = PartCollector(is_soviet=True)

    for sx in (-0.88, 0.88):
        for sz in (-0.74, 0.74):
            pc.add_track_pod((sx, 0.0, sz), width=0.40, height=0.48, length=1.24, wheel_count=4, has_skirt=True, skirt_mat="Mat_TeamPaint")
            pc.add_cylinder((sx * 0.72, 0.38, sz), 0.09, 0.09, 0.45, "Mat_DarkSteel", segments=10, axis='X')

    pc.add_sloped_hull((0.0, 0.50, 0.0), width=1.48, height=0.52, length=2.78, front_slope=0.55, rear_slope=0.35, side_taper=0.86, mat="Mat_DarkSteel", bevel=0.04)
    pc.add_box((0.0, 0.74, -0.80), (1.24, 0.18, 0.96), "Mat_TeamPaint", bevel=0.03)
    for ex in (-0.42, 0.42):
        pc.add_pipe_run([(ex, 0.72, -1.18), (ex, 1.18, -1.36)], radius=0.068, mat="Mat_BrushedSteel", flanges=True)

    # Toothed Front Dozer/Ramming Blade + Hydraulic Cylinders!
    pc.add_box((0.0, 0.28, 1.50), (1.82, 0.40, 0.18), "Mat_BrushedSteel", bevel=0.02, rot_deg=(24.0, 0.0, 0.0))
    for tx in (-0.72, -0.36, 0.0, 0.36, 0.72):
        pc.add_box((tx, 0.12, 1.60), (0.10, 0.16, 0.14), "Mat_GoldBrass", bevel=0.01, rot_deg=(30.0, 0.0, 0.0))
    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur_piv = (0.0, 0.78, 0.08)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=True)
    tpc.add_sloped_hull((0.0, 1.02, 0.05), width=1.36, height=0.46, length=1.52, front_slope=0.42, rear_slope=0.28, side_taper=0.80, mat="Mat_TeamPaint", bevel=0.04)
    tpc.add_box((0.0, 1.10, 0.60), (0.68, 0.11, 0.16), "Mat_GlowRed", bevel=0.01)
    tpc.add_cylinder((0.0, 1.28, -0.15), 0.26, 0.22, 0.12, "Mat_BrushedSteel", segments=14)
    # Mammoth Tusk Side AA Missile Pods with 6 visible rocket warheads!
    for mx in (-0.80, 0.80):
        tpc.add_box((mx, 1.08, -0.04), (0.30, 0.36, 0.76), "Mat_DarkSteel", bevel=0.02, rot_deg=(-10.0, 0.0, 0.0))
        tpc.add_decal_panel((mx, 1.10, 0.35), 0.26, 0.30, 0.04, "Mat_HazardYellow", rot_deg=(-10.0, 0.0, 0.0), face_plus_z=True)
        for rx in (-0.06, 0.06):
            for ry in (-0.07, 0.07):
                tpc.add_sphere((mx + rx, 1.10 + ry, 0.37), 0.038, "Mat_GlowRed", segments=8)
    tpc.build_mesh_object("TurretMesh", tur_piv, parent=tur)

    bar_piv = (0.0, 1.02, 0.68)
    bar = create_pivot("BarrelPivot", bar_piv, parent=tur)
    bpc = PartCollector(is_soviet=True)
    bpc.add_box((0.0, 1.02, 0.74), (0.72, 0.24, 0.30), "Mat_DarkSteel", bevel=0.02)
    for bx in (-0.22, 0.22):
        bpc.add_cylinder((bx, 1.02, 1.50), 0.078, 0.066, 1.58, "Mat_BrushedSteel", segments=16, axis='Z')
        bpc.add_cylinder((bx, 1.02, 1.30), 0.106, 0.102, 0.40, "Mat_DarkSteel", segments=16, axis='Z')
        bpc.add_box((bx, 1.02, 2.30), (0.18, 0.16, 0.24), "Mat_DarkSteel", bevel=0.02)
        bpc.add_cylinder((bx, 1.02, 2.42), 0.046, 0.046, 0.04, "Mat_TreadRubber", segments=12, axis='Z')
    bpc.build_mesh_object("BarrelMesh", bar_piv, parent=bar)
    return root


def build_sref():
    """Allied Prism Tank matched to Cameos/SREF.png."""
    root = create_root("SREF")
    pc = PartCollector(is_soviet=False)

    for sx in (-0.65, 0.65):
        pc.add_track_pod((sx, 0.0, 0.0), width=0.33, height=0.42, length=2.28, wheel_count=5, has_skirt=True, skirt_mat="Mat_GoldBrass")

    pc.add_sloped_hull((0.0, 0.42, 0.0), width=1.10, height=0.42, length=2.28, front_slope=0.64, rear_slope=0.22, side_taper=0.86, mat="Mat_BrushedSteel", bevel=0.03)
    pc.add_box((0.0, 0.56, 0.60), (0.74, 0.20, 0.56), "Mat_GlassDark", bevel=0.02, rot_deg=(20.0, 0.0, 0.0))
    for cx in (-0.44, 0.44):
        pc.add_cylinder((cx, 0.68, -0.68), 0.16, 0.16, 0.62, "Mat_TeamPaint", segments=14, axis='Z')
        pc.add_torus((cx, 0.68, -0.52), 0.168, 0.022, "Mat_GlowCyan", rot_deg=(90.0, 0.0, 0.0))
    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur_piv = (0.0, 0.66, -0.08)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=False)
    tpc.add_cylinder((0.0, 0.74, -0.08), 0.46, 0.38, 0.18, "Mat_TeamPaint", segments=16)
    for yx in (-0.34, 0.34):
        tpc.add_box((yx, 1.14, -0.14), (0.12, 0.78, 0.24), "Mat_GoldBrass", bevel=0.02, rot_deg=(-12.0, 0.0, 0.0))
    tpc.add_cylinder((0.0, 1.44, 0.02), 0.38, 0.20, 0.62, "Mat_BrushedSteel", segments=18, axis='Z')
    for rz in (-0.12, 0.06, 0.24):
        tpc.add_torus((0.0, 1.44, rz), 0.32, 0.04, "Mat_GoldBrass", rot_deg=(90.0, 0.0, 0.0))
    tpc.add_sphere((0.0, 1.44, 0.32), 0.27, "Mat_GlowCyan", segments=18)
    tpc.build_mesh_object("PrismTurret", tur_piv, parent=tur)
    return root


def build_fv():
    """Allied Multipurpose IFV matched to Cameos/FV.png."""
    root = create_root("FV")
    pc = PartCollector(is_soviet=False)

    for sx in (-0.65, 0.65):
        for wz in (-0.74, 0.0, 0.74):
            pc.add_cylinder((sx, 0.29, wz), 0.29, 0.29, 0.25, "Mat_TreadRubber", segments=18, axis='X', bevel_caps=0.03)
            pc.add_cylinder((sx + (0.05 if sx > 0 else -0.05), 0.29, wz), 0.15, 0.15, 0.22, "Mat_GoldBrass", segments=12, axis='X')

    pc.add_sloped_hull((0.0, 0.48, 0.0), width=1.18, height=0.46, length=2.26, front_slope=0.68, rear_slope=0.24, side_taper=0.84, mat="Mat_BrushedSteel", bevel=0.03)
    pc.add_box((0.0, 0.58, 0.70), (0.78, 0.16, 0.38), "Mat_GlassDark", bevel=0.02, rot_deg=(26.0, 0.0, 0.0))
    pc.add_box((0.0, 0.54, -0.15), (1.24, 0.16, 1.48), "Mat_TeamPaint", bevel=0.02)
    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur_piv = (0.0, 0.74, -0.10)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=False)
    tpc.add_cylinder((0.0, 0.88, -0.10), 0.26, 0.22, 0.28, "Mat_DarkSteel", segments=14)
    tpc.add_sphere((0.0, 1.10, 0.08), 0.15, "Mat_GlowCyan", segments=14)
    for mx in (-0.38, 0.38):
        tpc.add_box((mx, 1.04, -0.02), (0.32, 0.28, 0.80), "Mat_TeamPaint", bevel=0.02)
        tpc.add_box((mx, 1.04, 0.38), (0.26, 0.22, 0.05), "Mat_TreadRubber", bevel=0.005)
        for rx in (-0.06, 0.06):
            for ry in (-0.05, 0.05):
                tpc.add_sphere((mx + rx, 1.04 + ry, 0.41), 0.034, "Mat_BrushedSteel", segments=8)
    tpc.build_mesh_object("MissileTurret", tur_piv, parent=tur)
    return root


def build_cmin():
    """Allied Chrono Miner matched to Cameos/CMIN.png."""
    root = create_root("CMIN")
    pc = PartCollector(is_soviet=False)

    for sx in (-0.68, 0.68):
        pc.add_track_pod((sx, 0.0, -0.10), width=0.34, height=0.44, length=2.18, wheel_count=5, has_skirt=True, skirt_mat="Mat_BrushedSteel")

    # Rear ore hopper filled with golden ore chunks!
    pc.add_box((0.0, 0.72, -0.68), (1.30, 0.72, 1.08), "Mat_DarkSteel", bevel=0.03)
    pc.add_box((0.0, 1.10, -0.68), (1.18, 0.12, 0.94), "Mat_TeamPaint", bevel=0.02)
    for ox, oz, orad in ((-0.28, -0.65, 0.22), (0.24, -0.72, 0.24), (0.0, -0.52, 0.20)):
        pc.add_sphere((ox, 1.16, oz), orad, "Mat_GoldBrass", scale=(1.2, 0.65, 1.1), segments=10)

    # Central Chronosphere Energy Core with crossed containment arches
    pc.add_sphere((0.0, 0.78, 0.22), 0.52, "Mat_GlowPlasma", segments=20)
    pc.add_torus((0.0, 0.78, 0.22), 0.55, 0.065, "Mat_TeamPaint", rot_deg=(0.0, 90.0, 0.0))
    pc.add_torus((0.0, 0.78, 0.22), 0.55, 0.055, "Mat_GoldBrass", rot_deg=(90.0, 0.0, 0.0))

    # Front toothed rotary excavator drum + hydraulic lift arms
    for ax in (-0.62, 0.62):
        pc.add_box((ax, 0.36, 0.88), (0.12, 0.18, 0.86), "Mat_BrushedSteel", bevel=0.015, rot_deg=(18.0, 0.0, 0.0))
    pc.add_cylinder((0.0, 0.26, 1.25), 0.22, 0.22, 1.18, "Mat_GoldBrass", segments=16, axis='X')
    for ti in range(10):
        ta = math.radians(ti * 36.0)
        pc.add_box((0.0, 0.26 + math.cos(ta) * 0.23, 1.25 + math.sin(ta) * 0.23), (1.10, 0.04, 0.04), "Mat_DarkSteel", bevel=0.004)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_harv():
    """Soviet War Miner matched to Cameos/HARV.png."""
    root = create_root("HARV")
    pc = PartCollector(is_soviet=True)

    for sx in (-0.72, 0.72):
        pc.add_track_pod((sx, 0.0, -0.15), width=0.38, height=0.46, length=2.28, wheel_count=5, has_skirt=False)

    # Ribbed dump bed heaped with gold ore!
    pc.add_box((0.0, 0.78, -0.66), (1.44, 0.78, 1.26), "Mat_SovietBrick", bevel=0.03, top_scale=(1.08, 1.04))
    for rz in (-1.06, -0.66, -0.26):
        pc.add_box((0.0, 0.78, rz), (1.52, 0.74, 0.09), "Mat_BrushedSteel", bevel=0.01)
    for ox, oz in ((-0.32, -0.65), (0.30, -0.75), (0.0, -0.50)):
        pc.add_sphere((ox, 1.20, oz), 0.28, "Mat_GoldBrass", scale=(1.2, 0.55, 1.2), segments=10)

    pc.add_sloped_hull((0.0, 0.86, 0.38), width=1.20, height=0.58, length=0.94, front_slope=0.32, rear_slope=0.08, side_taper=0.86, mat="Mat_TeamPaint", bevel=0.03)
    pc.add_box((0.0, 0.94, 0.78), (0.84, 0.18, 0.14), "Mat_GlassDark", bevel=0.01, rot_deg=(20.0, 0.0, 0.0))
    for ex in (-0.52, 0.52):
        pc.add_pipe_run([(ex, 0.85, 0.05), (ex, 1.48, -0.02)], radius=0.055)

    for side in (-1, 1):
        pc.add_box((side * 0.44, 0.32, 1.28), (0.96, 0.50, 0.16), "Mat_BrushedSteel", bevel=0.02, rot_deg=(24.0, side * -24.0, 0.0))
        pc.add_pipe_run([(side * 0.42, 0.42, 0.78), (side * 0.42, 0.28, 1.22)], radius=0.055, mat="Mat_DarkSteel")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur_piv = (0.0, 1.18, 0.35)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=True)
    tpc.add_cylinder((0.0, 1.28, 0.35), 0.28, 0.22, 0.22, "Mat_DarkSteel", segments=14)
    tpc.add_cylinder((0.0, 1.30, 0.74), 0.045, 0.035, 0.72, "Mat_BrushedSteel", segments=12, axis='Z')
    tpc.build_mesh_object("MinerGunTurret", tur_piv, parent=tur)
    return root


def build_amcv():
    """Allied MCV matched to Cameos/AMCV.png."""
    root = create_root("AMCV")
    pc = PartCollector(is_soviet=False)

    for sx in (-0.78, 0.78):
        for wz in (-0.98, -0.32, 0.88):
            pc.add_cylinder((sx, 0.34, wz), 0.34, 0.34, 0.28, "Mat_TreadRubber", segments=18, axis='X', bevel_caps=0.03)
            pc.add_cylinder((sx + (0.06 if sx > 0 else -0.06), 0.34, wz), 0.17, 0.17, 0.26, "Mat_GoldBrass", segments=12, axis='X')

    pc.add_box((0.0, 0.46, 0.0), (1.44, 0.22, 2.88), "Mat_DarkSteel", bevel=0.02)
    pc.add_sloped_hull((0.0, 0.96, 1.02), width=1.54, height=0.88, length=0.90, front_slope=0.18, rear_slope=0.04, side_taper=0.92, mat="Mat_BrushedSteel", bevel=0.04)
    pc.add_box((0.0, 1.12, 1.44), (1.30, 0.32, 0.12), "Mat_GlassDark", bevel=0.01, rot_deg=(10.0, 0.0, 0.0))
    pc.add_decal_panel((0.0, 0.62, 1.48), 1.56, 0.24, 0.14, "Mat_HazardYellow", face_plus_z=True)
    for ex in (-0.72, 0.72):
        pc.add_pipe_run([(ex, 0.58, 0.56), (ex, 1.68, 0.52)], radius=0.055)

    pc.add_quonset_arch((0.0, 0.58, -0.45), width=1.50, arch_height=0.88, length=1.88, mat_shell="Mat_CorrugatedSteel", mat_ribs="Mat_TeamPaint", rib_count=5)
    pc.add_lattice_boom((0.0, 1.58, -1.25), (0.0, 1.58, 0.45), width=0.42, height=0.26, bays=5, mat="Mat_HazardYellow")

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_smcv():
    """Soviet MCV matched to Cameos/SMCV.png."""
    root = create_root("SMCV")
    pc = PartCollector(is_soviet=True)

    for sx in (-0.88, 0.88):
        for sz in (-0.75, 0.75):
            pc.add_track_pod((sx, 0.0, sz), width=0.38, height=0.46, length=1.24, wheel_count=4, has_skirt=True, skirt_mat="Mat_BrushedSteel")

    pc.add_box((0.0, 0.48, 0.0), (1.56, 0.28, 2.78), "Mat_DarkSteel", bevel=0.03)
    pc.add_sloped_hull((0.0, 0.98, 0.88), width=1.60, height=0.84, length=1.10, front_slope=0.38, rear_slope=0.08, side_taper=0.85, mat="Mat_TeamPaint", bevel=0.04)
    pc.add_box((0.0, 1.16, 1.34), (1.00, 0.20, 0.14), "Mat_GlassDark", bevel=0.01, rot_deg=(20.0, 0.0, 0.0))
    pc.add_decal_panel((0.0, 0.76, 1.38), 0.72, 0.42, 0.10, "Mat_SovietGate", rot_deg=(20.0, 0.0, 0.0), face_plus_z=True)

    pc.add_cylinder((0.0, 1.02, -0.52), 0.58, 0.58, 1.58, "Mat_BrushedSteel", segments=18, axis='Z')
    for gz in (-0.95, -0.15):
        pc.add_box((0.0, 1.12, gz), (1.64, 1.18, 0.24), "Mat_SovietBrick", bevel=0.03, top_scale=(0.78, 1.0))
        pc.add_box((0.0, 1.72, gz), (1.20, 0.14, 0.26), "Mat_TeamPaint", bevel=0.01)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def _build_humanoid_soldier(unit_id: str, is_soviet: bool, heavy_at: bool) -> object:
    """Builds a sculpted multi-joint 3D infantry trooper with anatomical limbs, helmet rim, web gear & weapon."""
    root = create_root(unit_id)
    pc = PartCollector(is_soviet=is_soviet)

    cloth_mat = "Mat_SovietCoat" if is_soviet else "Mat_AlliedCloth"

    # Articulated Boots -> Calves -> Knees -> Thighs -> Pelvis
    for side, lx, lz in ((-1, -0.12, 0.04), (1, 0.12, -0.05)):
        pc.add_box((lx, 0.07, lz + 0.04), (0.12, 0.13, 0.23), "Mat_TreadRubber", bevel=0.025)
        pc.add_cylinder((lx, 0.24, lz), 0.068, 0.078, 0.24, cloth_mat, segments=12)
        pc.add_sphere((lx, 0.37, lz + 0.01), 0.076, "Mat_TeamPaint" if heavy_at else cloth_mat, segments=10)
        pc.add_cylinder((lx, 0.49, lz), 0.082, 0.092, 0.24, cloth_mat, segments=12)

    if is_soviet:
        # Flared Soviet Conscript Greatcoat + Belt + Brass Buckle + Pouches
        pc.add_lathe_profile((0.0, 0.42, 0.0), [(0.26, 0.0), (0.21, 0.18), (0.17, 0.30)], "Mat_SovietCoat", segments=16, flute_amp=0.03, flute_count=8)
        pc.add_torus((0.0, 0.70, 0.0), 0.175, 0.030, "Mat_TreadRubber")
        pc.add_box((0.0, 0.90, 0.0), (0.36, 0.36, 0.23), "Mat_SovietCoat", bevel=0.035, top_scale=(1.12, 1.04))
        pc.add_box((-0.09, 0.90, 0.12), (0.10, 0.14, 0.06), "Mat_GoldBrass", bevel=0.01)
        pc.add_box(( 0.09, 0.90, 0.12), (0.10, 0.14, 0.06), "Mat_GoldBrass", bevel=0.01)
        for sx in (-0.21, 0.21):
            pc.add_sphere((sx, 1.04, 0.0), 0.085, "Mat_TeamPaint", scale=(1.1, 0.65, 1.1), segments=12)
    else:
        pc.add_sphere((0.0, 0.62, 0.0), 0.17, cloth_mat, scale=(1.05, 0.75, 0.85), segments=12)
        pc.add_torus((0.0, 0.68, 0.0), 0.17, 0.028, "Mat_TreadRubber")
        pc.add_box((0.0, 0.89, 0.0), (0.36, 0.36, 0.24), "Mat_TeamPaint", bevel=0.035, top_scale=(1.14, 1.04))
        pc.add_box((0.0, 0.89, -0.15), (0.26, 0.30, 0.12), "Mat_DarkSteel", bevel=0.025)
        for sx in (-0.23, 0.23):
            pc.add_sphere((sx, 1.03, 0.0), 0.092, "Mat_TeamPaint" if heavy_at else "Mat_BrushedSteel", segments=12)

    # Neck + Head + Iconic Helmet
    pc.add_cylinder((0.0, 1.11, 0.01), 0.06, 0.055, 0.08, "Mat_Skin", segments=10)
    pc.add_sphere((0.0, 1.21, 0.02), 0.115, "Mat_Skin", scale=(0.95, 1.08, 1.0), segments=16)
    if is_soviet:
        pc.add_sphere((0.0, 1.18, 0.11), 0.068, "Mat_TreadRubber", segments=12)
        pc.add_cylinder((0.0, 1.13, 0.15), 0.036, 0.036, 0.06, "Mat_DarkSteel", segments=10, axis='Z')
        pc.add_sphere((0.0, 1.26, 0.01), 0.145, "Mat_GoldBrass", scale=(1.06, 0.80, 1.14), segments=16)
        pc.add_torus((0.0, 1.23, 0.01), 0.142, 0.018, "Mat_GoldBrass", rot_deg=(8.0, 0.0, 0.0))
    elif heavy_at:
        pc.add_sphere((0.0, 1.26, 0.01), 0.150, "Mat_BrushedSteel", scale=(1.08, 0.82, 1.14), segments=16)
        pc.add_box((0.0, 1.22, 0.13), (0.20, 0.07, 0.05), "Mat_GlowCyan", bevel=0.01)
    else:
        pc.add_sphere((0.0, 1.26, 0.01), 0.145, "Mat_DarkSteel", scale=(1.06, 0.78, 1.12), segments=16)
        pc.add_torus((0.0, 1.23, 0.01), 0.140, 0.018, "Mat_DarkSteel", rot_deg=(6.0, 0.0, 0.0))

    # Multi-segment Upper Arm -> Elbow -> Forearm -> Gloved Hands gripping rifle!
    pc.add_pipe_run([( 0.22, 1.00, 0.0), ( 0.22, 0.84, 0.10), ( 0.09, 0.84, 0.24)], radius=0.052, mat=cloth_mat, flanges=False)
    pc.add_pipe_run([(-0.22, 1.00, 0.0), (-0.16, 0.84, 0.16), ( 0.06, 0.84, 0.40)], radius=0.052, mat=cloth_mat, flanges=False)
    pc.add_sphere((0.09, 0.84, 0.24), 0.048, "Mat_TreadRubber", segments=10)
    pc.add_sphere((0.06, 0.84, 0.40), 0.048, "Mat_TreadRubber", segments=10)

    if heavy_at:
        pc.add_cylinder((0.22, 1.08, 0.18), 0.082, 0.082, 0.94, "Mat_DarkSteel", segments=14, axis='Z')
        pc.add_torus((0.22, 1.08, 0.64), 0.092, 0.022, "Mat_HazardYellow", rot_deg=(90.0, 0.0, 0.0))
        pc.add_torus((0.22, 1.08, -0.28), 0.092, 0.022, "Mat_DarkSteel", rot_deg=(90.0, 0.0, 0.0))
    elif is_soviet:
        pc.add_box((0.08, 0.86, 0.32), (0.045, 0.075, 0.42), "Mat_DarkSteel", bevel=0.005)
        pc.add_box((0.08, 0.84, 0.08), (0.045, 0.085, 0.20), "Mat_WoodStock", bevel=0.006)
        pc.add_box((0.08, 0.87, 0.46), (0.050, 0.065, 0.16), "Mat_WoodStock", bevel=0.005)
        pc.add_cylinder((0.08, 0.87, 0.66), 0.016, 0.016, 0.30, "Mat_DarkSteel", segments=8, axis='Z')
        pc.add_box((0.08, 0.77, 0.35), (0.032, 0.14, 0.055), "Mat_DarkSteel", bevel=0.005, rot_deg=(20.0, 0.0, 0.0))
    else:
        pc.add_box((0.08, 0.86, 0.34), (0.050, 0.085, 0.46), "Mat_DarkSteel", bevel=0.005)
        pc.add_box((0.08, 0.92, 0.30), (0.028, 0.045, 0.18), "Mat_DarkSteel", bevel=0.004)  # Carry handle sight
        pc.add_cylinder((0.08, 0.88, 0.68), 0.018, 0.016, 0.34, "Mat_BrushedSteel", segments=8, axis='Z')

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_e1():
    return _build_humanoid_soldier("E1", is_soviet=False, heavy_at=False)


def build_ggi():
    return _build_humanoid_soldier("GGI", is_soviet=False, heavy_at=True)


def build_e2():
    return _build_humanoid_soldier("E2", is_soviet=True, heavy_at=False)


UNITS_AND_INFANTRY = {
    "MTNK": (build_mtnk, 3.2),
    "HTNK": (build_htnk, 3.4),
    "APOC": (build_apoc, 3.8),
    "SREF": (build_sref, 3.2),
    "FV":   (build_fv,   3.1),
    "CMIN": (build_cmin, 3.2),
    "HARV": (build_harv, 3.4),
    "AMCV": (build_amcv, 3.8),
    "SMCV": (build_smcv, 3.8),
    "E1":   (build_e1,   1.9),
    "GGI":  (build_ggi,  1.9),
    "E2":   (build_e2,   1.9),
}
