"""
units_and_infantry.py - High-detail 3D hard-surface Allied & Soviet vehicles + articulated 3D infantry.
All units face +Z (Unity Vector3.forward), with independent TurretPivot and BarrelPivot nodes!
"""

from __future__ import annotations
import math
from common import PartCollector, create_pivot, create_root


def build_mtnk():
    """Allied Grizzly Battle Tank: Sloped NATO glacis + side skirts + wedge turret + 105mm thermal-sleeved barrel."""
    root = create_root("MTNK")
    pc = PartCollector(is_soviet=False)

    # Left & Right 6-wheel track pods with Allied Blue side skirts
    for sx in (-0.64, 0.64):
        pc.add_track_pod((sx, 0.0, 0.0), width=0.34, height=0.44, length=2.25, wheel_count=6, has_skirt=True, skirt_mat="Mat_TeamPaint")

    # Main sloped armored hull (+Z forward!)
    pc.add_sloped_hull((0.0, 0.42, 0.0), width=1.08, height=0.42, length=2.28, front_slope=0.52, rear_slope=0.25, side_taper=0.90, mat="Mat_BrushedSteel", bevel=0.03)
    pc.add_box((0.0, 0.64, -0.58), (0.94, 0.08, 0.78), "Mat_DarkSteel", bevel=0.01)  # Engine deck grille
    for hx in (-0.42, 0.42):
        pc.add_sphere((hx, 0.48, 1.06), 0.06, "Mat_GlowCyan", segments=8)  # Headlights

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    # Rotating Wedge Turret
    tur_piv = (0.0, 0.66, 0.06)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=False)
    tpc.add_sloped_hull((0.0, 0.84, 0.02), width=0.98, height=0.36, length=1.22, front_slope=0.36, rear_slope=0.18, side_taper=0.78, mat="Mat_TeamPaint", bevel=0.03)
    tpc.add_box((0.0, 0.86, -0.62), (0.76, 0.24, 0.28), "Mat_DarkSteel", bevel=0.02)  # Bustle rack
    tpc.add_cylinder((0.22, 1.06, -0.10), 0.18, 0.16, 0.12, "Mat_BrushedSteel", segments=12)  # Commander cupola
    tpc.add_cylinder((0.22, 1.16, 0.14), 0.025, 0.02, 0.45, "Mat_DarkSteel", segments=8, axis='Z')  # Roof .50cal MG
    tpc.build_mesh_object("TurretMesh", tur_piv, parent=tur)

    # Recoiling 105mm Cannon Barrel (+Z forward!)
    bar_piv = (0.0, 0.84, 0.52)
    bar = create_pivot("BarrelPivot", bar_piv, parent=tur)
    bpc = PartCollector(is_soviet=False)
    bpc.add_box((0.0, 0.84, 0.56), (0.28, 0.20, 0.24), "Mat_DarkSteel", bevel=0.02)  # Gun mantlet
    bpc.add_cylinder((0.0, 0.84, 1.18), 0.068, 0.058, 1.25, "Mat_BrushedSteel", segments=14, axis='Z')
    bpc.add_cylinder((0.0, 0.84, 1.06), 0.092, 0.088, 0.34, "Mat_DarkSteel", segments=14, axis='Z')  # Bore evacuator
    bpc.add_cylinder((0.0, 0.84, 1.80), 0.078, 0.078, 0.14, "Mat_DarkSteel", segments=14, axis='Z')  # Muzzle collar
    bpc.build_mesh_object("BarrelMesh", bar_piv, parent=bar)
    return root


