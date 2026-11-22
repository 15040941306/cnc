"""
common.py - Shared Blender 5.3+ 3D hard-surface modeling & PBR material toolkit for ReCnc.

Coordinate convention for all builder functions:
  (x, y, z) = (Right +X, Up +Y, Forward +Z) in Unity meters (1 RA2 grid cell = 2.0m).
Internally mapped via pure +90deg rotation (x, -z, y) into Blender's (-Y Forward, +Z Up, +X Right)
so FBX export with (axis_forward='-Z', axis_up='Y', bake_space_transform=True) lands 1:1 in Unity!
"""

from __future__ import annotations
import math
from pathlib import Path
import bpy
import bmesh
from mathutils import Matrix, Vector


MATERIAL_SPECS: dict[str, tuple[tuple[float, float, float], float, float, tuple[float, float, float], float]] = {
    # name: (base_rgb, metallic, roughness, emission_rgb, emission_strength)
    "Mat_TeamPaint":    ((0.20, 0.52, 0.96), 0.35, 0.38, (0.0, 0.0, 0.0), 0.0),
    "Mat_BrushedSteel": ((0.70, 0.73, 0.77), 0.82, 0.32, (0.0, 0.0, 0.0), 0.0),
    "Mat_DarkSteel":    ((0.24, 0.26, 0.29), 0.78, 0.42, (0.0, 0.0, 0.0), 0.0),
    "Mat_TreadRubber":  ((0.11, 0.12, 0.13), 0.15, 0.78, (0.0, 0.0, 0.0), 0.0),
    "Mat_Concrete":     ((0.48, 0.49, 0.47), 0.05, 0.85, (0.0, 0.0, 0.0), 0.0),
    "Mat_SovietBrick":  ((0.46, 0.31, 0.24), 0.08, 0.76, (0.0, 0.0, 0.0), 0.0),
    "Mat_GoldBrass":    ((0.92, 0.72, 0.22), 0.88, 0.24, (0.0, 0.0, 0.0), 0.0),
    "Mat_HazardYellow": ((0.94, 0.74, 0.10), 0.20, 0.45, (0.0, 0.0, 0.0), 0.0),
    "Mat_GlassDark":    ((0.08, 0.18, 0.28), 0.90, 0.10, (0.04, 0.12, 0.22), 0.4),
    "Mat_GlowCyan":     ((0.35, 0.95, 1.00), 0.10, 0.15, (0.30, 0.92, 1.00), 4.2),
    "Mat_GlowPlasma":   ((0.55, 0.78, 1.00), 0.15, 0.15, (0.45, 0.72, 1.00), 3.6),
    "Mat_GlowRed":      ((1.00, 0.22, 0.16), 0.10, 0.20, (1.00, 0.18, 0.12), 3.2),
    "Mat_Skin":         ((0.82, 0.63, 0.50), 0.00, 0.65, (0.0, 0.0, 0.0), 0.0),
    "Mat_AlliedCloth":  ((0.28, 0.34, 0.30), 0.05, 0.75, (0.0, 0.0, 0.0), 0.0),
    "Mat_SovietCoat":   ((0.44, 0.35, 0.22), 0.05, 0.78, (0.0, 0.0, 0.0), 0.0),
    "Mat_WoodStock":    ((0.36, 0.20, 0.10), 0.05, 0.68, (0.0, 0.0, 0.0), 0.0),
}


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0


def get_mat(name: str, team_is_soviet: bool = False) -> bpy.types.Material:
    actual_name = name
    mat = bpy.data.materials.get(actual_name)
    if mat is not None:
        if name == "Mat_TeamPaint":
            bsdf = mat.node_tree.nodes.get("Principled BSDF")
            if bsdf:
                col = (0.86, 0.15, 0.15, 1.0) if team_is_soviet else (0.18, 0.50, 0.96, 1.0)
                bsdf.inputs["Base Color"].default_value = col
        return mat

    spec = MATERIAL_SPECS[name]
    mat = bpy.data.materials.new(name=actual_name)
    if not mat.use_nodes:
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    rgb = (0.86, 0.15, 0.15) if (name == "Mat_TeamPaint" and team_is_soviet) else spec[0]
    bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
    bsdf.inputs["Metallic"].default_value = spec[1]
    bsdf.inputs["Roughness"].default_value = spec[2]
    if spec[4] > 0.0 and "Emission Color" in bsdf.inputs:
        ec = spec[3]
        bsdf.inputs["Emission Color"].default_value = (ec[0], ec[1], ec[2], 1.0)
        bsdf.inputs["Emission Strength"].default_value = spec[4]
    return mat


