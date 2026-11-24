using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ReCnc.Core.Data;

namespace Ra2Converter.Formats;

public sealed class TheaterTileClassifier
{
    private readonly int[] _tileTerrain = new int[8192];
    private readonly bool[] _tileIsRamp = new bool[8192];

    public TheaterTileClassifier(IniDocument? theaterIni)
    {
        if (theaterIni == null) return;
        var gen = theaterIni.GetSection("General");
        int waterSet = gen?.GetInt("WaterSet", 21) ?? 21;
        int rampBase = gen?.GetInt("RampBase", 9) ?? 9;
        int slopePieces = gen?.GetInt("SlopeSetPieces", -1) ?? -1;
        int slopePieces2 = gen?.GetInt("SlopeSetPieces2", -1) ?? -1;
        int cliffSet = gen?.GetInt("CliffSet", 10) ?? 10;
        int waterCliffs = gen?.GetInt("WaterCliffs", -1) ?? -1;
        int shorePieces = gen?.GetInt("ShorePieces", 12) ?? 12;
        int pavedRoads = gen?.GetInt("PavedRoads", 20) ?? 20;

        int runningTileIdx = 0;
        for (int setIdx = 0; setIdx < 500 && runningTileIdx < _tileTerrain.Length; setIdx++)
        {
            var sec = theaterIni.GetSection($"TileSet{setIdx:D4}");
            if (sec == null) break;

            int count = Math.Max(0, sec.GetInt("TilesInSet", 0));
            string setName = (sec.GetString("SetName", "") ?? "").ToLowerInvariant();

            bool isRamp = setIdx == rampBase || setIdx == slopePieces || setIdx == slopePieces2 || setName.Contains("ramp") || setName.Contains("slope");
            int terrain = 0;
            if (setIdx == waterSet || (setName.Contains("water") && !setName.Contains("cliff") && !setName.Contains("bridge")))
                terrain = 2; // Water
            else if (setIdx == cliffSet || setIdx == waterCliffs || setName.Contains("cliff"))
                terrain = 3; // Cliff/Rock
            else if (setIdx == shorePieces || setName.Contains("shore"))
                terrain = 4; // Beach/Shore
            else if (setIdx == pavedRoads || setName.Contains("road") || setName.Contains("bridge") || setName.Contains("pave"))
                terrain = 1; // Road

            for (int t = 0; t < count && runningTileIdx < _tileTerrain.Length; t++, runningTileIdx++)
            {
                _tileTerrain[runningTileIdx] = terrain;
                _tileIsRamp[runningTileIdx] = isRamp;
            }
        }
    }

    public (int TerrainType, int RampType) Classify(int tileNum)
    {
        if (tileNum < 0 || tileNum >= _tileTerrain.Length) return (0, 0);
        return (_tileTerrain[tileNum], _tileIsRamp[tileNum] ? 1 : 0);
    }
}