def build_htnk():
    """Soviet Rhino Heavy Tank: Wide aggressive hull + rear fuel drums + cast rounded-wedge turret + heavy 120mm gun."""
    root = create_root("HTNK")
    pc = PartCollector(is_soviet=True)

    for sx in (-0.70, 0.70):
        pc.add_track_pod((sx, 0.0, 0.0), width=0.38, height=0.46, length=2.42, wheel_count=6, has_skirt=True, skirt_mat="Mat_TeamPaint")

    pc.add_sloped_hull((0.0, 0.44, 0.0), width=1.18, height=0.44, length=2.45, front_slope=0.58, rear_slope=0.28, side_taper=0.88, mat="Mat_DarkSteel", bevel=0.03)
    pc.add_box((0.0, 0.52, 0.78), (0.96, 0.14, 0.48), "Mat_TeamPaint", bevel=0.02, rot_deg=(18.0, 0.0, 0.0))
    # Iconic twin rear Soviet external fuel drums!
    for fx in (-0.34, 0.34):
        pc.add_cylinder((fx, 0.58, -1.24), 0.15, 0.15, 0.48, "Mat_BrushedSteel", segments=12, axis='X')

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur_piv = (0.0, 0.68, 0.10)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=True)
    tpc.add_cylinder((0.0, 0.85, 0.06), 0.62, 0.46, 0.36, "Mat_TeamPaint", segments=16)
    tpc.add_sloped_hull((0.0, 0.85, 0.14), width=1.02, height=0.34, length=1.18, front_slope=0.42, rear_slope=0.25, side_taper=0.72, mat="Mat_TeamPaint", bevel=0.04)
    tpc.add_cylinder((-0.24, 1.07, -0.05), 0.17, 0.15, 0.12, "Mat_DarkSteel", segments=12)
    tpc.add_cylinder((-0.36, 1.38, -0.28), 0.015, 0.008, 0.65, "Mat_BrushedSteel", segments=6)  # Whip antenna
    tpc.build_mesh_object("TurretMesh", tur_piv, parent=tur)

    bar_piv = (0.0, 0.86, 0.58)
    bar = create_pivot("BarrelPivot", bar_piv, parent=tur)
    bpc = PartCollector(is_soviet=True)
    bpc.add_box((0.0, 0.86, 0.62), (0.34, 0.22, 0.26), "Mat_DarkSteel", bevel=0.02)
    bpc.add_cylinder((0.0, 0.86, 1.32), 0.082, 0.070, 1.42, "Mat_DarkSteel", segments=14, axis='Z')
    bpc.add_cylinder((0.0, 0.86, 1.18), 0.108, 0.105, 0.36, "Mat_BrushedSteel", segments=14, axis='Z')
    bpc.add_box((0.0, 0.86, 2.04), (0.19, 0.16, 0.22), "Mat_DarkSteel", bevel=0.02)  # Heavy Soviet muzzle brake!
    bpc.build_mesh_object("BarrelMesh", bar_piv, parent=bar)
    return root


