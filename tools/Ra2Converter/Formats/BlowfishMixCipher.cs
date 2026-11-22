using System;
using System.Buffers.Binary;
using System.Numerics;

namespace Ra2Converter.Formats;

public sealed class BlowfishMixCipher
{
    private const string WestwoodPublicKeyBase64 = "AihRvNoIbTn85FZRYNZRcT+i6KpU+maCsEqr3Q5q+LDB5tH7Tz2qQ38V";
    private static readonly uint[] InitP;
    private static readonly uint[][] InitS;
    private static readonly BigInteger RsaModulus;
    private static readonly int PlainBlockBytes;

    private readonly uint[] _p = new uint[18];
    private readonly uint[][] _s = new uint[4][];

    static BlowfishMixCipher()
    {
        // Generate exact IEEE/Schneier Blowfish P-array & S-boxes from fractional bits of (pi - 3)
        const int totalWords = 18 + 1024;
        const int totalBits = 32 * (totalWords + 4);
        BigInteger piScaled = 4 * (4 * ArctanReciprocal(5, totalBits) - ArctanReciprocal(239, totalBits));
        BigInteger frac = piScaled - (new BigInteger(3) << totalBits);

        InitP = new uint[18];
        for (int i = 0; i < 18; i++)
        {
            InitP[i] = (uint)((frac >> (totalBits - 32 * (i + 1))) & 0xFFFFFFFFu);
        }

        InitS = new uint[4][];
        for (int s = 0; s < 4; s++)
        {
            InitS[s] = new uint[256];
            for (int k = 0; k < 256; k++)
            {
                int wordIndex = 18 + s * 256 + k;
                InitS[s][k] = (uint)((frac >> (totalBits - 32 * (wordIndex + 1))) & 0xFFFFFFFFu);
            }
        }

        // Decode Westwood ASN.1 INTEGER public modulus (0x02, 0x28, <40-byte big-endian modulus>)
        byte[] der = Convert.FromBase64String(WestwoodPublicKeyBase64);
        ReadOnlySpan<byte> modBigEndian = der.AsSpan(2, der[1]);
        RsaModulus = new BigInteger(modBigEndian, isUnsigned: true, isBigEndian: true);
        int bitLenMinusOne = (int)RsaModulus.GetBitLength() - 1;
        PlainBlockBytes = (bitLenMinusOne - 1) / 8; // 39
    }

    private static BigInteger ArctanReciprocal(int x, int bits)
    {
        BigInteger power = (BigInteger.One << bits) / x;
        BigInteger sum = power;
        BigInteger xSq = x * x;
        int k = 3;
        bool subtract = true;
        while (true)
        {
            power /= xSq;
            BigInteger term = power / k;
            if (term.IsZero) break;
            sum = subtract ? (sum - term) : (sum + term);
            subtract = !subtract;
            k += 2;
        }
        return sum;
    }

    public static byte[] DeriveBlowfishKey(ReadOnlySpan<byte> keySource80)
    {
        if (keySource80.Length < 80)
            throw new ArgumentException("MIX key_source requires 80 bytes.", nameof(keySource80));

        int cipherChunkLen = PlainBlockBytes + 1; // 40
        byte[] derived = new byte[PlainBlockBytes * 2];
        int srcPos = 0;
        int dstPos = 0;
        BigInteger exponent = new BigInteger(0x10001);

        while (srcPos + cipherChunkLen <= 80 && dstPos + PlainBlockBytes <= derived.Length)
        {
            BigInteger c = new BigInteger(keySource80.Slice(srcPos, cipherChunkLen), isUnsigned: true, isBigEndian: false);
            BigInteger m = BigInteger.ModPow(c, exponent, RsaModulus);
            byte[] mBytes = m.ToByteArray(isUnsigned: true, isBigEndian: false);
            int copyLen = Math.Min(PlainBlockBytes, mBytes.Length);
            Buffer.BlockCopy(mBytes, 0, derived, dstPos, copyLen);
            srcPos += cipherChunkLen;
            dstPos += PlainBlockBytes;
        }

        byte[] key56 = new byte[56];
        Buffer.BlockCopy(derived, 0, key56, 0, 56);
        return key56;
    }

    public BlowfishMixCipher(ReadOnlySpan<byte> key)
    {
        Array.Copy(InitP, _p, 18);
        for (int s = 0; s < 4; s++)
        {
            _s[s] = new uint[256];
            Array.Copy(InitS[s], _s[s], 256);
        }

        int j = 0;
        for (int i = 0; i < 18; i++)
        {
            uint word = ((uint)key[j % key.Length] << 24)
                      | ((uint)key[(j + 1) % key.Length] << 16)
                      | ((uint)key[(j + 2) % key.Length] << 8)
                      | key[(j + 3) % key.Length];
            j += 4;
            _p[i] ^= word;
        }

        uint dl = 0, dr = 0;
        for (int i = 0; i < 18; i += 2)
        {
            EncipherBlock(ref dl, ref dr);
            _p[i] = dl;
            _p[i + 1] = dr;
        }

        for (int s = 0; s < 4; s++)
        {
            for (int k = 0; k < 256; k += 2)
            {
                EncipherBlock(ref dl, ref dr);
                _s[s][k] = dl;
                _s[s][k + 1] = dr;
            }
        }
    }

    public void DecryptInPlace(Span<byte> buffer)
    {
        for (int offset = 0; offset + 8 <= buffer.Length; offset += 8)
        {
            uint xl = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(offset, 4));
            uint xr = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(offset + 4, 4));
            DecipherBlock(ref xl, ref xr);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset, 4), xl);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 4, 4), xr);
        }
    }

    private uint Feistel(uint x)
    {
        uint a = _s[0][(x >> 24) & 0xFF];
        uint b = _s[1][(x >> 16) & 0xFF];
        uint c = _s[2][(x >> 8) & 0xFF];
        uint d = _s[3][x & 0xFF];
        return ((a + b) ^ c) + d;
    }

    private void EncipherBlock(ref uint xl, ref uint xr)
    {
        uint l = xl, r = xr;
        for (int i = 0; i < 16; i++)
        {
            l ^= _p[i];
            r ^= Feistel(l);
            (l, r) = (r, l);
        }
        (l, r) = (r, l);
        r ^= _p[16];
        l ^= _p[17];
        xl = l;
        xr = r;
    }

    private void DecipherBlock(ref uint xl, ref uint xr)
    {
        uint l = xl, r = xr;
        for (int i = 17; i > 1; i--)
        {
            l ^= _p[i];
            r ^= Feistel(l);
            (l, r) = (r, l);
        }
        (l, r) = (r, l);
        r ^= _p[1];
        l ^= _p[0];
        xl = l;
        xr = r;
    }
}
