using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace Ra2Converter.Formats;

public readonly record struct Rgba32(byte R, byte G, byte B, byte A);

public sealed class PalPalette
{
    public readonly Rgba32[] Colors = new Rgba32[256];
    public readonly int RemapStart;
    public readonly int RemapEnd;

    private PalPalette(int remapStart, int remapEnd)
    {
        RemapStart = remapStart;
        RemapEnd = remapEnd;
    }

    public static PalPalette ParseSixBitPal(ReadOnlySpan<byte> data768, int remapStart = 16, int remapEnd = 31)
    {
        if (data768.Length < 768)
            throw new InvalidDataException($"PAL requires 768 bytes, got {data768.Length}.");

        var pal = new PalPalette(remapStart, remapEnd);
        for (int i = 0; i < 256; i++)
        {
            byte r6 = (byte)(data768[i * 3] & 0x3F);
            byte g6 = (byte)(data768[i * 3 + 1] & 0x3F);
            byte b6 = (byte)(data768[i * 3 + 2] & 0x3F);

            byte r = (byte)((r6 << 2) | (r6 >> 4));
            byte g = (byte)((g6 << 2) | (g6 >> 4));
            byte b = (byte)((b6 << 2) | (b6 >> 4));
            byte a = i == 0 ? (byte)0 : (byte)255;
            pal.Colors[i] = new Rgba32(r, g, b, a);
        }
        return pal;
    }

    public bool IsRemapIndex(int index)
    {
        return RemapStart >= 0 && index >= RemapStart && index <= RemapEnd;
    }

    /// <summary>
    /// Creates a 256x2 palette lookup texture for 3D Voxel meshes:
    /// - RGB holds neutral grayscale intensity on remap indices [16..31] and true RGB on all other indices.
    /// - Alpha = 255 for normal colors, Alpha = 128 for team-color remap indices (decoded by URP shader to tint by faction color while staying visible in standard viewers!).
    /// Wait: even better -> Row 0 (y=0) = Base RGB (with bright neutral gray on remap indices), Row 1 (y=1) = Team Mask (White on 16..31, Black elsewhere) OR baked Allied/Soviet palette!
    /// Exporting a 256x2 PNG gives BOTH universal OBJ viewer compatibility AND exact Shader team mask!
    /// </summary>
    public Rgba32[] BuildVoxelLut256x2()
    {
        var pixels = new Rgba32[256 * 2];
        for (int i = 0; i < 256; i++)
        {
            if (IsRemapIndex(i))
            {
                // RA2 remap 16 is brightest, 31 is darkest
                float t = 1f - (float)(i - RemapStart) / Math.Max(1, RemapEnd - RemapStart);
                byte lum = (byte)Math.Clamp((int)Math.Round((0.28f + 0.72f * t) * 255f), 40, 255);
                pixels[i] = new Rgba32(lum, lum, lum, 255);             // Row 0: shading intensity
                pixels[256 + i] = new Rgba32(255, 255, 255, 255);       // Row 1: team color mask = 1
            }
            else
            {
                var c = Colors[i];
                pixels[i] = new Rgba32(c.R, c.G, c.B, 255);             // Row 0: surface RGB
                pixels[256 + i] = new Rgba32(0, 0, 0, 255);             // Row 1: team color mask = 0
            }
        }
        return pixels;
    }

    public Rgba32 GetSpriteColor(byte index, Rgba32? houseTint = null)
    {
        if (index == 0) return new Rgba32(0, 0, 0, 0);
        if (houseTint.HasValue && IsRemapIndex(index))
        {
            float t = 1f - (float)(index - RemapStart) / Math.Max(1, RemapEnd - RemapStart);
            float shade = 0.25f + 0.75f * t;
            var tint = houseTint.Value;
            return new Rgba32(
                (byte)Math.Clamp((int)(tint.R * shade), 0, 255),
                (byte)Math.Clamp((int)(tint.G * shade), 0, 255),
                (byte)Math.Clamp((int)(tint.B * shade), 0, 255),
                255);
        }
        return Colors[index];
    }
}

public static class PngWriter
{
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static void SaveRgba32(string filePath, int width, int height, ReadOnlySpan<Rgba32> pixels)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        using var fs = File.Create(filePath);
        WriteRgba32(fs, width, height, pixels);
    }

    public static void WriteRgba32(Stream output, int width, int height, ReadOnlySpan<Rgba32> pixels)
    {
        if (pixels.Length < width * height)
            throw new ArgumentException("Pixel buffer smaller than width * height.");

        output.Write(PngSignature);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.Slice(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.Slice(4, 4), (uint)height);
        ihdr[8] = 8; // bit depth 8
        ihdr[9] = 6; // color type RGBA
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(output, "IHDR"u8, ihdr);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            byte[] rowBuf = new byte[1 + width * 4];
            for (int y = 0; y < height; y++)
            {
                rowBuf[0] = 0; // filter type None
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    var p = pixels[rowStart + x];
                    int o = 1 + x * 4;
                    rowBuf[o] = p.R;
                    rowBuf[o + 1] = p.G;
                    rowBuf[o + 2] = p.B;
                    rowBuf[o + 3] = p.A;
                }
                zlib.Write(rowBuf, 0, rowBuf.Length);
            }
        }
        WriteChunk(output, "IDAT"u8, compressed.ToArray());
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> chunkType4, ReadOnlySpan<byte> data)
    {
        Span<byte> lenBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBuf, (uint)data.Length);
        output.Write(lenBuf);
        output.Write(chunkType4);
        if (data.Length > 0) output.Write(data);

        uint crc = 0xFFFFFFFFu;
        crc = UpdateCrc(crc, chunkType4);
        crc = UpdateCrc(crc, data);
        crc = ~crc;

        Span<byte> crcBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuf, crc);
        output.Write(crcBuf);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            uint c = (crc ^ bytes[i]) & 0xFFu;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
            }
            crc = c ^ (crc >> 8);
        }
        return crc;
    }
}