def build_apoc():
    """Soviet Apocalypse Tank: Quadruple independent track pods + front ramming blade + mammoth missile pods + twin 120mm guns!"""
    root = create_root("APOC")
    pc = PartCollector(is_soviet=True)

    # 4 Quad-Track Pods (Front-Left, Front-Right, Rear-Left, Rear-Right)!
    for sx in (-0.86, 0.86):
        for sz in (-0.72, 0.72):
            pc.add_track_pod((sx, 0.0, sz), width=0.40, height=0.48, length=1.22, wheel_count=4, has_skirt=True, skirt_mat="Mat_TeamPaint")

    # Massive armored super-heavy hull + front steel dozer/grinder blade
    pc.add_sloped_hull((0.0, 0.50, 0.0), width=1.45, height=0.52, length=2.75, front_slope=0.55, rear_slope=0.35, side_taper=0.86, mat="Mat_DarkSteel", bevel=0.04)
    pc.add_box((0.0, 0.72, -0.78), (1.22, 0.18, 0.95), "Mat_TeamPaint", bevel=0.03)
    # Front heavy ramming blade
    pc.add_box((0.0, 0.28, 1.48), (1.75, 0.38, 0.22), "Mat_BrushedSteel", bevel=0.02, rot_deg=(24.0, 0.0, 0.0))

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    # Fortress Turret + Side Mammoth Tusk AA Missile Pods
    tur_piv = (0.0, 0.78, 0.08)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=True)
    tpc.add_sloped_hull((0.0, 1.00, 0.05), width=1.32, height=0.44, length=1.48, front_slope=0.42, rear_slope=0.28, side_taper=0.80, mat="Mat_TeamPaint", bevel=0.04)
    tpc.add_box((0.0, 1.08, 0.58), (0.68, 0.12, 0.18), "Mat_GlowRed", bevel=0.01)  # Glowing red sensor slit
    for mx in (-0.76, 0.76):
        tpc.add_box((mx, 1.06, -0.05), (0.28, 0.34, 0.72), "Mat_DarkSteel", bevel=0.02, rot_deg=(-12.0, 0.0, 0.0))
        tpc.add_box((mx, 1.08, 0.30), (0.22, 0.24, 0.08), "Mat_HazardYellow", bevel=0.01, rot_deg=(-12.0, 0.0, 0.0))
    tpc.build_mesh_object("TurretMesh", tur_piv, parent=tur)

    # Twin 120mm Heavy Cannons!
    bar_piv = (0.0, 1.00, 0.68)
    bar = create_pivot("BarrelPivot", bar_piv, parent=tur)
    bpc = PartCollector(is_soviet=True)
    bpc.add_box((0.0, 1.00, 0.72), (0.68, 0.24, 0.28), "Mat_DarkSteel", bevel=0.02)
    for bx in (-0.21, 0.21):
        bpc.add_cylinder((bx, 1.00, 1.48), 0.076, 0.066, 1.55, "Mat_BrushedSteel", segments=14, axis='Z')
        bpc.add_cylinder((bx, 1.00, 1.28), 0.102, 0.100, 0.38, "Mat_DarkSteel", segments=14, axis='Z')
        bpc.add_box((bx, 1.00, 2.26), (0.17, 0.15, 0.22), "Mat_DarkSteel", bevel=0.02)
    bpc.build_mesh_object("BarrelMesh", bar_piv, parent=bar)
    return root


def build_sref():
    """Allied Prism Tank: Gold/blue high-tech tracked chassis + rotating twin-yoke crystal prism cannon tower!"""
    root = create_root("SREF")
    pc = PartCollector(is_soviet=False)

    for sx in (-0.64, 0.64):
        pc.add_track_pod((sx, 0.0, 0.0), width=0.32, height=0.42, length=2.25, wheel_count=5, has_skirt=True, skirt_mat="Mat_GoldBrass")

    pc.add_sloped_hull((0.0, 0.42, 0.0), width=1.08, height=0.42, length=2.25, front_slope=0.62, rear_slope=0.22, side_taper=0.86, mat="Mat_BrushedSteel", bevel=0.03)
    pc.add_box((0.0, 0.56, 0.58), (0.72, 0.20, 0.55), "Mat_GlassDark", bevel=0.02, rot_deg=(20.0, 0.0, 0.0))
    for cx in (-0.42, 0.42):
        pc.add_cylinder((cx, 0.68, -0.68), 0.16, 0.16, 0.58, "Mat_TeamPaint", segments=12, axis='Z')
    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur_piv = (0.0, 0.66, -0.08)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=False)
    tpc.add_cylinder((0.0, 0.74, -0.08), 0.44, 0.38, 0.18, "Mat_TeamPaint", segments=14)
    # Twin curved support yoke arms cradling the elevated Prism Emitter Dish!
    for yx in (-0.32, 0.32):
        tpc.add_box((yx, 1.12, -0.14), (0.12, 0.76, 0.24), "Mat_GoldBrass", bevel=0.02, rot_deg=(-12.0, 0.0, 0.0))
    tpc.add_cylinder((0.0, 1.42, -0.02), 0.36, 0.22, 0.55, "Mat_BrushedSteel", segments=16, axis='Z')
    tpc.add_sphere((0.0, 1.42, 0.26), 0.26, "Mat_GlowCyan", segments=16)
    tpc.add_torus((0.0, 1.42, 0.18), 0.32, 0.045, "Mat_TeamPaint", rot_deg=(90.0, 0.0, 0.0))
    tpc.build_mesh_object("PrismTurret", tur_piv, parent=tur)
    return root


