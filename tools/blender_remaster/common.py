"""
common.py - High-detail Westwood-grade Blender 5.3 3D modeling & UV-textured PBR toolkit.
"""

from __future__ import annotations
import math
from pathlib import Path
import bpy
import bmesh
from mathutils import Matrix, Vector


MATERIAL_SPECS: dict[str, tuple[tuple[float, float, float], float, float, tuple[float, float, float], float]] = {
    # name: (base_rgb, metallic, roughness, emission_rgb, emission_strength)
    "Mat_TeamPaint":       ((0.20, 0.54, 0.98), 0.28, 0.36, (0.0, 0.0, 0.0), 0.0),
    "Mat_BrushedSteel":    ((0.86, 0.89, 0.94), 0.42, 0.32, (0.0, 0.0, 0.0), 0.0),
    "Mat_CorrugatedSteel": ((0.88, 0.91, 0.96), 0.38, 0.34, (0.0, 0.0, 0.0), 0.0),
    "Mat_DarkSteel":       ((0.32, 0.35, 0.39), 0.48, 0.42, (0.0, 0.0, 0.0), 0.0),
    "Mat_TreadRubber":     ((0.22, 0.23, 0.25), 0.25, 0.68, (0.0, 0.0, 0.0), 0.0),
    "Mat_Concrete":        ((0.66, 0.65, 0.61), 0.05, 0.82, (0.0, 0.0, 0.0), 0.0),
    "Mat_SovietBrick":     ((0.68, 0.50, 0.40), 0.06, 0.76, (0.0, 0.0, 0.0), 0.0),
    "Mat_GoldBrass":       ((0.96, 0.78, 0.32), 0.55, 0.26, (0.0, 0.0, 0.0), 0.0),
    "Mat_HazardYellow":    ((0.96, 0.78, 0.12), 0.18, 0.42, (0.0, 0.0, 0.0), 0.0),
    "Mat_ShutterDoor":     ((0.36, 0.39, 0.44), 0.45, 0.40, (0.0, 0.0, 0.0), 0.0),
    "Mat_SovietGate":      ((0.92, 0.92, 0.92), 0.30, 0.45, (0.0, 0.0, 0.0), 0.0),
    "Mat_AlliedCrest":     ((0.95, 0.96, 1.00), 0.25, 0.35, (0.0, 0.0, 0.0), 0.0),
    "Mat_GlassDark":       ((0.10, 0.26, 0.46), 0.65, 0.12, (0.06, 0.20, 0.40), 0.6),
    "Mat_GlowCyan":        ((0.38, 0.96, 1.00), 0.10, 0.15, (0.30, 0.92, 1.00), 4.2),
    "Mat_GlowPlasma":      ((0.52, 0.82, 1.00), 0.15, 0.15, (0.42, 0.76, 1.00), 3.8),
    "Mat_GlowGreen":       ((0.20, 0.95, 0.62), 0.10, 0.18, (0.15, 0.88, 0.52), 3.2),
    "Mat_GlowRed":         ((1.00, 0.24, 0.16), 0.10, 0.20, (1.00, 0.20, 0.12), 3.5),
    "Mat_Skin":            ((0.85, 0.66, 0.53), 0.00, 0.62, (0.0, 0.0, 0.0), 0.0),
    "Mat_AlliedCloth":     ((0.30, 0.38, 0.33), 0.04, 0.76, (0.0, 0.0, 0.0), 0.0),
    "Mat_SovietCoat":      ((0.48, 0.38, 0.24), 0.04, 0.78, (0.0, 0.0, 0.0), 0.0),
    "Mat_WoodStock":       ((0.42, 0.23, 0.11), 0.04, 0.66, (0.0, 0.0, 0.0), 0.0),
}

TEXTURE_MAP_PATHS: dict[str, tuple[Path, Path | None]] = {}


def set_texture_registry(tex_dict: dict[str, tuple[Path, Path | None]]) -> None:
    TEXTURE_MAP_PATHS.clear()
    TEXTURE_MAP_PATHS.update(tex_dict)


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0


def _load_image_cached(path: Path, non_color: bool = False) -> bpy.types.Image:
    key = path.name
    img = bpy.data.images.get(key)
    if img is None:
        img = bpy.data.images.load(str(path))
        img.name = key
    if non_color:
        img.colorspace_settings.name = 'Non-Color'
    return img


