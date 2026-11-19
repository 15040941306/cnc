#!/usr/bin/env python3
"""
collect_ra2_assets.py

在安装有《红色警戒2》/《尤里的复仇》的电脑上运行此脚本，
自动扫描并将移植所需的全部原版资源文件（.mix、补丁包、字库 .csf、地图及外置规则 .ini）
直接按目录结构收集复制到项目的指定目录下（默认为项目根目录下的 raw_assets/），无需压缩，
同时生成 manifest.json 校验清单供离线转换器直接读取。

零第三方依赖，仅使用 Python 3 标准库。
"""

from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
import fnmatch
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_OUTPUT_DIR = REPO_ROOT / "raw_assets"

CORE_REQUIRED_ANY = ("ra2.mix",)

MOVIE_MIX_PATTERNS = (
    "mov*.mix",
    "movies*.mix",
)

MUSIC_MIX_PATTERNS = (
    "theme*.mix",
)

ALLOWED_LOOSE_INI_NAMES = {
    "rules.ini",
    "rulesmd.ini",
    "art.ini",
    "artmd.ini",
    "sound.ini",
    "soundmd.ini",
    "ai.ini",
    "aimd.ini",
    "eva.ini",
    "evamd.ini",
    "ui.ini",
    "uimd.ini",
    "theme.ini",
    "thememd.ini",
    "battle.ini",
    "battlemd.ini",
    "mapsel.ini",
    "mapselmd.ini",
    "mission.ini",
    "missionmd.ini",
    "fsd.ini",
}

ALLOWED_LOOSE_EXTENSIONS = {
    ".csf": "string_table",
    ".mpr": "map",
    ".yrm": "map",
    ".map": "map",
    ".pkt": "map_list",
    ".pal": "palette",
    ".vxl": "voxel",
    ".hva": "voxel_anim",
    ".shp": "sprite",
    ".tmp": "terrain_tile",
    ".tem": "terrain_tile",
    ".sno": "terrain_tile",
    ".urb": "terrain_tile",
    ".ubn": "terrain_tile",
    ".lun": "terrain_tile",
    ".des": "terrain_tile",
    ".bag": "audio_bank",
    ".idx": "audio_index",
    ".fnt": "font",
}

SKIP_SUBDIRS = {
    ".git",
    "__pycache__",
    "cncnet",
    "saved games",
    "save",
    "logs",
    "debug",
    "screenshots",
    "taunts",
}


@dataclass
class CollectedFile:
    relative_path: str
    category: str
    size_bytes: int
    sha256: str = ""


def matches_any(filename_lower: str, patterns: tuple[str, ...]) -> bool:
    return any(fnmatch.fnmatch(filename_lower, pat) for pat in patterns)


def classify_mix(filename_lower: str) -> str:
    if filename_lower in {"ra2.mix", "language.mix", "multi.mix"}:
        return "core_mix"
    if filename_lower in {"ra2md.mix", "langmd.mix", "multimd.mix"}:
        return "yuri_expansion_mix"
    if matches_any(filename_lower, ("expand*.mix", "ecache*.mix", "elocal*.mix")):
        return "patch_or_expand_mix"
    if matches_any(filename_lower, MUSIC_MIX_PATTERNS):
        return "music_mix"
    if matches_any(filename_lower, MOVIE_MIX_PATTERNS):
        return "movie_mix"
    return "extra_mix"


def is_valid_ra2_dir(path: Path) -> bool:
    if not path.is_dir():
        return False
    try:
        lower_names = {p.name.lower() for p in path.iterdir() if p.is_file()}
    except OSError:
        return False
    return any(req in lower_names for req in CORE_REQUIRED_ANY)


def find_steam_ra2_dirs(candidates: list[Path]) -> None:
    if sys.platform != "win32":
        return
    try:
        import winreg
    except ImportError:
        return

    steam_roots: list[Path] = []
    reg_queries = [
        (winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam", "SteamPath"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Valve\Steam", "InstallPath"),
    ]
    for hive, subkey, value_name in reg_queries:
        try:
            with winreg.OpenKey(hive, subkey) as key:
                val, _ = winreg.QueryValueEx(key, value_name)
                if val:
                    steam_roots.append(Path(str(val)))
        except OSError:
            continue

    library_paths: list[Path] = []
    for root in steam_roots:
        library_paths.append(root)
        vdf = root / "steamapps" / "libraryfolders.vdf"
        if vdf.is_file():
            try:
                content = vdf.read_text(encoding="utf-8", errors="ignore")
                for match in re.finditer(r'"path"\s+"([^"]+)"', content):
                    raw_lib = match.group(1).replace(r"\\", "\\")
                    library_paths.append(Path(raw_lib))
            except OSError:
                pass

    folder_names = (
        "Command & Conquer Red Alert II",
        "Command and Conquer Red Alert II",
    )
    for lib in library_paths:
        for folder in folder_names:
            candidates.append(lib / "steamapps" / "common" / folder)