def build_fv():
    """Allied Multipurpose IFV: 6x6 wheeled scout APC with gold wheel hubs + twin 6-tube missile pod turret."""
    root = create_root("FV")
    pc = PartCollector(is_soviet=False)

    # 6x6 off-road tires with gold hubs
    for sx in (-0.64, 0.64):
        for wz in (-0.72, 0.0, 0.72):
            pc.add_cylinder((sx, 0.28, wz), 0.28, 0.28, 0.24, "Mat_TreadRubber", segments=16, axis='X', bevel_caps=0.03)
            pc.add_cylinder((sx + (0.04 if sx > 0 else -0.04), 0.28, wz), 0.14, 0.14, 0.22, "Mat_GoldBrass", segments=12, axis='X')

    pc.add_sloped_hull((0.0, 0.48, 0.0), width=1.16, height=0.46, length=2.22, front_slope=0.68, rear_slope=0.24, side_taper=0.84, mat="Mat_BrushedSteel", bevel=0.03)
    pc.add_box((0.0, 0.58, 0.68), (0.76, 0.16, 0.38), "Mat_GlassDark", bevel=0.02, rot_deg=(26.0, 0.0, 0.0))
    pc.add_box((0.0, 0.54, -0.15), (1.22, 0.16, 1.45), "Mat_TeamPaint", bevel=0.02)
    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    tur_piv = (0.0, 0.74, -0.10)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=False)
    tpc.add_cylinder((0.0, 0.88, -0.10), 0.26, 0.22, 0.28, "Mat_DarkSteel", segments=12)
    tpc.add_sphere((0.0, 1.08, 0.06), 0.15, "Mat_GlowCyan", segments=12)  # Optical targeter
    for mx in (-0.36, 0.36):
        tpc.add_box((mx, 1.04, -0.04), (0.30, 0.28, 0.78), "Mat_TeamPaint", bevel=0.02)
        tpc.add_box((mx, 1.04, 0.36), (0.24, 0.22, 0.06), "Mat_DarkSteel", bevel=0.005)
    tpc.build_mesh_object("MissileTurret", tur_piv, parent=tur)
    return root


