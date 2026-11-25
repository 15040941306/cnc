using System;
using System.Collections.Generic;
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
        Beach = 4,
        Rough = 5,
        OutOfBounds = 6
    }

    public class GridCell
    {
        public int X;
        public int Y;
        public int HeightLevel;
        public int RampType; // 0..20 exact Westwood SlopeType
        public TerrainKind Terrain;
        public int OreType;    // 0 = none, 1 = gold, 2 = gem
        public int OreAmount;  // 0..12
        public bool HasBridge;
        public bool HasProp;         // Any [Terrain] prop (blocks building placement)
        public bool HasBlockingProp; // Tree or Wall (also blocks ground vehicles)
        public int OccupiedByBuildingId; // 0 = empty

        public bool IsLandPassable
        {
            get
            {
                if (OccupiedByBuildingId != 0 || HasBlockingProp) return false;
                if (Terrain == TerrainKind.OutOfBounds || Terrain == TerrainKind.Rock) return false;
                if (Terrain == TerrainKind.Water && !HasBridge) return false;
                return true;
            }
        }

        public bool IsBuildable
        {
            get
            {
                return OccupiedByBuildingId == 0
                    && !HasProp
                    && !HasBridge
                    && RampType == 0
                    && (Terrain == TerrainKind.Clear || Terrain == TerrainKind.Road || Terrain == TerrainKind.Rough)
                    && OreAmount == 0;
            }
        }

        public void GetWorldCornerLevels(out int tl, out int tr, out int br, out int bl, out int split)
        {
            Ra2RampGeometry.GetCornerOffsets(RampType, out int otl, out int otr, out int obr, out int obl, out split);
            tl = HeightLevel + otl;
            tr = HeightLevel + otr;
            br = HeightLevel + obr;
            bl = HeightLevel + obl;
        }
    }

    public class GridMap
    {
        public readonly int Width;
        public readonly int Height;
        public readonly string DisplayName;
        public readonly string Theater;
        public readonly bool IsNativeIsometricGrid;
        public readonly int MinRx;
        public readonly int MaxRx;
        public readonly int MinRy;
        public readonly int MaxRy;
        public readonly int FullMapWidth;
        public readonly int LocalLeft;
        public readonly int LocalTop;
        public readonly int LocalWidth;
        public readonly int LocalHeight;
        public readonly string BakedTerrainTextureRelPath;
        public readonly int BakedTexWidth;
        public readonly int BakedTexHeight;
        public readonly int BakedOriginPx;
        public readonly int BakedOriginPy;
        public readonly List<MapPropData> Props;
        public readonly List<MapStructureData> PreplacedStructures;

        private readonly GridCell[,] _cells;

        public GridMap(MapBundleData bundle)
        {
            Width = Math.Max(16, bundle.Width);
            Height = Math.Max(16, bundle.Height);
            DisplayName = bundle.DisplayName ?? bundle.MapId;
            Theater = bundle.Theater ?? "TEMPERATE";
            IsNativeIsometricGrid = bundle.IsNativeIsometricGrid;
            MinRx = bundle.MinRx;
            MaxRx = bundle.MaxRx;
            MinRy = bundle.MinRy;
            MaxRy = bundle.MaxRy;
            FullMapWidth = bundle.FullMapWidth;
            LocalLeft = bundle.LocalLeft;
            LocalTop = bundle.LocalTop;
            LocalWidth = bundle.LocalWidth > 0 ? bundle.LocalWidth : Width;
            LocalHeight = bundle.LocalHeight > 0 ? bundle.LocalHeight : Height;
            BakedTerrainTextureRelPath = bundle.BakedTerrainTextureRelPath ?? "";
            BakedTexWidth = bundle.BakedTexWidth;
            BakedTexHeight = bundle.BakedTexHeight;
            BakedOriginPx = bundle.BakedOriginPx;
            BakedOriginPy = bundle.BakedOriginPy;
            Props = bundle.Props ?? new List<MapPropData>();
            PreplacedStructures = bundle.PreplacedStructures ?? new List<MapStructureData>();

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
                        cell.HasBridge = c.HasBridge;
                    }
                }
            }

            // Apply [Terrain] & wall props (Trees/Walls block movement & building; Streetlights block building only)
            for (int i = 0; i < Props.Count; i++)
            {
                var p = Props[i];
                if (!InBounds(p.X, p.Y)) continue;
                var cell = _cells[p.X, p.Y];
                cell.HasProp = true;
                if (p.Kind != 2) // Kind 2 = Urban Streetlight/Traffic Light (vehicles can pass!)
                {
                    cell.HasBlockingProp = true;
                }
            }

            // Mark pre-placed neutral/civilian structure footprints
            for (int i = 0; i < PreplacedStructures.Count; i++)
            {
                var s = PreplacedStructures[i];
                SetBuildingFootprint(new CellCoord(s.X, s.Y), s.FootprintX, s.FootprintY, 90000 + i);
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

        public float SampleExactCellLevel(float gx, float gy)
        {
            int cx = Math.Clamp((int)Math.Floor(gx), 0, Width - 1);
            int cy = Math.Clamp((int)Math.Floor(gy), 0, Height - 1);
            var cell = _cells[cx, cy];
            float fx = Math.Clamp(gx - cx, 0f, 1f);
            float fy = Math.Clamp(gy - cy, 0f, 1f);
            return Ra2RampGeometry.SampleCellHeight(cell.HeightLevel, cell.RampType, fx, fy);
        }

        public bool CanStep(int fromX, int fromY, int toX, int toY)
        {
            if (!InBounds(toX, toY) || !InBounds(fromX, fromY)) return false;
            var src = _cells[fromX, fromY];
            var dst = _cells[toX, toY];
            if (!dst.IsLandPassable) return false;

            int dx = toX - fromX;
            int dy = toY - fromY;

            // Check shared-edge corner elevation continuity (exact RA2 20-ramp & bridge traversal!)
            src.GetWorldCornerLevels(out int sTL, out int sTR, out int sBR, out int sBL, out _);
            dst.GetWorldCornerLevels(out int dTL, out int dTR, out int dBR, out int dBL, out _);

            int maxEdgeDiff;
            if (dx == 0 && dy == 1) // North (+gy): src.(TL,TR) == dst.(BL,BR)
                maxEdgeDiff = Math.Max(Math.Abs(sTL - dBL), Math.Abs(sTR - dBR));
            else if (dx == 0 && dy == -1) // South (-gy): src.(BL,BR) == dst.(TL,TR)
                maxEdgeDiff = Math.Max(Math.Abs(sBL - dTL), Math.Abs(sBR - dTR));
            else if (dx == 1 && dy == 0) // East (+gx): src.(TR,BR) == dst.(TL,BL)
                maxEdgeDiff = Math.Max(Math.Abs(sTR - dTL), Math.Abs(sBR - dBL));
            else if (dx == -1 && dy == 0) // West (-gx): src.(TL,BL) == dst.(TR,BR)
                maxEdgeDiff = Math.Max(Math.Abs(sTL - dTR), Math.Abs(sBL - dBR));
            else // Diagonal: check shared vertex + orthogonal corner-cut prevention
            {
                if (!CanStep(fromX, fromY, fromX + dx, fromY) || !CanStep(fromX, fromY, fromX, fromY + dy))
                    return false;
                if (dx == 1 && dy == 1) maxEdgeDiff = Math.Abs(sTR - dBL);
                else if (dx == 1 && dy == -1) maxEdgeDiff = Math.Abs(sBR - dTL);
                else if (dx == -1 && dy == 1) maxEdgeDiff = Math.Abs(sTL - dBR);
                else maxEdgeDiff = Math.Abs(sBL - dTR);
            }

            int allowedTolerance = (src.HasBridge || dst.HasBridge) ? 4
                : (src.RampType != 0 || dst.RampType != 0) ? 1
                : 0;
            return maxEdgeDiff <= allowedTolerance;
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

        public CellCoord FindNearestPassable(CellCoord origin, int maxRadius = 12)
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
