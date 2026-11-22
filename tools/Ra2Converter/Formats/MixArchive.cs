using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace Ra2Converter.Formats;

public readonly record struct MixEntry(uint Id, uint Offset, uint Size);

public sealed class MixArchive
{
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    public string Name { get; }
    private readonly byte[] _data;
    private readonly int _bodyOffset;
    private readonly Dictionary<uint, MixEntry> _entries = new();
    private readonly Dictionary<uint, string> _knownNames = new();

    public IReadOnlyDictionary<uint, MixEntry> Entries => _entries;
    public IReadOnlyDictionary<uint, string> KnownNames => _knownNames;

    private MixArchive(string name, byte[] data, int bodyOffset, List<MixEntry> list)
    {
        Name = name;
        _data = data;
        _bodyOffset = bodyOffset;
        foreach (var entry in list)
        {
            _entries[entry.Id] = entry;
        }
        TryReadEmbeddedXccDatabase();
    }

    public static MixArchive Open(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        return Parse(Path.GetFileName(filePath), bytes);
    }

    public static MixArchive Parse(string name, byte[] data)
    {
        if (data.Length < 6)
            throw new InvalidDataException($"MIX '{name}' is too small ({data.Length} bytes).");

        ushort firstWord = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        if (firstWord != 0)
        {
            // Classic TD/RA1 unflagged header (e.g. THEME.MIX, thememd.mix)
            return ReadPlainIndex(name, data, indexStart: 0);
        }

        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2, 2));
        bool isEncrypted = (flags & 0x0002) != 0;
        if (!isEncrypted)
        {
            return ReadPlainIndex(name, data, indexStart: 4);
        }

        if (data.Length < 4 + 80 + 8)
            throw new InvalidDataException($"Encrypted MIX '{name}' is truncated.");

        byte[] bfKey = BlowfishMixCipher.DeriveBlowfishKey(data.AsSpan(4, 80));
        var cipher = new BlowfishMixCipher(bfKey);

        Span<byte> firstBlock = stackalloc byte[8];
        data.AsSpan(84, 8).CopyTo(firstBlock);
        cipher.DecryptInPlace(firstBlock);

        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(firstBlock.Slice(0, 2));
        int rawHeaderBytes = 6 + count * 12;
        int alignedHeaderBytes = ((rawHeaderBytes + 7) / 8) * 8;
        if (84 + alignedHeaderBytes > data.Length)
            throw new InvalidDataException($"Encrypted MIX '{name}' header exceeds file length (count={count}).");

        byte[] decryptedHeader = new byte[alignedHeaderBytes];
        Buffer.BlockCopy(data, 84, decryptedHeader, 0, alignedHeaderBytes);
        cipher.DecryptInPlace(decryptedHeader);

        var entries = new List<MixEntry>(count);
        for (int i = 0; i < count; i++)
        {
            int off = 6 + i * 12;
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(decryptedHeader.AsSpan(off, 4));
            uint relOffset = BinaryPrimitives.ReadUInt32LittleEndian(decryptedHeader.AsSpan(off + 4, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(decryptedHeader.AsSpan(off + 8, 4));
            entries.Add(new MixEntry(id, relOffset, size));
        }

        int bodyOffset = 84 + alignedHeaderBytes;
        return new MixArchive(name, data, bodyOffset, entries);
    }

    private static MixArchive ReadPlainIndex(string name, byte[] data, int indexStart)
    {
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(indexStart, 2));
        int headerBytes = 6 + count * 12;
        if (indexStart + headerBytes > data.Length)
            throw new InvalidDataException($"Plain MIX '{name}' header exceeds file length (count={count}).");

        var entries = new List<MixEntry>(count);
        for (int i = 0; i < count; i++)
        {
            int off = indexStart + 6 + i * 12;
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off, 4));
            uint relOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 4, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 8, 4));
            entries.Add(new MixEntry(id, relOffset, size));
        }

        return new MixArchive(name, data, indexStart + headerBytes, entries);
    }

    public bool Contains(string filename)
    {
        return TryGetEntry(filename, out _);
    }

    public bool TryGetEntry(string filename, out MixEntry entry)
    {
        uint tsHash = ComputeTsCrc(filename);
        if (_entries.TryGetValue(tsHash, out entry)) return true;

        uint classicHash = ComputeClassicHash(filename);
        return _entries.TryGetValue(classicHash, out entry);
    }

    public byte[]? TryReadBytes(string filename)
    {
        if (!TryGetEntry(filename, out var entry)) return null;
        return ReadEntryBytes(entry);
    }

    public byte[] ReadEntryBytes(MixEntry entry)
    {
        long start = (long)_bodyOffset + entry.Offset;
        long end = start + entry.Size;
        if (start < 0 || end > _data.Length)
            return Array.Empty<byte>();

        byte[] slice = new byte[entry.Size];
        Buffer.BlockCopy(_data, (int)start, slice, 0, (int)entry.Size);
        return slice;
    }

    public void RegisterCandidateName(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return;
        uint ts = ComputeTsCrc(filename);
        if (_entries.ContainsKey(ts)) _knownNames[ts] = filename.ToLowerInvariant();
        uint cl = ComputeClassicHash(filename);
        if (_entries.ContainsKey(cl)) _knownNames[cl] = filename.ToLowerInvariant();
    }

    private void TryReadEmbeddedXccDatabase()
    {
        const string dbFile = "local mix database.dat";
        byte[]? db = TryReadBytes(dbFile);
        if (db == null || db.Length < 52) return;
        RegisterCandidateName(dbFile);

        // XCC local mix database starts with 32-byte "XCC by Olaf van der Spek..." header + 20 bytes metadata,
        // followed by null-terminated filenames
        int pos = 52;
        while (pos < db.Length)
        {
            int end = Array.IndexOf(db, (byte)0, pos);
            if (end < 0) break;
            if (end > pos)
            {
                string candidate = Encoding.ASCII.GetString(db, pos, end - pos);
                RegisterCandidateName(candidate);
            }
            pos = end + 1;
        }
    }

    public static uint ComputeTsCrc(string filename)
    {
        string upper = filename.ToUpperInvariant();
        int len = upper.Length;
        int alignedDown = len & ~3;
        int paddedLen = (len + 3) & ~3;
        Span<byte> buf = paddedLen <= 256 ? stackalloc byte[paddedLen] : new byte[paddedLen];

        for (int i = 0; i < len; i++)
        {
            buf[i] = (byte)upper[i];
        }

        if ((len & 3) != 0)
        {
            buf[len] = (byte)(len - alignedDown);
            byte fill = buf[alignedDown];
            for (int i = len + 1; i < paddedLen; i++)
            {
                buf[i] = fill;
            }
        }

        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < paddedLen; i++)
        {
            crc = Crc32Table[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8);
        }
        return ~crc;
    }

    public static uint ComputeClassicHash(string filename)
    {
        string upper = filename.ToUpperInvariant();
        int len = upper.Length;
        int paddedLen = (len + 3) & ~3;
        Span<byte> buf = paddedLen <= 256 ? stackalloc byte[paddedLen] : new byte[paddedLen];
        buf.Clear();
        for (int i = 0; i < len; i++) buf[i] = (byte)upper[i];

        uint id = 0;
        for (int i = 0; i < paddedLen; i += 4)
        {
            uint group = BinaryPrimitives.ReadUInt32LittleEndian(buf.Slice(i, 4));
            id = BitOperations.RotateLeft(id, 1) + group;
        }
        return id;
    }

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
            }
            table[i] = c;
        }
        return table;
    }
}
