using System.Collections.Generic;
using ReCnc.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReCnc.Unity.World
{
    public static class WorldCoords
    {
        public const float CellSize = 2.0f;
        public const float HeightStep = 0.44f;

        private static float[,] _activeCornerHeights;
        private static int _cachedW;
        private static int _cachedH;

        public static void SetActiveHeightfield(float[,] cornerHeights, int w, int h)
        {
            _activeCornerHeights = cornerHeights;
            _cachedW = w;
            _cachedH = h;
        }

        public static Vector3 CellToWorld(GridMap map, int x, int y)
        {
            return SimToWorld(map, new SimVec2(x + 0.5f, y + 0.5f));
        }

        public static Vector3 SimToWorld(GridMap map, SimVec2 pos)
        {
            float h = SampleBilinearHeight(map, pos.X, pos.Y);
            return new Vector3(pos.X * CellSize, h, pos.Y * CellSize);
        }

        public static float SampleBilinearHeight(GridMap map, float gx, float gy)
        {
            if (_activeCornerHeights != null && _cachedW == map.Width + 1 && _cachedH == map.Height + 1)
            {
                float cx = Mathf.Clamp(gx, 0f, map.Width - 0.001f);
                float cy = Mathf.Clamp(gy, 0f, map.Height - 0.001f);
                int x0 = Mathf.FloorToInt(cx);
                int y0 = Mathf.FloorToInt(cy);
                int x1 = Mathf.Min(x0 + 1, map.Width);
                int y1 = Mathf.Min(y0 + 1, map.Height);
                float tx = cx - x0;
                float ty = cy - y0;

                float h00 = _activeCornerHeights[x0, y0];
                float h10 = _activeCornerHeights[x1, y0];
                float h01 = _activeCornerHeights[x0, y1];
                float h11 = _activeCornerHeights[x1, y1];
                return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
            }

            int ix = Mathf.Clamp(Mathf.FloorToInt(gx), 0, map.Width - 1);
            int iy = Mathf.Clamp(Mathf.FloorToInt(gy), 0, map.Height - 1);
            var cell = map.GetCell(ix, iy);
            return (cell != null ? cell.HeightLevel : 2) * HeightStep;
        }

        public static CellCoord WorldToCell(Vector3 worldPos)
        {
            return new CellCoord(
                Mathf.FloorToInt(worldPos.x / CellSize),
                Mathf.FloorToInt(worldPos.z / CellSize));
        }
    }

    public class Terrain3DBuilder : MonoBehaviour
    {
        private static readonly Dictionary<string, Texture2D> _proceduralTexCache = new Dictionary<string, Texture2D>();

        private readonly Dictionary<CellCoord, GameObject> _oreClusters = new Dictionary<CellCoord, GameObject>();
        private Material _goldOreMat;
        private Material _gemOreMat;

        public void BuildTerrain(GridMap map)
        {
            int vw = map.Width + 1;
            int vh = map.Height + 1;

            // 1. Build continuous corner heightfield & smooth organic transitions (no blocky cubes!)
            var rawCornerH = new float[vw, vh];
            var isCliffCorner = new bool[vw, vh];
            bool hasWater = false;

            for (int cy = 0; cy < vh; cy++)
            {
                for (int cx = 0; cx < vw; cx++)
                {
                    float sum = 0f;
                    int cnt = 0;
                    int minLvl = 99, maxLvl = -99;
                    bool touchesRock = false;

                    for (int dy = -1; dy <= 0; dy++)
                    {
                        for (int dx = -1; dx <= 0; dx++)
                        {
                            var c = map.GetCell(cx + dx, cy + dy);
                            if (c == null) continue;
                            if (c.Terrain == TerrainKind.Water) hasWater = true;
                            if (c.Terrain == TerrainKind.Rock) touchesRock = true;

                            float cellH = c.Terrain == TerrainKind.Water ? 0.65f : c.HeightLevel;
                            sum += cellH * WorldCoords.HeightStep;
                            cnt++;
                            if (c.HeightLevel < minLvl) minLvl = c.HeightLevel;
                            if (c.HeightLevel > maxLvl) maxLvl = c.HeightLevel;
                        }
                    }

                    rawCornerH[cx, cy] = cnt > 0 ? (sum / cnt) : (2f * WorldCoords.HeightStep);
                    isCliffCorner[cx, cy] = touchesRock || (maxLvl - minLvl >= 2);
                }
            }

            // Gentle Laplacian smoothing pass on non-cliff terrain so hills & shores flow naturally
            var smoothH = new float[vw, vh];
            for (int cy = 0; cy < vh; cy++)
            {
                for (int cx = 0; cx < vw; cx++)
                {
                    if (isCliffCorner[cx, cy] || cx == 0 || cy == 0 || cx == vw - 1 || cy == vh - 1)
                    {
                        smoothH[cx, cy] = rawCornerH[cx, cy];
                        continue;
                    }
                    float avg4 = 0.25f * (rawCornerH[cx - 1, cy] + rawCornerH[cx + 1, cy] + rawCornerH[cx, cy - 1] + rawCornerH[cx, cy + 1]);
                    smoothH[cx, cy] = Mathf.Lerp(rawCornerH[cx, cy], avg4, 0.45f);
                }
            }

            WorldCoords.SetActiveHeightfield(smoothH, vw, vh);

            // 2. Build shared-vertex continuous mesh + smooth splat weights (R=Road, G=ShoreSand, B=CliffRock, A=AO)
            int vertCount = vw * vh;
            var verts = new Vector3[vertCount];
            var splatColors = new Color[vertCount];
            var tris = new int[map.Width * map.Height * 6];

            for (int cy = 0; cy < vh; cy++)
            {
                for (int cx = 0; cx < vw; cx++)
                {
                    int vIdx = cy * vw + cx;
                    verts[vIdx] = new Vector3(cx * WorldCoords.CellSize, smoothH[cx, cy], cy * WorldCoords.CellSize);

                    float roadW = 0f, sandW = 0f, rockW = 0f;
                    int cnt = 0;
                    bool nearWater = false;

                    for (int dy = -1; dy <= 0; dy++)
                    {
                        for (int dx = -1; dx <= 0; dx++)
                        {
                            var c = map.GetCell(cx + dx, cy + dy);
                            if (c == null) continue;
                            cnt++;
                            if (c.Terrain == TerrainKind.Road) roadW += 1f;
                            else if (c.Terrain == TerrainKind.Beach || c.Terrain == TerrainKind.Water) { sandW += 1f; }
                            else if (c.Terrain == TerrainKind.Rock) rockW += 1f;
                            if (c.Terrain == TerrainKind.Water) nearWater = true;
                        }
                    }

                    if (cnt > 0)
                    {
                        roadW /= cnt;
                        sandW /= cnt;
                        rockW /= cnt;
                    }
                    if (nearWater) sandW = Mathf.Max(sandW, 0.78f);

                    float ao = nearWater ? 0.85f : 1.0f;
                    splatColors[vIdx] = new Color(roadW, sandW, rockW, ao);
                }
            }

            int tPtr = 0;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    int i00 = y * vw + x;
                    int i10 = i00 + 1;
                    int i01 = (y + 1) * vw + x;
                    int i11 = i01 + 1;

                    tris[tPtr++] = i00; tris[tPtr++] = i01; tris[tPtr++] = i11;
                    tris[tPtr++] = i00; tris[tPtr++] = i11; tris[tPtr++] = i10;
                }
            }

            var mesh = new Mesh
            {
                name = "Ra2ContinuousTerrainMesh",
                indexFormat = IndexFormat.UInt32
            };
            mesh.vertices = verts;
            mesh.colors = splatColors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var terrainGo = new GameObject("TerrainMesh");
            terrainGo.transform.SetParent(transform, false);
            terrainGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = terrainGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreateSplatTerrainMaterial(map.Theater);
            mr.receiveShadows = true;
            terrainGo.AddComponent<MeshCollider>().sharedMesh = mesh;

            // 3. Animated Rippling Water Surface
            if (hasWater)
            {
                var waterGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                waterGo.name = "RiverWaterSurface";
                Destroy(waterGo.GetComponent<Collider>());
                waterGo.transform.SetParent(transform, false);
                waterGo.transform.position = new Vector3(
                    map.Width * WorldCoords.CellSize * 0.5f,
                    1.28f * WorldCoords.HeightStep,
                    map.Height * WorldCoords.CellSize * 0.5f);
                waterGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                waterGo.transform.localScale = new Vector3(map.Width * WorldCoords.CellSize, map.Height * WorldCoords.CellSize, 1f);
                waterGo.GetComponent<MeshRenderer>().sharedMaterial = CreateWaterMaterial(map.Theater);
            }

            _goldOreMat = CreateLitMaterial(new Color(0.96f, 0.76f, 0.16f, 1f), 0.78f);
            _gemOreMat = CreateLitMaterial(new Color(0.88f, 0.18f, 0.94f, 1f), 0.90f);
            SyncOreVisuals(map);
        }

        public void SyncOreVisuals(GridMap map)
        {
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = map.GetCell(x, y);
                    var key = new CellCoord(x, y);
                    GameObject existing;
                    bool hasExisting = _oreClusters.TryGetValue(key, out existing);

                    if (cell.OreAmount <= 0)
                    {
                        if (hasExisting && existing != null) existing.SetActive(false);
                        continue;
                    }

                    if (!hasExisting || existing == null)
                    {
                        existing = CreateOreCluster(cell.OreType == 2 ? _gemOreMat : _goldOreMat, x, y);
                        existing.transform.SetParent(transform, false);
                        _oreClusters[key] = existing;
                    }

                    existing.SetActive(true);
                    float scale = 0.55f + (cell.OreAmount / 12f) * 0.55f;
                    existing.transform.position = WorldCoords.CellToWorld(map, x, y);
                    existing.transform.localScale = new Vector3(scale, scale, scale);
                }
            }
        }

        private static GameObject CreateOreCluster(Material mat, int seedX, int seedY)
        {
            var root = new GameObject("Ore_" + seedX + "_" + seedY);
            for (int i = 0; i < 4; i++)
            {
                var nugget = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(nugget.GetComponent<Collider>());
                nugget.transform.SetParent(root.transform, false);
                float angle = (seedX * 73 + seedY * 37 + i * 92) % 360;
                float r = 0.18f + (i % 2) * 0.22f;
                nugget.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * r, 0.08f, Mathf.Sin(angle * Mathf.Deg2Rad) * r);
                nugget.transform.localRotation = Quaternion.Euler(15f, angle, 25f);
                nugget.transform.localScale = new Vector3(0.38f, 0.22f, 0.32f);
                nugget.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return root;
        }

        private static Material CreateSplatTerrainMaterial(string theater)
        {
            string th = (theater ?? "TEMPERATE").ToUpperInvariant();
            Shader s = Shader.Find("ReCnc/Ra2TerrainLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(s);

            Texture2D primaryTex;
            Texture2D sandTex;
            switch (th)
            {
                case "SNOW":
                    primaryTex = GetOrBuildNoiseTexture("SnowPrimary", new Color(0.84f, 0.89f, 0.95f), new Color(0.95f, 0.97f, 1.00f), 7f, 0.12f);
                    sandTex    = GetOrBuildNoiseTexture("SnowShore",   new Color(0.62f, 0.68f, 0.75f), new Color(0.76f, 0.81f, 0.87f), 10f, 0.18f);
                    break;
                case "URBAN":
                case "NEWURBAN":
                    primaryTex = GetOrBuildPavedTexture("UrbanPrimary", new Color(0.48f, 0.50f, 0.52f), new Color(0.57f, 0.58f, 0.60f));
                    sandTex    = GetOrBuildNoiseTexture("UrbanPark",    new Color(0.30f, 0.45f, 0.24f), new Color(0.42f, 0.54f, 0.30f), 9f, 0.22f);
                    break;
                case "DESERT":
                    primaryTex = GetOrBuildDuneTexture("DesertPrimary", new Color(0.74f, 0.58f, 0.36f), new Color(0.87f, 0.72f, 0.47f));
                    sandTex    = GetOrBuildNoiseTexture("DesertShore",  new Color(0.62f, 0.48f, 0.30f), new Color(0.72f, 0.58f, 0.38f), 11f, 0.20f);
                    break;
                case "LUNAR":
                    primaryTex = GetOrBuildNoiseTexture("LunarPrimary", new Color(0.46f, 0.47f, 0.50f), new Color(0.62f, 0.62f, 0.66f), 8f, 0.25f);
                    sandTex    = GetOrBuildNoiseTexture("LunarDust",    new Color(0.36f, 0.37f, 0.40f), new Color(0.48f, 0.49f, 0.52f), 12f, 0.20f);
                    break;
                default:
                    primaryTex = GetOrBuildNoiseTexture("TempGrass",    new Color(0.24f, 0.42f, 0.17f), new Color(0.38f, 0.56f, 0.24f), 9f, 0.26f);
                    sandTex    = GetOrBuildNoiseTexture("TempSand",     new Color(0.68f, 0.59f, 0.40f), new Color(0.82f, 0.73f, 0.52f), 12f, 0.18f);
                    break;
            }

            Texture2D roadTex = GetOrBuildPavedTexture("AsphaltRoad", new Color(0.27f, 0.28f, 0.30f), new Color(0.36f, 0.37f, 0.40f));
            Texture2D rockTex = GetOrBuildRockTexture("CliffRock_" + th,
                th == "DESERT" ? new Color(0.52f, 0.38f, 0.24f) : new Color(0.35f, 0.34f, 0.32f),
                th == "DESERT" ? new Color(0.68f, 0.52f, 0.34f) : new Color(0.52f, 0.50f, 0.47f));

            if (mat.HasProperty("_PrimaryMap"))  mat.SetTexture("_PrimaryMap", primaryTex);
            if (mat.HasProperty("_DirtSandMap")) mat.SetTexture("_DirtSandMap", sandTex);
            if (mat.HasProperty("_RoadMap"))     mat.SetTexture("_RoadMap", roadTex);
            if (mat.HasProperty("_RockMap"))     mat.SetTexture("_RockMap", rockTex);
            mat.mainTexture = primaryTex;
            return mat;
        }

        private static Material CreateWaterMaterial(string theater)
        {
            string th = (theater ?? "TEMPERATE").ToUpperInvariant();
            Shader ws = Shader.Find("ReCnc/Ra2WaterLit");
            if (ws != null)
            {
                var wMat = new Material(ws);
                if (th == "SNOW")
                {
                    wMat.SetColor("_DeepColor", new Color(0.10f, 0.32f, 0.52f, 0.90f));
                    wMat.SetColor("_ShallowColor", new Color(0.28f, 0.62f, 0.82f, 0.86f));
                }
                else if (th == "DESERT")
                {
                    wMat.SetColor("_DeepColor", new Color(0.06f, 0.30f, 0.42f, 0.88f));
                    wMat.SetColor("_ShallowColor", new Color(0.18f, 0.60f, 0.68f, 0.84f));
                }
                return wMat;
            }
            return CreateLitMaterial(new Color(0.11f, 0.38f, 0.62f, 0.85f), 0.85f);
        }

        private static Texture2D GetOrBuildNoiseTexture(string key, Color colDark, Color colLight, float baseFreq, float detailAmp)
        {
            Texture2D tex;
            if (_proceduralTexCache.TryGetValue(key, out tex) && tex != null) return tex;

            const int sz = 256;
            tex = new Texture2D(sz, sz, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            var pixels = new Color[sz * sz];
            for (int y = 0; y < sz; y++)
            {
                float v = y / (float)sz;
                for (int x = 0; x < sz; x++)
                {
                    float u = x / (float)sz;
                    float n1 = TileablePerlin(u, v, baseFreq);
                    float n2 = TileablePerlin(u, v, baseFreq * 2.0f) * 0.5f;
                    float n3 = TileablePerlin(u, v, baseFreq * 4.0f) * detailAmp;
                    float micro = Mathf.PerlinNoise(x * 0.42f + 7.1f, y * 0.42f + 3.3f) * 0.14f;
                    float t = Mathf.Clamp01((n1 + n2 + n3) / 1.65f + micro - 0.05f);
                    pixels[y * sz + x] = Color.Lerp(colDark, colLight, t);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            _proceduralTexCache[key] = tex;
            return tex;
        }

        private static Texture2D GetOrBuildDuneTexture(string key, Color colDark, Color colLight)
        {
            Texture2D tex;
            if (_proceduralTexCache.TryGetValue(key, out tex) && tex != null) return tex;

            const int sz = 256;
            tex = new Texture2D(sz, sz, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            var pixels = new Color[sz * sz];
            for (int y = 0; y < sz; y++)
            {
                float v = y / (float)sz;
                for (int x = 0; x < sz; x++)
                {
                    float u = x / (float)sz;
                    float warp = TileablePerlin(u, v, 4f) * 0.25f;
                    float ripple = Mathf.Sin((u * 3f + v * 9f + warp) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float grain = TileablePerlin(u, v, 24f) * 0.22f;
                    pixels[y * sz + x] = Color.Lerp(colDark, colLight, Mathf.Clamp01(ripple * 0.72f + grain));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            _proceduralTexCache[key] = tex;
            return tex;
        }

        private static Texture2D GetOrBuildPavedTexture(string key, Color colJoint, Color colSlab)
        {
            Texture2D tex;
            if (_proceduralTexCache.TryGetValue(key, out tex) && tex != null) return tex;

            const int sz = 256;
            tex = new Texture2D(sz, sz, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            var pixels = new Color[sz * sz];
            for (int y = 0; y < sz; y++)
            {
                float v = y / (float)sz;
                for (int x = 0; x < sz; x++)
                {
                    float u = x / (float)sz;
                    float gx = (u * 4f) % 1f;
                    float gy = (v * 4f) % 1f;
                    bool seam = gx < 0.035f || gy < 0.035f;
                    float grit = TileablePerlin(u, v, 16f) * 0.35f + Mathf.PerlinNoise(x * 0.5f, y * 0.5f) * 0.15f;
                    Color baseC = Color.Lerp(colJoint, colSlab, seam ? 0.15f : Mathf.Clamp01(0.55f + grit));
                    pixels[y * sz + x] = baseC;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            _proceduralTexCache[key] = tex;
            return tex;
        }

        private static Texture2D GetOrBuildRockTexture(string key, Color colDark, Color colLight)
        {
            Texture2D tex;
            if (_proceduralTexCache.TryGetValue(key, out tex) && tex != null) return tex;

            const int sz = 256;
            tex = new Texture2D(sz, sz, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            var pixels = new Color[sz * sz];
            for (int y = 0; y < sz; y++)
            {
                float v = y / (float)sz;
                for (int x = 0; x < sz; x++)
                {
                    float u = x / (float)sz;
                    float strata = Mathf.Sin((v * 10f + TileablePerlin(u, v, 6f) * 0.8f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float crag = TileablePerlin(u, v, 14f);
                    pixels[y * sz + x] = Color.Lerp(colDark, colLight, Mathf.Clamp01(strata * 0.55f + crag * 0.55f));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            _proceduralTexCache[key] = tex;
            return tex;
        }

        private static float TileablePerlin(float u, float v, float freq)
        {
            float x = u * freq;
            float y = v * freq;
            float n00 = Mathf.PerlinNoise(x, y);
            float n10 = Mathf.PerlinNoise(x - freq, y);
            float n01 = Mathf.PerlinNoise(x, y - freq);
            float n11 = Mathf.PerlinNoise(x - freq, y - freq);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v);
        }

        public static Material CreateLitMaterial(Color color, float smoothness = 0.25f)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(s);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }
    }
}
