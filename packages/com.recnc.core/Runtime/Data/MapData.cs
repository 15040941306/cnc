using System;
using System.Collections.Generic;

namespace ReCnc.Core.Data
{
    [Serializable]
    public class MapBundleData
    {
        public string MapId = "demo_skirmish";
        public string DisplayName = "双雄争霸 (3D 重制遭遇战)";
        public string Theater = "TEMPERATE";
        public int Width = 64;
        public int Height = 64;
        public List<WaypointData> Waypoints = new List<WaypointData>();
        public List<MapCellData> Cells = new List<MapCellData>();
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
        public int HeightLevel; // 0..15
        public int RampType;    // 0 = flat, 1..20 = slope
        public int TerrainType; // 0 = Clear/Grass, 1 = Road/Urban, 2 = Water, 3 = Cliff/Rock, 4 = Beach
        public int OreType;     // 0 = None, 1 = Gold Ore (Riparius), 2 = Gems (Cruentus)
        public int OreAmount;   // 0..12
    }

    public static class DefaultMapFactory
    {
        public static MapBundleData CreateSkirmishArena(int width = 64, int height = 64)
        {
            var map = new MapBundleData
            {
                MapId = "skirmish_valley",
                DisplayName = "河谷争锋 (Skirmish Valley)",
                Theater = "TEMPERATE",
                Width = width,
                Height = height
            };

            map.Waypoints.Add(new WaypointData { Index = 0, X = 12, Y = 12 });
            map.Waypoints.Add(new WaypointData { Index = 1, X = width - 14, Y = height - 14 });

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int h = 2;
                    int ramp = 0;
                    int terrain = 0;
                    int oreType = 0;
                    int oreAmount = 0;

                    // Central river with two wide bridges
                    int riverCenter = width / 2 + (int)Math.Round(Math.Sin(y * 0.14) * 2.0);
                    bool isBridge1 = y >= 18 && y <= 22;
                    bool isBridge2 = y >= height - 23 && y <= height - 19;
                    int distRiver = Math.Abs(x - riverCenter);

                    if (distRiver <= 2 && !isBridge1 && !isBridge2)
                    {
                        terrain = 2; // Water
                        h = 1;
                    }
                    else if (isBridge1 || isBridge2)
                    {
                        if (distRiver <= 3)
                        {
                            terrain = 1; // Road / Bridge deck
                            h = 2;
                        }
                    }

                    // Center gem island plateau
                    int dxCenter = x - width / 2;
                    int dyCenter = y - height / 2;
                    int centerDistSq = dxCenter * dxCenter + dyCenter * dyCenter;
                    if (centerDistSq <= 36)
                    {
                        terrain = 0;
                        h = 3;
                        if (centerDistSq >= 25)
                        {
                            ramp = 1; // Sloped perimeter so tanks can climb onto center plateau
                        }
                        if (centerDistSq <= 14)
                        {
                            oreType = 2; // High-value Gems!
                            oreAmount = 10;
                        }
                    }

                    // Player 0 natural gold ore patches (SW quadrant)
                    if (InCircle(x, y, 22, 11, 4.2f) || InCircle(x, y, 10, 24, 4.0f))
                    {
                        if (terrain == 0)
                        {
                            oreType = 1;
                            oreAmount = 9;
                        }
                    }

                    // Player 1 natural gold ore patches (NE quadrant)
                    if (InCircle(x, y, width - 23, height - 12, 4.2f) || InCircle(x, y, width - 11, height - 25, 4.0f))
                    {
                        if (terrain == 0)
                        {
                            oreType = 1;
                            oreAmount = 9;
                        }
                    }

                    // Decorative high cliff ridges near corners
                    if ((x < 3 || y < 3 || x >= width - 3 || y >= height - 3) && terrain == 0)
                    {
                        h = 4;
                        terrain = 3;
                    }

                    map.Cells.Add(new MapCellData
                    {
                        X = x,
                        Y = y,
                        HeightLevel = h,
                        RampType = ramp,
                        TerrainType = terrain,
                        OreType = oreType,
                        OreAmount = oreAmount
                    });
                }
            }

            return map;
        }

        private static bool InCircle(int x, int y, int cx, int cy, float radius)
        {
            float dx = x - cx;
            float dy = y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
