using System;
using System.Collections.Generic;

namespace ReCnc.Core.Data
{
    [Serializable]
    public class SkirmishMapsCatalogData
    {
        public List<SkirmishMapEntryData> Maps = new List<SkirmishMapEntryData>();
    }

    [Serializable]
    public class SkirmishMapEntryData
    {
        public string MapId;
        public string DisplayName;
        public string Theater;
        public int Width;
        public int Height;
        public int PlayableWidth;
        public int PlayableHeight;
        public int MinPlayers = 2;
        public int MaxPlayers = 2;
        public string JsonRelPath;
        public string PreviewAssetPath;
        public bool IsOfficialRa2Map;
    }

    [Serializable]
    public class MapBundleData
    {
        public string MapId = "demo_skirmish";
        public string DisplayName = "双雄争霸 (3D 重制遭遇战)";
        public string Theater = "TEMPERATE";
        public bool IsNativeIsometricGrid;
        public int Width = 64;
        public int Height = 64;

        // Native RA2 isometric projection parameters for exact 1:1 UV & boundary alignment
        public int MinRx;
        public int MaxRx;
        public int MinRy;
        public int MaxRy;
        public int FullMapWidth;
        public int LocalLeft;
        public int LocalTop;
        public int LocalWidth;
        public int LocalHeight;

        // Full-resolution baked original .TMP ground/cliff texture in StreamingAssets/Ra2Converted/
        public string BakedTerrainTextureRelPath;
        public int BakedTexWidth;
        public int BakedTexHeight;
        public int BakedOriginPx;
        public int BakedOriginPy;

        public List<WaypointData> Waypoints = new List<WaypointData>();
        public List<MapCellData> Cells = new List<MapCellData>();
        public List<MapPropData> Props = new List<MapPropData>();
        public List<MapStructureData> PreplacedStructures = new List<MapStructureData>();
    }

    [Serializable]
    public class WaypointData
    {
        public int Index;
        public int X;
        public int Y;
    }

    [Serializable]
    public class MapCellData
    {
        public int X;
        public int Y;
        public int HeightLevel; // Exact RA2 z-level (0..15, +4 for high bridges)
        public int RampType;    // Exact Westwood SlopeType 0..20
        public int TerrainType; // 0=Clear, 1=Road, 2=Water, 3=Rock/Cliff, 4=Beach, 5=Rough, 6=OutOfBounds
        public int OreType;     // 0=None, 1=Gold Ore (Riparius/Vinifera), 2=Gems (Cruentus)
        public int OreAmount;   // 0..12
        public bool HasBridge;
    }

    [Serializable]
    public class MapPropData
    {
        public string TypeId;   // e.g. TREE01, TIBTRE01, LT01, GAWALL
        public int X;
        public int Y;
        public byte Kind;       // 0=Tree, 1=OreTree, 2=StreetLight, 3=Wall/Rock
    }

    [Serializable]
    public class MapStructureData
    {
        public string TypeId;   // e.g. CATEXS02, CAWASH05, CAOILD, CABHUT
        public string Name;
        public int X;
        public int Y;
        public int FootprintX = 2;
        public int FootprintY = 2;
        public int MaxHp = 800;
    }

    /// <summary>
    /// Exact Westwood Tiberian Sun / Red Alert 2 20-slope (SlopeType 0..20) corner elevation table.
    /// Corner order in isometric diamond space:
    ///   TL = Top    (rx,   ry)   = Unity (gx,   gy+1)
    ///   TR = Right  (rx+1, ry)   = Unity (gx+1, gy+1)
    ///   BR = Bottom (rx+1, ry+1) = Unity (gx+1, gy)
    ///   BL = Left   (rx,   ry+1) = Unity (gx,   gy)
    /// Elevation offsets are in RA2 Level units (0 = Low, 1 = Half/1-step, 2 = Full/2-step).
    /// </summary>
    public static class Ra2RampGeometry
    {
        // (tl, tr, br, bl, splitMode: 0=Flat, 1=SplitX [TL-BL-TR & TR-BL-BR], 2=SplitY [TL-BR-TR & TL-BL-BR])
        private static readonly byte[,] Table = new byte[21, 5]
        {
            { 0, 0, 0, 0, 0 }, //  0: Flat
            // 1..4: Two adjacent corners raised by +1 level
            { 0, 1, 1, 0, 0 }, //  1: TR, BR raised (slopes up toward Bottom-Right / +rx)
            { 0, 0, 1, 1, 0 }, //  2: BR, BL raised (slopes up toward Bottom-Left  / +ry)
            { 1, 0, 0, 1, 0 }, //  3: TL, BL raised (slopes up toward Top-Left     / -rx)
            { 1, 1, 0, 0, 0 }, //  4: TL, TR raised (slopes up toward Top-Right    / -ry)
            // 5..8: Single corner raised by +1 level
            { 0, 0, 1, 0, 1 }, //  5: BR raised
            { 0, 0, 0, 1, 2 }, //  6: BL raised
            { 1, 0, 0, 0, 1 }, //  7: TL raised
            { 0, 1, 0, 0, 2 }, //  8: TR raised
            // 9..12: Three corners raised by +1 level
            { 0, 1, 1, 1, 1 }, //  9: all except TL raised
            { 1, 0, 1, 1, 2 }, // 10: all except TR raised
            { 1, 1, 0, 1, 1 }, // 11: all except BR raised
            { 1, 1, 1, 0, 2 }, // 12: all except BL raised
            // 13..16: Steep diagonal full slope (mid corners +1, far corner +2)
            { 0, 1, 2, 1, 0 }, // 13: TL=0, TR=1, BR=2, BL=1
            { 1, 0, 1, 2, 0 }, // 14: TR=0, BR=1, BL=2, TL=1
            { 2, 1, 0, 1, 0 }, // 15: BR=0, BL=1, TL=2, TR=1
            { 1, 2, 1, 0, 0 }, // 16: BL=0, TL=1, TR=2, BR=1
            // 17..20: Saddle (two opposite corners raised by +1)
            { 0, 1, 0, 1, 2 }, // 17: TR, BL raised (split Y)
            { 1, 0, 1, 0, 2 }, // 18: TL, BR raised (split Y)
            { 0, 1, 0, 1, 1 }, // 19: TR, BL raised (split X)
            { 1, 0, 1, 0, 1 }, // 20: TL, BR raised (split X)
        };

