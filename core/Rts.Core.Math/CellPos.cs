using System;

namespace Rts.Core.Math
{
    public readonly struct CellPos : IEquatable<CellPos>
    {
        public readonly int X;
        public readonly int Y;

        public CellPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static CellPos operator +(CellPos a, CellPos b) => new CellPos(a.X + b.X, a.Y + b.Y);
        public static CellPos operator -(CellPos a, CellPos b) => new CellPos(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(CellPos a, CellPos b) => a.Equals(b);
        public static bool operator !=(CellPos a, CellPos b) => !a.Equals(b);

        public bool Equals(CellPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is CellPos other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public override string ToString() => $"({X},{Y})";
    }
}
