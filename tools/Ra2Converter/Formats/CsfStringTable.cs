using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ra2Converter.Formats;

public static class CsfStringTable
{
    public static Dictionary<string, string> Parse(ReadOnlySpan<byte> data)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (data.Length < 24) return result;

        // Magic: " FSC"
        if (data[0] != (byte)' ' || data[1] != (byte)'F' || data[2] != (byte)'S' || data[3] != (byte)'C')
            throw new InvalidDataException("Invalid CSF header magic.");

        uint numLabels = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
        int offset = 24;

        for (uint i = 0; i < numLabels && offset + 12 <= data.Length; i++)
        {
            if (data[offset] != (byte)' ' || data[offset + 1] != (byte)'L' || data[offset + 2] != (byte)'B' || data[offset + 3] != (byte)'L')
                break;

            uint numPairs = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
            int labelLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 8, 4));
            offset += 12;
            if (labelLen < 0 || offset + labelLen > data.Length) break;

            string label = Encoding.ASCII.GetString(data.Slice(offset, labelLen));
            offset += labelLen;

            for (uint p = 0; p < numPairs && offset + 8 <= data.Length; p++)
            {
                string tag = Encoding.ASCII.GetString(data.Slice(offset, 4));
                int charCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
                offset += 8;

                int byteLen = charCount * 2;
                if (charCount < 0 || offset + byteLen > data.Length)
                {
                    offset = data.Length;
                    break;
                }

                byte[] decodedUtf16 = new byte[byteLen];
                for (int b = 0; b < byteLen; b++)
                {
                    decodedUtf16[b] = (byte)~data[offset + b];
                }
                offset += byteLen;

                string value = Encoding.Unicode.GetString(decodedUtf16);
                if (p == 0 && !string.IsNullOrWhiteSpace(label))
                {
                    result[label] = value;
                }

                if (tag == "WRTS" || tag == "STRW")
                {
                    if (offset + 4 > data.Length) break;
                    int extraLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
                    offset += 4 + Math.Max(0, extraLen);
                }
            }
        }

        return result;
    }
}
