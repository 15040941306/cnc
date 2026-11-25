using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Ra2Converter.Formats;

public sealed class TmpSubTile
{
    public int Col { get; init; }
    public int Row { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int ExtraX { get; init; }
    public int ExtraY { get; init; }
    public int ExtraWidth { get; init; }
    public int ExtraHeight { get; init; }
    public uint Flags { get; init; }
    public byte HeightOffset { get; init; }
    public byte LandType { get; init; }
    public byte RampType { get; init; }
    public Rgba32 RadarColorLeft { get; init; }
    public byte[] IsoPixels { get; init; } = Array.Empty<byte>();   // 60 x 30 unpacked diamond indices (0 = transparent)
    public byte[]? ExtraPixels { get; init; }                        // ExtraWidth x ExtraHeight indices (0 = transparent)

    public bool HasExtra => (Flags & 0x01) != 0 && ExtraPixels != null && ExtraWidth > 0 && ExtraHeight > 0;
}

public sealed class TmpDocument
{
    public int TemplateCols { get; init; }
    public int TemplateRows { get; init; }
    public int TileWidth { get; init; }
    public int TileHeight { get; init; }
    public TmpSubTile?[] SubTiles { get; init; } = Array.Empty<TmpSubTile?>();

    public static TmpDocument? TryParse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 20) return null;

        uint cols = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
        uint rows = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        int tw = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
        int th = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(12, 4));

        if (cols == 0 || rows == 0 || cols > 64 || rows > 64 || tw <= 0 || th <= 0 || (tw & 1) != 0 || (th & 1) != 0)
            return null;

        int count = (int)(cols * rows);
        if (16 + count * 4 > data.Length) return null;

        int isoPackBytes = (tw * th) / 2;
        var subTiles = new TmpSubTile?[count];

        for (int idx = 0; idx < count; idx++)
        {
            uint offU = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16 + idx * 4, 4));
            if (offU == 0) continue;
            int off = (int)offU;
            if (off < 0 || off + 52 + isoPackBytes > data.Length) continue;

            int x = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off, 4));
            int y = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off + 4, 4));
            uint extraOffRel = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(off + 8, 4));
            int exX = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off + 20, 4));
            int exY = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off + 24, 4));
            int exW = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off + 28, 4));
            int exH = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off + 32, 4));
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(off + 36, 4));
            byte heightOff = data[off + 40];
            byte landType = data[off + 41];
            byte rampType = data[off + 42];
            var radarL = new Rgba32(data[off + 43], data[off + 44], data[off + 45], 255);

            // Unpack diamond scanlines into tw x th grid (4, 8, ..., tw, tw-4, ..., 4)
            byte[] isoCanvas = new byte[tw * th];
            int srcPos = off + 52;
            int runWidth = 4;
            int halfH = th / 2;
            for (int row = 0; row < th && srcPos + runWidth <= data.Length; row++)
            {
                if (runWidth > 0 && runWidth <= tw)
                {
                    int startX = (tw - runWidth) / 2;
                    data.Slice(srcPos, runWidth).CopyTo(isoCanvas.AsSpan(row * tw + startX, runWidth));
                    srcPos += runWidth;
                }
                runWidth += (row < halfH - 1) ? 4 : -4;
            }

            byte[]? extraPixels = null;
            if ((flags & 0x01) != 0 && exW > 0 && exH > 0 && exW <= 512 && exH <= 512)
            {
                int exArea = exW * exH;
                int exStart = extraOffRel > 0 && off + (int)extraOffRel + exArea <= data.Length
                    ? off + (int)extraOffRel
                    : off + 52 + isoPackBytes * (((flags & 0x02) != 0) ? 2 : 1);

                if (exStart >= 0 && exStart + exArea <= data.Length)
                {
                    extraPixels = data.Slice(exStart, exArea).ToArray();
                }
            }

            subTiles[idx] = new TmpSubTile
            {
                Col = idx % (int)cols,
                Row = idx / (int)cols,
                X = x,
                Y = y,
                ExtraX = exX,
                ExtraY = exY,
                ExtraWidth = exW,
                ExtraHeight = exH,
                Flags = flags,
                HeightOffset = heightOff,
                LandType = landType,
                RampType = rampType,
                RadarColorLeft = radarL,
                IsoPixels = isoCanvas,
                ExtraPixels = extraPixels
            };
        }

        return new TmpDocument
        {
            TemplateCols = (int)cols,
            TemplateRows = (int)rows,
            TileWidth = tw,
            TileHeight = th,
            SubTiles = subTiles
        };
    }
}

public sealed class TheaterTilesetAtlas
{
    public string TheaterName { get; }
    public PalPalette Palette { get; }
    private readonly List<TmpDocument?> _templates = new();
    private readonly List<int> _templateTileSetId = new();
    private readonly HashSet<int> _bridgeSetIds = new();

    public TheaterTilesetAtlas(string theaterName, IniDocument? theaterIni, PalPalette palette, Ra2VirtualFileSystem vfs, string primaryExt)
    {
        TheaterName = theaterName;
        Palette = palette;
        if (theaterIni == null) return;

        var gen = theaterIni.GetSection("General");
        if (gen != null)
        {
            foreach (string key in new[] { "BridgeSet", "WoodBridgeSet", "WaterBridge" })
            {
                int id = gen.GetInt(key, -1);
                if (id >= 0) _bridgeSetIds.Add(id);
            }
        }

        string[] fallbackExts = { primaryExt, "tem", "urb", "sno", "gen" };

        for (int setIdx = 0; setIdx < 600; setIdx++)
        {
            var sec = theaterIni.GetSection($"TileSet{setIdx:D4}");
            if (sec == null) break;

            int tilesInSet = Math.Max(0, sec.GetInt("TilesInSet", 0));
            string filePrefix = sec.GetString("FileName", "") ?? "";
            string setName = (sec.GetString("SetName", "") ?? "").ToLowerInvariant();
            if (setName.Contains("bridge")) _bridgeSetIds.Add(setIdx);

            for (int i = 1; i <= tilesInSet; i++)
            {
                byte[]? bytes = null;
                foreach (string ext in fallbackExts)
                {
                    bytes = vfs.TryReadFile($"{filePrefix}{i:D2}.{ext}");
                    if (bytes != null) break;
                }

                TmpDocument? doc = bytes != null ? TmpDocument.TryParse(bytes) : null;
                _templates.Add(doc);
                _templateTileSetId.Add(setIdx);
            }
        }
    }

    public TmpDocument? GetTemplate(int tileIndex)
    {
        if (_templates.Count == 0) return null;
        if (tileIndex < 0 || tileIndex >= _templates.Count) return _templates[0];
        return _templates[tileIndex] ?? _templates[0];
    }

    public TmpSubTile? GetSubTile(int tileIndex, int subTileIndex)
    {
        var tmpl = GetTemplate(tileIndex);
        if (tmpl == null || tmpl.SubTiles.Length == 0) return null;
        if (subTileIndex >= 0 && subTileIndex < tmpl.SubTiles.Length && tmpl.SubTiles[subTileIndex] != null)
            return tmpl.SubTiles[subTileIndex];
        for (int i = 0; i < tmpl.SubTiles.Length; i++)
        {
            if (tmpl.SubTiles[i] != null) return tmpl.SubTiles[i];
        }
        return null;
    }

    public bool IsBridgeTemplate(int tileIndex)
    {
        return tileIndex >= 0 && tileIndex < _templateTileSetId.Count && _bridgeSetIds.Contains(_templateTileSetId[tileIndex]);
    }
}
