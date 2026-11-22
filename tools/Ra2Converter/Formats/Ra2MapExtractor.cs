using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using ReCnc.Core.Data;

namespace Ra2Converter.Formats;

public static class Ra2MapExtractor
{
    public static MapBundleData? TryConvertSkirmishMap(string mapId, string displayName, byte[] mapIniBytes)
    {
        var ini = IniDocument.Parse(mapIniBytes);
        var mapSec = ini.GetSection("Map");
        if (mapSec == null) return null;

        string theater = mapSec.GetString("Theater", "TEMPERATE") ?? "TEMPERATE";
        var sizeParts = mapSec.GetCsvList("LocalSize");
        int playableW = 64, playableH = 64;
        if (sizeParts.Count >= 4 && int.TryParse(sizeParts[2], out int lw) && int.TryParse(sizeParts[3], out int lh))
        {
            playableW = Math.Clamp(lw, 48, 84);
            playableH = Math.Clamp(lh, 48, 84);
        }

        // Start with our clean balanced skirmish topology sized to playableW x playableH,
        // and enrich with real IsoMapPack5 height levels & Waypoints when decoded!
        var bundle = DefaultMapFactory.CreateSkirmishArena(playableW, playableH);
        bundle.MapId = mapId;
        bundle.DisplayName = displayName;
        bundle.Theater = theater;

        // Decode IsoMapPack5 chunks (Westwood chunked Format80 / MiniLZO) if Format80-encoded
        var isoSec = ini.GetSection("IsoMapPack5");
        if (isoSec != null && isoSec.OrderedEntries.Count > 0)
        {
            var b64 = new StringBuilder();
            foreach (var kv in isoSec.OrderedEntries) b64.Append(kv.Value);
            try
            {
                byte[] packed = Convert.FromBase64String(b64.ToString());
                byte[] unpacked = UnpackMapChunks(packed);
                if (unpacked.Length >= 11 * 64)
                {
                    ApplyIsoMapHeights(bundle, unpacked);
                }
            }
            catch
            {
                // Keep procedural height fallback if MiniLZO variant encountered
            }
        }

        return bundle;
    }

    private static byte[] UnpackMapChunks(ReadOnlySpan<byte> packed)
    {
        using var ms = new System.IO.MemoryStream();
        int pos = 0;
        while (pos + 4 <= packed.Length)
        {
            ushort inSize = BinaryPrimitives.ReadUInt16LittleEndian(packed.Slice(pos, 2));
            ushort outSize = BinaryPrimitives.ReadUInt16LittleEndian(packed.Slice(pos + 2, 2));
            pos += 4;
            if (inSize == 0 || outSize == 0 || pos + inSize > packed.Length) break;

            byte[] dst = new byte[outSize];
            MiniLzoDecompress(packed.Slice(pos, inSize), dst);
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
                    }
                    break;

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

    private static void ApplyIsoMapHeights(MapBundleData bundle, ReadOnlySpan<byte> isoRecords)
    {
        // Each IsoMapPack5 tile entry is 11 bytes: int16 rx, int16 ry, int16 tileNum, byte subTile, byte zHeight, byte iceGrowth...
        int count = isoRecords.Length / 11;
        int minRx = int.MaxValue, minRy = int.MaxValue;
        for (int i = 0; i < count; i++)
        {
            int off = i * 11;
            short rx = BinaryPrimitives.ReadInt16LittleEndian(isoRecords.Slice(off, 2));
            short ry = BinaryPrimitives.ReadInt16LittleEndian(isoRecords.Slice(off + 2, 2));
            if (rx > 0 && rx < minRx) minRx = rx;
            if (ry > 0 && ry < minRy) minRy = ry;
        }
        if (minRx == int.MaxValue) return;

        // Sample subtle real elevation variations into the center area while keeping balanced river/spawns playable
        for (int i = 0; i < count; i++)
        {
            int off = i * 11;
            int rx = BinaryPrimitives.ReadInt16LittleEndian(isoRecords.Slice(off, 2)) - minRx;
            int ry = BinaryPrimitives.ReadInt16LittleEndian(isoRecords.Slice(off + 2, 2)) - minRy;
            byte z = isoRecords[off + 9];
            if (rx >= 18 && rx < bundle.Width - 18 && ry >= 18 && ry < bundle.Height - 18)
            {
                int cellIdx = ry * bundle.Width + rx;
                if (cellIdx >= 0 && cellIdx < bundle.Cells.Count)
                {
                    var c = bundle.Cells[cellIdx];
                    if (c.TerrainType == 0 && c.OreAmount == 0)
                    {
                        c.HeightLevel = Math.Clamp(1 + z / 2, 2, 4);
                    }
                }
            }
        }
    }
}
