"""
tex_gen.py - Vectorized NumPy procedural PBR Albedo + Tangent-Space Normal Map generator.
Synthesizes authentic Westwood-style industrial textures (corrugated steel, riveted armor,
weathered Soviet brick masonry, concrete pavers, hazard stripes, shutter doors, tank treads,
Soviet Hammer & Sickle blast gate, and Allied Eagle crest) into 512x512 PNGs.
"""

from __future__ import annotations
from pathlib import Path
import struct
import zlib
import numpy as np


def _png_chunk(tag: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(tag)
    crc = zlib.crc32(data, crc) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)


def save_png_rgba(path: Path, rgb_or_rgba: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    arr = np.clip(rgb_or_rgba, 0.0, 1.0)
    h, w, c = arr.shape
    if c == 3:
        alpha = np.ones((h, w, 1), dtype=np.float32)
        arr = np.concatenate([arr, alpha], axis=2)
    u8 = (arr * 255.0 + 0.5).astype(np.uint8)
    # Prepend filter byte 0 to each scanline
    scanlines = np.concatenate([np.zeros((h, 1), dtype=np.uint8), u8.reshape(h, w * 4)], axis=1)
    raw_bytes = scanlines.tobytes()

    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    compressed = zlib.compress(raw_bytes, level=6)
    png_data = (
        b"\x89PNG\r\n\x1a\n"
        + _png_chunk(b"IHDR", ihdr)
        + _png_chunk(b"IDAT", compressed)
        + _png_chunk(b"IEND", b"")
    )
    path.write_bytes(png_data)


def height_to_normal_png(path: Path, height: np.ndarray, strength: float = 3.0) -> None:
    """Computes OpenGL/Unity tangent-space normal map from a [0..1] heightfield with seamless wrap."""
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5 * strength
    dy = (np.roll(height, 1, axis=0) - np.roll(height, -1, axis=0)) * 0.5 * strength
    nx = -dx
    ny = -dy
    nz = np.ones_like(height)
    inv_len = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz + 1e-8)
    n_rgb = np.stack([nx * inv_len * 0.5 + 0.5, ny * inv_len * 0.5 + 0.5, nz * inv_len * 0.5 + 0.5], axis=-1)
    save_png_rgba(path, n_rgb)