def get_mat(name: str, team_is_soviet: bool = False) -> bpy.types.Material:
    mat = bpy.data.materials.get(name)
    team_tint = (0.88, 0.16, 0.14, 1.0) if team_is_soviet else (0.18, 0.52, 0.98, 1.0)
    if mat is not None:
        if name == "Mat_TeamPaint":
            mix_node = mat.node_tree.nodes.get("TeamTintMix")
            if mix_node:
                mix_node.inputs[7].default_value = team_tint
        return mat

    spec = MATERIAL_SPECS[name]
    mat = bpy.data.materials.new(name=name)
    tree = mat.node_tree
    bsdf = tree.nodes.get("Principled BSDF")
    rgb = spec[0]
    bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
    bsdf.inputs["Metallic"].default_value = spec[1]
    bsdf.inputs["Roughness"].default_value = spec[2]
    if spec[4] > 0.0 and "Emission Color" in bsdf.inputs:
        ec = spec[3]
        bsdf.inputs["Emission Color"].default_value = (ec[0], ec[1], ec[2], 1.0)
        bsdf.inputs["Emission Strength"].default_value = spec[4]

    if name in TEXTURE_MAP_PATHS:
        albedo_path, norm_path = TEXTURE_MAP_PATHS[name]
        tex_node = tree.nodes.new("ShaderNodeTexImage")
        tex_node.image = _load_image_cached(albedo_path, non_color=False)

        if name == "Mat_TeamPaint":
            # Multiply grayscale riveted armor texture by faction color!
            mix = tree.nodes.new("ShaderNodeMix")
            mix.name = "TeamTintMix"
            mix.data_type = 'RGBA'
            mix.blend_type = 'MULTIPLY'
            mix.inputs[0].default_value = 1.0
            mix.inputs[7].default_value = team_tint
            tree.links.new(tex_node.outputs["Color"], mix.inputs[6])
            tree.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        else:
            tree.links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])

        if norm_path is not None:
            n_tex = tree.nodes.new("ShaderNodeTexImage")
            n_tex.image = _load_image_cached(norm_path, non_color=True)
            n_map = tree.nodes.new("ShaderNodeNormalMap")
            n_map.inputs["Strength"].default_value = 0.85
            tree.links.new(n_tex.outputs["Color"], n_map.inputs["Color"])
            tree.links.new(n_map.outputs["Normal"], bsdf.inputs["Normal"])

    return mat


def u2b(x: float, y: float, z: float) -> Vector:
    return Vector((x, -z, y))


def u2b_rot(rx_deg: float = 0.0, ry_deg: float = 0.0, rz_deg: float = 0.0) -> Matrix:
    p = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))
    rx = Matrix.Rotation(math.radians(rx_deg), 3, 'X')
    ry = Matrix.Rotation(math.radians(ry_deg), 3, 'Y')
    rz = Matrix.Rotation(math.radians(rz_deg), 3, 'Z')
    ru = ry @ rx @ rz
    return (p @ ru @ p.transposed()).to_4x4()


def _assign_triplanar_uvs(bm: bmesh.types.BMesh, scale: float = 0.52) -> None:
    uv_layer = bm.loops.layers.uv.verify()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        n = f.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in f.loops:
            co = loop.vert.co
            if az >= ax and az >= ay:
                loop[uv_layer].uv = (co.x * scale, co.y * scale)
            elif ax >= ay:
                loop[uv_layer].uv = (-co.y * scale, co.z * scale)
            else:
                loop[uv_layer].uv = (co.x * scale, co.z * scale)


