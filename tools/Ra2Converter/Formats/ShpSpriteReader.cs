using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace Ra2Converter.Formats;

public sealed class ShpFrame
{
    public int X;
    public int Y;
    public int CropWidth;
    public int CropHeight;
    public byte Compression;
    public uint FileOffset;
    public byte[] IndexedPixels = Array.Empty<byte>(); // CropWidth * CropHeight
}

public sealed class ShpTsDocument
{
    public int Width { get; }
    public int Height { get; }
    public List<ShpFrame> Frames { get; }

    private ShpTsDocument(int width, int height, List<ShpFrame> frames)
    {
        Width = width;
        Height = height;
        Frames = frames;
    }

    public static ShpTsDocument Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
            throw new InvalidDataException("SHP file too small.");

        ushort zero = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0, 2));
        if (zero != 0)
            throw new InvalidDataException("Not a TS/RA2 SHP v2 file (first uint16 != 0).");

        int width = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2, 2));
        int height = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2));
        int numFrames = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2));

        if (numFrames > 4096 || 8 + numFrames * 24 > data.Length)
            throw new InvalidDataException($"Invalid SHP frame count {numFrames}.");

        var frames = new List<ShpFrame>(numFrames);
        for (int i = 0; i < numFrames; i++)
        {
            int off = 8 + i * 24;
            int x = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(off, 2));
            int y = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(off + 2, 2));
            int cx = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(off + 4, 2));
            int cy = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(off + 6, 2));
            byte comp = data[off + 8];
            uint fileOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(off + 20, 4));

            byte[] pixels = Array.Empty<byte>();
            if (cx > 0 && cy > 0 && fileOffset > 0 && fileOffset < data.Length)
            {
                var raw = data.Slice((int)fileOffset);
                pixels = DecodePixels(raw, cx, cy, comp);
            }

            frames.Add(new ShpFrame
            {
                X = x,
                Y = y,
                CropWidth = cx,
                CropHeight = cy,
                Compression = comp,
                FileOffset = fileOffset,
                IndexedPixels = pixels
            });
        }

        return new ShpTsDocument(width, height, frames);
    }

    public Rgba32[] RenderFrameToCanvas(int frameIndex, PalPalette palette, Rgba32? houseTint, bool autoTrimTransparent, out int outWidth, out int outHeight)
    {
        outWidth = Width;
        outHeight = Height;
        var canvas = new Rgba32[Width * Height];
        if (frameIndex < 0 || frameIndex >= Frames.Count) return canvas;

        var f = Frames[frameIndex];
        if (f.IndexedPixels.Length == 0) return canvas;

        for (int ry = 0; ry < f.CropHeight; ry++)
        {
            int dstY = f.Y + ry;
            if (dstY < 0 || dstY >= Height) continue;
            for (int rx = 0; rx < f.CropWidth; rx++)
            {
                int dstX = f.X + rx;
                if (dstX < 0 || dstX >= Width) continue;
                byte idx = f.IndexedPixels[ry * f.CropWidth + rx];
                if (idx == 0) continue;
                canvas[dstY * Width + dstX] = palette.GetSpriteColor(idx, houseTint);
            }
        }

        if (!autoTrimTransparent || f.CropWidth <= 0 || f.CropHeight <= 0)
        {
            return canvas;
        }

        // Crop tightly with 2px margin so 3D billboards don't have huge empty canvas margins
        int minX = Math.Max(0, f.X - 2);
        int minY = Math.Max(0, f.Y - 2);
        int maxX = Math.Min(Width - 1, f.X + f.CropWidth + 1);
        int maxY = Math.Min(Height - 1, f.Y + f.CropHeight + 1);
        outWidth = Math.Max(1, maxX - minX + 1);
        outHeight = Math.Max(1, maxY - minY + 1);

        var trimmed = new Rgba32[outWidth * outHeight];
        for (int ty = 0; ty < outHeight; ty++)
        {
            for (int tx = 0; tx < outWidth; tx++)
            {
                trimmed[ty * outWidth + tx] = canvas[(minY + ty) * Width + (minX + tx)];
            }
        }
        return trimmed;
    }

    private static byte[] DecodePixels(ReadOnlySpan<byte> raw, int cx, int cy, byte flags)
    {
        int area = cx * cy;
        byte[] output = new byte[area];

        // In Westwood TS/RA2 SHP FrameHeader, Flags is a bitmask:
        // Bit 0 (0x01) = frame contains transparent pixels
        // Bit 1 (0x02) = scanline RLE compressed (0 = uncompressed raw cx * cy bytes)
        if ((flags & 0x02) == 0)
        {
            int n = Math.Min(area, raw.Length);
            raw.Slice(0, n).CopyTo(output);
            return output;
        }

        int pos = 0;
        for (int row = 0; row < cy; row++)
        {
            if (pos + 2 > raw.Length) break;
            int scanLen = BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(pos, 2));
            int scanEnd = Math.Min(raw.Length, pos + Math.Max(2, scanLen));
            int cur = pos + 2;
            int col = 0;
            int rowBase = row * cx;

            while (cur < scanEnd && col < cx)
            {
                byte b = raw[cur++];
                if (b == 0)
                {
                    if (cur >= scanEnd) break;
                    int zeroRun = raw[cur++];
                    col = Math.Min(cx, col + zeroRun);
                }
                else
                {
                    output[rowBase + col++] = b;
                }
            }
            pos = scanEnd;
        }
        return output;
    }

    public static int LcwDecompress(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        int sp = 0;
        int dp = 0;
        bool relative = src.Length > 0 && src[0] == 0;
        if (relative) sp++;

        while (sp < src.Length && dp < dst.Length)
        {
            byte cmd = src[sp++];
            if (cmd == 0x80) break;

            if ((cmd & 0x80) == 0)
            {
                if (sp >= src.Length) break;
                int count = (cmd >> 4) + 3;
                int relOff = ((cmd & 0x0F) << 8) | src[sp++];
                int copySrc = dp - relOff;
                for (int i = 0; i < count && dp < dst.Length; i++)
                {
                    dst[dp++] = copySrc >= 0 && copySrc < dp ? dst[copySrc++] : (byte)0;
                }
            }
            else if ((cmd & 0x40) == 0)
            {
                int count = cmd & 0x3F;
                int n = Math.Min(count, Math.Min(src.Length - sp, dst.Length - dp));
                src.Slice(sp, n).CopyTo(dst.Slice(dp, n));
                sp += n;
                dp += n;
            }
            else if (cmd == 0xFE)
            {
                if (sp + 3 > src.Length) break;
                int count = BinaryPrimitives.ReadUInt16LittleEndian(src.Slice(sp, 2));
                byte val = src[sp + 2];
                sp += 3;
                int n = Math.Min(count, dst.Length - dp);
                dst.Slice(dp, n).Fill(val);
                dp += n;
            }
            else
            {
                int count;
                if (cmd == 0xFF)
                {
                    if (sp + 4 > src.Length) break;
                    count = BinaryPrimitives.ReadUInt16LittleEndian(src.Slice(sp, 2));
                    sp += 2;
                }
                else
                {
                    count = (cmd & 0x3F) + 3;
                    if (sp + 2 > src.Length) break;
                }
                int off = BinaryPrimitives.ReadUInt16LittleEndian(src.Slice(sp, 2));
                sp += 2;
                int copySrc = relative ? (dp - off) : off;
                for (int i = 0; i < count && dp < dst.Length; i++)
                {
                    dst[dp++] = copySrc >= 0 && copySrc < dp ? dst[copySrc++] : (byte)0;
                }
            }
        }
        return dp;
    }
}
