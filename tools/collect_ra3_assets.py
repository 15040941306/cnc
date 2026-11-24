#!/usr/bin/env python3
"""
collect_ra3_assets.py

在安装有《命令与征服：红色警戒3》（或起义时刻）的 Windows 电脑上运行此脚本，
自动扫描 Steam / EA App / 注册表安装目录，提取 3D 单位/建筑模型、地表材质以及全部遭遇战/战役地图包
（StaticStream.big、GlobalStream.big、Terrain.big、MapsMultiplayer.big、MapsCampaign.big、Core 补丁包、外置 .map 与 .SkuDef），
仅跳过数 GB 的真人过场电影与 WorldBuilder 重复编辑器缓存，复制到项目的 raw_assets/ra3/ 目录。

零第三方依赖，仅使用 Python 3 标准库。
"""

from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_OUTPUT_DIR = REPO_ROOT / "raw_assets" / "ra3"

# Skip huge non-gameplay archives (FMV live-action movies & redundant 1.3GB WorldBuilder editor cache)
SKIP_BIG_SUBSTRINGS = (
    "movie",
    "wbdata",
    "apt.big",
)

SKIP_AUDIO_UNLESS_REQUESTED = (
    "audio",
)


@dataclass
class CollectedRa3File:
    relative_path: str
    category: str
    size_bytes: int
    sha256: str


def is_valid_ra3_dir(path: Path) -> bool:
    if not path.is_dir():
        return False
    # Check for StaticStream.big in Data/ or Core/ or *.SkuDef in root
    if (path / "Data" / "StaticStream.big").is_file():
        return True
    try:
        for entry in path.iterdir():
            if entry.is_file() and entry.suffix.lower() == ".skudef":
                return True
            if entry.is_dir() and entry.name.lower() in {"data", "core"}:
                for sub in entry.rglob("*.big"):
                    if sub.name.lower() == "staticstream.big":
                        return True
    except OSError:
        return False
    return False


def find_steam_ra3_dirs(candidates: list[Path]) -> None:
    if sys.platform != "win32":
        return
    try:
        import winreg
    except ImportError:
        return

    steam_roots: list[Path] = []
    for hive, subkey, val_name in (
        (winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam", "SteamPath"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Valve\Steam", "InstallPath"),
    ):
        try:
            with winreg.OpenKey(hive, subkey) as k:
                val, _ = winreg.QueryValueEx(k, val_name)
                if val:
                    steam_roots.append(Path(str(val)))
        except OSError:
            continue

    libs: list[Path] = []
    for root in steam_roots:
        libs.append(root)
        vdf = root / "steamapps" / "libraryfolders.vdf"
        if vdf.is_file():
            try:
                text = vdf.read_text(encoding="utf-8", errors="ignore")
                for m in re.finditer(r'"path"\s+"([^"]+)"', text):
                    libs.append(Path(m.group(1).replace(r"\\", "\\")))
            except OSError:
                pass

    folder_names = (
        "Command and Conquer Red Alert 3",
        "Command & Conquer Red Alert 3",
        "Command and Conquer Red Alert 3 - Uprising",
        "Command & Conquer Red Alert 3 - Uprising",
    )
    for lib in libs:
        for fn in folder_names:
            candidates.append(lib / "steamapps" / "common" / fn)


def find_registry_ra3_dirs(candidates: list[Path]) -> None:
    if sys.platform != "win32":
        return
    try:
        import winreg
    except ImportError:
        return

    reg_keys = (
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Electronic Arts\Electronic Arts\Red Alert 3", "Install Dir"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Electronic Arts\Electronic Arts\Red Alert 3", "Install Dir"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Electronic Arts\Electronic Arts\Red Alert 3 Uprising", "Install Dir"),
    )
    for hive, subkey, val_name in reg_keys:
        try:
            with winreg.OpenKey(hive, subkey) as k:
                val, _ = winreg.QueryValueEx(k, val_name)
                if val:
                    candidates.append(Path(str(val)))
        except OSError:
            continue


