using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ReCnc.Core.Data;

namespace Ra2Converter.Formats;

public sealed class ExtractedRa2MapResult
{
    public required MapBundleData Bundle { get; init; }
    public Rgba32[]? BakedTerrainPixels { get; init; }
    public int BakedTerrainW { get; init; }
    public int BakedTerrainH { get; init; }
    public Rgba32[]? PreviewPixels { get; init; }
    public int PreviewW { get; init; }
    public int PreviewH { get; init; }
}

public static class Ra2MapExtractor
{
    private readonly record struct RawIsoTile(short Rx, short Ry, int TileIndex, byte SubTile, byte Level);

    public static ExtractedRa2MapResult? TryExtractFullMap(
        string mapId,
        string displayName,
        byte[] mapIniBytes,
        Dictionary<string, TheaterTilesetAtlas> theaterAtlases,
        IniDocument? rulesIni = null,
        IniDocument? artIni = null,
        Dictionary<string, string>? csf = null)
    {
        var ini = IniDocument.Parse(mapIniBytes);
        var mapSec = ini.GetSection("Map");
        if (mapSec == null) return null;

        string theater = (mapSec.GetString("Theater", "TEMPERATE") ?? "TEMPERATE").Trim().ToUpperInvariant();
        var sizeParts = mapSec.GetCsvList("Size");
        var localParts = mapSec.GetCsvList("LocalSize");
        if (sizeParts.Count < 4 || localParts.Count < 4) return null;

        int fullW = Math.Max(16, int.TryParse(sizeParts[2], out int fw) ? fw : 64);
        int fullH = Math.Max(16, int.TryParse(sizeParts[3], out int fh) ? fh : 64);
        int localLeft = Math.Max(0, int.TryParse(localParts[0], out int ll) ? ll : 2);
        int localTop = Math.Max(0, int.TryParse(localParts[1], out int lt) ? lt : 4);
        int localW = Math.Clamp(int.TryParse(localParts[2], out int lw) ? lw : 64, 16, 220);
        int localH = Math.Clamp(int.TryParse(localParts[3], out int lh) ? lh : 64, 16, 220);

        theaterAtlases.TryGetValue(theater, out var atlas);
        atlas ??= theaterAtlases.GetValueOrDefault("TEMPERATE");

        // Playable screen diamond bounds (dx in [dxMinPlay..dxMaxPlay], dy in [dyMinPlay..dyMaxPlay])
        int dxMinPlay = 2 * localLeft;
        int dxMaxPlay = 2 * (localLeft + localW) - 1;
        int dyMinPlay = 2 * localTop;
        int dyMaxPlay = 2 * (localTop + localH) + 3;

        // Margin for visual border around playable area
        const int margin = 4;
        var rawTiles = new List<RawIsoTile>(16384);
        int minRx = int.MaxValue, maxRx = int.MinValue;
        int minRy = int.MaxValue, maxRy = int.MinValue;

        var isoSec = ini.GetSection("IsoMapPack5");
        if (isoSec != null && isoSec.OrderedEntries.Count > 0)
        {
            byte[] unpacked = UnpackChunks(DecodeBase64Section(isoSec), useFormat80: false);
            int recCount = unpacked.Length / 11;
            for (int i = 0; i < recCount; i++)
            {
                int off = i * 11;
                short rx = BinaryPrimitives.ReadInt16LittleEndian(unpacked.AsSpan(off, 2));
                short ry = BinaryPrimitives.ReadInt16LittleEndian(unpacked.AsSpan(off + 2, 2));
                if (rx == 0 && ry == 0) continue;

                int tileIdx = BinaryPrimitives.ReadInt32LittleEndian(unpacked.AsSpan(off + 4, 4));
                byte subTile = unpacked[off + 8];
                byte level = unpacked[off + 9];

                int dx = rx - ry + fullW - 1;
                int dy = rx + ry - fullW - 1 - level;
                if (dx < dxMinPlay - margin || dx > dxMaxPlay + margin || dy < dyMinPlay - margin || dy > dyMaxPlay + margin + 8)
                    continue;

                rawTiles.Add(new RawIsoTile(rx, ry, tileIdx, subTile, level));
                if (dx >= dxMinPlay && dx <= dxMaxPlay && dy >= dyMinPlay && dy <= dyMaxPlay)
                {
                    if (rx < minRx) minRx = rx;
                    if (rx > maxRx) maxRx = rx;
                    if (ry < minRy) minRy = ry;
                    if (ry > maxRy) maxRy = ry;
                }
            }
        }

        if (minRx > maxRx || minRy > maxRy)
        {
            minRx = 1; maxRx = 64; minRy = 1; maxRy = 64;
        }

        int gridW = maxRx - minRx + 1;
        int gridH = maxRy - minRy + 1;
        var grid = new MapCellData[gridW, gridH];

        for (int gy = 0; gy < gridH; gy++)
        {
            for (int gx = 0; gx < gridW; gx++)
            {
                int rx = gx + minRx;
                int ry = maxRy - gy;
                int dx = rx - ry + fullW - 1;
                int dy = rx + ry - fullW - 1;
                bool inPlayable = dx >= dxMinPlay && dx <= dxMaxPlay && dy >= dyMinPlay - 2 && dy <= dyMaxPlay + 6;

                grid[gx, gy] = new MapCellData
                {
                    X = gx,
                    Y = gy,
                    HeightLevel = 0,
                    RampType = 0,
                    TerrainType = inPlayable ? 0 : 6 // 6 = OutOfBounds outside playable rectangle!
                };
            }
        }

        bool RxRyToGrid(int rx, int ry, out int gx, out int gy)
        {
            gx = rx - minRx;
            gy = maxRy - ry;
            return gx >= 0 && gx < gridW && gy >= 0 && gy < gridH;
        }

        // Populate exact 1:1 cells from [IsoMapPack5] + .TMP sub-tile LandType & RampType!
        foreach (var t in rawTiles)
        {
            if (!RxRyToGrid(t.Rx, t.Ry, out int gx, out int gy)) continue;

            var cell = grid[gx, gy];
            int dx = t.Rx - t.Ry + fullW - 1;
            int dy = t.Rx + t.Ry - fullW - 1 - t.Level;
            bool inPlayable = dx >= dxMinPlay && dx <= dxMaxPlay && dy >= dyMinPlay && dy <= dyMaxPlay;

            cell.HeightLevel = t.Level;
            if (!inPlayable)
            {
                cell.TerrainType = 6; // OutOfBounds outside LocalSize!
                if (atlas != null && atlas.GetSubTile(t.TileIndex, t.SubTile) is { } borderSub)
                    cell.RampType = borderSub.RampType;
                continue;
            }

            if (atlas != null && atlas.GetSubTile(t.TileIndex, t.SubTile) is { } sub)
            {
                cell.RampType = sub.RampType;
                cell.TerrainType = MapWestwoodLandType(sub.LandType);
                if (atlas.IsBridgeTemplate(t.TileIndex))
                {
                    cell.TerrainType = 1;
                    cell.HasBridge = true;
                }
            }
            else
            {
                cell.TerrainType = 0;
                cell.RampType = 0;
            }
        }

        // Bake 100% Pixel-Exact Original RA2 .TMP Ground & Cliff Texture!
        Rgba32[]? bakedPixels = null;
        int bakedW = 0, bakedH = 0, originPx = 0, originPy = 0;
        if (atlas != null && rawTiles.Count > 0)
        {
            originPx = (dxMinPlay - 2) * 30;
            originPy = (dyMinPlay - 6) * 15;
            bakedW = (localW + 2) * 60;
            bakedH = (localH + 8) * 30;

            // Bake full 60x30/tile .TMP canvas (supports maps up to 8192px, downsampling 2x only if > 4096px)
            if (bakedW <= 12288 && bakedH <= 12288)
            {
                int rawW = bakedW, rawH = bakedH;
                var rawCanvas = new Rgba32[rawW * rawH];
                var bgCol = atlas.GetSubTile(0, 0)?.RadarColorLeft ?? new Rgba32(48, 72, 36, 255);
                Array.Fill(rawCanvas, bgCol);

                var palColors = atlas.Palette.Colors;
                foreach (var t in rawTiles.OrderBy(x => x.Rx + x.Ry).ThenBy(x => x.Level))
                {
                    var sub = atlas.GetSubTile(t.TileIndex, t.SubTile);
                    if (sub == null) continue;

                    int tileLeft = (t.Rx - t.Ry + fullW) * 30 - 30 - originPx;
                    int tileTop = (t.Rx + t.Ry - fullW - 1 - t.Level) * 15 - originPy;

                    BlitIndexed(sub.IsoPixels, 60, 30, tileLeft, tileTop, rawCanvas, rawW, rawH, palColors);

                    if (sub.HasExtra && sub.ExtraPixels != null)
                    {
                        int exLeft = tileLeft + (sub.ExtraX - sub.X);
                        int exTop = tileTop + (sub.ExtraY - sub.Y);
                        BlitIndexed(sub.ExtraPixels, sub.ExtraWidth, sub.ExtraHeight, exLeft, exTop, rawCanvas, rawW, rawH, palColors);
                    }
                }

                if (rawW > 4096 || rawH > 4096)
                {
                    int dsW = rawW / 2, dsH = rawH / 2;
                    var ds = new Rgba32[dsW * dsH];
                    for (int y = 0; y < dsH; y++)
                    {
                        int sy = y * 2 * rawW;
                        for (int x = 0; x < dsW; x++)
                        {
                            ds[y * dsW + x] = rawCanvas[sy + x * 2];
                        }
                    }
                    bakedPixels = ds;
                }
                else
                {
                    bakedPixels = rawCanvas;
                }
            }
        }

        // Snapshot base ground levels before applying high-bridge +4 offsets
        var baseLevels = new int[gridW, gridH];
        for (int gy = 0; gy < gridH; gy++)
            for (int gx = 0; gx < gridW; gx++)
                baseLevels[gx, gy] = grid[gx, gy].HeightLevel;

        // 2. Decode [OverlayPack] & [OverlayDataPack] (Gold Ore, Gems, High/Low Bridges, Walls!)
        var props = new List<MapPropData>();
        var ovSec = ini.GetSection("OverlayPack");
        var ovDataSec = ini.GetSection("OverlayDataPack");
        if (ovSec != null && ovSec.OrderedEntries.Count > 0)
        {
            byte[] ovTypes = UnpackChunks(DecodeBase64Section(ovSec), useFormat80: true);
            byte[] ovData = ovDataSec != null
                ? UnpackChunks(DecodeBase64Section(ovDataSec), useFormat80: true)
                : Array.Empty<byte>();

            int limit = Math.Min(ovTypes.Length, 512 * 512);
            for (int idx = 0; idx < limit; idx++)
            {
                byte ov = ovTypes[idx];
                if (ov == 0xFF) continue;

                int rx = idx % 512;
                int ry = idx / 512;
                if (!RxRyToGrid(rx, ry, out int gx, out int gy)) continue;

                var cell = grid[gx, gy];
                if (cell.TerrainType == 6) continue;

                bool isHighBridge = (ov >= 24 && ov <= 26) || (ov >= 237 && ov <= 238);
                bool isLowBridge = (ov >= 59 && ov <= 60) || (ov >= 74 && ov <= 101) || (ov >= 122 && ov <= 125) || (ov >= 233 && ov <= 236);

                if (isHighBridge || isLowBridge)
                {
                    int deckLevel = isHighBridge ? baseLevels[gx, gy] + 4 : baseLevels[gx, gy];
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int bgx = gx + dx, bgy = gy + dy;
                            if (bgx < 0 || bgx >= gridW || bgy < 0 || bgy >= gridH) continue;
                            var bc = grid[bgx, bgy];
                            if (bc.TerrainType == 6) continue;
                            bc.HasBridge = true;
                            bc.TerrainType = 1; // Road
                            bc.RampType = 0;
                            bc.HeightLevel = Math.Max(bc.HeightLevel, deckLevel);
                        }
                    }
                    continue;
                }

                if (ov >= 27 && ov <= 38)
                {
                    int stage = idx < ovData.Length ? ovData[idx] : 8;
                    cell.OreType = 2; // Gems
                    cell.OreAmount = Math.Clamp(stage + 1, 4, 12);
                }
                else if ((ov >= 102 && ov <= 121) || (ov >= 127 && ov <= 166))
                {
                    int stage = idx < ovData.Length ? ovData[idx] : 6;
                    cell.OreType = 1; // Gold Ore
                    cell.OreAmount = Math.Clamp(stage + 1, 4, 12);
                }
                else if (ov <= 23 || (ov >= 167 && ov <= 179))
                {
                    // Walls / Sandbags / Fences / Rocks
                    props.Add(new MapPropData { TypeId = $"WALL_{ov}", X = gx, Y = gy, Kind = 3 });
                }
            }

