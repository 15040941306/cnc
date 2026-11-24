"""
postprocess_glb_to_unity.py - Headless Blender 5.3 post-processor for Hunyuan3D .glb outputs.

1. Imports the neural-generated textured .glb mesh (30k-80k faces).
2. Rotates/aligns to match RA2 isometric front orientation (-Z for buildings, +Z for units in Unity),
   centers X/Z, flattens/grounds the base cleanly at Y = 0, and scales to exact RA2 grid footprint!
3. Optionally decimates polycount to game-ready target (e.g. 18,000 faces) while preserving UVs & PBR textures.
4. Exports embedded-texture .fbx (with path_mode='COPY', embed_textures=True!) directly into
   unity/re_cnc/Assets/_ReCncGenerated/RemasteredModels/<ID>.fbx AND renders a 512x512 preview!
"""

from __future__ import annotations
import argparse
import math
from pathlib import Path
import bpy
from mathutils import Vector


def parse_cli() -> argparse.Namespace:
    argv = []
    import sys
    if "--" in sys.argv:
        argv = sys.argv[sys.argv.index("--") + 1:]
    p = argparse.ArgumentParser()
    p.add_argument("--glb", required=True)
    p.add_argument("--fbx-out", required=True)
    p.add_argument("--preview-out", required=True)
    p.add_argument("--actor-id", required=True)
    p.add_argument("--footprint-x", type=float, default=7.4)
    p.add_argument("--footprint-z", type=float, default=7.4)
    p.add_argument("--yaw-deg", type=float, default=0.0)
    p.add_argument("--target-faces", type=int, default=22000)
    return p.parse_args(argv)


def main() -> None:
    args = parse_cli()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'

    if args.glb.lower().endswith(".obj"):
        bpy.ops.wm.obj_import(filepath=args.glb)
    else:
        bpy.ops.import_scene.gltf(filepath=args.glb)
    bpy.context.view_layer.update()

    mesh_objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not mesh_objs:
        raise RuntimeError(f"No mesh objects found in {args.glb}")

    # Apply existing object transforms so vertices are in world space
    bpy.ops.object.select_all(action='DESELECT')
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = "Body"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Rotate around Z if yaw adjustment requested
    if abs(args.yaw_deg) > 1e-3:
        obj.rotation_euler = (0.0, 0.0, math.radians(args.yaw_deg))
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    # Compute world bounds in Blender (X, Y, Z_up)
    verts = [v.co for v in obj.data.vertices]
    min_x = min(v.x for v in verts)
    max_x = max(v.x for v in verts)
    min_y = min(v.y for v in verts)
    max_y = max(v.y for v in verts)
    min_z = min(v.z for v in verts)
    max_z = max(v.z for v in verts)

    span_x = max(1e-4, max_x - min_x)
    span_y = max(1e-4, max_y - min_y)
    scale_factor = min(args.footprint_x / span_x, args.footprint_z / span_y)

    cx = 0.5 * (min_x + max_x)
    cy = 0.5 * (min_y + max_y)
    # Shift center to (0,0) and bottom (5th percentile lowest Z to ignore stray bottom noise) to Z = 0
    sorted_z = sorted(v.z for v in verts)
    ground_z = sorted_z[max(0, int(len(sorted_z) * 0.01))]

    for v in obj.data.vertices:
        v.co.x = (v.co.x - cx) * scale_factor
        v.co.y = (v.co.y - cy) * scale_factor
        v.co.z = max(0.0, (v.co.z - ground_z) * scale_factor)
    obj.data.update()

    # Decimate cleanly while preserving UVs if triangle count is huge
    poly_count = len(obj.data.polygons)
    if poly_count > args.target_faces > 0:
        dec = obj.modifiers.new(name="Decimate", type='DECIMATE')
        dec.ratio = args.target_faces / poly_count
        bpy.ops.object.modifier_apply(modifier=dec.name)

    # Parent under clean root Empty matching our Unity entity loader
    root = bpy.data.objects.new(args.actor_id, None)
    bpy.context.collection.objects.link(root)
    obj.parent = root

    # Save baked Albedo texture into RemasteredTextures/AiBaked/<ActorId>_albedo.png & tag material Mat_AiBaked
    fbx_out = Path(args.fbx_out)
    fbx_out.parent.mkdir(parents=True, exist_ok=True)
    ai_tex_dir = fbx_out.parent.parent / "RemasteredTextures" / "AiBaked"
    ai_tex_dir.mkdir(parents=True, exist_ok=True)

    saved_albedo = None
    for mat in obj.data.materials:
        if mat is None or not mat.node_tree:
            continue
        mat.name = f"Mat_AiBaked_{args.actor_id}"
        for node in mat.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image is not None and node.image.size[0] > 0:
                # Pick baseColor texture (avoid metallicRoughness if multiple exist)
                is_base = any(
                    link.to_socket.name == "Base Color"
                    for link in node.outputs["Color"].links
                )
                if is_base or saved_albedo is None:
                    out_img = ai_tex_dir / f"{args.actor_id}_albedo.png"
                    node.image.filepath_raw = str(out_img)
                    node.image.file_format = 'PNG'
                    node.image.save()
                    saved_albedo = out_img
                    if is_base:
                        break

    bpy.ops.export_scene.fbx(
        filepath=str(fbx_out),
        use_selection=False,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        object_types={'EMPTY', 'MESH'},
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        path_mode='COPY',
        embed_textures=True,
    )

    # Render 512x512 Studio Preview
    preview_out = Path(args.preview_out)
    preview_out.parent.mkdir(parents=True, exist_ok=True)
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.film_transparent = True

    world = bpy.data.worlds.new("Sky")
    scene.world = world
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.72, 0.78, 0.88, 1.0)
        bg.inputs[1].default_value = 1.0

    sun = bpy.data.lights.new("Sun", type='SUN')
    sun.energy = 3.2
    sun_obj = bpy.data.objects.new("Sun", sun)
    bpy.context.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = (math.radians(48), math.radians(18), math.radians(140))

    cam = bpy.data.cameras.new("Cam")
    cam.type = 'ORTHO'
    cam.ortho_scale = max(args.footprint_x, args.footprint_z) * 1.22
    cam_obj = bpy.data.objects.new("Cam", cam)
    bpy.context.collection.objects.link(cam_obj)

    dist = 24.0
    elev = math.radians(32.0)
    azim = math.radians(-42.0)
    cam_obj.location = (dist * math.cos(elev) * math.sin(azim), -dist * math.cos(elev) * math.cos(azim), dist * math.sin(elev) + 1.5)
    direction = Vector((0.0, 0.0, (max_z - ground_z) * scale_factor * 0.38)) - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam_obj

    scene.render.filepath = str(preview_out)
    bpy.ops.render.render(write_still=True)
    print(f"[PostProcess OK] PolyCount={len(obj.data.polygons)} -> {fbx_out}")


if __name__ == "__main__":
    main()
