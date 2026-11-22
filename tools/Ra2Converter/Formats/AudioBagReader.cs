using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ra2Converter.Formats;

public readonly record struct AudioIdxEntry(
    string Name,
    uint Offset,
    uint Size,
    uint SampleRate,
    uint Flags,
    uint ChunkSize);

public sealed class AudioBagBank
{
    private static readonly int[] ImaIndexTable =
    {
        -1, -1, -1, -1, 2, 4, 6, 8,
        -1, -1, -1, -1, 2, 4, 6, 8
    };

    private static readonly int[] ImaStepTable =
    {
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17,
        19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
        50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
        130, 143, 157, 173, 190, 209, 230, 253, 279, 307,
        337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
        876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066,
        2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358,
        5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
        15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
    };

    private readonly Dictionary<string, AudioIdxEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] _bagData;

    public IReadOnlyDictionary<string, AudioIdxEntry> Entries => _entries;

    public AudioBagBank(ReadOnlySpan<byte> idxData, byte[] bagData)
    {
        _bagData = bagData;
        if (idxData.Length < 12) return;

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(idxData.Slice(0, 4));
        if (magic != 0x41424147u && magic != 0x47414241u) // "GABA"
            return;

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(idxData.Slice(8, 4));
        int off = 12;
        for (uint i = 0; i < count && off + 36 <= idxData.Length; i++, off += 36)
        {
            var nameSpan = idxData.Slice(off, 16);
            int nul = nameSpan.IndexOf((byte)0);
            if (nul >= 0) nameSpan = nameSpan.Slice(0, nul);
            string name = Encoding.ASCII.GetString(nameSpan).Trim();

            uint bagOff = BinaryPrimitives.ReadUInt32LittleEndian(idxData.Slice(off + 16, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(idxData.Slice(off + 20, 4));
            uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(idxData.Slice(off + 24, 4));
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(idxData.Slice(off + 28, 4));
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(idxData.Slice(off + 32, 4));

            if (!string.IsNullOrEmpty(name))
            {
                _entries[name] = new AudioIdxEntry(name, bagOff, size, sampleRate, flags, chunkSize);
            }
        }
    }

    public bool TryExportWav(string soundName, string outputPath, out int sampleRate)
    {
        sampleRate = 22050;
        if (!_entries.TryGetValue(soundName, out var entry)) return false;
        if (entry.Offset + entry.Size > _bagData.Length || entry.Size == 0) return false;

        sampleRate = entry.SampleRate > 0 ? (int)entry.SampleRate : 22050;
        int channels = (entry.Flags & 1) != 0 ? 2 : 1;
        bool isImaAdpcm = (entry.Flags & 8) != 0 || (entry.Flags & 4) != 0;

        ReadOnlySpan<byte> raw = _bagData.AsSpan((int)entry.Offset, (int)entry.Size);
        short[] pcmSamples;

        if (isImaAdpcm)
        {
            int blockAlign = entry.ChunkSize > 0 ? (int)entry.ChunkSize : 512;
            pcmSamples = DecodeImaAdpcm(raw, channels, blockAlign);
        }
        else if ((entry.Flags & 2) != 0) // 16-bit PCM
        {
            pcmSamples = new short[raw.Length / 2];
            for (int i = 0; i < pcmSamples.Length; i++)
            {
                pcmSamples[i] = BinaryPrimitives.ReadInt16LittleEndian(raw.Slice(i * 2, 2));
            }
        }
        else // 8-bit unsigned PCM
        {
            pcmSamples = new short[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                pcmSamples[i] = (short)((raw[i] - 128) << 8);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        WritePcm16Wav(outputPath, pcmSamples, sampleRate, channels);
        return true;
    }

    private static short[] DecodeImaAdpcm(ReadOnlySpan<byte> src, int channels, int blockAlign)
    {
        var samples = new List<short>(src.Length * 2);
        if (blockAlign < 4 * channels) blockAlign = 512;

        for (int blockStart = 0; blockStart < src.Length; blockStart += blockAlign)
        {
            int blockLen = Math.Min(blockAlign, src.Length - blockStart);
            if (blockLen < 4 * channels) break;
            var block = src.Slice(blockStart, blockLen);

            if (channels == 1)
            {
                int predictor = BinaryPrimitives.ReadInt16LittleEndian(block.Slice(0, 2));
                int stepIdx = Math.Clamp((int)block[2], 0, 88);
                samples.Add((short)predictor);

                for (int i = 4; i < block.Length; i++)
                {
                    byte b = block[i];
                    samples.Add(DecodeImaNibble(b & 0x0F, ref predictor, ref stepIdx));
                    samples.Add(DecodeImaNibble((b >> 4) & 0x0F, ref predictor, ref stepIdx));
                }
            }
            else
            {
                int predL = BinaryPrimitives.ReadInt16LittleEndian(block.Slice(0, 2));
                int idxL = Math.Clamp((int)block[2], 0, 88);
                int predR = BinaryPrimitives.ReadInt16LittleEndian(block.Slice(4, 2));
                int idxR = Math.Clamp((int)block[6], 0, 88);
                samples.Add((short)predL);
                samples.Add((short)predR);

                // MS IMA ADPCM stereo interleaves 4 bytes (8 samples) Left, 4 bytes (8 samples) Right
                short[] left8 = new short[8];
                short[] right8 = new short[8];
                for (int pos = 8; pos + 8 <= block.Length; pos += 8)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        byte bl = block[pos + k];
                        left8[k * 2] = DecodeImaNibble(bl & 0x0F, ref predL, ref idxL);
                        left8[k * 2 + 1] = DecodeImaNibble((bl >> 4) & 0x0F, ref predL, ref idxL);

                        byte br = block[pos + 4 + k];
                        right8[k * 2] = DecodeImaNibble(br & 0x0F, ref predR, ref idxR);
                        right8[k * 2 + 1] = DecodeImaNibble((br >> 4) & 0x0F, ref predR, ref idxR);
                    }
                    for (int s = 0; s < 8; s++)
                    {
                        samples.Add(left8[s]);
                        samples.Add(right8[s]);
                    }
                }
            }
        }

        return samples.ToArray();
    }

    private static short DecodeImaNibble(int nibble, ref int predictor, ref int stepIndex)
    {
        int step = ImaStepTable[stepIndex];
        int diff = step >> 3;
        if ((nibble & 4) != 0) diff += step;
        if ((nibble & 2) != 0) diff += step >> 1;
        if ((nibble & 1) != 0) diff += step >> 2;
        if ((nibble & 8) != 0) predictor -= diff;
        else predictor += diff;

        predictor = Math.Clamp(predictor, short.MinValue, short.MaxValue);
        stepIndex = Math.Clamp(stepIndex + ImaIndexTable[nibble], 0, 88);
        return (short)predictor;
    }

    private static void WritePcm16Wav(string filePath, ReadOnlySpan<short> samples, int sampleRate, int channels)
    {
        using var fs = File.Create(filePath);
        using var bw = new BinaryWriter(fs);
        int dataBytes = samples.Length * 2;
        bw.Write("RIFF"u8);
        bw.Write(36 + dataBytes);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * 2);
        bw.Write((short)(channels * 2));
        bw.Write((short)16);
        bw.Write("data"u8);
        bw.Write(dataBytes);
        for (int i = 0; i < samples.Length; i++)
        {
            bw.Write(samples[i]);
        }
    }
}