def u2b(x: float, y: float, z: float) -> Vector:
    """Convert Unity (Right +X, Up +Y, Forward +Z) -> Blender (+X, -Y, +Z)."""
    return Vector((x, -z, y))


def u2b_rot(rx_deg: float = 0.0, ry_deg: float = 0.0, rz_deg: float = 0.0) -> Matrix:
    """Rotation matrix in Unity axes converted to Blender space."""
    # Change-of-basis P maps Unity vec v_u -> Blender vec v_b = P @ v_u
    p = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))
    rx = Matrix.Rotation(math.radians(rx_deg), 3, 'X')
    ry = Matrix.Rotation(math.radians(ry_deg), 3, 'Y')
    rz = Matrix.Rotation(math.radians(rz_deg), 3, 'Z')
    ru = ry @ rx @ rz
    return (p @ ru @ p.transposed()).to_4x4()


class PartCollector:
    """Collects bmesh geometry tagged with material names and builds clean hierarchical Mesh objects."""

    def __init__(self, is_soviet: bool = False):
        self.is_soviet = is_soviet
        self.entries: list[tuple[bmesh.types.BMesh, str]] = []

    def _push(self, bm: bmesh.types.BMesh, mat_name: str, center: tuple[float, float, float], rot_deg: tuple[float, float, float] = (0.0, 0.0, 0.0)) -> None:
        if any(abs(a) > 1e-4 for a in rot_deg):
            bmesh.ops.transform(bm, matrix=u2b_rot(*rot_deg), verts=bm.verts)
        bmesh.ops.translate(bm, vec=u2b(*center), verts=bm.verts)
        self.entries.append((bm, mat_name))

    def add_box(
        self,
        center: tuple[float, float, float],
        size: tuple[float, float, float],
        mat: str,
        bevel: float = 0.03,
        rot_deg: tuple[float, float, float] = (0.0, 0.0, 0.0),
        top_scale: tuple[float, float] = (1.0, 1.0),
    ) -> None:
        sx, sy, sz = size
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            # Blender cube has z in [-0.5, 0.5] = Up! x = Right, y = -Forward
            is_top = v.co.z > 0
            tx = top_scale[0] if is_top else 1.0
            tz = top_scale[1] if is_top else 1.0
            v.co.x *= sx * tx
            v.co.y *= sz * tz
            v.co.z *= sy
        if bevel > 0.001:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(sx, sy, sz) * 0.22), segments=1, profile=0.5, affect='EDGES')
        self._push(bm, mat, center, rot_deg)

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
        bevel: float = 0.03,
    ) -> None:
        """Creates an armored tank/bunker hull with sloped front glacis, sloped rear, and angled upper hull."""
        bm = bmesh.new()
        hw = width * 0.5
        hh = height * 0.5
        hl = length * 0.5
        tw = hw * side_taper
        # 8 corners in Unity (x, y, z) -> converted via u2b
        pts_u = [
            (-hw, -hh, -hl + rear_slope * 0.3),
            ( hw, -hh, -hl + rear_slope * 0.3),
            ( hw, -hh,  hl - front_slope * 0.45),
            (-hw, -hh,  hl - front_slope * 0.45),
            (-tw,  hh, -hl + rear_slope),
            ( tw,  hh, -hl + rear_slope),
            ( tw,  hh,  hl - front_slope),
            (-tw,  hh,  hl - front_slope),
        ]
        # Extended glacis nose tip at mid-height
        nose_u = [
            (-hw * 0.92, -hh * 0.15, hl),
            ( hw * 0.92, -hh * 0.15, hl),
            (-hw * 0.95, -hh * 0.15, -hl),
            ( hw * 0.95, -hh * 0.15, -hl),
        ]
        for p in pts_u + nose_u:
            bm.verts.new(u2b(*p))
        bm.verts.ensure_lookup_table()
        bmesh.ops.convex_hull(bm, input=list(bm.verts))
        if bevel > 0.001:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, profile=0.5, affect='EDGES')
        self._push(bm, mat, center)

    def add_cylinder(
        self,
        center: tuple[float, float, float],
        radius_bottom: float,
        radius_top: float,
        height: float,
        mat: str,
        segments: int = 18,
        axis: str = 'Y',
        rot_deg: tuple[float, float, float] = (0.0, 0.0, 0.0),
        bevel_caps: float = 0.02,
    ) -> None:
        bm = bmesh.new()
        bmesh.ops.create_cone(
            bm,
            cap_ends=True,
            cap_tris=False,
            segments=segments,
            radius1=radius_bottom,
            radius2=radius_top,
            depth=height,
        )
        # In Blender create_cone is along +Z (which IS Unity +Y Up!)
        if axis == 'Z':  # Unity Forward (+Z -> Blender -Y): rotate -90 around X so +Z -> -Y
            bmesh.ops.rotate(bm, matrix=Matrix.Rotation(math.radians(-90.0), 3, 'X'), verts=bm.verts)
        elif axis == 'X':  # Unity Right (+X -> Blender +X): rotate +90 around Y
            bmesh.ops.rotate(bm, matrix=Matrix.Rotation(math.radians(90.0), 3, 'Y'), verts=bm.verts)

        if bevel_caps > 0.001:
            cap_edges = [e for e in bm.edges if len(e.link_faces) == 2 and abs(e.link_faces[0].normal.dot(e.link_faces[1].normal)) < 0.5]
            if cap_edges:
                bmesh.ops.bevel(bm, geom=cap_edges, offset=min(bevel_caps, min(radius_bottom, radius_top, height) * 0.2), segments=1, affect='EDGES')

        self._push(bm, mat, center, rot_deg)

    def add_sphere(
        self,
        center: tuple[float, float, float],
        radius: float,
        mat: str,
        scale: tuple[float, float, float] = (1.0, 1.0, 1.0),
        segments: int = 18,
    ) -> None:
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=max(8, segments // 2), radius=radius)
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
        major_seg: int = 20,
        minor_seg: int = 10,
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
        segments: int = 20,
    ) -> None:
        """Revolves a (radius, up_y) profile around Unity +Y axis (for cooling towers, Russian onion domes, radar dishes)."""
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
                rr = max(0.01, r)
                ring = []
                for s in range(segments):
                    ang = (2.0 * math.pi * s) / segments
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

    def add_onion_dome(self, center: tuple[float, float, float], base_radius: float, height: float, mat_dome: str = "Mat_GoldBrass", mat_spire: str = "Mat_BrushedSteel") -> None:
        r = base_radius
        h = height
        profile = [
            (r * 0.72, 0.00),
            (r * 0.95, h * 0.16),
            (r * 1.08, h * 0.34),
            (r * 0.96, h * 0.52),
            (r * 0.62, h * 0.70),
            (r * 0.26, h * 0.86),
            (0.00,     h * 1.00),
        ]
        self.add_lathe_profile(center, profile, mat_dome, segments=20)
        cx, cy, cz = center
        self.add_cylinder((cx, cy + h + h * 0.22, cz), r * 0.07, r * 0.02, h * 0.48, mat_spire, segments=10)
        self.add_sphere((cx, cy + h + h * 0.12, cz), r * 0.14, mat_dome, segments=12)

    def add_quonset_arch(
        self,
        center: tuple[float, float, float],
        width: float,
        arch_height: float,
        length: float,
        mat_shell: str = "Mat_BrushedSteel",
        mat_ribs: str = "Mat_TeamPaint",
        rib_count: int = 5,
        rot_y_deg: float = 0.0,
    ) -> None:
        """Creates RA2's signature corrugated arched Quonset hangar with raised structural ribs."""
        cx, cy, cz = center
        hw = width * 0.5
        hl = length * 0.5
        segs = 14

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
        self._push(bm, mat_shell, center, (0.0, rot_y_deg, 0.0))

        # Structural arch ribs
        if rib_count >= 2:
            for r_idx in range(rib_count):
                t = r_idx / (rib_count - 1)
                rz = -hl * 0.88 + (2 * hl * 0.88) * t
                bm_rib = bmesh.new()
                rib_th = length / max(10.0, rib_count * 3.6)
                f_ring = []
                b_ring = []
                for i in range(segs + 1):
                    ang = math.pi * (i / segs)
                    px = -math.cos(ang) * (hw + 0.06)
                    py = math.sin(ang) * (arch_height + 0.06)
                    f_ring.append(bm_rib.verts.new(u2b(px, py, rz + rib_th)))
                    b_ring.append(bm_rib.verts.new(u2b(px, py, rz - rib_th)))
                for i in range(segs):
                    bm_rib.faces.new((f_ring[i], f_ring[i + 1], b_ring[i + 1], b_ring[i]))
                bm_rib.faces.new(f_ring)
                bm_rib.faces.new(list(reversed(b_ring)))
                bmesh.ops.recalc_face_normals(bm_rib, faces=bm_rib.faces)
                self._push(bm_rib, mat_ribs, center, (0.0, rot_y_deg, 0.0))

    def add_track_pod(
        self,
        center: tuple[float, float, float],
        width: float,
        height: float,
        length: float,
        wheel_count: int = 5,
        has_skirt: bool = True,
        skirt_mat: str = "Mat_TeamPaint",
    ) -> None:
        cx, cy, cz = center
        # Outer continuous rubber track belt (sloped front/rear sprockets)
        self.add_sloped_hull(
            center=(cx, cy + height * 0.5, cz),
            width=width,
            height=height,
            length=length,
            front_slope=height * 0.55,
            rear_slope=height * 0.45,
            side_taper=0.98,
            mat="Mat_TreadRubber",
            bevel=0.025,
        )
        # Steel roadwheels protruding slightly so wheel detail catches light
        wheel_r = height * 0.36
        span = length * 0.72
        for i in range(wheel_count):
            wz = cz - span * 0.5 + (span * i / max(1, wheel_count - 1))
            self.add_cylinder((cx, cy + wheel_r + 0.03, wz), wheel_r, wheel_r, width * 1.08, "Mat_DarkSteel", segments=14, axis='X', bevel_caps=0.01)
            self.add_cylinder((cx, cy + wheel_r + 0.03, wz), wheel_r * 0.48, wheel_r * 0.48, width * 1.14, "Mat_BrushedSteel", segments=10, axis='X', bevel_caps=0.005)

        if has_skirt:
            side_sign = 1.0 if cx >= 0 else -1.0
            self.add_box(
                (cx + side_sign * width * 0.36, cy + height * 0.76, cz),
                (width * 0.36, height * 0.46, length * 0.94),
                skirt_mat,
                bevel=0.02,
            )

    def build_mesh_object(self, name: str, pivot_unity: tuple[float, float, float] = (0.0, 0.0, 0.0), parent: bpy.types.Object | None = None) -> bpy.types.Object:
        merged = bmesh.new()
        mat_order: list[str] = []
        mat_index_map: dict[str, int] = {}

        pivot_b = u2b(*pivot_unity)

        for bm_part, mat_name in self.entries:
            if mat_name not in mat_index_map:
                mat_index_map[mat_name] = len(mat_order)
                mat_order.append(mat_name)
            m_idx = mat_index_map[mat_name]

            vert_map: dict[int, bmesh.types.BMVert] = {}
            for v in bm_part.verts:
                nv = merged.verts.new(v.co - pivot_b)
                vert_map[v.index] = nv
            for f in bm_part.faces:
                try:
                    nf = merged.faces.new([vert_map[v.index] for v in f.verts])
                    nf.material_index = m_idx
                    nf.smooth = True
                except ValueError:
                    pass
            bm_part.free()

        self.entries.clear()
        bmesh.ops.recalc_face_normals(merged, faces=merged.faces)

        # Edge-split sharp angles (> 21 deg) so smooth cylinders stay smooth while flat armor facets stay razor-sharp!
        sharp_edges = [
            e for e in merged.edges
            if len(e.link_faces) == 2 and e.link_faces[0].normal.angle(e.link_faces[1].normal, 0.0) > math.radians(21.0)
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
        path_mode='AUTO',
    )


