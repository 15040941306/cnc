#!/usr/bin/env python3
"""
generate_ra2_3d.py - End-to-end Local Hunyuan3D-2.1 (Apple Silicon MPS) -> Blender -> Unity FBX pipeline.

For any RA2 Actor ID (or --all-buildings):
  1. Enhances original transparent RA2 sprite (Sprites/<ID>.png) to crisp 1024x1024 RGBA (no rembg loss!).
  2. Runs Hunyuan3D-2.1 DiT+VAE Shape generation on Apple MPS (unloads before Paint to fit 16GB RAM).
  3. Runs Hunyuan3D-2.1 PBR Multi-view Texturing («safe» preset on Apple MPS) -> textured .glb.
  4. Invokes headless Blender 5.3 to ground, center, scale to exact RA2 grid footprint, decimate cleanly,
     embed PBR textures into .fbx, and export straight into Unity's RemasteredModels/<ID>.fbx!

Usage:
  /Users/wuhao/CodeProjects/re_cnc/artifacts/hunyuan3d-mac/venv/bin/python tools/local_ai_3d/generate_ra2_3d.py --ids GACNST
"""

from __future__ import annotations
import argparse
import gc
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

os.environ.setdefault("PYTORCH_MPS_HIGH_WATERMARK_RATIO", "0.0")
os.environ.setdefault("PYTORCH_ENABLE_MPS_FALLBACK", "1")

REPO_ROOT = Path(__file__).resolve().parents[2]
HY3D_DIR = REPO_ROOT / "artifacts" / "hunyuan3d-mac"
SPRITES_DIR = REPO_ROOT / "unity" / "re_cnc" / "Assets" / "_ReCncGenerated" / "Sprites"
AI_WORK_DIR = REPO_ROOT / "artifacts" / "ai_3d_work"
UNITY_FBX_DIR = REPO_ROOT / "unity" / "re_cnc" / "Assets" / "_ReCncGenerated" / "RemasteredModels"
PREVIEW_DIR = REPO_ROOT / "artifacts" / "blender_previews"
BLENDER_EXE = "/Applications/Blender.app/Contents/MacOS/Blender"

# Exact RA2 grid footprints in Unity meters (1 cell = 2.0m, slight 0.92 margin)
BUILDING_FOOTPRINTS: dict[str, tuple[float, float]] = {
    "GACNST": (7.4, 7.4),
    "GAPOWR": (3.7, 3.7),
    "GAREFN": (7.4, 5.5),
    "GAPILE": (3.7, 3.7),
    "GAWEAP": (9.3, 5.5),
    "GAAIRC": (5.5, 3.7),
    "GATECH": (5.5, 3.7),
    "GAPILL": (1.85, 1.85),
    "ATESLA": (1.85, 1.85),
    "NACNST": (7.4, 7.4),
    "NAPOWR": (3.7, 3.7),
    "NAREFN": (7.4, 5.5),
    "NAHAND": (3.7, 3.7),
    "NAWEAP": (9.3, 5.5),
    "NATECH": (5.5, 3.7),
    "TESLA":  (1.85, 1.85),
}