def detect_ra3_dirs(exclude_dir: Path) -> list[Path]:
    candidates: list[Path] = [Path.cwd()]
    find_steam_ra3_dirs(candidates)
    find_registry_ra3_dirs(candidates)

    if sys.platform == "win32":
        common_rels = (
            r"Program Files (x86)\Origin Games\Command and Conquer Red Alert 3",
            r"Program Files\EA Games\Command and Conquer Red Alert 3",
            r"Program Files (x86)\Electronic Arts\Red Alert 3",
            r"Games\Red Alert 3",
            r"Red Alert 3",
        )
        for drv in "CDEFGH":
            root = Path(f"{drv}:\\")
            if root.exists():
                for rel in common_rels:
                    candidates.append(root / rel)

    exclude_norm = str(exclude_dir.resolve()).lower()
    found: list[Path] = []
    seen: set[str] = set()
    for p in candidates:
        try:
            res = p.resolve()
        except OSError:
            continue
        k = str(res).lower()
        if k in seen or k == exclude_norm:
            continue
        seen.add(k)
        if is_valid_ra3_dir(res):
            found.append(res)
    return found


def scan_ra3_model_packages(game_dir: Path, output_dir: Path, include_audio: bool, skip_maps: bool) -> list[tuple[Path, Path, str]]:
    """Returns list of (src_path, relative_dest_path, category)."""
    results: list[tuple[Path, Path, str]] = []
    out_res = output_dir.resolve()

    for entry in sorted(game_dir.rglob("*"), key=lambda p: p.as_posix().lower()):
        if not entry.is_file() or entry.is_symlink():
            continue
        try:
            if out_res in entry.resolve().parents or entry.resolve() == out_res:
                continue
        except OSError:
            continue

        rel = entry.relative_to(game_dir)
        name_lower = entry.name.lower()
        ext_lower = entry.suffix.lower()

        if ext_lower == ".skudef":
            results.append((entry, rel, "skudef_index"))
            continue

        if not skip_maps and ext_lower == ".map":
            results.append((entry, rel, "loose_map"))
            continue

        if ext_lower != ".big":
            continue

        if any(skip in name_lower for skip in SKIP_BIG_SUBSTRINGS):
            continue
        if not include_audio and any(a in name_lower for a in SKIP_AUDIO_UNLESS_REQUESTED):
            continue
        if skip_maps and ("maps" in name_lower or "terrain" in name_lower):
            continue

        if "maps" in name_lower:
            category = "maps_big"
        elif "terrain" in name_lower:
            category = "terrain_big"
        elif "staticstream" in name_lower or "patch" in name_lower or "core" in entry.as_posix().lower():
            category = "model_static_stream"
        elif "globalstream" in name_lower or "shaders" in name_lower or "misc" in name_lower:
            category = "shared_stream"
        else:
            category = "extra_big"
        results.append((entry, rel, category))

    # Also check Windows %APPDATA%\Red Alert 3\Maps for user/custom skirmish maps
    if not skip_maps and sys.platform == "win32":
        import os
        appdata = os.environ.get("APPDATA")
        if appdata:
            for folder_name in ("Red Alert 3", "Red Alert 3 Uprising"):
                custom_maps_dir = Path(appdata) / folder_name / "Maps"
                if custom_maps_dir.is_dir():
                    for mp in sorted(custom_maps_dir.rglob("*")):
                        if mp.is_file() and mp.suffix.lower() in {".map", ".tga", ".xml", ".str"}:
                            rel_custom = Path("CustomMaps") / mp.relative_to(custom_maps_dir)
                            results.append((mp, rel_custom, "custom_map"))

    return results


def copy_with_sha256(src: Path, dst: Path, overwrite: bool) -> tuple[str, bool]:
    dst.parent.mkdir(parents=True, exist_ok=True)
    if not overwrite and dst.is_file():
        s_st, d_st = src.stat(), dst.stat()
        if s_st.st_size == d_st.st_size and abs(s_st.st_mtime - d_st.st_mtime) < 1e-3:
            h = hashlib.sha256()
            with dst.open("rb") as f:
                while chunk := f.read(4 * 1024 * 1024):
                    h.update(chunk)
            return h.hexdigest(), False

    h = hashlib.sha256()
    with src.open("rb") as fin, dst.open("wb") as fout:
        while chunk := fin.read(4 * 1024 * 1024):
            h.update(chunk)
            fout.write(chunk)
    shutil.copystat(src, dst)
    return h.hexdigest(), True


def fmt_size(num: int) -> str:
    if num >= 1024 ** 3:
        return f"{num / (1024 ** 3):.2f} GB"
    if num >= 1024 ** 2:
        return f"{num / (1024 ** 2):.2f} MB"
    if num >= 1024:
        return f"{num / 1024:.1f} KB"
    return f"{num} B"


