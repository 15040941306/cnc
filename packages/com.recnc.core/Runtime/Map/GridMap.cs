using System;
using ReCnc.Core.Data;

namespace ReCnc.Core.Map
{
    [Serializable]
    public struct CellCoord : IEquatable<CellCoord>
    {
        public int X;
        public int Y;

        public CellCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(CellCoord other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is CellCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (X * 397) ^ Y;
        }

        public static bool operator ==(CellCoord a, CellCoord b) { return a.Equals(b); }
        public static bool operator !=(CellCoord a, CellCoord b) { return !a.Equals(b); }

        public float DistanceTo(CellCoord other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + ")";
        }
    }

    [Serializable]
    public struct SimVec2
    {
        public float X;
        public float Y;

        public SimVec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float Length
        {
            get { return (float)Math.Sqrt(X * X + Y * Y); }
        }

        public float DistanceTo(SimVec2 other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public CellCoord ToCell()
        {
            return new CellCoord((int)Math.Floor(X), (int)Math.Floor(Y));
        }

        public static SimVec2 FromCellCenter(CellCoord cell)
        {
            return new SimVec2(cell.X + 0.5f, cell.Y + 0.5f);
        }
    }

    public enum TerrainKind
    {
        Clear = 0,
        Road = 1,
        Water = 2,
        Rock = 3,
        Beach = 4
    }

    public class GridCell
    {
        public int X;
        public int Y;
        public int HeightLevel;
        public int RampType;
        public TerrainKind Terrain;
        public int OreType;    // 0 = none, 1 = gold, 2 = gem
        public int OreAmount;  // 0..12
        public int OccupiedByBuildingId; // 0 = empty

        public bool IsLandPassable
        {
            get
            {
                return OccupiedByBuildingId == 0
                    && Terrain != TerrainKind.Water
                    && Terrain != TerrainKind.Rock;
            }
        }

        public bool IsBuildable
        {
            get
            {
                return OccupiedByBuildingId == 0
                    && RampType == 0
                    && (Terrain == TerrainKind.Clear || Terrain == TerrainKind.Road)
                    && OreAmount == 0;
            }
        }
    }

    public class GridMap
    {
        public readonly int Width;
        public readonly int Height;
        public readonly string DisplayName;
        public readonly string Theater;
        private readonly GridCell[,] _cells;

        public GridMap(MapBundleData bundle)
        {
            Width = Math.Max(16, bundle.Width);
            Height = Math.Max(16, bundle.Height);
            DisplayName = bundle.DisplayName ?? bundle.MapId;
            Theater = bundle.Theater ?? "TEMPERATE";
            _cells = new GridCell[Width, Height];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    _cells[x, y] = new GridCell
                    {
                        X = x,
                        Y = y,
                        HeightLevel = 2,
                        Terrain = TerrainKind.Clear
                    };
                }
            }

            if (bundle.Cells != null)
            {
                for (int i = 0; i < bundle.Cells.Count; i++)
                {
                    var c = bundle.Cells[i];
                    if (InBounds(c.X, c.Y))
                    {
                        var cell = _cells[c.X, c.Y];
                        cell.HeightLevel = c.HeightLevel;
                        cell.RampType = c.RampType;
                        cell.Terrain = (TerrainKind)c.TerrainType;
                        cell.OreType = c.OreType;
                        cell.OreAmount = c.OreAmount;
                    }
                }
            }
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public bool InBounds(CellCoord coord)
        {
            return InBounds(coord.X, coord.Y);
        }

        public GridCell GetCell(int x, int y)
        {
            return InBounds(x, y) ? _cells[x, y] : null;
        }

        public GridCell GetCell(CellCoord coord)
        {
            return GetCell(coord.X, coord.Y);
        }

        public float SampleWorldElevation(float gridX, float gridY)
        {
            int cx = Math.Clamp((int)Math.Floor(gridX), 0, Width - 1);
            int cy = Math.Clamp((int)Math.Floor(gridY), 0, Height - 1);
            return _cells[cx, cy].HeightLevel * 0.45f;
        }

        public bool CanStep(int fromX, int fromY, int toX, int toY)
        {
            if (!InBounds(toX, toY) || !InBounds(fromX, fromY)) return false;
            var src = _cells[fromX, fromY];
            var dst = _cells[toX, toY];
            if (!dst.IsLandPassable) return false;

            int dh = Math.Abs(dst.HeightLevel - src.HeightLevel);
            if (dh > 1) return false;
            if (dh == 1 && src.RampType == 0 && dst.RampType == 0) return false;

            // Prevent diagonal corner cutting through buildings/cliffs
            int dx = toX - fromX;
            int dy = toY - fromY;
            if (dx != 0 && dy != 0)
            {
                if (!_cells[fromX + dx, fromY].IsLandPassable || !_cells[fromX, fromY + dy].IsLandPassable)
                {
                    return false;
                }
            }
            return true;
        }

        public bool CanPlaceBuildingFootprint(CellCoord topLeft, int sizeX, int sizeY)
        {
            if (!InBounds(topLeft.X, topLeft.Y) || !InBounds(topLeft.X + sizeX - 1, topLeft.Y + sizeY - 1))
            {
                return false;
            }
            int baseHeight = _cells[topLeft.X, topLeft.Y].HeightLevel;
            for (int dy = 0; dy < sizeY; dy++)
            {
                for (int dx = 0; dx < sizeX; dx++)
                {
                    var cell = _cells[topLeft.X + dx, topLeft.Y + dy];
                    if (!cell.IsBuildable || cell.HeightLevel != baseHeight)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public void SetBuildingFootprint(CellCoord topLeft, int sizeX, int sizeY, int buildingId)
        {
            for (int dy = 0; dy < sizeY; dy++)
            {
                for (int dx = 0; dx < sizeX; dx++)
                {
                    int x = topLeft.X + dx;
                    int y = topLeft.Y + dy;
                    if (InBounds(x, y))
                    {
                        _cells[x, y].OccupiedByBuildingId = buildingId;
                    }
                }
            }
        }

        public CellCoord FindNearestPassable(CellCoord origin, int maxRadius = 10)
        {
            var startCell = GetCell(origin);
            if (startCell != null && startCell.IsLandPassable) return origin;

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        var candidate = new CellCoord(origin.X + dx, origin.Y + dy);
                        var cell = GetCell(candidate);
                        if (cell != null && cell.IsLandPassable)
                        {
                            return candidate;
                        }
                    }
                }
            }
            return origin;
        }

        public bool TryFindNearestOre(CellCoord origin, int maxRadius, out CellCoord oreCoord)
        {
            float bestDistSq = float.MaxValue;
            oreCoord = origin;
            bool found = false;

            int minX = Math.Max(0, origin.X - maxRadius);
            int maxX = Math.Min(Width - 1, origin.X + maxRadius);
            int minY = Math.Max(0, origin.Y - maxRadius);
            int maxY = Math.Min(Height - 1, origin.Y + maxRadius);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = _cells[x, y];
                    if (cell.OreAmount > 0 && cell.IsLandPassable)
                    {
                        float dx = x - origin.X;
                        float dy = y - origin.Y;
                        float d2 = dx * dx + dy * dy;
                        // Slightly prefer high-value gems when close
                        if (cell.OreType == 2) d2 *= 0.75f;
                        if (d2 < bestDistSq)
                        {
                            bestDistSq = d2;
                            oreCoord = new CellCoord(x, y);
                            found = true;
                        }
                    }
                }
            }
            return found;
        }
    }
}