def build_cmin():
    """Allied Chrono Miner: Front gold rotary ore harvester drum + spherical Chronosphere core + rear ore hopper."""
    root = create_root("CMIN")
    pc = PartCollector(is_soviet=False)

    for sx in (-0.68, 0.68):
        pc.add_track_pod((sx, 0.0, -0.10), width=0.34, height=0.44, length=2.15, wheel_count=5, has_skirt=True, skirt_mat="Mat_BrushedSteel")

    # Rear boxy ore hopper
    pc.add_box((0.0, 0.72, -0.68), (1.28, 0.72, 1.05), "Mat_DarkSteel", bevel=0.03)
    pc.add_box((0.0, 1.12, -0.68), (1.16, 0.14, 0.92), "Mat_TeamPaint", bevel=0.02)

    # Central glowing spherical Chronosphere core with red/blue containment arches!
    pc.add_sphere((0.0, 0.78, 0.22), 0.52, "Mat_GlowPlasma", segments=18)
    pc.add_torus((0.0, 0.78, 0.22), 0.55, 0.065, "Mat_TeamPaint", rot_deg=(0.0, 90.0, 0.0))
    pc.add_torus((0.0, 0.78, 0.22), 0.55, 0.055, "Mat_HazardYellow", rot_deg=(90.0, 0.0, 0.0))

    # Front rotary gold-ore scoop arms & roller drum
    for ax in (-0.62, 0.62):
        pc.add_box((ax, 0.36, 0.88), (0.12, 0.18, 0.85), "Mat_BrushedSteel", bevel=0.015, rot_deg=(18.0, 0.0, 0.0))
    pc.add_cylinder((0.0, 0.26, 1.24), 0.22, 0.22, 1.18, "Mat_GoldBrass", segments=14, axis='X')

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_harv():
    """Soviet War Miner: Heavy tracks + massive front V-plow scoop + crimson armored cab + roof 20mm turret + ribbed dump bed."""
    root = create_root("HARV")
    pc = PartCollector(is_soviet=True)

    for sx in (-0.72, 0.72):
        pc.add_track_pod((sx, 0.0, -0.15), width=0.38, height=0.46, length=2.25, wheel_count=5, has_skirt=False)

    # Rear ribbed bronze/steel ore dump bed
    pc.add_box((0.0, 0.78, -0.65), (1.42, 0.78, 1.25), "Mat_SovietBrick", bevel=0.03, top_scale=(1.08, 1.04))
    for rz in (-1.05, -0.65, -0.25):
        pc.add_box((0.0, 0.78, rz), (1.50, 0.72, 0.09), "Mat_BrushedSteel", bevel=0.01)

    # Raised Crimson Armored Cab
    pc.add_sloped_hull((0.0, 0.86, 0.38), width=1.18, height=0.58, length=0.92, front_slope=0.32, rear_slope=0.08, side_taper=0.86, mat="Mat_TeamPaint", bevel=0.03)
    pc.add_box((0.0, 0.94, 0.76), (0.82, 0.18, 0.14), "Mat_GlassDark", bevel=0.01, rot_deg=(20.0, 0.0, 0.0))

    # Massive Front Angled Steel V-Plow Ore Scoop!
    for side in (-1, 1):
        pc.add_box((side * 0.42, 0.32, 1.26), (0.95, 0.48, 0.18), "Mat_BrushedSteel", bevel=0.02, rot_deg=(24.0, side * -24.0, 0.0))

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)

    # Roof-mounted rotating 20mm defensive turret!
    tur_piv = (0.0, 1.18, 0.35)
    tur = create_pivot("TurretPivot", tur_piv, parent=root)
    tpc = PartCollector(is_soviet=True)
    tpc.add_cylinder((0.0, 1.28, 0.35), 0.28, 0.22, 0.22, "Mat_DarkSteel", segments=12)
    tpc.add_cylinder((0.0, 1.30, 0.72), 0.045, 0.035, 0.68, "Mat_BrushedSteel", segments=10, axis='Z')
    tpc.build_mesh_object("MinerGunTurret", tur_piv, parent=tur)
    return root