def run_single_actor(actor_id: str, steps: int, octree: int, tex_views: int, skip_texture: bool) -> Path:
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    sys.path.insert(0, str(HY3D_DIR))
    from prepare_ra2_inputs import enhance_sprite_for_3d

    src_sprite = SPRITES_DIR / f"{actor_id}.png"
    if not src_sprite.is_file():
        raise FileNotFoundError(f"Sprite not found: {src_sprite}")

    actor_work = AI_WORK_DIR / actor_id
    actor_work.mkdir(parents=True, exist_ok=True)
    input_1024 = actor_work / f"{actor_id}_input_1024.png"
    enhance_sprite_for_3d(src_sprite, input_1024, canvas_size=1024)
    print(f"[{actor_id}] 1/4 Enhanced input sprite -> {input_1024}", flush=True)

    # Import gradio_app inside HY3D_DIR working directory so relative paths resolve cleanly
    prev_cwd = Path.cwd()
    os.chdir(HY3D_DIR)
    try:
        from PIL import Image
        import torch
        import gradio_app

        img = Image.open(input_1024).convert("RGBA")

        t0 = time.time()
        print(f"[{actor_id}] 2/4 Running Hunyuan3D-2.1 Shape (steps={steps}, octree={octree}, remove_bg=False)...", flush=True)
        # Pass remove_bg=False so Westwood's exact palette-0 alpha mask is preserved!
        raw_glb, mesh_state, log_text, _ = gradio_app.generate_3d(
            img, False, steps, 5.0, octree, 42, False, "en"
        )
        if not raw_glb or not mesh_state:
            raise RuntimeError(f"Shape generation failed for {actor_id}:\n{log_text}")
        shape_glb = actor_work / f"{actor_id}_shape.glb"
        shutil.copy2(raw_glb, shape_glb)
        print(
            f"[{actor_id}] Shape complete in {time.time()-t0:.1f}s "
            f"({mesh_state['original_faces']} faces) -> {shape_glb}",
            flush=True,
        )

        final_glb = shape_glb
        if not skip_texture:
            t1 = time.time()
            print(f"[{actor_id}] 3/4 Running Hunyuan3D-2.1 PBR Paint (safe preset, {tex_views}x256)...", flush=True)
            tex_path, _, _, tex_log = gradio_app.texture_3d_handler(
                mesh_state, img, "safe", tex_views, 256, "auto", True, "en", None
            )
            if tex_path and Path(tex_path).is_file():
                # Prefer sibling .obj output to avoid glTF 1-v UV coordinate flip in mesh_io.py!
                sibling_obj = Path(tex_path).with_suffix(".obj")
                if sibling_obj.is_file():
                    final_glb = sibling_obj
                else:
                    textured_glb = actor_work / f"{actor_id}_textured.glb"
                    shutil.copy2(tex_path, textured_glb)
                    final_glb = textured_glb
                print(f"[{actor_id}] PBR Paint complete in {time.time()-t1:.1f}s -> {final_glb}", flush=True)
            else:
                print(f"[WARN] PBR Paint returned empty, using untextured mesh:\n{tex_log}", flush=True)

        gc.collect()
        if torch.backends.mps.is_available():
            torch.mps.empty_cache()
    finally:
        os.chdir(prev_cwd)

    # 4. Post-process via Blender 5.3 -> Unity RemasteredModels/<ID>.fbx + Preview PNG
    fx, fz = BUILDING_FOOTPRINTS.get(actor_id.upper(), (4.0, 4.0))
    fbx_out = UNITY_FBX_DIR / f"{actor_id}.fbx"
    preview_out = PREVIEW_DIR / f"{actor_id}_ai3d.png"
    post_script = Path(__file__).resolve().parent / "postprocess_glb_to_unity.py"

    print(f"[{actor_id}] 4/4 Post-processing in Blender -> {fbx_out}...", flush=True)
    subprocess.run(
        [
            BLENDER_EXE, "--background", "--factory-startup",
            "--python", str(post_script),
            "--",
            "--glb", str(final_glb),
            "--fbx-out", str(fbx_out),
            "--preview-out", str(preview_out),
            "--actor-id", actor_id,
            "--footprint-x", str(fx),
            "--footprint-z", str(fz),
            "--yaw-deg", "-45.0",
            "--target-faces", "0",
        ],
        check=True,
    )
    return preview_out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids", nargs="+", default=["GACNST"], help="Actor IDs to generate (e.g. GACNST GAPOWR)")
    ap.add_argument("--all-buildings", action="store_true", help="Generate all 16 Allied & Soviet buildings")
    ap.add_argument("--steps", type=int, default=25)
    ap.add_argument("--octree", type=int, default=224)
    ap.add_argument("--tex-views", type=int, default=4, help="4 views = 1.9GiB attention (safest for 16GB RAM!), 6 = 4.2GiB")
    ap.add_argument("--skip-texture", action="store_true")
    args = ap.parse_args()

    targets = list(BUILDING_FOOTPRINTS.keys()) if args.all_buildings else [x.upper() for x in args.ids]
    for idx, aid in enumerate(targets, 1):
        print(f"\n================ [{idx}/{len(targets)}] {aid} ================", flush=True)
        preview = run_single_actor(aid, args.steps, args.octree, args.tex_views, args.skip_texture)
        print(f"[SUCCESS] {aid} ready in Unity! Preview: {preview}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
