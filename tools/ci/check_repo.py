#!/usr/bin/env python3
"""Repository rules that the compiler cannot enforce. See docs/architecture.md §2.3 and §3."""
import json
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]

FORBIDDEN_EXTENSIONS = {
    ".mix", ".shp", ".vxl", ".hva", ".tmp", ".pal", ".csf", ".aud", ".bag", ".idx", ".bik",
}

ENGINE_PATTERNS = [
    (re.compile(r"\bUnity(Engine|Editor)\b"), "Unity API in core/"),
]
SIM_PATTERNS = [
    (re.compile(r"\bSystem\.Random\b|\bnew\s+Random\s*\("), "System.Random (use SimRandom)"),
    (re.compile(r"\bDateTime\.(Utc)?Now\b|\bStopwatch\b"), "wall-clock time in simulation code"),
]


def tracked_files():
    out = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard"],
        cwd=ROOT, check=True, capture_output=True, text=True,
    ).stdout
    return [line for line in out.splitlines() if line]


def main():
    errors = []

    for path in tracked_files():
        if pathlib.PurePosixPath(path).suffix.lower() in FORBIDDEN_EXTENSIONS:
            errors.append(f"{path}: original-game file types must not be in the repository")

    for asmdef in sorted((ROOT / "core").glob("*/*.asmdef")):
        name = asmdef.parent.name
        rel = asmdef.relative_to(ROOT)
        data = json.loads(asmdef.read_text(encoding="utf-8"))
        if data.get("name") != name or asmdef.stem != name:
            errors.append(f"{rel}: asmdef name must match its directory ({name})")
        if data.get("noEngineReferences") is not True:
            errors.append(f"{rel}: noEngineReferences must be true")
        if not (ROOT / "dotnet" / name / f"{name}.csproj").is_file():
            errors.append(f"{rel}: missing mirror project dotnet/{name}/{name}.csproj")

    for source in sorted((ROOT / "core").rglob("*.cs")):
        rel = source.relative_to(ROOT)
        patterns = list(ENGINE_PATTERNS)
        if rel.parts[1].startswith("Rts.Core."):
            patterns += SIM_PATTERNS
        for number, line in enumerate(source.read_text(encoding="utf-8").splitlines(), 1):
            code = line.split("//", 1)[0]
            for pattern, message in patterns:
                if pattern.search(code):
                    errors.append(f"{rel}:{number}: {message}")

    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 1
    print("repository checks passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
