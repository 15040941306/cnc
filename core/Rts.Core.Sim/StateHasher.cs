namespace Rts.Core.Sim
{
    /// <summary>FNV-1a 64-bit over little-endian bytes, independent of platform byte order.</summary>
    public struct StateHasher
    {
        private const ulong OffsetBasis = 0xCBF29CE484222325UL;
        private const ulong Prime = 0x100000001B3UL;

        private ulong _hash;
        private bool _started;

        public ulong Value => _started ? _hash : OffsetBasis;

        public void Add(int value) => AddBytes(unchecked((uint)value), 4);

        public void Add(ulong value) => AddBytes(value, 8);

        private void AddBytes(ulong value, int count)
        {
            if (!_started)
            {
                _hash = OffsetBasis;
                _started = true;
            }

            unchecked
            {
                for (int i = 0; i < count; i++)
                {
                    _hash ^= (value >> (8 * i)) & 0xFF;
                    _hash *= Prime;
                }
            }
        }
    }
}
