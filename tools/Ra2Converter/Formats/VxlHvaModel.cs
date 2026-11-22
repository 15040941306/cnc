using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace Ra2Converter.Formats;

public sealed class VxlLimb
{
    public string Name = "";
    public int SizeX;
    public int SizeY;
    public int SizeZ;
    public float Scale;
    public Vector3 MinBounds;
    public Vector3 MaxBounds;
    public float[] TailerMatrix12 = new float[12];
    public byte[] ColorGrid = Array.Empty<byte>(); // length = SizeX * SizeY * SizeZ, 0 = empty
}

public sealed class VxlDocument
{
    public PalPalette EmbeddedPalette { get; }
    public List<VxlLimb> Limbs { get; }

    private VxlDocument(PalPalette palette, List<VxlLimb> limbs)
    {
        EmbeddedPalette = palette;
        Limbs = limbs;
    }

    public static VxlDocument Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 802)
            throw new InvalidDataException($"VXL too short ({data.Length} bytes).");

        string magic = Encoding.ASCII.GetString(data.Slice(0, 15));
        if (!magic.StartsWith("Voxel Animation", StringComparison.Ordinal))
            throw new InvalidDataException($"Invalid VXL magic '{magic}'.");

        uint numLimbs = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20, 4));
        uint numTailers = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(24, 4));
        uint bodySize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(28, 4));
        byte remapStart = data[32];
        byte remapEnd = data[33];
        var palette = PalPalette.ParseSixBitPal(data.Slice(34, 768), remapStart, remapEnd);

        int limbHeadersOffset = 802;
        int bodyOffset = limbHeadersOffset + (int)numLimbs * 28;
        int tailersOffset = bodyOffset + (int)bodySize;

        if (tailersOffset + (int)numTailers * 92 > data.Length)
            throw new InvalidDataException("VXL section offsets exceed file length.");

        int count = (int)Math.Min(numLimbs, numTailers);
        var limbs = new List<VxlLimb>(count);

        for (int i = 0; i < count; i++)
        {
            int lhOff = limbHeadersOffset + i * 28;
            string name = ReadFixedAscii(data.Slice(lhOff, 16));

            int ltOff = tailersOffset + i * 92;
            uint spanStartOff = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(ltOff, 4));
            uint spanDataOff = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(ltOff + 8, 4));
            float scale = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(ltOff + 12, 4));

            float[] mat = new float[12];
            for (int m = 0; m < 12; m++)
            {
                mat[m] = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(ltOff + 16 + m * 4, 4));
            }

            Vector3 minB = new(
                BinaryPrimitives.ReadSingleLittleEndian(data.Slice(ltOff + 64, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(data.Slice(ltOff + 68, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(data.Slice(ltOff + 72, 4)));
            Vector3 maxB = new(
                BinaryPrimitives.ReadSingleLittleEndian(data.Slice(ltOff + 76, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(data.Slice(ltOff + 80, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(data.Slice(ltOff + 84, 4)));

            int sx = data[ltOff + 88];
            int sy = data[ltOff + 89];
            int sz = data[ltOff + 90];

            byte[] grid = new byte[sx * sy * sz];
            int colCount = sx * sy;
            int startsAbs = bodyOffset + (int)spanStartOff;
            int dataBaseAbs = bodyOffset + (int)spanDataOff;

            for (int col = 0; col < colCount; col++)
            {
                if (startsAbs + (col + 1) * 4 > tailersOffset) break;
                int relColStart = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(startsAbs + col * 4, 4));
                if (relColStart < 0) continue;

                int x = col % sx;
                int y = col / sx;
                int pos = dataBaseAbs + relColStart;
                int z = 0;

                while (z < sz && pos + 2 <= tailersOffset)
                {
                    byte skip = data[pos++];
                    z += skip;
                    if (z >= sz || pos >= tailersOffset) break;

                    byte voxCount = data[pos++];
                    for (int v = 0; v < voxCount; v++)
                    {
                        if (pos + 2 > tailersOffset) break;
                        byte colorIdx = data[pos];
                        pos += 2; // colorIndex + normalIndex
                        if (z < sz && colorIdx != 0)
                        {
                            grid[x + sx * (y + sy * z)] = colorIdx;
                        }
                        z++;
                    }
                    if (pos < tailersOffset) pos++; // duplicate count byte
                }
            }

            limbs.Add(new VxlLimb
            {
                Name = name,
                SizeX = sx,
                SizeY = sy,
                SizeZ = sz,
                Scale = scale > 0 ? scale : (1f / 12f),
                MinBounds = minB,
                MaxBounds = maxB,
                TailerMatrix12 = mat,
                ColorGrid = grid
            });
        }

        return new VxlDocument(palette, limbs);
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> bytes)
    {
        int nul = bytes.IndexOf((byte)0);
        if (nul >= 0) bytes = bytes.Slice(0, nul);
        return Encoding.ASCII.GetString(bytes).Trim();
    }
}

public sealed class HvaDocument
{
    public int NumFrames { get; }
    public List<string> SectionNames { get; }
    private readonly float[][] _frame0Matrices; // [section][12]

    private HvaDocument(int numFrames, List<string> names, float[][] frame0)
    {
        NumFrames = numFrames;
        SectionNames = names;
        _frame0Matrices = frame0;
    }

    public float[]? GetFrame0Matrix(int sectionIndex)
    {
        return sectionIndex >= 0 && sectionIndex < _frame0Matrices.Length ? _frame0Matrices[sectionIndex] : null;
    }

    public static HvaDocument Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24)
            throw new InvalidDataException("HVA too short.");

        int numFrames = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
        int numSections = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20, 4));
        int namesOffset = 24;
        int matricesOffset = namesOffset + numSections * 16;

        var names = new List<string>(numSections);
        for (int s = 0; s < numSections && namesOffset + (s + 1) * 16 <= data.Length; s++)
        {
            var span = data.Slice(namesOffset + s * 16, 16);
            int nul = span.IndexOf((byte)0);
            if (nul >= 0) span = span.Slice(0, nul);
            names.Add(Encoding.ASCII.GetString(span).Trim());
        }

        var frame0 = new float[numSections][];
        for (int s = 0; s < numSections; s++)
        {
            frame0[s] = new float[12];
            int off = matricesOffset + s * 48; // Frame 0, Section s
            if (off + 48 <= data.Length)
            {
                for (int m = 0; m < 12; m++)
                {
                    frame0[s][m] = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(off + m * 4, 4));
                }
            }
            else
            {
                frame0[s][0] = frame0[s][5] = frame0[s][10] = 1f;
            }
        }

        return new HvaDocument(numFrames, names, frame0);
    }
}

