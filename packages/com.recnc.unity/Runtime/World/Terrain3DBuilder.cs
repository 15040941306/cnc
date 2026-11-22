using System.Collections.Generic;
using ReCnc.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReCnc.Unity.World
{
    public static class WorldCoords
    {
        public const float CellSize = 2.0f;
        public const float HeightStep = 0.7f;

        public static Vector3 CellToWorld(GridMap map, int x, int y)
        {
            var c = map.GetCell(x, y);
            float h = (c != null ? c.HeightLevel : 2) * HeightStep;
            return new Vector3((x + 0.5f) * CellSize, h, (y + 0.5f) * CellSize);
        }

        public static Vector3 SimToWorld(GridMap map, SimVec2 pos)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(pos.X), 0, map.Width - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt(pos.Y), 0, map.Height - 1);
            var cell = map.GetCell(cx, cy);
            float h = (cell != null ? cell.HeightLevel : 2) * HeightStep;
            if (cell != null && cell.RampType != 0)
            {
                h += HeightStep * 0.35f;
            }
            return new Vector3(pos.X * CellSize, h, pos.Y * CellSize);
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
        private readonly Dictionary<CellCoord, GameObject> _oreClusters = new Dictionary<CellCoord, GameObject>();
        private Material _goldOreMat;
        private Material _gemOreMat;

        public void BuildTerrain(GridMap map)
        {
            var verts = new List<Vector3>(map.Width * map.Height * 8);
            var norms = new List<Vector3>(map.Width * map.Height * 8);
            var cols = new List<Color>(map.Width * map.Height * 8);
            var tris = new List<int>(map.Width * map.Height * 18);

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = map.GetCell(x, y);
                    float x0 = x * WorldCoords.CellSize;
                    float x1 = (x + 1) * WorldCoords.CellSize;
                    float z0 = y * WorldCoords.CellSize;
                    float z1 = (y + 1) * WorldCoords.CellSize;

                    float h00 = SampleCornerHeight(map, x, y, 0, 0);
                    float h10 = SampleCornerHeight(map, x, y, 1, 0);
                    float h11 = SampleCornerHeight(map, x, y, 1, 1);
                    float h01 = SampleCornerHeight(map, x, y, 0, 1);

                    Color topColor = GetTerrainColor(cell, x, y);
                    AddQuad(verts, norms, cols, tris,
                        new Vector3(x0, h00, z0),
                        new Vector3(x0, h01, z1),
                        new Vector3(x1, h11, z1),
                        new Vector3(x1, h10, z0),
                        topColor);

                    // Cliff skirts between adjacent cells of different heights
                    Color cliffCol = new Color(0.36f, 0.33f, 0.28f, 1f);
                    if (x + 1 < map.Width)
                    {
                        float nh0 = SampleCornerHeight(map, x + 1, y, 0, 0);
                        float nh1 = SampleCornerHeight(map, x + 1, y, 0, 1);
                        if (h10 > nh0 + 0.02f || h11 > nh1 + 0.02f)
                        {
                            AddQuad(verts, norms, cols, tris,
                                new Vector3(x1, nh0, z0),
                                new Vector3(x1, h10, z0),
                                new Vector3(x1, h11, z1),
                                new Vector3(x1, nh1, z1),
                                cliffCol);
                        }
                        else if (nh0 > h10 + 0.02f || nh1 > h11 + 0.02f)
                        {
                            AddQuad(verts, norms, cols, tris,
                                new Vector3(x1, h11, z1),
                                new Vector3(x1, nh1, z1),
                                new Vector3(x1, nh0, z0),
                                new Vector3(x1, h10, z0),
                                cliffCol);
                        }
                    }

                    if (y + 1 < map.Height)
                    {
                        float nh0 = SampleCornerHeight(map, x, y + 1, 0, 0);
                        float nh1 = SampleCornerHeight(map, x, y + 1, 1, 0);
                        if (h01 > nh0 + 0.02f || h11 > nh1 + 0.02f)
                        {
                            AddQuad(verts, norms, cols, tris,
                                new Vector3(x1, nh1, z1),
                                new Vector3(x1, h11, z1),
                                new Vector3(x0, h01, z1),
                                new Vector3(x0, nh0, z1),
                                cliffCol);
                        }
                        else if (nh0 > h01 + 0.02f || nh1 > h11 + 0.02f)
                        {
                            AddQuad(verts, norms, cols, tris,
                                new Vector3(x0, h01, z1),
                                new Vector3(x0, nh0, z1),
                                new Vector3(x1, nh1, z1),
                                new Vector3(x1, h11, z1),
                                cliffCol);
                        }
                    }
                }
            }

            var mesh = new Mesh
            {
                name = "Ra2TerrainMesh",
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var terrainGo = new GameObject("TerrainMesh");
            terrainGo.transform.SetParent(transform, false);
            var mf = terrainGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = terrainGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreateVertexColorTerrainMaterial();
            var mc = terrainGo.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            // Create semi-transparent 3D river water surface at height 1.55
            var waterGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            waterGo.name = "RiverWaterSurface";
            Destroy(waterGo.GetComponent<Collider>());
            waterGo.transform.SetParent(transform, false);
            waterGo.transform.position = new Vector3(map.Width * WorldCoords.CellSize * 0.5f, 1.52f * WorldCoords.HeightStep, map.Height * WorldCoords.CellSize * 0.5f);
            waterGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            waterGo.transform.localScale = new Vector3(map.Width * WorldCoords.CellSize, map.Height * WorldCoords.CellSize, 1f);
            var waterMat = CreateLitMaterial(new Color(0.11f, 0.38f, 0.62f, 0.85f), 0.85f);
            waterGo.GetComponent<MeshRenderer>().sharedMaterial = waterMat;

            _goldOreMat = CreateLitMaterial(new Color(0.95f, 0.74f, 0.14f, 1f), 0.72f);
            _gemOreMat = CreateLitMaterial(new Color(0.86f, 0.18f, 0.92f, 1f), 0.88f);
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
                        if (hasExisting && existing != null)
                        {
                            existing.SetActive(false);
                        }
                        continue;
                    }

                    if (!hasExisting || existing == null)
                    {
                        existing = CreateOreCluster(cell.OreType == 2 ? _gemOreMat : _goldOreMat, x, y);
                        existing.transform.SetParent(transform, false);
                        _oreClusters[key] = existing;
                    }

                    existing.SetActive(true);
                    float scale = 0.45f + (cell.OreAmount / 12f) * 0.65f;
                    Vector3 basePos = WorldCoords.CellToWorld(map, x, y);
                    existing.transform.position = basePos;
                    existing.transform.localScale = new Vector3(scale, scale, scale);
                }
            }
        }

        private static GameObject CreateOreCluster(Material mat, int seedX, int seedY)
        {
            var root = new GameObject("Ore_" + seedX + "_" + seedY);
            for (int i = 0; i < 3; i++)
            {
                var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(crystal.GetComponent<Collider>());
                crystal.transform.SetParent(root.transform, false);
                float angle = (seedX * 73 + seedY * 37 + i * 120) % 360;
                float r = 0.35f;
                crystal.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * r, 0.16f, Mathf.Sin(angle * Mathf.Deg2Rad) * r);
                crystal.transform.localRotation = Quaternion.Euler(22f, angle, 30f);
                crystal.transform.localScale = new Vector3(0.36f, 0.48f, 0.36f);
                crystal.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return root;
        }

        private static float SampleCornerHeight(GridMap map, int cx, int cy, int ox, int oy)
        {
            var cell = map.GetCell(cx, cy);
            if (cell == null) return 2f * WorldCoords.HeightStep;
            if (cell.RampType == 0) return cell.HeightLevel * WorldCoords.HeightStep;

            // Smooth corner height across neighboring cells for ramp cells
            float sum = 0f;
            int cnt = 0;
            for (int dy = -1; dy <= 0; dy++)
            {
                for (int dx = -1; dx <= 0; dx++)
                {
                    var n = map.GetCell(cx + ox + dx, cy + oy + dy);
                    if (n != null)
                    {
                        sum += n.HeightLevel * WorldCoords.HeightStep;
                        cnt++;
                    }
                }
            }
            return cnt > 0 ? sum / cnt : cell.HeightLevel * WorldCoords.HeightStep;
        }

        private static Color GetTerrainColor(GridCell cell, int x, int y)
        {
            float checker = ((x + y) & 1) == 0 ? 1.0f : 0.955f;
            switch (cell.Terrain)
            {
                case TerrainKind.Road:
                    return new Color(0.42f, 0.43f, 0.45f) * checker;
                case TerrainKind.Water:
                    return new Color(0.08f, 0.25f, 0.38f);
                case TerrainKind.Rock:
                    return new Color(0.38f, 0.35f, 0.31f) * checker;
                default:
                    if (cell.HeightLevel >= 3)
                        return new Color(0.32f, 0.49f, 0.24f) * checker;
                    return new Color(0.27f, 0.45f, 0.21f) * checker;
            }
        }

        private static void AddQuad(
            List<Vector3> verts,
            List<Vector3> norms,
            List<Color> cols,
            List<int> tris,
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Color c)
        {
            int idx = verts.Count;
            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;

            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            norms.Add(normal); norms.Add(normal); norms.Add(normal); norms.Add(normal);
            cols.Add(c); cols.Add(c); cols.Add(c); cols.Add(c);

            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
        }

        private static Material CreateVertexColorTerrainMaterial()
        {
            // Particles/Standard Surface or Sprites/Default or URP Particles Lit supports vertex color out of the box;
            // Let's also generate a crisp procedural grass grid texture on URP Lit so lighting + shadows work!
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(s);
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    float g = (x == 0 || y == 0) ? 0.39f : 0.44f;
                    tex.SetPixel(x, y, new Color(g * 0.68f, g, g * 0.48f, 1f));
                }
            }
            tex.Apply();
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(32f, 32f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            return mat;
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