def build_amcv():
    """Allied MCV: Heavy 6x6 cab-over truck carrying the folded Allied Construction Yard crane & blue fin module!"""
    root = create_root("AMCV")
    pc = PartCollector(is_soviet=False)

    for sx in (-0.78, 0.78):
        for wz in (-0.98, -0.32, 0.88):
            pc.add_cylinder((sx, 0.34, wz), 0.34, 0.34, 0.28, "Mat_TreadRubber", segments=16, axis='X', bevel_caps=0.03)
            pc.add_cylinder((sx + (0.05 if sx > 0 else -0.05), 0.34, wz), 0.16, 0.16, 0.26, "Mat_BrushedSteel", segments=12, axis='X')

    # Heavy chassis frame + Front Cab-Over Truck Cabin
    pc.add_box((0.0, 0.46, 0.0), (1.42, 0.22, 2.85), "Mat_DarkSteel", bevel=0.02)
    pc.add_sloped_hull((0.0, 0.96, 1.02), width=1.52, height=0.88, length=0.88, front_slope=0.18, rear_slope=0.04, side_taper=0.92, mat="Mat_BrushedSteel", bevel=0.04)
    pc.add_box((0.0, 1.12, 1.42), (1.28, 0.32, 0.12), "Mat_GlassDark", bevel=0.01, rot_deg=(10.0, 0.0, 0.0))
    pc.add_box((0.0, 0.62, 1.46), (1.54, 0.24, 0.14), "Mat_TeamPaint", bevel=0.02)

    # Folded Construction Yard Module on Rear Flatbed (Blue arches + Yellow Crane Boom)
    pc.add_quonset_arch((0.0, 0.58, -0.45), width=1.48, arch_height=0.88, length=1.85, mat_shell="Mat_BrushedSteel", mat_ribs="Mat_TeamPaint", rib_count=4)
    pc.add_box((0.0, 1.56, -0.25), (0.44, 0.28, 1.65), "Mat_HazardYellow", bevel=0.02)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def build_smcv():
    """Soviet MCV: Colossal quad-tracked crawler + sloped crimson prow + central boiler cylinder + twin steel gantry ribs."""
    root = create_root("SMCV")
    pc = PartCollector(is_soviet=True)

    for sx in (-0.88, 0.88):
        for sz in (-0.75, 0.75):
            pc.add_track_pod((sx, 0.0, sz), width=0.38, height=0.46, length=1.22, wheel_count=4, has_skirt=True, skirt_mat="Mat_BrushedSteel")

    # Heavy crawler deck + Sloped Crimson Armored Prow
    pc.add_box((0.0, 0.48, 0.0), (1.55, 0.28, 2.75), "Mat_DarkSteel", bevel=0.03)
    pc.add_sloped_hull((0.0, 0.98, 0.88), width=1.58, height=0.82, length=1.08, front_slope=0.38, rear_slope=0.08, side_taper=0.85, mat="Mat_TeamPaint", bevel=0.04)
    pc.add_box((0.0, 1.16, 1.32), (0.98, 0.20, 0.14), "Mat_GlassDark", bevel=0.01, rot_deg=(20.0, 0.0, 0.0))

    # Rear Horizontal Cylindrical Boiler + Twin Heavy Steel Gantry Arches
    pc.add_cylinder((0.0, 1.02, -0.52), 0.58, 0.58, 1.55, "Mat_DarkSteel", segments=16, axis='Z')
    for gz in (-0.95, -0.15):
        pc.add_box((0.0, 1.12, gz), (1.62, 1.18, 0.24), "Mat_SovietBrick", bevel=0.03, top_scale=(0.78, 1.0))
        pc.add_box((0.0, 1.72, gz), (1.18, 0.14, 0.26), "Mat_TeamPaint", bevel=0.01)

    pc.build_mesh_object("Body", (0.0, 0.0, 0.0), parent=root)
    return root