            // Open cliff bridgehead abutments where a bridge meets a cliff edge within 2 height levels
            for (int gy = 1; gy < gridH - 1; gy++)
            {
                for (int gx = 1; gx < gridW - 1; gx++)
                {
                    var c = grid[gx, gy];
                    if (c.TerrainType != 3) continue; // Only check Rock/Cliff cells
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var nb = grid[gx + dx, gy + dy];
                            if (nb.HasBridge && Math.Abs(nb.HeightLevel - c.HeightLevel) <= 2)
                            {
                                c.TerrainType = 1;
                                c.HasBridge = true;
                                break;
                            }
                        }
                        if (c.HasBridge) break;
                    }
                }
            }
        }

        // 3. Parse [Terrain] section (Trees TREE01..28, Blossom Trees TIBTRE01..03, Urban Streetlights LT/TRFF!)
        var terrSec = ini.GetSection("Terrain");
        if (terrSec != null)
        {
            foreach (var kv in terrSec.OrderedEntries)
            {
                if (!int.TryParse(kv.Key.Trim(), out int encoded) || encoded <= 0) continue;
                int ry = encoded / 1000;
                int rx = encoded % 1000;
                if (!RxRyToGrid(rx, ry, out int gx, out int gy)) continue;
                if (grid[gx, gy].TerrainType == 6) continue;

                string tid = kv.Value.Split(',')[0].Trim().ToUpperInvariant();
                byte kind = tid.StartsWith("TIBTRE") ? (byte)1
                    : (tid.StartsWith("LT") || tid.StartsWith("TRFF") || tid.StartsWith("SIGN") || tid.StartsWith("POLE")) ? (byte)2
                    : (byte)0;
                props.Add(new MapPropData { TypeId = tid, X = gx, Y = gy, Kind = kind });
            }
        }

        // 4. Parse [Structures] section (Pre-placed Civilian Buildings, Alamo Fort, Tech Oil Derricks, Hospitals!)
        var preStructures = new List<MapStructureData>();
        var strSec = ini.GetSection("Structures");
        if (strSec != null)
        {
            foreach (var kv in strSec.OrderedEntries)
            {
                string[] parts = kv.Value.Split(',');
                if (parts.Length < 5) continue;
                string typeId = parts[1].Trim().ToUpperInvariant();
                if (!int.TryParse(parts[3].Trim(), out int rx) || !int.TryParse(parts[4].Trim(), out int ry))
                    continue;

                var (fwSize, fhSize) = LookupFoundation(typeId, rulesIni, artIni);
                // Note: In RA2, structure top-left is (rx, ry), spanning [rx..rx+fw-1, ry..ry+fh-1].
                // Since gy = maxRy - ry decreases as ry increases, the minimum gy of [ry..ry+fh-1] is maxRy - (ry + fh - 1)!
                int gx = rx - minRx;
                int gyBottom = maxRy - (ry + fhSize - 1);
                if (gx < 0 || gyBottom < 0 || gx + fwSize > gridW || gyBottom + fhSize > gridH) continue;

                int maxHp = rulesIni?.GetSection(typeId)?.GetInt("Strength", 800) ?? 800;
                string uiKey = rulesIni?.GetSection(typeId)?.GetString("UIName", "") ?? "";
                string locName = (!string.IsNullOrEmpty(uiKey) && csf != null && csf.TryGetValue(uiKey, out string? n) && !string.IsNullOrWhiteSpace(n))
                    ? n!
                    : (rulesIni?.GetSection(typeId)?.GetString("Name", typeId) ?? typeId);

                preStructures.Add(new MapStructureData
                {
                    TypeId = typeId,
                    Name = locName,
                    X = gx,
                    Y = gyBottom,
                    FootprintX = fwSize,
                    FootprintY = fhSize,
                    MaxHp = Math.Max(200, maxHp)
                });
            }
        }

        // 5. Extract [Waypoints] (0..7 are official multiplayer starting locations)
        var waypoints = new List<WaypointData>();
        var wpSec = ini.GetSection("Waypoints");
        if (wpSec != null)
        {
            for (int wpIdx = 0; wpIdx < 8; wpIdx++)
            {
                string? rawWp = wpSec.GetString(wpIdx.ToString());
                if (string.IsNullOrWhiteSpace(rawWp) || !int.TryParse(rawWp.Trim(), out int encoded) || encoded <= 0)
                    continue;

                int ry = encoded / 1000;
                int rx = encoded % 1000;
                if (RxRyToGrid(rx, ry, out int gx, out int gy))
                {
                    waypoints.Add(new WaypointData
                    {
                        Index = wpIdx,
                        X = Math.Clamp(gx, 4, gridW - 6),
                        Y = Math.Clamp(gy, 4, gridH - 6)
                    });
                }
            }
        }

        if (waypoints.Count < 2)
        {
            waypoints.Clear();
            waypoints.Add(new WaypointData { Index = 0, X = gridW / 4, Y = gridH / 2 });
            waypoints.Add(new WaypointData { Index = 1, X = (gridW * 3) / 4, Y = gridH / 2 });
        }

        // 6. Decode [Preview] + [PreviewPack] official RGB24 minimap thumbnail
        Rgba32[]? previewRgba = null;
        int prevW = 0, prevH = 0;
        var prevSec = ini.GetSection("Preview");
        var prevPackSec = ini.GetSection("PreviewPack");
        if (prevSec != null && prevPackSec != null && prevPackSec.OrderedEntries.Count > 0)
        {
            var pSize = prevSec.GetCsvList("Size");
            if (pSize.Count >= 4 && int.TryParse(pSize[2], out prevW) && int.TryParse(pSize[3], out prevH) && prevW > 8 && prevH > 8)
            {
                byte[] rgb24 = UnpackChunks(DecodeBase64Section(prevPackSec), useFormat80: false);
                if (rgb24.Length >= prevW * prevH * 3)
                {
                    previewRgba = new Rgba32[prevW * prevH];
                    for (int p = 0; p < prevW * prevH; p++)
                    {
                        previewRgba[p] = new Rgba32(rgb24[p * 3], rgb24[p * 3 + 1], rgb24[p * 3 + 2], 255);
                    }
                }
            }
        }

        var bundle = new MapBundleData
        {
            MapId = mapId,
            DisplayName = displayName,
            Theater = theater,
            IsNativeIsometricGrid = true,
            Width = gridW,
            Height = gridH,
            MinRx = minRx,
            MaxRx = maxRx,
            MinRy = minRy,
            MaxRy = maxRy,
            FullMapWidth = fullW,
            LocalLeft = localLeft,
            LocalTop = localTop,
            LocalWidth = localW,
            LocalHeight = localH,
            BakedTerrainTextureRelPath = bakedPixels != null ? $"terrain_tex/{mapId}_terrain.png" : "",
            BakedTexWidth = bakedW,
            BakedTexHeight = bakedH,
            BakedOriginPx = originPx,
            BakedOriginPy = originPy,
            Waypoints = waypoints,
            Cells = new List<MapCellData>(gridW * gridH),
            Props = props,
            PreplacedStructures = preStructures
        };

        for (int gy = 0; gy < gridH; gy++)
        {
            for (int gx = 0; gx < gridW; gx++)
            {
                bundle.Cells.Add(grid[gx, gy]);
            }
        }

        return new ExtractedRa2MapResult
        {
            Bundle = bundle,
            BakedTerrainPixels = bakedPixels,
            BakedTerrainW = bakedW,
            BakedTerrainH = bakedH,
            PreviewPixels = previewRgba,
            PreviewW = prevW,
            PreviewH = prevH
        };
    }

    private static int MapWestwoodLandType(byte landType)
    {
        return landType switch
        {
            0 or 1 or 2 or 3 or 4 or 13 => 0, // Clear / Ice (Passable)
            5 or 6 or 11 or 12 => 1,          // Road / Tunnel / Rail
            9 => 2,                           // Water
            7 or 8 or 15 => 3,                // Rock / Impassable / Cliff
            10 => 4,                          // Beach / Shore
            14 => 5,                          // Rough (Passable, Not Buildable)
            _ => 0
        };
    }

    private static (int W, int H) LookupFoundation(string typeId, IniDocument? rulesIni, IniDocument? artIni)
    {
        string artId = rulesIni?.GetSection(typeId)?.GetString("Image", typeId) ?? typeId;
        string? found = artIni?.GetSection(artId)?.GetString("Foundation")
            ?? artIni?.GetSection(typeId)?.GetString("Foundation");
        if (!string.IsNullOrWhiteSpace(found))
        {
            string[] parts = found!.ToLowerInvariant().Split('x');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
            {
                return (Math.Clamp(w, 1, 8), Math.Clamp(h, 1, 8));
            }
        }
        return (2, 2);
    }

    private static void BlitIndexed(
        byte[] srcIndices,
        int srcW,
        int srcH,
        int dstLeft,
        int dstTop,
        Rgba32[] canvas,
        int canvasW,
        int canvasH,
        Rgba32[] pal)
    {
        int yStart = Math.Max(0, -dstTop);
        int yEnd = Math.Min(srcH, canvasH - dstTop);
        int xStart = Math.Max(0, -dstLeft);
        int xEnd = Math.Min(srcW, canvasW - dstLeft);

        for (int y = yStart; y < yEnd; y++)
        {
            int srcRow = y * srcW;
            int dstRow = (dstTop + y) * canvasW + dstLeft;
            for (int x = xStart; x < xEnd; x++)
            {
                byte idx = srcIndices[srcRow + x];
                if (idx != 0)
                {
                    canvas[dstRow + x] = pal[idx];
                }
            }
        }
    }

    private static byte[] DecodeBase64Section(IniSection sec)
    {
        var sb = new StringBuilder(sec.OrderedEntries.Count * 72);
        foreach (var kv in sec.OrderedEntries) sb.Append(kv.Value);
        try
        {
            return Convert.FromBase64String(sb.ToString());
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    public static byte[] UnpackChunks(ReadOnlySpan<byte> packed, bool useFormat80)
    {
        using var ms = new MemoryStream();
        int pos = 0;
        while (pos + 4 <= packed.Length)
        {
            ushort inSize = BinaryPrimitives.ReadUInt16LittleEndian(packed.Slice(pos, 2));
            ushort outSize = BinaryPrimitives.ReadUInt16LittleEndian(packed.Slice(pos + 2, 2));
            pos += 4;
            if (inSize == 0 || outSize == 0 || pos + inSize > packed.Length) break;

            byte[] dst = new byte[outSize];
            var chunk = packed.Slice(pos, inSize);
            if (useFormat80)
                ShpTsDocument.LcwDecompress(chunk, dst);
            else
                MiniLzoDecompress(chunk, dst);

            ms.Write(dst, 0, dst.Length);
            pos += inSize;
        }
        return ms.ToArray();
    }

    private enum LzoPhase { TopLoop, FirstLiteralRun, Match, MatchNext }

    private static int MiniLzoDecompress(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        int ip = 0, op = 0, t = 0;
        if (src.Length == 0) return 0;

        LzoPhase phase = LzoPhase.TopLoop;
        if (src[ip] > 17)
        {
            t = src[ip++] - 17;
            if (t < 4)
            {
                phase = LzoPhase.MatchNext;
            }
            else
            {
                CopyLiterals(src, ref ip, dst, ref op, t);
                phase = LzoPhase.FirstLiteralRun;
            }
        }

        while (ip <= src.Length && op < dst.Length)
        {
            switch (phase)
            {
                case LzoPhase.TopLoop:
                    if (ip >= src.Length) return op;
                    t = src[ip++];
                    if (t >= 16) { phase = LzoPhase.Match; continue; }
                    if (t == 0)
                    {
                        while (ip < src.Length && src[ip] == 0) { t += 255; ip++; }
                        if (ip >= src.Length) return op;
                        t += 15 + src[ip++];
                    }
                    CopyLiterals(src, ref ip, dst, ref op, t + 3);
                    phase = LzoPhase.FirstLiteralRun;
                    break;

                case LzoPhase.FirstLiteralRun:
                    if (ip >= src.Length) return op;
                    t = src[ip++];
                    if (t >= 16) { phase = LzoPhase.Match; continue; }
                    if (ip >= src.Length) return op;
                    {
                        int mPos = op - (1 + 0x0800) - (t >> 2) - (src[ip++] << 2);
                        CopyMatch(dst, ref op, mPos, 3);
                        t = src[ip - 2] & 3;
                        phase = t == 0 ? LzoPhase.TopLoop : LzoPhase.MatchNext;
                    }
                    break;

                case LzoPhase.Match:
                {
                    int trailing;
                    if (t >= 64)
                    {
                        if (ip >= src.Length) return op;
                        trailing = src[ip - 1] & 3;
                        int matchPos = op - 1 - ((t >> 2) & 7) - (src[ip++] << 3);
                        int matchLen = (t >> 5) + 1;
                        CopyMatch(dst, ref op, matchPos, matchLen);
                    }
                    else if (t >= 32)
                    {
                        int matchLen = t & 31;
                        if (matchLen == 0)
                        {
                            while (ip < src.Length && src[ip] == 0) { matchLen += 255; ip++; }
                            if (ip >= src.Length) return op;
                            matchLen += 31 + src[ip++];
                        }
                        if (ip + 2 > src.Length) return op;
                        int matchPos = op - 1 - (BinaryPrimitives.ReadUInt16LittleEndian(src.Slice(ip, 2)) >> 2);
                        trailing = src[ip] & 3;
                        ip += 2;
                        CopyMatch(dst, ref op, matchPos, matchLen + 2);
                    }
                    else if (t >= 16)
                    {
                        int matchPos = op - ((t & 8) << 11);
                        int matchLen = t & 7;
                        if (matchLen == 0)
                        {
                            while (ip < src.Length && src[ip] == 0) { matchLen += 255; ip++; }
                            if (ip >= src.Length) return op;
                            matchLen += 7 + src[ip++];
                        }
                        if (ip + 2 > src.Length) return op;
                        matchPos -= BinaryPrimitives.ReadUInt16LittleEndian(src.Slice(ip, 2)) >> 2;
                        trailing = src[ip] & 3;
                        ip += 2;
                        if (matchPos == op) return op;
                        matchPos -= 0x4000;
                        CopyMatch(dst, ref op, matchPos, matchLen + 2);
                    }
                    else
                    {
                        if (ip >= src.Length) return op;
                        trailing = src[ip - 1] & 3;
                        int matchPos = op - 1 - (t >> 2) - (src[ip++] << 2);
                        CopyMatch(dst, ref op, matchPos, 2);
                    }

                    t = trailing;
                    phase = t == 0 ? LzoPhase.TopLoop : LzoPhase.MatchNext;
                    break;
                }

                case LzoPhase.MatchNext:
                    CopyLiterals(src, ref ip, dst, ref op, t);
                    if (ip >= src.Length) return op;
                    t = src[ip++];
                    phase = LzoPhase.Match;
                    break;
            }
        }
        return op;
    }

    private static void CopyLiterals(ReadOnlySpan<byte> src, ref int ip, Span<byte> dst, ref int op, int count)
    {
        int n = Math.Min(count, Math.Min(src.Length - ip, dst.Length - op));
        if (n > 0)
        {
            src.Slice(ip, n).CopyTo(dst.Slice(op, n));
            ip += n;
            op += n;
        }
    }

    private static void CopyMatch(Span<byte> dst, ref int op, int mPos, int count)
    {
        for (int i = 0; i < count && op < dst.Length; i++)
        {
            dst[op++] = mPos >= 0 && mPos < op ? dst[mPos++] : (byte)0;
        }
    }
}