def render_iso_preview(out_png: Path, target_span: float = 6.0, res: int = 384, is_building: bool = False) -> None:
    """Renders a crisp 45-degree isometric studio preview matching Unity's camera angle."""
    out_png.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'

    key_data = bpy.data.lights.new("KeySun", type='SUN')
    key_data.energy = 3.8
    key_data.color = (1.0, 0.97, 0.92)
    key_obj = bpy.data.objects.new("KeySun", key_data)
    bpy.context.collection.objects.link(key_obj)
    key_obj.rotation_euler = (math.radians(48), math.radians(22 if is_building else -18), math.radians(145 if is_building else 32))

    fill_data = bpy.data.lights.new("FillSun", type='SUN')
    fill_data.energy = 1.6
    fill_data.color = (0.76, 0.88, 1.0)
    fill_obj = bpy.data.objects.new("FillSun", fill_data)
    bpy.context.collection.objects.link(fill_obj)
    fill_obj.rotation_euler = (math.radians(62), math.radians(-20), math.radians(-35 if is_building else -135))

    cam_data = bpy.data.cameras.new("IsoCam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = target_span
    cam_obj = bpy.data.objects.new("IsoCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)

    dist = 24.0
    elev = math.radians(33.0)
    # Buildings face -Z in Unity (+Y in Blender!), Units face +Z in Unity (-Y in Blender!)
    if is_building:
        azim = math.radians(222.0)
    else:
        azim = math.radians(-38.0)
    cx = dist * math.cos(elev) * math.sin(azim)
    cy = -dist * math.cos(elev) * math.cos(azim)
    cz = dist * math.sin(elev) + target_span * 0.24
    cam_obj.location = (cx, cy, cz)

    direction = Vector((0.0, 0.0, target_span * 0.20)) - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam_obj

    scene.render.filepath = str(out_png)
    bpy.ops.render.render(write_still=True)
