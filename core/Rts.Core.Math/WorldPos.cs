using System;

namespace Rts.Core.Math
{
    public readonly struct WorldPos : IEquatable<WorldPos>
    {
        public const int UnitsPerCell = 256;

        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public WorldPos(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static WorldPos CenterOf(CellPos cell, int z = 0) =>
            new WorldPos(cell.X * UnitsPerCell + UnitsPerCell / 2, cell.Y * UnitsPerCell + UnitsPerCell / 2, z);

        public CellPos ToCell() => new CellPos(FloorDiv(X, UnitsPerCell), FloorDiv(Y, UnitsPerCell));

        public static WorldPos operator +(WorldPos a, WorldPos b) => new WorldPos(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static WorldPos operator -(WorldPos a, WorldPos b) => new WorldPos(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static bool operator ==(WorldPos a, WorldPos b) => a.Equals(b);
        public static bool operator !=(WorldPos a, WorldPos b) => !a.Equals(b);

        public bool Equals(WorldPos other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldPos other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397 ^ Y) * 397 ^ Z);
        public override string ToString() => $"({X},{Y},{Z})";

        private static int FloorDiv(int a, int b)
        {
            int q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }
    }
}