        public static void GetCornerOffsets(int rampType, out int tl, out int tr, out int br, out int bl, out int split)
        {
            if (rampType < 0 || rampType > 20) rampType = 0;
            tl = Table[rampType, 0];
            tr = Table[rampType, 1];
            br = Table[rampType, 2];
            bl = Table[rampType, 3];
            split = Table[rampType, 4];
        }

        public static float SampleCellHeight(int baseLevel, int rampType, float fracX, float fracY)
        {
            // fracX in [0..1] along +gx (+rx), fracY in [0..1] along +gy (-ry):
            // (0,1)=TL, (1,1)=TR, (1,0)=BR, (0,0)=BL
            GetCornerOffsets(rampType, out int tl, out int tr, out int br, out int bl, out _);
            float hBottom = bl + (br - bl) * fracX;
            float hTop = tl + (tr - tl) * fracX;
            return baseLevel + hBottom + (hTop - hBottom) * fracY;
        }
    }

    public static class DefaultMapFactory
    {
        public static MapBundleData CreateSkirmishArena(int width = 64, int height = 64)
        {
            var map = new MapBundleData
            {
                MapId = "skirmish_valley",
                DisplayName = "河谷争锋 (内置对称演示图)",
                Theater = "TEMPERATE",
                IsNativeIsometricGrid = false,
                Width = width,
                Height = height,
                LocalWidth = width,
                LocalHeight = height
            };

            map.Waypoints.Add(new WaypointData { Index = 0, X = 10, Y = 10 });
            map.Waypoints.Add(new WaypointData { Index = 1, X = width - 14, Y = height - 14 });

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int h = 2;
                    int ramp = 0;
                    int terrain = 0;
                    int oreType = 0;
                    int oreAmt = 0;

                    int riverCenter = width - 1 - y;
                    int distToRiver = Math.Abs(x - riverCenter);
                    bool onCenterBridge = Math.Abs(x - width / 2) <= 3 && Math.Abs(y - height / 2) <= 3;
                    bool onNorthBridge = Math.Abs(x - (int)(width * 0.72f)) <= 2 && Math.Abs(y - (int)(height * 0.28f)) <= 2;
                    bool onSouthBridge = Math.Abs(x - (int)(width * 0.28f)) <= 2 && Math.Abs(y - (int)(height * 0.72f)) <= 2;

                    if (onCenterBridge || onNorthBridge || onSouthBridge)
                    {
                        terrain = 1;
                        h = 2;
                    }
                    else if (distToRiver <= 2)
                    {
                        terrain = 2;
                        h = 1;
                    }

                    if (terrain == 0)
                    {
                        if (Distance(x, y, 22, 12) < 5.5f || Distance(x, y, 12, 22) < 5.0f ||
                            Distance(x, y, width - 23, height - 13) < 5.5f || Distance(x, y, width - 13, height - 23) < 5.0f)
                        {
                            oreType = 1;
                            oreAmt = 10;
                        }
                        else if (Distance(x, y, width / 2 - 7, height / 2 + 7) < 4.2f ||
                                 Distance(x, y, width / 2 + 7, height / 2 - 7) < 4.2f)
                        {
                            oreType = 2;
                            oreAmt = 12;
                        }
                    }

                    map.Cells.Add(new MapCellData
                    {
                        X = x,
                        Y = y,
                        HeightLevel = h,
                        RampType = ramp,
                        TerrainType = terrain,
                        OreType = oreType,
                        OreAmount = oreAmt
                    });
                }
            }

            return map;
        }

        private static float Distance(int x1, int y1, int x2, int y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