def find_registry_ra2_dirs(candidates: list[Path]) -> None:
    if sys.platform != "win32":
        return
    try:
        import winreg
    except ImportError:
        return

    reg_entries = [
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Westwood\Red Alert 2", "InstallPath"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Westwood\Red Alert 2", "InstallPath"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\EA Games\Command and Conquer Red Alert II", "Install Dir"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\EA Games\Command and Conquer Red Alert II", "Install Dir"),
    ]
    for hive, subkey, value_name in reg_entries:
        try:
            with winreg.OpenKey(hive, subkey) as key:
                val, _ = winreg.QueryValueEx(key, value_name)
                if val:
                    p = Path(str(val))
                    candidates.append(p if p.is_dir() else p.parent)
        except OSError:
            continue


def detect_game_dirs(exclude_dir: Path) -> list[Path]:
    candidates: list[Path] = [
        Path.cwd(),
        Path(__file__).resolve().parent,
    ]
    find_steam_ra2_dirs(candidates)
    find_registry_ra2_dirs(candidates)

    if sys.platform == "win32":
        common_rel_paths = [
            r"Program Files (x86)\Origin Games\Command and Conquer Red Alert II",
            r"Program Files\EA Games\Command and Conquer Red Alert II",
            r"Program Files (x86)\Steam\steamapps\common\Command & Conquer Red Alert II",
            r"Westwood\RA2",
            r"Games\RA2",
            r"RA2",
        ]
        for drive in "CDEFGH":
            drive_root = Path(f"{drive}:\\")
            if drive_root.exists():
                for rel in common_rel_paths:
                    candidates.append(drive_root / rel)

    exclude_norm = str(exclude_dir.resolve()).lower()
    found: list[Path] = []
    seen: set[str] = set()
    for path in candidates:
        try:
            resolved = path.resolve()
        except OSError:
            continue
        norm_key = str(resolved).lower()
        if norm_key in seen or norm_key == exclude_norm:
            continue
        seen.add(norm_key)
        if is_valid_ra2_dir(resolved):
            found.append(resolved)
    return found


def scan_assets(
    game_dir: Path,
    output_dir: Path,
    include_music: bool,
    include_movies: bool,
    max_depth: int = 2,
) -> list[tuple[Path, str]]:
    results: list[tuple[Path, str]] = []
    output_resolved = output_dir.resolve()

    def walk(current_dir: Path, depth: int) -> None:
        try:
            entries = sorted(current_dir.iterdir(), key=lambda p: p.name.lower())
        except OSError:
            return

        for entry in entries:
            if entry.is_symlink():
                continue
            if entry.resolve() == output_resolved:
                continue
            if entry.is_dir():
                if depth < max_depth and entry.name.lower() not in SKIP_SUBDIRS:
                    walk(entry, depth + 1)
                continue
            if not entry.is_file():
                continue

            name_lower = entry.name.lower()
            ext_lower = entry.suffix.lower()

            if ext_lower == ".mix":
                if not include_movies and matches_any(name_lower, MOVIE_MIX_PATTERNS):
                    continue
                if not include_music and matches_any(name_lower, MUSIC_MIX_PATTERNS):
                    continue
                results.append((entry, classify_mix(name_lower)))
            elif ext_lower == ".ini":
                if name_lower in ALLOWED_LOOSE_INI_NAMES:
                    results.append((entry, "loose_ini"))
            elif ext_lower in ALLOWED_LOOSE_EXTENSIONS:
                results.append((entry, ALLOWED_LOOSE_EXTENSIONS[ext_lower]))
            elif include_movies and ext_lower == ".bik":
                results.append((entry, "movie_bik"))

    walk(game_dir, 0)
    return results


