"""
build_all_models.py - Synthesizes procedural PBR + Normal textures and exports all 28 UV-mapped
high-detail 3D .fbx models + 448x448 studio comparison renders via Blender 5.3.
"""

from __future__ import annotations
import json
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from tex_gen import generate_all_textures                                          # noqa: E402
from common import export_fbx, render_iso_preview, reset_scene, set_texture_registry  # noqa: E402
from allied_buildings import ALLIED_BUILDINGS                                      # noqa: E402
from soviet_buildings import SOVIET_BUILDINGS                                      # noqa: E402
from units_and_infantry import UNITS_AND_INFANTRY                                  # noqa: E402


def main() -> None:
    repo_root = SCRIPT_DIR.parent.parent
    tex_out_dir = repo_root / "unity" / "re_cnc" / "Assets" / "_ReCncGenerated" / "RemasteredTextures"
    fbx_out_dir = repo_root / "unity" / "re_cnc" / "Assets" / "_ReCncGenerated" / "RemasteredModels"
    preview_dir = repo_root / "artifacts" / "blender_previews"
    manifest_out = repo_root / "unity" / "re_cnc" / "Assets" / "StreamingAssets" / "Ra2Converted" / "remastered_manifest.json"

    print("[0/28] Synthesizing 512x512 PBR Albedo + Normal Maps...", flush=True)
    tex_registry = generate_all_textures(tex_out_dir, size=512)
    set_texture_registry(tex_registry)

    fbx_out_dir.mkdir(parents=True, exist_ok=True)
    preview_dir.mkdir(parents=True, exist_ok=True)
    manifest_out.parent.mkdir(parents=True, exist_ok=True)

    all_builders: dict[str, tuple[object, float]] = {}
    all_builders.update(ALLIED_BUILDINGS)
    all_builders.update(SOVIET_BUILDINGS)
    all_builders.update(UNITS_AND_INFANTRY)

    records = []
    total = len(all_builders)
    for idx, (actor_id, (builder_fn, ortho_span)) in enumerate(all_builders.items(), start=1):
        reset_scene()
        builder_fn()

        fbx_path = fbx_out_dir / f"{actor_id}.fbx"
        export_fbx(fbx_path)

        preview_path = preview_dir / f"{actor_id}.png"
        is_bldg = actor_id in ALLIED_BUILDINGS or actor_id in SOVIET_BUILDINGS
        render_iso_preview(preview_path, target_span=ortho_span, res=448, is_building=is_bldg)

        rel_unity_path = f"Assets/_ReCncGenerated/RemasteredModels/{actor_id}.fbx"
        records.append({
            "ActorId": actor_id,
            "FbxAssetPath": rel_unity_path,
        })
        print(f"[{idx:02d}/{total:02d}] Generated High-Detail UV-PBR FBX & Preview: {actor_id}", flush=True)

    manifest_data = {
        "SchemaVersion": 2,
        "Models": records,
    }
    manifest_out.write_text(json.dumps(manifest_data, indent=2), encoding="utf-8")
    print(f"[DONE] Exported {len(records)} high-detail remastered 3D FBX models + PBR textures!")


if __name__ == "__main__":
    main()