class PartCollector:
    def __init__(self, is_soviet: bool = False):
        self.is_soviet = is_soviet
        self.entries: list[tuple[bmesh.types.BMesh, str]] = []

    def _push(
        self,
        bm: bmesh.types.BMesh,
        mat_name: str,
        center: tuple[float, float, float],
        rot_deg: tuple[float, float, float] = (0.0, 0.0, 0.0),
        uv_scale: float = 0.52,
        preserve_uv: bool = False,
    ) -> None:
        if any(abs(a) > 1e-4 for a in rot_deg):
            bmesh.ops.transform(bm, matrix=u2b_rot(*rot_deg), verts=bm.verts)
        bmesh.ops.translate(bm, vec=u2b(*center), verts=bm.verts)
        if not preserve_uv:
            _assign_triplanar_uvs(bm, scale=uv_scale)
        self.entries.append((bm, mat_name))

    def add_decal_panel(
        self,
        center: tuple[float, float, float],
        width: float,
        height: float,
        thickness: float,
        mat: str,
        rot_deg: tuple[float, float, float] = (0.0, 0.0, 0.0),
        face_plus_z: bool = False,
    ) -> None:
        """Creates a framed flat panel whose front face maps exact [0..1]x[0..1] UVs (for emblems, shutter doors, hazard pads!)."""
        bm = bmesh.new()
        hw, hh, ht = width * 0.5, height * 0.5, thickness * 0.5
        # In Unity space, front face is at -ht (facing -Z for buildings) or +ht (facing +Z)
        zf = ht if face_plus_z else -ht
        zb = -ht if face_plus_z else ht
        if face_plus_z:
            f_pts = [(-hw, -hh, zf), (hw, -hh, zf), (hw, hh, zf), (-hw, hh, zf)]
            b_pts = [(-hw, -hh, zb), (hw, -hh, zb), (hw, hh, zb), (-hw, hh, zb)]
        else:
            f_pts = [(hw, -hh, zf), (-hw, -hh, zf), (-hw, hh, zf), (hw, hh, zf)]
            b_pts = [(hw, -hh, zb), (-hw, -hh, zb), (-hw, hh, zb), (hw, hh, zb)]

        fv = [bm.verts.new(u2b(*p)) for p in f_pts]
        bv = [bm.verts.new(u2b(*p)) for p in b_pts]
        front_face = bm.faces.new(fv)
        bm.faces.new(list(reversed(bv)))
        for i in range(4):
            ni = (i + 1) % 4
            bm.faces.new((fv[ni], fv[i], bv[i], bv[ni]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

        uv_layer = bm.loops.layers.uv.verify()
        uv_coords = [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)]
        for f in bm.faces:
            if f == front_face:
                for idx, loop in enumerate(f.loops):
                    loop[uv_layer].uv = uv_coords[idx]
            else:
                for loop in f.loops:
                    loop[uv_layer].uv = (0.02, 0.02)

        self._push(bm, mat, center, rot_deg, preserve_uv=True)

    def add_box(
        self,
        center: tuple[float, float, float],
        size: tuple[float, float, float],
        mat: str,
        bevel: float = 0.025,
        rot_deg: tuple[float, float, float] = (0.0, 0.0, 0.0),
        top_scale: tuple[float, float] = (1.0, 1.0),
        uv_scale: float = 0.52,
    ) -> None:
        sx, sy, sz = size
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            is_top = v.co.z > 0
            tx = top_scale[0] if is_top else 1.0
            tz = top_scale[1] if is_top else 1.0
            v.co.x *= sx * tx
            v.co.y *= sz * tz
            v.co.z *= sy
        if bevel > 0.001:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(sx, sy, sz) * 0.22), segments=1, profile=0.5, affect='EDGES')
        self._push(bm, mat, center, rot_deg, uv_scale=uv_scale)

    def add_sloped_hull(
        self,
        center: tuple[float, float, float],
        width: float,
        height: float,
        length: float,
        front_slope: float,
        rear_slope: float,
        side_taper: float,
        mat: str,
        bevel: float = 0.025,
        uv_scale: float = 0.60,
    ) -> None:
        bm = bmesh.new()
        hw, hh, hl = width * 0.5, height * 0.5, length * 0.5
        tw = hw * side_taper
        pts_u = [
            (-hw, -hh, -hl + rear_slope * 0.3),
            ( hw, -hh, -hl + rear_slope * 0.3),
            ( hw, -hh,  hl - front_slope * 0.45),
            (-hw, -hh,  hl - front_slope * 0.45),
            (-tw,  hh, -hl + rear_slope),
            ( tw,  hh, -hl + rear_slope),
            ( tw,  hh,  hl - front_slope),
            (-tw,  hh,  hl - front_slope),
            (-hw * 0.92, -hh * 0.12, hl),
            ( hw * 0.92, -hh * 0.12, hl),
            (-hw * 0.95, -hh * 0.12, -hl),
            ( hw * 0.95, -hh * 0.12, -hl),
        ]
        for p in pts_u:
            bm.verts.new(u2b(*p))
        bmesh.ops.convex_hull(bm, input=list(bm.verts))
        if bevel > 0.001:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, profile=0.5, affect='EDGES')
        self._push(bm, mat, center, uv_scale=uv_scale)

    def add_cylinder(
        self,
        center: tuple[float, float, float],
        radius_bottom: float,
        radius_top: float,
        height: float,
        mat: str,
        segments: int = 20,
        axis: str = 'Y',
        rot_deg: tuple[float, float, float] = (0.0, 0.0, 0.0),
        bevel_caps: float = 0.018,
        uv_scale: float = 0.55,
    ) -> None:
        bm = bmesh.new()
        bmesh.ops.create_cone(
            bm, cap_ends=True, cap_tris=False, segments=segments,
            radius1=radius_bottom, radius2=radius_top, depth=height,
        )
        if axis == 'Z':
            bmesh.ops.rotate(bm, matrix=Matrix.Rotation(math.radians(-90.0), 3, 'X'), verts=bm.verts)
        elif axis == 'X':
            bmesh.ops.rotate(bm, matrix=Matrix.Rotation(math.radians(90.0), 3, 'Y'), verts=bm.verts)

        if bevel_caps > 0.001:
            cap_edges = [e for e in bm.edges if len(e.link_faces) == 2 and abs(e.link_faces[0].normal.dot(e.link_faces[1].normal)) < 0.5]
            if cap_edges:
                bmesh.ops.bevel(bm, geom=cap_edges, offset=min(bevel_caps, min(max(0.01, radius_bottom), max(0.01, radius_top), height) * 0.2), segments=1, affect='EDGES')

        self._push(bm, mat, center, rot_deg, uv_scale=uv_scale)

    def add_sphere(
        self,
        center: tuple[float, float, float],
        radius: float,
        mat: str,
        scale: tuple[float, float, float] = (1.0, 1.0, 1.0),
        segments: int = 20,
    ) -> None:
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=max(10, segments // 2), radius=radius)
        sx, sy, sz = scale
        for v in bm.verts:
            v.co.x *= sx
            v.co.y *= sz
            v.co.z *= sy
        self._push(bm, mat, center)

    def add_torus(
        self,
        center: tuple[float, float, float],
        major_radius: float,
        minor_radius: float,
        mat: str,
        rot_deg: tuple[float, float, float] = (0.0, 0.0, 0.0),
        major_seg: int = 24,
        minor_seg: int = 12,
    ) -> None:
        bm = bmesh.new()
        rings = []
        for i in range(major_seg):
            theta = (2.0 * math.pi * i) / major_seg
            ct, st = math.cos(theta), math.sin(theta)
            ring = []
            for j in range(minor_seg):
                phi = (2.0 * math.pi * j) / minor_seg
                r = major_radius + minor_radius * math.cos(phi)
                z = minor_radius * math.sin(phi)
                ring.append(bm.verts.new((r * ct, r * st, z)))
            rings.append(ring)
        for i in range(major_seg):
            ni = (i + 1) % major_seg
            for j in range(minor_seg):
                nj = (j + 1) % minor_seg
                bm.faces.new((rings[i][j], rings[ni][j], rings[ni][nj], rings[i][nj]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        self._push(bm, mat, center, rot_deg)

    def add_lathe_profile(
        self,
        center: tuple[float, float, float],
        profile_ry: list[tuple[float, float]],
        mat: str,
        segments: int = 24,
        flute_amp: float = 0.0,
        flute_count: int = 12,
    ) -> None:
        bm = bmesh.new()
        rings: list[list[bmesh.types.BMVert]] = []
        bottom_cap: bmesh.types.BMVert | None = None
        top_cap: bmesh.types.BMVert | None = None

        for idx, (r, uy) in enumerate(profile_ry):
            if r <= 1e-4 and idx == 0:
                bottom_cap = bm.verts.new((0.0, 0.0, uy))
                rings.append([])
            elif r <= 1e-4 and idx == len(profile_ry) - 1:
                top_cap = bm.verts.new((0.0, 0.0, uy))
                rings.append([])
            else:
                rr_base = max(0.01, r)
                ring = []
                for s in range(segments):
                    ang = (2.0 * math.pi * s) / segments
                    flute = 1.0 + flute_amp * math.cos(ang * flute_count) if flute_amp > 0.0 else 1.0
                    rr = rr_base * flute
                    ring.append(bm.verts.new((rr * math.cos(ang), rr * math.sin(ang), uy)))
                rings.append(ring)

        for idx in range(len(profile_ry) - 1):
            r0, r1 = rings[idx], rings[idx + 1]
            if not r0 and bottom_cap and r1:
                for s in range(segments):
                    ns = (s + 1) % segments
                    bm.faces.new((bottom_cap, r1[ns], r1[s]))
            elif not r1 and top_cap and r0:
                for s in range(segments):
                    ns = (s + 1) % segments
                    bm.faces.new((r0[s], r0[ns], top_cap))
            elif r0 and r1:
                for s in range(segments):
                    ns = (s + 1) % segments
                    bm.faces.new((r0[s], r0[ns], r1[ns], r1[s]))

        if rings[0]:
            bm.faces.new(list(reversed(rings[0])))
        if rings[-1]:
            bm.faces.new(rings[-1])

        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        self._push(bm, mat, center)

    def add_onion_dome(
        self,
        center: tuple[float, float, float],
        base_radius: float,
        height: float,
        mat_dome: str = "Mat_GoldBrass",
        mat_spire: str = "Mat_GoldBrass",
    ) -> None:
        """Creates an authentic fluted Russian Kremlin onion dome with windowed drum & spire finial."""
        cx, cy, cz = center
        r = base_radius
        # Windowed cylindrical drum below dome
        drum_h = height * 0.28
        self.add_cylinder((cx, cy + drum_h * 0.5, cz), r * 0.74, r * 0.72, drum_h, "Mat_BrushedSteel", segments=16)
        for i in range(8):
            ang = i * 45.0
            rad = math.radians(ang)
            self.add_box((cx + math.sin(rad) * r * 0.73, cy + drum_h * 0.5, cz + math.cos(rad) * r * 0.73), (r * 0.16, drum_h * 0.58, 0.06), "Mat_DarkSteel", bevel=0.005, rot_deg=(0.0, ang, 0.0))

        dy = cy + drum_h
        h = height * 0.85
        profile = [
            (r * 0.72, 0.00),
            (r * 0.98, h * 0.16),
            (r * 1.12, h * 0.34),
            (r * 0.98, h * 0.54),
            (r * 0.62, h * 0.72),
            (r * 0.26, h * 0.88),
            (0.00,     h * 1.00),
        ]
        self.add_lathe_profile((cx, dy, cz), profile, mat_dome, segments=32, flute_amp=0.045, flute_count=16)
        self.add_sphere((cx, dy + h + h * 0.09, cz), r * 0.14, mat_spire, segments=12)
        self.add_cylinder((cx, dy + h + h * 0.32, cz), r * 0.055, r * 0.015, h * 0.52, mat_spire, segments=10)

    def add_quonset_arch(
        self,
        center: tuple[float, float, float],
        width: float,
        arch_height: float,
        length: float,
        mat_shell: str = "Mat_CorrugatedSteel",
        mat_ribs: str = "Mat_TeamPaint",
        rib_count: int = 6,
        rot_y_deg: float = 0.0,
    ) -> None:
        hw = width * 0.5
        hl = length * 0.5
        segs = 20

        bm = bmesh.new()
        front_outer = []
        back_outer = []
        for i in range(segs + 1):
            ang = math.pi * (i / segs)
            px = -math.cos(ang) * hw
            py = math.sin(ang) * arch_height
            front_outer.append(bm.verts.new(u2b(px, py, hl)))
            back_outer.append(bm.verts.new(u2b(px, py, -hl)))

        for i in range(segs):
            bm.faces.new((front_outer[i], front_outer[i + 1], back_outer[i + 1], back_outer[i]))
        bm.faces.new(front_outer)
        bm.faces.new(list(reversed(back_outer)))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

        # Cylindrical arch UV unwrap so corrugated ribs run cleanly around the arch!
        uv_layer = bm.loops.layers.uv.verify()
        for idx_f, f in enumerate(bm.faces):
            if idx_f < segs:
                v0, v1 = idx_f / segs, (idx_f + 1) / segs
                u_len = length * 0.45
                uvs = [(0.0, v0 * 2.0), (0.0, v1 * 2.0), (u_len, v1 * 2.0), (u_len, v0 * 2.0)]
                for li, loop in enumerate(f.loops):
                    loop[uv_layer].uv = uvs[li]
            else:
                for loop in f.loops:
                    co = loop.vert.co
                    loop[uv_layer].uv = (co.x * 0.5, co.z * 0.5)

        self._push(bm, mat_shell, center, (0.0, rot_y_deg, 0.0), preserve_uv=True)

        # Raised bolted structural arch ribs
        if rib_count >= 2:
            for r_idx in range(rib_count):
                t = r_idx / (rib_count - 1)
                rz = -hl * 0.94 + (2 * hl * 0.94) * t
                bm_rib = bmesh.new()
                rib_th = 0.055
                f_ring, b_ring = [], []
                for i in range(segs + 1):
                    ang = math.pi * (i / segs)
                    px = -math.cos(ang) * (hw + 0.055)
                    py = math.sin(ang) * (arch_height + 0.055)
                    f_ring.append(bm_rib.verts.new(u2b(px, py, rz + rib_th)))
                    b_ring.append(bm_rib.verts.new(u2b(px, py, rz - rib_th)))
                for i in range(segs):
                    bm_rib.faces.new((f_ring[i], f_ring[i + 1], b_ring[i + 1], b_ring[i]))
                bm_rib.faces.new(f_ring)
                bm_rib.faces.new(list(reversed(b_ring)))
                bmesh.ops.recalc_face_normals(bm_rib, faces=bm_rib.faces)
                self._push(bm_rib, mat_ribs, center, (0.0, rot_y_deg, 0.0))

    def add_lattice_boom(
        self,
        p0: tuple[float, float, float],
        p1: tuple[float, float, float],
        width: float = 0.34,
        height: float = 0.30,
        bays: int = 5,
        mat: str = "Mat_HazardYellow",
    ) -> None:
        """Creates an authentic open 3D 4-chord structural steel lattice truss boom with X-bracing!"""
        v0 = Vector(p0)
        v1 = Vector(p1)
        delta = v1 - v0
        length = delta.length
        if length < 0.05:
            return
        fwd = delta.normalized()
        up_ref = Vector((0.0, 1.0, 0.0)) if abs(fwd.y) < 0.95 else Vector((1.0, 0.0, 0.0))
        right = up_ref.cross(fwd).normalized()
        up = fwd.cross(right).normalized()

        hw, hh = width * 0.5, height * 0.5
        corners = [
            -right * hw - up * hh,
             right * hw - up * hh,
             right * hw + up * hh,
            -right * hw + up * hh,
        ]
        # 4 longitudinal main chords
        for c in corners:
            self._add_strut(v0 + c, v1 + c, 0.032, mat)

        # Transverse frames & diagonal X-braces per bay
        for b in range(bays + 1):
            t = b / bays
            base = v0 + delta * t
            for i in range(4):
                ni = (i + 1) % 4
                self._add_strut(base + corners[i], base + corners[ni], 0.022, mat)
            if b < bays:
                next_base = v0 + delta * ((b + 1) / bays)
                for i in range(4):
                    ni = (i + 1) % 4
                    self._add_strut(base + corners[i], next_base + corners[ni], 0.018, mat)

    def _add_strut(self, a: Vector, b: Vector, radius: float, mat: str) -> None:
        diff = b - a
        dist = diff.length
        if dist < 0.01:
            return
        mid = (a + b) * 0.5
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=6, radius1=radius, radius2=radius, depth=dist)
        b_dir = (u2b(b.x, b.y, b.z) - u2b(a.x, a.y, a.z)).normalized()
        rot = Vector((0.0, 0.0, 1.0)).rotation_difference(b_dir).to_matrix()
        bmesh.ops.rotate(bm, matrix=rot, verts=bm.verts)
        bmesh.ops.translate(bm, vec=u2b(mid.x, mid.y, mid.z), verts=bm.verts)
        _assign_triplanar_uvs(bm, 0.8)
        self.entries.append((bm, mat))

    def add_pipe_run(
        self,
        points: list[tuple[float, float, float]],
        radius: float = 0.085,
        mat: str = "Mat_BrushedSteel",
        flanges: bool = True,
    ) -> None:
        """Creates a 3D industrial pipeline with elbow spheres and bolted flange rings."""
        for i in range(len(points) - 1):
            a = Vector(points[i])
            b = Vector(points[i + 1])
            self._add_strut(a, b, radius, mat)
            if flanges:
                mid = a.lerp(b, 0.5)
                self._add_strut(mid - (b - a).normalized() * 0.04, mid + (b - a).normalized() * 0.04, radius * 1.42, "Mat_DarkSteel")
        for p in points[1:-1]:
            self.add_sphere(p, radius * 1.18, mat, segments=12)

    def add_spoked_fan(
        self,
        center: tuple[float, float, float],
        radius: float,
        height: float,
        blades: int = 7,
        mat_shroud: str = "Mat_BrushedSteel",
    ) -> None:
        """Creates Westwood's signature rooftop cylindrical turbofan cowling with recessed dark pit & angled fan blades!"""
        cx, cy, cz = center
        self.add_cylinder((cx, cy + height * 0.5, cz), radius, radius * 0.96, height, mat_shroud, segments=20)
        self.add_cylinder((cx, cy + height - 0.04, cz), radius * 0.84, radius * 0.84, 0.10, "Mat_TreadRubber", segments=18)
        self.add_torus((cx, cy + height, cz), radius * 0.88, radius * 0.09, mat_shroud)
        self.add_cylinder((cx, cy + height + 0.01, cz), radius * 0.24, radius * 0.20, 0.12, "Mat_DarkSteel", segments=12)
        for i in range(blades):
            ang = (360.0 * i) / blades
            rad = math.radians(ang)
            bx = cx + math.sin(rad) * radius * 0.48
            bz = cz + math.cos(rad) * radius * 0.48
            self.add_box((bx, cy + height + 0.01, bz), (radius * 0.18, 0.03, radius * 0.56), "Mat_BrushedSteel", bevel=0.004, rot_deg=(0.0, ang, 22.0))

    def add_louver_stack(
        self,
        center: tuple[float, float, float],
        size: tuple[float, float, float],
        slats: int = 4,
        mat_frame: str = "Mat_TeamPaint",
        rot_y_deg: float = 0.0,
    ) -> None:
        """Creates RA2 Soviet crimson rooftop exhaust vent stack with individual recessed 3D louver slats!"""
        cx, cy, cz = center
        sx, sy, sz = size
        self.add_box((cx, cy, cz), (sx, sy, sz), mat_frame, bevel=0.025, rot_deg=(0.0, rot_y_deg, 0.0), top_scale=(0.88, 0.72))
        rad = math.radians(rot_y_deg)
        fwd_x, fwd_z = -math.sin(rad), -math.cos(rad)
        for s in range(slats):
            t = (s + 0.5) / slats - 0.5
            ly = cy + t * sy * 0.72
            off = sz * 0.46
            self.add_box((cx + fwd_x * off, ly, cz + fwd_z * off), (sx * 0.78, sy / (slats * 2.1), 0.12), "Mat_TreadRubber", bevel=0.005, rot_deg=(-25.0, rot_y_deg, 0.0))

    def add_railing(self, p0: tuple[float, float, float], p1: tuple[float, float, float], height: float = 0.28, posts: int = 4, mat: str = "Mat_BrushedSteel") -> None:
        v0, v1 = Vector(p0), Vector(p1)
        top0 = v0 + Vector((0.0, height, 0.0))
        top1 = v1 + Vector((0.0, height, 0.0))
        mid0 = v0 + Vector((0.0, height * 0.52, 0.0))
        mid1 = v1 + Vector((0.0, height * 0.52, 0.0))
        self._add_strut(top0, top1, 0.020, mat)
        self._add_strut(mid0, mid1, 0.014, mat)
        for i in range(posts):
            p = v0.lerp(v1, i / max(1, posts - 1))
            self._add_strut(p, p + Vector((0.0, height, 0.0)), 0.020, mat)

    def add_track_pod(
        self,
        center: tuple[float, float, float],
        width: float,
        height: float,
        length: float,
        wheel_count: int = 6,
        has_skirt: bool = True,
        skirt_mat: str = "Mat_TeamPaint",
    ) -> None:
        cx, cy, cz = center
        side_sign = 1.0 if cx >= 0 else -1.0

        # Track belt with UV-mapped cleats + recessed inner bogie frame
        self.add_sloped_hull(
            center=(cx, cy + height * 0.50, cz),
            width=width * 0.92,
            height=height,
            length=length,
            front_slope=height * 0.52,
            rear_slope=height * 0.44,
            side_taper=0.98,
            mat="Mat_TreadRubber",
            bevel=0.02,
            uv_scale=1.2,
        )
        # Front toothed drive sprocket + Rear idler wheel
        sp_r = height * 0.34
        for end_z in (cz + length * 0.39, cz - length * 0.39):
            self.add_cylinder((cx, cy + height * 0.56, end_z), sp_r, sp_r, width * 1.08, "Mat_DarkSteel", segments=14, axis='X', bevel_caps=0.01)
            self.add_cylinder((cx + side_sign * width * 0.52, cy + height * 0.56, end_z), sp_r * 0.52, sp_r * 0.45, 0.06, "Mat_BrushedSteel", segments=10, axis='X')

        # Roadwheels with recessed rims & center hub bolts
        wheel_r = height * 0.33
        span = length * 0.64
        for i in range(wheel_count):
            wz = cz - span * 0.5 + (span * i / max(1, wheel_count - 1))
            self.add_cylinder((cx, cy + wheel_r + 0.02, wz), wheel_r, wheel_r, width * 1.06, "Mat_DarkSteel", segments=16, axis='X', bevel_caps=0.01)
            self.add_cylinder((cx + side_sign * width * 0.51, cy + wheel_r + 0.02, wz), wheel_r * 0.58, wheel_r * 0.54, 0.05, "Mat_BrushedSteel", segments=12, axis='X')
            self.add_cylinder((cx + side_sign * width * 0.54, cy + wheel_r + 0.02, wz), wheel_r * 0.22, wheel_r * 0.22, 0.04, "Mat_DarkSteel", segments=8, axis='X')

        if has_skirt:
            # Multi-panel bolted armored side skirt + angled front/rear mudguards!
            self.add_box(
                (cx + side_sign * width * 0.38, cy + height * 0.78, cz),
                (width * 0.34, height * 0.42, length * 0.90),
                skirt_mat,
                bevel=0.018,
            )
            self.add_box(
                (cx, cy + height * 0.96, cz),
                (width * 1.06, 0.06, length * 0.96),
                skirt_mat,
                bevel=0.012,
            )
            # Front & Rear angled mudflaps
            self.add_box((cx, cy + height * 0.76, cz + length * 0.48), (width * 1.04, 0.18, 0.06), "Mat_DarkSteel", bevel=0.008, rot_deg=(-32.0, 0.0, 0.0))
            self.add_box((cx, cy + height * 0.76, cz - length * 0.48), (width * 1.04, 0.18, 0.06), "Mat_DarkSteel", bevel=0.008, rot_deg=( 32.0, 0.0, 0.0))

    def build_mesh_object(self, name: str, pivot_unity: tuple[float, float, float] = (0.0, 0.0, 0.0), parent: bpy.types.Object | None = None) -> bpy.types.Object:
        merged = bmesh.new()
        merged_uv = merged.loops.layers.uv.verify()
        mat_order: list[str] = []
        mat_index_map: dict[str, int] = {}

        pivot_b = u2b(*pivot_unity)

        for bm_part, mat_name in self.entries:
            if mat_name not in mat_index_map:
                mat_index_map[mat_name] = len(mat_order)
                mat_order.append(mat_name)
            m_idx = mat_index_map[mat_name]

            part_uv = bm_part.loops.layers.uv.verify()
            vert_map: dict[int, bmesh.types.BMVert] = {}
            for v in bm_part.verts:
                nv = merged.verts.new(v.co - pivot_b)
                vert_map[v.index] = nv
            for f in bm_part.faces:
                try:
                    nf = merged.faces.new([vert_map[v.index] for v in f.verts])
                    nf.material_index = m_idx
                    nf.smooth = True
                    for src_loop, dst_loop in zip(f.loops, nf.loops):
                        dst_loop[merged_uv].uv = src_loop[part_uv].uv.copy()
                except ValueError:
                    pass
            bm_part.free()

        self.entries.clear()
        bmesh.ops.recalc_face_normals(merged, faces=merged.faces)

        sharp_edges = [
            e for e in merged.edges
            if len(e.link_faces) == 2 and e.link_faces[0].normal.angle(e.link_faces[1].normal, 0.0) > math.radians(22.0)
        ]
        if sharp_edges:
            bmesh.ops.split_edges(merged, edges=sharp_edges)

        mesh = bpy.data.meshes.new(name + "_Mesh")
        merged.to_mesh(mesh)
        merged.free()

        for m_name in mat_order:
            mesh.materials.append(get_mat(m_name, team_is_soviet=self.is_soviet))

        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        bpy.context.view_layer.update()
        if parent is not None:
            obj.parent = parent
            parent_b = parent.matrix_world.translation.copy()
            obj.location = pivot_b - parent_b
        else:
            obj.location = pivot_b
        bpy.context.view_layer.update()
        return obj


def create_root(unit_id: str) -> bpy.types.Object:
    root = bpy.data.objects.new(unit_id, None)
    bpy.context.collection.objects.link(root)
    root.location = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()
    return root


def create_pivot(name: str, pivot_unity: tuple[float, float, float], parent: bpy.types.Object) -> bpy.types.Object:
    piv = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(piv)
    bpy.context.view_layer.update()
    piv.parent = parent
    piv_b = u2b(*pivot_unity)
    parent_b = parent.matrix_world.translation.copy()
    piv.location = piv_b - parent_b
    bpy.context.view_layer.update()
    return piv


def export_fbx(out_path: Path) -> None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(out_path),
        use_selection=False,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        object_types={'EMPTY', 'MESH'},
        mesh_smooth_type='OFF',
        add_leaf_bones=False,
        path_mode='COPY',
        embed_textures=False,
    )


def render_iso_preview(out_png: Path, target_span: float = 6.0, res: int = 448, is_building: bool = False) -> None:
    """Renders a crisp 45-degree studio preview with sky ambient reflection matching RA2's bright daylight look!"""
    out_png.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'

    # Bright daylight sky world so metallic steel & brass reflect realistic bright sky instead of black!
    world = bpy.data.worlds.new("StudioSky")
    scene.world = world
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.58, 0.68, 0.82, 1.0)
        bg.inputs[1].default_value = 0.85

    key_data = bpy.data.lights.new("KeySun", type='SUN')
    key_data.energy = 3.6
    key_data.color = (1.0, 0.97, 0.91)
    key_obj = bpy.data.objects.new("KeySun", key_data)
    bpy.context.collection.objects.link(key_obj)
    key_obj.rotation_euler = (math.radians(46), math.radians(20 if is_building else -18), math.radians(148 if is_building else 34))

    fill_data = bpy.data.lights.new("FillSun", type='SUN')
    fill_data.energy = 1.4
    fill_data.color = (0.78, 0.88, 1.0)
    fill_obj = bpy.data.objects.new("FillSun", fill_data)
    bpy.context.collection.objects.link(fill_obj)
    fill_obj.rotation_euler = (math.radians(60), math.radians(-20), math.radians(-35 if is_building else -135))

    cam_data = bpy.data.cameras.new("IsoCam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = target_span
    cam_obj = bpy.data.objects.new("IsoCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)

    dist = 24.0
    elev = math.radians(32.0)
    azim = math.radians(222.0 if is_building else -38.0)
    cx = dist * math.cos(elev) * math.sin(azim)
    cy = -dist * math.cos(elev) * math.cos(azim)
    cz = dist * math.sin(elev) + target_span * 0.23
    cam_obj.location = (cx, cy, cz)

    direction = Vector((0.0, 0.0, target_span * 0.19)) - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam_obj

    scene.render.filepath = str(out_png)
    bpy.ops.render.render(write_still=True)