public static class VxlObjExporter
{
    // Scale Westwood VXL world coordinates into ~1.8m tank length in Unity (where 1 grid cell = 2.0m)
    public const float WorldScale = 0.032f;

    public static (Vector3 BoundsMin, Vector3 BoundsMax, int TriangleCount) ExportToObj(
        VxlDocument vxl,
        HvaDocument? hva,
        string objPath,
        string mtlFileName,
        string materialName,
        bool groundAlignBottom)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(objPath)!);

        var vertices = new List<Vector3>(4096);
        var normals = new List<Vector3>(6)
        {
            new( 1,  0,  0),
            new(-1,  0,  0),
            new( 0,  1,  0),
            new( 0, -1,  0),
            new( 0,  0,  1),
            new( 0,  0, -1),
        };
        var uvs = new List<float>(256);
        for (int c = 0; c < 256; c++)
        {
            uvs.Add((c + 0.5f) / 256f);
        }

        // Each quad stores (v0, v1, v2, v3, normalIdx1Based, colorIdx1Based)
        var quads = new List<(int V0, int V1, int V2, int V3, int N, int Uv)>(2048);

        for (int limbIdx = 0; limbIdx < vxl.Limbs.Count; limbIdx++)
        {
            var limb = vxl.Limbs[limbIdx];
            if (limb.SizeX == 0 || limb.SizeY == 0 || limb.SizeZ == 0) continue;

            float dx = (limb.MaxBounds.X - limb.MinBounds.X) / limb.SizeX;
            float dy = (limb.MaxBounds.Y - limb.MinBounds.Y) / limb.SizeY;
            float dz = (limb.MaxBounds.Z - limb.MinBounds.Z) / limb.SizeZ;
            if (dx <= 0.0001f) dx = 1f;
            if (dy <= 0.0001f) dy = 1f;
            if (dz <= 0.0001f) dz = 1f;

            float[]? mat = hva?.GetFrame0Matrix(limbIdx);

            for (int z = 0; z < limb.SizeZ; z++)
            {
                for (int y = 0; y < limb.SizeY; y++)
                {
                    for (int x = 0; x < limb.SizeX; x++)
                    {
                        byte col = Sample(limb, x, y, z);
                        if (col == 0) continue;

                        float wx0 = limb.MinBounds.X + x * dx;
                        float wx1 = wx0 + dx;
                        float wy0 = limb.MinBounds.Y + y * dy;
                        float wy1 = wy0 + dy;
                        float wz0 = limb.MinBounds.Z + z * dz;
                        float wz1 = wz0 + dz;

                        int uvIdx = col + 1;

                        // +X in Westwood -> +Z in OBJ (Forward)
                        if (Sample(limb, x + 1, y, z) == 0)
                            EmitQuad(vertices, quads, mat, limb.Scale,
                                new(wx1, wy0, wz0), new(wx1, wy1, wz0), new(wx1, wy1, wz1), new(wx1, wy0, wz1), 5, uvIdx);

                        // -X in Westwood -> -Z in OBJ (Back)
                        if (Sample(limb, x - 1, y, z) == 0)
                            EmitQuad(vertices, quads, mat, limb.Scale,
                                new(wx0, wy1, wz0), new(wx0, wy0, wz0), new(wx0, wy0, wz1), new(wx0, wy1, wz1), 6, uvIdx);

                        // +Y in Westwood -> +X in OBJ
                        if (Sample(limb, x, y + 1, z) == 0)
                            EmitQuad(vertices, quads, mat, limb.Scale,
                                new(wx1, wy1, wz0), new(wx0, wy1, wz0), new(wx0, wy1, wz1), new(wx1, wy1, wz1), 1, uvIdx);

                        // -Y in Westwood -> -X in OBJ
                        if (Sample(limb, x, y - 1, z) == 0)
                            EmitQuad(vertices, quads, mat, limb.Scale,
                                new(wx0, wy0, wz0), new(wx1, wy0, wz0), new(wx1, wy0, wz1), new(wx0, wy0, wz1), 2, uvIdx);

                        // +Z in Westwood -> +Y in OBJ (Up)
                        if (Sample(limb, x, y, z + 1) == 0)
                            EmitQuad(vertices, quads, mat, limb.Scale,
                                new(wx0, wy0, wz1), new(wx1, wy0, wz1), new(wx1, wy1, wz1), new(wx0, wy1, wz1), 3, uvIdx);

                        // -Z in Westwood -> -Y in OBJ (Down)
                        if (Sample(limb, x, y, z - 1) == 0)
                            EmitQuad(vertices, quads, mat, limb.Scale,
                                new(wx0, wy1, wz0), new(wx1, wy1, wz0), new(wx1, wy0, wz0), new(wx0, wy0, wz0), 4, uvIdx);
                    }
                }
            }
        }

        Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);
        foreach (var v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        if (vertices.Count == 0)
        {
            min = Vector3.Zero;
            max = Vector3.Zero;
        }
        else if (groundAlignBottom && min.Y != 0f)
        {
            float shiftY = -min.Y;
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                vertices[i] = new Vector3(v.X, v.Y + shiftY, v.Z);
            }
            max.Y += shiftY;
            min.Y = 0f;
        }

        var sb = new StringBuilder(vertices.Count * 48);
        sb.AppendLine($"mtllib {mtlFileName}");
        sb.AppendLine($"usemtl {materialName}");

        var inv = CultureInfo.InvariantCulture;
        foreach (var v in vertices)
        {
            sb.AppendFormat(inv, "v {0:F4} {1:F4} {2:F4}\n", v.X, v.Y, v.Z);
        }
        for (int i = 0; i < uvs.Count; i++)
        {
            // Row 0 (top row of 256x2 PNG) = v 0.75 in bottom-up UV space
            sb.AppendFormat(inv, "vt {0:F6} 0.750000\n", uvs[i]);
        }
        foreach (var n in normals)
        {
            sb.AppendFormat(inv, "vn {0:F2} {1:F2} {2:F2}\n", n.X, n.Y, n.Z);
        }
        foreach (var q in quads)
        {
            sb.AppendFormat(inv, "f {0}/{4}/{5} {1}/{4}/{5} {2}/{4}/{5}\n", q.V0, q.V1, q.V2, q.V3, q.Uv, q.N);
            sb.AppendFormat(inv, "f {0}/{4}/{5} {2}/{4}/{5} {3}/{4}/{5}\n", q.V0, q.V1, q.V2, q.V3, q.Uv, q.N);
        }

        File.WriteAllText(objPath, sb.ToString());
        return (min, max, quads.Count * 2);
    }

    public static void WriteMtl(string mtlPath, string materialName, string textureFileName)
    {
        string content = $"newmtl {materialName}\nKd 1.000 1.000 1.000\nKa 0.200 0.200 0.200\nd 1.0\nmap_Kd {textureFileName}\n";
        File.WriteAllText(mtlPath, content);
    }

    private static byte Sample(VxlLimb limb, int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0 || x >= limb.SizeX || y >= limb.SizeY || z >= limb.SizeZ)
            return 0;
        return limb.ColorGrid[x + limb.SizeX * (y + limb.SizeY * z)];
    }

    private static void EmitQuad(
        List<Vector3> verts,
        List<(int, int, int, int, int, int)> quads,
        float[]? hvaMat,
        float limbScale,
        Vector3 w0, Vector3 w1, Vector3 w2, Vector3 w3,
        int normalIdx1,
        int uvIdx1)
    {
        int baseIdx = verts.Count + 1;
        verts.Add(TransformWestwoodToObj(w0, hvaMat, limbScale));
        verts.Add(TransformWestwoodToObj(w1, hvaMat, limbScale));
        verts.Add(TransformWestwoodToObj(w2, hvaMat, limbScale));
        verts.Add(TransformWestwoodToObj(w3, hvaMat, limbScale));
        quads.Add((baseIdx, baseIdx + 1, baseIdx + 2, baseIdx + 3, normalIdx1, uvIdx1));
    }

    private static Vector3 TransformWestwoodToObj(Vector3 w, float[]? m, float limbScale)
    {
        float wx = w.X, wy = w.Y, wz = w.Z;
        if (m != null && m.Length == 12)
        {
            // Apply HVA 3x4 transform (translation scaled by limbScale in Westwood voxel space)
            float tx = m[0] * wx + m[1] * wy + m[2] * wz + m[3] * limbScale;
            float ty = m[4] * wx + m[5] * wy + m[6] * wz + m[7] * limbScale;
            float tz = m[8] * wx + m[9] * wy + m[10] * wz + m[11] * limbScale;
            wx = tx;
            wy = ty;
            wz = tz;
        }
        // Cyclic right-handed permutation (wx, wy, wz) -> (wy, wz, wx) so:
        // OBJ +Y = Westwood +Z (Up), OBJ +Z = Westwood +X (Forward), OBJ +X = Westwood +Y
        return new Vector3(wy * WorldScale, wz * WorldScale, wx * WorldScale);
    }
}