def _build_humanoid_soldier(unit_id: str, is_soviet: bool, heavy_at: bool) -> object:
    """Builds a genuine 3D articulated low/mid-poly RTS soldier with helmet, armor/greatcoat, boots & weapon."""
    root = create_root(unit_id)
    pc = PartCollector(is_soviet=is_soviet)

    cloth_mat = "Mat_SovietCoat" if is_soviet else "Mat_AlliedCloth"

    # Boots & Legs in combat stance
    for side, lx, lz in ((-1, -0.13, 0.05), (1, 0.13, -0.06)):
        pc.add_box((lx, 0.10, lz + 0.04), (0.13, 0.20, 0.22), "Mat_TreadRubber", bevel=0.02)
        pc.add_cylinder((lx, 0.36, lz), 0.085, 0.095, 0.42, cloth_mat, segments=10)
        if heavy_at:
            pc.add_box((lx, 0.32, lz + 0.08), (0.12, 0.14, 0.06), "Mat_TeamPaint", bevel=0.01)

    if is_soviet:
        # Flared Soviet Conscript Greatcoat Skirt + Torso + Ammo Pouches
        pc.add_cylinder((0.0, 0.62, 0.0), 0.26, 0.19, 0.36, "Mat_SovietCoat", segments=12)
        pc.add_box((0.0, 0.90, 0.0), (0.38, 0.38, 0.24), "Mat_SovietCoat", bevel=0.04, top_scale=(1.12, 1.0))
        pc.add_box((0.0, 0.92, 0.12), (0.28, 0.16, 0.06), "Mat_GoldBrass", bevel=0.01)  # Chest ammo rig
        for sx in (-0.22, 0.22):
            pc.add_box((sx, 1.06, 0.0), (0.12, 0.06, 0.18), "Mat_TeamPaint", bevel=0.01)  # Crimson shoulder boards
    else:
        # Allied Tactical Armor Vest + Shoulder Pads
        pc.add_box((0.0, 0.62, 0.0), (0.30, 0.16, 0.20), cloth_mat, bevel=0.02)
        pc.add_box((0.0, 0.88, 0.0), (0.38, 0.38, 0.25), "Mat_TeamPaint", bevel=0.04, top_scale=(1.14, 1.0))
        pc.add_box((0.0, 0.88, -0.14), (0.26, 0.32, 0.12), "Mat_DarkSteel", bevel=0.02)  # Backpack
        for sx in (-0.24, 0.24):
            pc.add_sphere((sx, 1.02, 0.0), 0.095, "Mat_TeamPaint" if heavy_at else "Mat_BrushedSteel", segments=10)

    # Head & Iconic Helmet
    pc.add_sphere((0.0, 1.20, 0.02), 0.12, "Mat_Skin", segments=12)
    if is_soviet:
        # Conscript Gasmask + Brass/Gold-Olive Soviet Helmet
        pc.add_sphere((0.0, 1.18, 0.11), 0.065, "Mat_TreadRubber", segments=10)
        pc.add_sphere((0.0, 1.25, 0.01), 0.148, "Mat_GoldBrass", scale=(1.06, 0.78, 1.12), segments=12)
    elif heavy_at:
        # Guardian GI Heavy Visored Helmet
        pc.add_sphere((0.0, 1.25, 0.01), 0.152, "Mat_BrushedSteel", scale=(1.08, 0.82, 1.12), segments=12)
        pc.add_box((0.0, 1.22, 0.13), (0.20, 0.07, 0.05), "Mat_GlassDark", bevel=0.01)
    else:
        # Allied GI Rounded M1 Helmet
        pc.add_sphere((0.0, 1.25, 0.01), 0.146, "Mat_DarkSteel", scale=(1.06, 0.76, 1.10), segments=12)

    # Arms & Weapon (+Z Forward!)
    pc.add_cylinder(( 0.18, 0.88, 0.14), 0.058, 0.052, 0.34, cloth_mat, segments=8, axis='Z', rot_deg=(8.0, -18.0, 0.0))
    pc.add_cylinder((-0.12, 0.86, 0.18), 0.058, 0.052, 0.38, cloth_mat, segments=8, axis='Z', rot_deg=(8.0,  28.0, 0.0))

    if heavy_at:
        # Shoulder-mounted heavy Anti-Tank Missile Launcher Tube!
        pc.add_cylinder((0.22, 1.06, 0.18), 0.085, 0.085, 0.92, "Mat_DarkSteel", segments=12, axis='Z')
        pc.add_cylinder((0.22, 1.06, 0.64), 0.105, 0.105, 0.10, "Mat_HazardYellow", segments=12, axis='Z')
    elif is_soviet:
        # AK Assault Rifle with Wooden Stock & Curved Magazine
        pc.add_box((0.08, 0.86, 0.32), (0.05, 0.08, 0.42), "Mat_DarkSteel", bevel=0.005)
        pc.add_box((0.08, 0.84, 0.08), (0.05, 0.09, 0.20), "Mat_WoodStock", bevel=0.005)
        pc.add_cylinder((0.08, 0.87, 0.64), 0.018, 0.018, 0.32, "Mat_DarkSteel", segments=8, axis='Z')
        pc.add_box((0.08, 0.77, 0.36), (0.035, 0.14, 0.06), "Mat_DarkSteel", bevel=0.005, rot_deg=(18.0, 0.0, 0.0))
    else:
        # Allied M60 / Assault Carbine
        pc.add_box((0.08, 0.86, 0.34), (0.055, 0.09, 0.46), "Mat_DarkSteel", bevel=0.005)
        pc.add_cylinder((0.08, 0.88, 0.68), 0.020, 0.018, 0.34, "Mat_BrushedSteel", segments=8, axis='Z')

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