public static class Ra2MapExtractor
{
    public static (MapBundleData? Map, Rgba32[]? PreviewPixels, int PreviewW, int PreviewH) TryExtractFullMap(
        string mapId,
        string displayName,
        byte[] mapIniBytes,
        Dictionary<string, TheaterTileClassifier> theaterClassifiers)
    {
        var ini = IniDocument.Parse(mapIniBytes);
        var mapSec = ini.GetSection("Map");
        if (mapSec == null) return (null, null, 0, 0);

        string theater = (mapSec.GetString("Theater", "TEMPERATE") ?? "TEMPERATE").Trim().ToUpperInvariant();
        var sizeParts = mapSec.GetCsvList("Size");
        var localParts = mapSec.GetCsvList("LocalSize");
        if (sizeParts.Count < 4 || localParts.Count < 4) return (null, null, 0, 0);

        int fullW = Math.Max(20, int.TryParse(sizeParts[2], out int fw) ? fw : 64);
        int fullH = Math.Max(20, int.TryParse(sizeParts[3], out int fh) ? fh : 64);
        int localLeft = Math.Max(0, int.TryParse(localParts[0], out int ll) ? ll : 2);
        int localTop = Math.Max(0, int.TryParse(localParts[1], out int lt) ? lt : 4);
        int localW = Math.Clamp(int.TryParse(localParts[2], out int lw) ? lw : 64, 32, 128);
        int localH = Math.Clamp(int.TryParse(localParts[3], out int lh) ? lh : 64, 32, 128);

        theaterClassifiers.TryGetValue(theater, out var classifier);
        classifier ??= theaterClassifiers.GetValueOrDefault("TEMPERATE");

        // Allocate dense playable grid [localW, localH]
        var grid = new MapCellData[localW, localH];
        var visited = new bool[localW, localH];
        for (int y = 0; y < localH; y++)
        {
            for (int x = 0; x < localW; x++)
            {
                grid[x, y] = new MapCellData { X = x, Y = y, HeightLevel = 2, TerrainType = 0 };
            }
        }

        // Coordinate projection helper: RA2 isometric (rx, ry) -> playable rectangular (gx, gy)
        bool ProjectRxRy(int rx, int ry, out int gx, out int gy)
        {
            int u = ((rx - ry + fullW - 1) / 2) - localLeft;
            int vScreen = ((rx + ry - fullW - 1) / 2) - localTop;
            gx = u;
            gy = (localH - 1) - vScreen; // Flip vertical so North in RA2 is +Z in Unity!
            return gx >= 0 && gx < localW && gy >= 0 && gy < localH;
        }

        // 1. Decode [IsoMapPack5] (LZO1X-1 compressed 11-byte isometric tile records)
        var isoSec = ini.GetSection("IsoMapPack5");
        if (isoSec != null && isoSec.OrderedEntries.Count > 0)
        {
            byte[] packed = DecodeBase64Section(isoSec);
            byte[] unpacked = UnpackChunks(packed, useFormat80: false);
            int recCount = unpacked.Length / 11;
            for (int i = 0; i < recCount; i++)
            {
                int off = i * 11;
                short rx = BinaryPrimitives.ReadInt16LittleEndian(unpacked.AsSpan(off, 2));
                short ry = BinaryPrimitives.ReadInt16LittleEndian(unpacked.AsSpan(off + 2, 2));
                short tileNum = BinaryPrimitives.ReadInt16LittleEndian(unpacked.AsSpan(off + 4, 2));
                byte zHeight = unpacked[off + 9];

                if (!ProjectRxRy(rx, ry, out int gx, out int gy)) continue;

                var cell = grid[gx, gy];
                cell.HeightLevel = Math.Clamp(zHeight / 2 + 1, 1, 12);
                if (classifier != null)
                {
                    var (terr, ramp) = classifier.Classify(tileNum);
                    cell.TerrainType = terr;
                    cell.RampType = ramp;
                    if (terr == 2) cell.HeightLevel = 1;
                }
                visited[gx, gy] = true;
            }

            // Fill unvisited diamond-parity neighbor cells so rectangular 3D grid has zero holes!
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < localH; y++)
                {
                    for (int x = 0; x < localW; x++)
                    {
                        if (visited[x, y]) continue;
                        MapCellData? donor =
                            (x > 0 && visited[x - 1, y]) ? grid[x - 1, y] :
                            (x + 1 < localW && visited[x + 1, y]) ? grid[x + 1, y] :
                            (y > 0 && visited[x, y - 1]) ? grid[x, y - 1] :
                            (y + 1 < localH && visited[x, y + 1]) ? grid[x, y + 1] : null;
                        if (donor != null)
                        {
                            grid[x, y].HeightLevel = donor.HeightLevel;
                            grid[x, y].TerrainType = donor.TerrainType;
                            grid[x, y].RampType = donor.RampType;
                            visited[x, y] = true;
                        }
                    }
                }
            }