def resolve_game_dir(arg_dir: str | None, out_dir: Path) -> Path:
    if arg_dir:
        p = Path(arg_dir.strip().strip('"').strip("'")).expanduser().resolve()
        if not p.is_dir():
            raise SystemExit(f"[错误] 目录不存在: {p}")
        if not is_valid_ra3_dir(p):
            raise SystemExit(f"[错误] 在 {p} 中未检测到 StaticStream.big 或 .SkuDef，请确认这是红警3安装目录。")
        return p

    detected = detect_ra3_dirs(out_dir)
    if len(detected) == 1:
        print(f"[自动识别] 找到《红色警戒3》目录: {detected[0]}")
        return detected[0]
    if len(detected) > 1:
        print("检测到多个《红色警戒3》目录：")
        for i, d in enumerate(detected, 1):
            print(f"  [{i}] {d}")
        if sys.stdin.isatty():
            c = input("请输入序号 [默认 1]: ").strip()
            idx = int(c) - 1 if c.isdigit() and 1 <= int(c) <= len(detected) else 0
            return detected[idx]
        return detected[0]

    if not sys.stdin.isatty():
        raise SystemExit("[错误] 未自动检测到红警3目录，请通过 -g / --game-dir 指定安装路径。")
    raw = input("未自动扫到目录，请输入或拖入《红色警戒3》安装目录后回车: ").strip().strip('"').strip("'")
    if not raw:
        raise SystemExit("[已取消]")
    p = Path(raw).expanduser().resolve()
    if not is_valid_ra3_dir(p):
        raise SystemExit(f"[错误] 在 {p} 中未检测到 StaticStream.big。")
    return p


def main() -> None:
    ap = argparse.ArgumentParser(description="收集《红色警戒3》3D 模型、贴图、地表材质与全部地图包到 raw_assets/ra3/")
    ap.add_argument("-g", "--game-dir", help="红警3安装目录（不传则在 Windows 下自动扫描 Steam / EA App / 注册表）")
    ap.add_argument("-o", "--output-dir", default=str(DEFAULT_OUTPUT_DIR), help=f"输出目录（默认: {DEFAULT_OUTPUT_DIR}）")
    ap.add_argument("--include-audio", action="store_true", help="同时收集语音音效包 (*Audio.big，约额外 740MB)")
    ap.add_argument("--no-maps", action="store_true", help="跳过地图与地表包（默认包含 MapsMultiplayer/MapsCampaign/Terrain.big）")
    ap.add_argument("--overwrite", action="store_true", help="强制覆盖已存在的文件")
    args = ap.parse_args()

    out_dir = Path(args.output_dir.strip().strip('"').strip("'")).expanduser().resolve()
    game_dir = resolve_game_dir(args.game_dir, out_dir)
    scanned = scan_ra3_model_packages(game_dir, out_dir, args.include_audio, args.no_maps)
    if not scanned:
        raise SystemExit(f"[错误] 在 {game_dir} 中未找到可用的 .big 模型/地图包。")

    total_bytes = sum(p.stat().st_size for p, _, _ in scanned)
    print(f"源目录   : {game_dir}")
    print(f"目标目录 : {out_dir}")
    print(f"待收集数 : {len(scanned)} 个核心模型与地图文件（总计 {fmt_size(total_bytes)}，已自动跳过过场电影大包）")
    print("-" * 60)

    records: list[CollectedRa3File] = []
    for idx, (src, rel, cat) in enumerate(scanned, 1):
        sz = src.stat().st_size
        sha, copied = copy_with_sha256(src, out_dir / rel, args.overwrite)
        tag = "复制" if copied else "跳过(未变)"
        print(f"[{idx}/{len(scanned)}] {tag} {rel.as_posix()} ({fmt_size(sz)}) [{cat}]")
        records.append(CollectedRa3File(rel.as_posix(), cat, sz, sha))

    manifest = {
        "schema_version": 1,
        "game": "red_alert_3",
        "created_at_utc": datetime.now(timezone.utc).isoformat(),
        "total_files": len(records),
        "total_bytes": total_bytes,
        "files": [asdict(r) for r in records],
    }
    manifest_path = out_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print("-" * 60)
    print(f"[完成] 红警3模型包已收集至: {out_dir} (共 {fmt_size(total_bytes)})")


if __name__ == "__main__":
    main()