def _fbm_noise(size: int, seed: int) -> np.ndarray:
    rng = np.random.default_rng(seed)
    acc = np.zeros((size, size), dtype=np.float32)
    amp = 0.5
    for freq in (8, 16, 32, 64, 128):
        small = rng.random((freq, freq), dtype=np.float32)
        rep = size // freq
        # Bilinear smooth upsample viaseparable roll blend
        up = np.repeat(np.repeat(small, rep, axis=0), rep, axis=1)
        up = 0.25 * (up + np.roll(up, rep // 2, 0) + np.roll(up, rep // 2, 1) + np.roll(np.roll(up, rep // 2, 0), rep // 2, 1))
        acc += amp * up
        amp *= 0.5
    return (acc - acc.min()) / (acc.max() - acc.min() + 1e-6)


def _rivet_grid(u: np.ndarray, v: np.ndarray, tiles_x: int, tiles_y: int, radius: float = 0.032) -> np.ndarray:
    fx = (u * tiles_x) % 1.0
    fy = (v * tiles_y) % 1.0
    rivets = np.zeros_like(u)
    for cx in (0.07, 0.93):
        for cy in (0.07, 0.50, 0.93):
            d2 = (fx - cx) ** 2 + (fy - cy) ** 2
            dome = np.clip(1.0 - d2 / (radius * radius), 0.0, 1.0)
            rivets = np.maximum(rivets, np.sqrt(dome))
    return rivets


def generate_all_textures(out_dir: Path, size: int = 512) -> dict[str, tuple[Path, Path | None]]:
    out_dir.mkdir(parents=True, exist_ok=True)
    lin = np.linspace(0.0, 1.0, size, endpoint=False, dtype=np.float32)
    u, v = np.meshgrid(lin, lin)
    noise_a = _fbm_noise(size, 42)
    noise_b = _fbm_noise(size, 137)

    result: dict[str, tuple[Path, Path | None]] = {}

    # 1. Riveted Brushed Silver Steel Plates (Allied hangars, silos, gun barrels)
    fx = (u * 4.0) % 1.0
    fy = (v * 4.0) % 1.0
    seam = ((fx < 0.028) | (fx > 0.972) | (fy < 0.028) | (fy > 0.972)).astype(np.float32)
    rivets = _rivet_grid(u, v, 4, 4, 0.030)
    brushed = 0.78 + 0.12 * noise_a + 0.04 * np.sin(v * np.pi * 90.0 + noise_b * 2.0)
    h_steel = np.clip(0.5 - 0.35 * seam + 0.28 * rivets + 0.06 * noise_a, 0.0, 1.0)
    c_steel = brushed * (1.0 - 0.32 * seam) + 0.10 * rivets
    rgb_steel = np.stack([c_steel * 0.95, c_steel * 0.98, c_steel * 1.03], axis=-1)
    p_a, p_n = out_dir / "tex_steel_plates.png", out_dir / "tex_steel_plates_n.png"
    save_png_rgba(p_a, rgb_steel)
    height_to_normal_png(p_n, h_steel, 3.8)
    result["Mat_BrushedSteel"] = (p_a, p_n)

    # 2. Corrugated Quonset Steel Roofing (GACNST, GAPILE, GAWEAP, GAREFN arches!)
    wave = 0.5 + 0.5 * np.sin(v * 2.0 * np.pi * 28.0)
    lap_seam = (((u * 6.0) % 1.0) < 0.035).astype(np.float32)
    h_corr = np.clip(0.65 * wave - 0.25 * lap_seam + 0.05 * noise_b, 0.0, 1.0)
    shade_corr = (0.72 + 0.22 * wave + 0.07 * noise_a) * (1.0 - 0.28 * lap_seam)
    rgb_corr = np.stack([shade_corr * 0.95, shade_corr * 0.98, shade_corr * 1.04], axis=-1)
    p_ca, p_cn = out_dir / "tex_corrugated.png", out_dir / "tex_corrugated_n.png"
    save_png_rgba(p_ca, rgb_corr)
    height_to_normal_png(p_cn, h_corr, 4.5)
    result["Mat_CorrugatedSteel"] = (p_ca, p_cn)

    # 3. Grayscale Team-Tinted Military Armor Plate (multiplied by Allied Blue / Soviet Red!)
    fx_t = (u * 3.0) % 1.0
    fy_t = (v * 3.0) % 1.0
    seam_t = ((fx_t < 0.032) | (fx_t > 0.968) | (fy_t < 0.032) | (fy_t > 0.968)).astype(np.float32)
    bevel_hi = (((fx_t > 0.032) & (fx_t < 0.065)) | ((fy_t > 0.032) & (fy_t < 0.065))).astype(np.float32)
    riv_t = _rivet_grid(u, v, 3, 3, 0.034)
    h_team = np.clip(0.55 - 0.40 * seam_t + 0.30 * riv_t + 0.05 * noise_a, 0.0, 1.0)
    val_team = np.clip((0.86 + 0.10 * noise_a + 0.09 * bevel_hi + 0.08 * riv_t) * (1.0 - 0.35 * seam_t), 0.25, 1.0)
    rgb_team = np.stack([val_team, val_team, val_team], axis=-1)
    p_ta, p_tn = out_dir / "tex_team_armor.png", out_dir / "tex_team_armor_n.png"
    save_png_rgba(p_ta, rgb_team)
    height_to_normal_png(p_tn, h_team, 3.6)
    result["Mat_TeamPaint"] = (p_ta, p_tn)

    # 4. Dark Gunmetal Mechanical & Slotted Louver Housing
    louver = 0.5 + 0.5 * np.sin(v * 2.0 * np.pi * 36.0)
    in_vent = ((fx > 0.18) & (fx < 0.82) & (fy > 0.22) & (fy < 0.78)).astype(np.float32)
    h_dark = np.clip(0.5 - 0.30 * seam - 0.35 * in_vent * (1.0 - louver) + 0.22 * rivets, 0.0, 1.0)
    val_dark = (0.30 + 0.09 * noise_b) * (1.0 - 0.30 * seam) * (1.0 - 0.48 * in_vent * (1.0 - louver)) + 0.05 * rivets
    rgb_dark = np.stack([val_dark * 0.96, val_dark * 0.99, val_dark * 1.06], axis=-1)
    p_da, p_dn = out_dir / "tex_dark_mech.png", out_dir / "tex_dark_mech_n.png"
    save_png_rgba(p_da, rgb_dark)
    height_to_normal_png(p_dn, h_dark, 4.0)
    result["Mat_DarkSteel"] = (p_da, p_dn)

    # 5. Industrial Tarmac / Concrete Paving Slabs
    cx_f = (u * 6.0) % 1.0
    cy_f = (v * 6.0) % 1.0
    joint = ((cx_f < 0.035) | (cy_f < 0.035)).astype(np.float32)
    h_conc = np.clip(0.6 - 0.42 * joint + 0.14 * (noise_a - 0.5) + 0.08 * (noise_b - 0.5), 0.0, 1.0)
    val_conc = (0.52 + 0.14 * noise_a - 0.06 * noise_b) * (1.0 - 0.36 * joint)
    rgb_conc = np.stack([val_conc * 1.01, val_conc * 1.00, val_conc * 0.95], axis=-1)
    p_coa, p_con = out_dir / "tex_concrete.png", out_dir / "tex_concrete_n.png"
    save_png_rgba(p_coa, rgb_conc)
    height_to_normal_png(p_con, h_conc, 3.2)
    result["Mat_Concrete"] = (p_coa, p_con)

    # 6. Weathered Soviet Masonry Brick & Concrete Ribbing (NACNST, NAPOWR, NAHAND, NAWEAP)
    rows = v * 20.0
    row_idx = np.floor(rows)
    by = rows % 1.0
    bx = (u * 10.0 + (row_idx % 2.0) * 0.5) % 1.0
    mortar = ((by < 0.11) | (bx < 0.055)).astype(np.float32)
    brick_var = np.sin(row_idx * 12.9898 + np.floor(u * 10.0 + (row_idx % 2.0) * 0.5) * 78.233) * 0.5 + 0.5
    concrete_band = (((v * 3.0) % 1.0) < 0.14).astype(np.float32)
    h_brick = np.clip((1.0 - 0.55 * mortar) * (1.0 - concrete_band) + 0.72 * concrete_band + 0.08 * noise_a, 0.0, 1.0)
    r_b = (0.54 + 0.12 * brick_var + 0.08 * noise_a) * (1.0 - 0.35 * mortar)
    g_b = (0.37 + 0.08 * brick_var + 0.07 * noise_a) * (1.0 - 0.25 * mortar)
    b_b = (0.29 + 0.06 * brick_var + 0.06 * noise_a) * (1.0 - 0.20 * mortar)
    # Blend light weathered concrete reinforcement bands just like RA2 Soviet bunkers!
    r_b = r_b * (1.0 - concrete_band) + (0.64 + 0.08 * noise_b) * concrete_band
    g_b = g_b * (1.0 - concrete_band) + (0.62 + 0.08 * noise_b) * concrete_band
    b_b = b_b * (1.0 - concrete_band) + (0.58 + 0.08 * noise_b) * concrete_band
    p_ba, p_bn = out_dir / "tex_soviet_brick.png", out_dir / "tex_soviet_brick_n.png"
    save_png_rgba(p_ba, np.stack([r_b, g_b, b_b], axis=-1))
    height_to_normal_png(p_bn, h_brick, 4.2)
    result["Mat_SovietBrick"] = (p_ba, p_bn)

    # 7. Fluted Hammered Gold / Brass (Russian Onion Domes, Chrono Miner Drum, Power Plant Caps)
    flute = 0.5 + 0.5 * np.cos(u * 2.0 * np.pi * 24.0)
    h_gold = np.clip(0.55 + 0.35 * flute + 0.06 * noise_a, 0.0, 1.0)
    g_val = 0.78 + 0.18 * flute + 0.06 * noise_b
    rgb_gold = np.stack([g_val * 1.02, g_val * 0.80, g_val * 0.28], axis=-1)
    p_ga, p_gn = out_dir / "tex_gold_fluted.png", out_dir / "tex_gold_fluted_n.png"
    save_png_rgba(p_ga, rgb_gold)
    height_to_normal_png(p_gn, h_gold, 3.5)
    result["Mat_GoldBrass"] = (p_ga, p_gn)

    # 8. 45-Degree Yellow/Black Industrial Hazard Stripes
    stripe = (((u + v) * 10.0) % 1.0 < 0.52).astype(np.float32)
    wear = 0.85 + 0.15 * noise_a
    rgb_haz = np.stack(
        [(0.12 + 0.84 * stripe) * wear, (0.12 + 0.64 * stripe) * wear, (0.12 + 0.02 * stripe) * wear],
        axis=-1,
    )
    p_ha = out_dir / "tex_hazard.png"
    save_png_rgba(p_ha, rgb_haz)
    result["Mat_HazardYellow"] = (p_ha, None)

    # 9. Tank Track Cleats & Rubber Pads
    cleat = (((v * 24.0) % 1.0) < 0.62).astype(np.float32)
    pin_col = ((u < 0.12) | (u > 0.88)).astype(np.float32)
    h_tr = np.clip(0.25 + 0.55 * cleat + 0.20 * pin_col, 0.0, 1.0)
    v_tr = 0.12 + 0.14 * cleat + 0.12 * pin_col + 0.04 * noise_a
    p_tra, p_trn = out_dir / "tex_tread.png", out_dir / "tex_tread_n.png"
    save_png_rgba(p_tra, np.stack([v_tr, v_tr * 1.02, v_tr * 1.05], axis=-1))
    height_to_normal_png(p_trn, h_tr, 5.0)
    result["Mat_TreadRubber"] = (p_tra, p_trn)

    # 10. Soviet Blast Gate with Authentic Crimson Hammer & Sickle Emblem!
    slats = 0.5 + 0.5 * np.sin(v * 2.0 * np.pi * 22.0)
    gate_bg = 0.14 + 0.07 * slats + 0.04 * noise_b
    # Analytic Hammer & Sickle centered at (0.5, 0.5) (note: v=0 top in image, flip y for upright)
    x_c = (u - 0.5) * 2.4
    y_c = (0.5 - v) * 2.4
    r_c = np.sqrt((x_c - 0.04) ** 2 + (y_c - 0.04) ** 2)
    r_inner = np.sqrt((x_c + 0.07) ** 2 + (y_c - 0.08) ** 2)
    sickle = (r_c < 0.56) & (r_inner > 0.42) & (x_c + y_c > -0.32)
    # Diagonal handle from bottom-left (-0.42, -0.45) to top-right (0.28, 0.25) ( hammer ) + sickle grip
    rot_p = (x_c + y_c) * 0.7071
    rot_q = (y_c - x_c) * 0.7071
    hammer_shaft = (np.abs(rot_q) < 0.065) & (rot_p > -0.58) & (rot_p < 0.34)
    hammer_head = (np.abs(rot_p - 0.30) < 0.09) & (np.abs(rot_q - 0.05) < 0.22)
    sickle_grip = (np.abs(rot_p + 0.42) < 0.14) & (np.abs(rot_q + 0.38) < 0.065)
    emblem = (sickle | hammer_shaft | hammer_head | sickle_grip).astype(np.float32)
    rgb_sgate = np.stack(
        [gate_bg * (1.0 - emblem) + 0.88 * emblem, gate_bg * (1.0 - emblem) + 0.12 * emblem, gate_bg * (1.0 - emblem) + 0.10 * emblem],
        axis=-1,
    )
    p_sg = out_dir / "tex_soviet_gate.png"
    save_png_rgba(p_sg, rgb_sgate)
    result["Mat_SovietGate"] = (p_sg, None)

    # 11. Allied Eagle Crest Shield (White/Silver border + Royal Blue field + Silver Swept Eagle Wings)
    ax = (u - 0.5) * 2.2
    ay = (0.5 - v) * 2.2
    border = ((np.abs(ax) > 0.84) | (np.abs(ay) > 0.84)).astype(np.float32)
    wing = ((np.abs(ay - 0.18 * np.abs(ax) + 0.05) < 0.22 - 0.16 * np.abs(ax)) & (np.abs(ax) < 0.78)).astype(np.float32)
    star_core = ((np.abs(ax) < 0.12) & (np.abs(ay) < 0.42)).astype(np.float32)
    eagle = np.clip(wing + star_core, 0.0, 1.0) * (1.0 - border)
    r_al = 0.14 * (1.0 - border - eagle) + 0.90 * (border + eagle)
    g_al = 0.42 * (1.0 - border - eagle) + 0.93 * (border + eagle)
    b_al = 0.88 * (1.0 - border - eagle) + 0.98 * (border + eagle)
    p_ac = out_dir / "tex_allied_crest.png"
    save_png_rgba(p_ac, np.stack([r_al, g_al, b_al], axis=-1))
    result["Mat_AlliedCrest"] = (p_ac, None)

    # 12. Roll-up Garage Shutter Door
    sh_wave = 0.5 + 0.5 * np.sin(v * 2.0 * np.pi * 28.0)
    sh_frame = ((u < 0.06) | (u > 0.94)).astype(np.float32)
    h_sh = np.clip(0.5 * sh_wave * (1.0 - sh_frame) + 0.8 * sh_frame, 0.0, 1.0)
    v_sh = (0.18 + 0.14 * sh_wave) * (1.0 - sh_frame) + 0.55 * sh_frame
    p_sha, p_shn = out_dir / "tex_shutter_door.png", out_dir / "tex_shutter_door_n.png"
    save_png_rgba(p_sha, np.stack([v_sh, v_sh * 1.02, v_sh * 1.06], axis=-1))
    height_to_normal_png(p_shn, h_sh, 4.5)
    result["Mat_ShutterDoor"] = (p_sha, p_shn)

    return result