            // Automatically mark gentle height transitions (dh == 1) as navigable ramps unless cliff rock!
            for (int y = 1; y < localH - 1; y++)
            {
                for (int x = 1; x < localW - 1; x++)
                {
                    var c = grid[x, y];
                    if (c.TerrainType == 2 || c.TerrainType == 3) continue;
                    int h = c.HeightLevel;
                    if (Math.Abs(grid[x - 1, y].HeightLevel - h) == 1 ||
                        Math.Abs(grid[x + 1, y].HeightLevel - h) == 1 ||
                        Math.Abs(grid[x, y - 1].HeightLevel - h) == 1 ||
                        Math.Abs(grid[x, y + 1].HeightLevel - h) == 1)
                    {
                        c.RampType = 1;
                    }
                }
            }
        }

        // 2. Decode [OverlayPack] & [OverlayDataPack] (Format80 LCW 512x512 arrays for Gold Ore & Gems!)
        var ovSec = ini.GetSection("OverlayPack");
        var ovDataSec = ini.GetSection("OverlayDataPack");
        int totalOrePlaced = 0;
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
                if (!ProjectRxRy(rx, ry, out int gx, out int gy)) continue;

                int oreType = 0;
                if ((ov >= 27 && ov <= 38) || (ov >= 102 && ov <= 126))
                    oreType = 1; // Riparius / Vinifera Gold Ore
                else if ((ov >= 39 && ov <= 50) || (ov >= 127 && ov <= 166))
                    oreType = 2; // Cruentus / Aboreus Gems
                else if (ov >= 24 && ov <= 26 || ov >= 237 && ov <= 238)
                {
                    // High bridge deck over water
                    grid[gx, gy].TerrainType = 1;
                    grid[gx, gy].HeightLevel = Math.Max(2, grid[gx, gy].HeightLevel);
                }

                if (oreType > 0 && grid[gx, gy].TerrainType != 2 && grid[gx, gy].TerrainType != 3)
                {
                    int stage = idx < ovData.Length ? ovData[idx] : 6;
                    grid[gx, gy].OreType = oreType;
                    grid[gx, gy].OreAmount = Math.Clamp(stage + 2, 5, 12);
                    totalOrePlaced++;
                }
            }
        }

        // 3. Extract [Waypoints] (0..7 are multiplayer start positions: value = ry * 1000 + rx)
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
                if (ProjectRxRy(rx, ry, out int gx, out int gy))
                {
                    waypoints.Add(new WaypointData
                    {
                        Index = wpIdx,
                        X = Math.Clamp(gx, 6, localW - 8),
                        Y = Math.Clamp(gy, 6, localH - 8)
                    });
                }
            }
        }

        if (waypoints.Count < 2)
        {
            waypoints.Clear();
            waypoints.Add(new WaypointData { Index = 0, X = 10, Y = 10 });
            waypoints.Add(new WaypointData { Index = 1, X = localW - 12, Y = localH - 12 });
        }

        // Ensure natural ore exists if map used oil derricks only
        if (totalOrePlaced < 12)
        {
            foreach (var wp in waypoints)
            {
                int ox = Math.Clamp(wp.X + (wp.X < localW / 2 ? 9 : -9), 4, localW - 5);
                int oy = Math.Clamp(wp.Y, 4, localH - 5);
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        var c = grid[Math.Clamp(ox + dx, 0, localW - 1), Math.Clamp(oy + dy, 0, localH - 1)];
                        if (c.TerrainType == 0) { c.OreType = 1; c.OreAmount = 9; }
                    }
                }
            }
        }

        // 4. Decode [Preview] + [PreviewPack] official RGB24 minimap thumbnail!
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
            Width = localW,
            Height = localH,
            Waypoints = waypoints,
            Cells = new List<MapCellData>(localW * localH)
        };
        for (int y = 0; y < localH; y++)
        {
            for (int x = 0; x < localW; x++)
            {
                bundle.Cells.Add(grid[x, y]);
            }
        }

        return (bundle, previewRgba, prevW, prevH);
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

    private static byte[] UnpackChunks(ReadOnlySpan<byte> packed, bool useFormat80)
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