def compute_sha256(file_path: Path) -> str:
    digest = hashlib.sha256()
    with file_path.open("rb") as f:
        while chunk := f.read(4 * 1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def copy_with_sha256(src: Path, dst: Path, overwrite: bool) -> tuple[str, bool]:
    dst.parent.mkdir(parents=True, exist_ok=True)
    if not overwrite and dst.is_file():
        src_stat = src.stat()
        dst_stat = dst.stat()
        if src_stat.st_size == dst_stat.st_size and abs(src_stat.st_mtime - dst_stat.st_mtime) < 1e-3:
            return compute_sha256(dst), False

    digest = hashlib.sha256()
    with src.open("rb") as f_in, dst.open("wb") as f_out:
        while chunk := f_in.read(4 * 1024 * 1024):
            digest.update(chunk)
            f_out.write(chunk)
    shutil.copystat(src, dst)
    return digest.hexdigest(), True


def format_size(num_bytes: int) -> str:
    if num_bytes >= 1024 * 1024 * 1024:
        return f"{num_bytes / (1024 ** 3):.2f} GB"
    if num_bytes >= 1024 * 1024:
        return f"{num_bytes / (1024 ** 2):.2f} MB"
    if num_bytes >= 1024:
        return f"{num_bytes / 1024:.1f} KB"
    return f"{num_bytes} B"


def resolve_nested_ra2_dir(path: Path, exclude_dir: Path) -> Path | None:
    exclude_norm = str(exclude_dir.resolve()).lower()
    if str(path.resolve()).lower() != exclude_norm and is_valid_ra2_dir(path):
        return path
    try:
        sub_matches = [
            sub.resolve()
            for sub in sorted(path.iterdir(), key=lambda p: p.name.lower())
            if sub.is_dir()
            and str(sub.resolve()).lower() != exclude_norm
            and is_valid_ra2_dir(sub)
        ]
    except OSError:
        return None
    return sub_matches[0] if len(sub_matches) == 1 else None


def resolve_game_dir_interactive(arg_game_dir: str | None, output_dir: Path, force: bool) -> Path:
    if arg_game_dir:
        cleaned = arg_game_dir.strip().strip('"').strip("'")
        game_dir = Path(cleaned).expanduser().resolve()
        if not game_dir.is_dir():
            raise SystemExit(f"[错误] 目录不存在: {game_dir}")
        nested = resolve_nested_ra2_dir(game_dir, output_dir)
        if nested is not None:
            if nested != game_dir:
                print(f"[自动定位子目录] {nested}")
            return nested
        if not force:
            raise SystemExit(
                f"[错误] 在目录中未找到 ra2.mix，请确认这是红警2安装目录（或加 --force 强制收集）: {game_dir}"
            )
        return game_dir

    detected = detect_game_dirs(output_dir)
    if len(detected) == 1:
        print(f"[自动识别] 找到《红色警戒2》目录: {detected[0]}")
        return detected[0]

    if len(detected) > 1:
        print("检测到多个《红色警戒2》安装目录：")
        for idx, d in enumerate(detected, start=1):
            print(f"  [{idx}] {d}")
        if sys.stdin.isatty():
            choice = input("请输入序号 [默认 1]: ").strip()
            selected_idx = int(choice) - 1 if choice.isdigit() and 1 <= int(choice) <= len(detected) else 0
            return detected[selected_idx]
        return detected[0]

    if not sys.stdin.isatty():
        raise SystemExit("[错误] 未自动检测到游戏目录，请通过 --game-dir 指定路径。")

    print("未自动检测到《红色警戒2》安装目录。")
    raw_input_path = input("请输入或直接拖入游戏安装文件夹路径后回车: ").strip().strip('"').strip("'")
    if not raw_input_path:
        raise SystemExit("[已取消] 未输入路径。")
    game_dir = Path(raw_input_path).expanduser().resolve()
    if not game_dir.is_dir():
        raise SystemExit(f"[错误] 目录不存在: {game_dir}")
    nested = resolve_nested_ra2_dir(game_dir, output_dir)
    if nested is not None:
        if nested != game_dir:
            print(f"[自动定位子目录] {nested}")
        return nested
    if not force:
        raise SystemExit(f"[错误] 在 {game_dir} 中未找到 ra2.mix，请确认路径正确。")
    return game_dir


def collect_to_directory(
    game_dir: Path,
    output_dir: Path,
    include_music: bool,
    include_movies: bool,
    overwrite: bool,
    force: bool,
) -> None:
    if game_dir.resolve() == output_dir.resolve():
        raise SystemExit(f"[错误] 输出目录不能与游戏安装源目录相同: {output_dir}")

    scanned = scan_assets(
        game_dir=game_dir,
        output_dir=output_dir,
        include_music=include_music,
        include_movies=include_movies,
    )
    if not scanned:
        raise SystemExit(f"[错误] 在 {game_dir} 中未扫描到任何可用的游戏资源文件。")

    lower_rel_names = {p.relative_to(game_dir).as_posix().lower() for p, _ in scanned}
    has_base_ra2 = "ra2.mix" in lower_rel_names
    has_language = "language.mix" in lower_rel_names
    has_yuri = "ra2md.mix" in lower_rel_names

    if not has_base_ra2 and not force:
        raise SystemExit("[错误] 缺少核心文件 ra2.mix，终止收集（如需强制继续请加 --force）。")
    if not has_language:
        print("[提示] 未发现 language.mix，语音和部分默认字库可能位于外置文件或其他目录。")

    total_raw_bytes = sum(p.stat().st_size for p, _ in scanned)
    edition_label = "红色警戒2 + 尤里的复仇" if has_yuri else "红色警戒2 原版"
    print(f"版本识别 : {edition_label}")
    print(f"待收集数 : {len(scanned)} 个文件（总大小 {format_size(total_raw_bytes)}）")
    print(f"目标目录 : {output_dir}")
    print("-" * 60)

    output_dir.mkdir(parents=True, exist_ok=True)
    manifest_files: list[CollectedFile] = []
    copied_count = 0
    skipped_count = 0

    for index, (file_path, category) in enumerate(scanned, start=1):
        rel_path = file_path.relative_to(game_dir)
        rel_posix = rel_path.as_posix()
        size_bytes = file_path.stat().st_size
        dst_path = output_dir / rel_path

        sha256_hex, did_copy = copy_with_sha256(file_path, dst_path, overwrite=overwrite)
        action_label = "复制" if did_copy else "跳过(未变)"
        if did_copy:
            copied_count += 1
        else:
            skipped_count += 1

        print(f"[{index}/{len(scanned)}] {action_label} {rel_posix} ({format_size(size_bytes)}) [{category}]")
        manifest_files.append(
            CollectedFile(
                relative_path=rel_posix,
                category=category,
                size_bytes=size_bytes,
                sha256=sha256_hex,
            )
        )

    manifest = {
        "schema_version": 1,
        "created_at_utc": datetime.now(timezone.utc).isoformat(),
        "detected_edition": "yuris_revenge" if has_yuri else "red_alert_2",
        "has_base_ra2": has_base_ra2,
        "has_language_mix": has_language,
        "has_yuris_revenge": has_yuri,
        "included_music": include_music,
        "included_movies": include_movies,
        "total_files": len(manifest_files),
        "total_raw_bytes": total_raw_bytes,
        "files": [asdict(item) for item in manifest_files],
    }
    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    print("-" * 60)
    print(f"[完成] 资源已收集至目录: {output_dir}")
    print(f"       文件统计: 共 {len(manifest_files)} 个（新复制 {copied_count} 个，未变化跳过 {skipped_count} 个），总计 {format_size(total_raw_bytes)}")
    print(f"       校验清单: {manifest_path}")


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="收集《红色警戒2》/《尤里的复仇》移植所需的资源文件到项目指定目录。"
    )
    parser.add_argument(
        "-g",
        "--game-dir",
        help="红警2安装目录路径（不传则在 Windows 下自动扫描 Steam / EA / 注册表 / 当前目录）",
    )
    parser.add_argument(
        "-o",
        "--output-dir",
        default=str(DEFAULT_OUTPUT_DIR),
        help=f"项目内输出目录路径（默认: {DEFAULT_OUTPUT_DIR}）",
    )
    parser.add_argument(
        "--no-music",
        action="store_true",
        help="不包含背景音乐包（theme.mix / thememd.mix），可减小约 150MB 体积",
    )
    parser.add_argument(
        "--include-movies",
        action="store_true",
        help="包含过场真人电影包（movies*.mix / mov*.mix / *.bik，默认跳过以节省约 1GB 体积）",
    )
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="强制覆盖目标目录中已存在的同名文件（默认对大小与修改时间一致的文件自动跳过）",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="即使未检测到 ra2.mix 也强制收集匹配到的资源文件",
    )
    return parser.parse_args(argv)


def main() -> None:
    args = parse_args()
    is_double_click_win = sys.platform == "win32" and len(sys.argv) == 1 and sys.stdin.isatty()
    try:
        cleaned_out = args.output_dir.strip().strip('"').strip("'")
        output_dir = Path(cleaned_out).expanduser().resolve()
        game_dir = resolve_game_dir_interactive(args.game_dir, output_dir, args.force)
        collect_to_directory(
            game_dir=game_dir,
            output_dir=output_dir,
            include_music=not args.no_music,
            include_movies=args.include_movies,
            overwrite=args.overwrite,
            force=args.force,
        )
    except SystemExit as exc:
        if exc.code:
            print(exc, file=sys.stderr)
        if is_double_click_win:
            input("\n按回车键退出...")
        raise
    except Exception as exc:
        print(f"[异常] {exc}", file=sys.stderr)
        if is_double_click_win:
            input("\n按回车键退出...")
        raise

    if is_double_click_win:
        input("\n按回车键退出...")


if __name__ == "__main__":
    main()
