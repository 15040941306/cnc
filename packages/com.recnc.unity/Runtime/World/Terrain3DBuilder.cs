using System.Collections.Generic;
using System.IO;
using ReCnc.Core.Data;
using ReCnc.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReCnc.Unity.World
{
    public static class WorldCoords
    {
        public const float CellSize = 2.0f;
        // Exact 3ds Max dimetric height ratio for RA2 (15px vertical per 60x30 diamond at 30-deg pitch)
        public const float HeightStep = 0.8165f;

        public static Vector3 CellToWorld(GridMap map, int x, int y)
        {
            return SimToWorld(map, new SimVec2(x + 0.5f, y + 0.5f));
        }

        public static Vector3 SimToWorld(GridMap map, SimVec2 pos)
        {
            float level = map.SampleExactCellLevel(pos.X, pos.Y);
            return new Vector3(pos.X * CellSize, level * HeightStep, pos.Y * CellSize);
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
        private Texture2D _loadedBakedMapTex;

        private void OnDestroy()
        {
            if (_loadedBakedMapTex != null)
            {
                Destroy(_loadedBakedMapTex);
                _loadedBakedMapTex = null;
            }
        }

        public void BuildTerrain(GridMap map)
        {
            if (map.IsNativeIsometricGrid)
            {
                BuildAuthenticIsometric3DTerrain(map);
            }
            else
            {
                BuildFallbackProceduralTerrain(map);
            }

            _goldOreMat = CreateLitMaterial(new Color(0.96f, 0.76f, 0.14f, 1f), 0.78f);
            _gemOreMat = CreateLitMaterial(new Color(0.88f, 0.16f, 0.94f, 1f), 0.90f);
            SyncOreVisuals(map);
        }

        private void BuildAuthenticIsometric3DTerrain(GridMap map)
        {
            int estQuads = map.Width * map.Height * 2;
            var verts = new List<Vector3>(estQuads * 4);
            var norms = new List<Vector3>(estQuads * 4);
            var uvs = new List<Vector2>(estQuads * 4);
            var cols = new List<Color>(estQuads * 4);
            var tris = new List<int>(estQuads * 6);

            var wVerts = new List<Vector3>();
            var wTris = new List<int>();
            var bVerts = new List<Vector3>();
            var bNorms = new List<Vector3>();
            var bCols = new List<Color>();
            var bTris = new List<int>();

            float invTexW = map.BakedTexWidth > 0 ? (1f / map.BakedTexWidth) : 0.001f;
            float invTexH = map.BakedTexHeight > 0 ? (1f / map.BakedTexHeight) : 0.001f;

            Vector2 CalcBakedUv(float vx, float vz, float levelZ)
            {
                float rxF = vx + map.MinRx;
                float ryF = (map.MaxRy + 1) - vz;
                float px = (rxF - ryF + map.FullMapWidth) * 30f - map.BakedOriginPx;
                float py = (rxF + ryF - map.FullMapWidth - 1f - levelZ) * 15f - map.BakedOriginPy;
                return new Vector2(px * invTexW, 1f - py * invTexH);
            }

            void EmitTri(Vector3 a, Vector2 uvA, Vector3 b, Vector2 uvB, Vector3 c, Vector2 uvC, Color maskCol)
            {
                int idx = verts.Count;
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (n.sqrMagnitude < 1e-4f) n = Vector3.up;
                verts.Add(a); verts.Add(b); verts.Add(c);
                norms.Add(n); norms.Add(n); norms.Add(n);
                uvs.Add(uvA); uvs.Add(uvB); uvs.Add(uvC);
                cols.Add(maskCol); cols.Add(maskCol); cols.Add(maskCol);
                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            }

            void EmitCliffWall(float vx0, float vz0, float zLow0, float zHigh0, float vx1, float vz1, float zLow1, float zHigh1, Color maskCol)
            {
                if (Mathf.Abs(zHigh0 - zLow0) < 0.02f && Mathf.Abs(zHigh1 - zLow1) < 0.02f) return;
                Vector3 p0Low  = new Vector3(vx0 * WorldCoords.CellSize, zLow0 * WorldCoords.HeightStep, vz0 * WorldCoords.CellSize);
                Vector3 p0High = new Vector3(vx0 * WorldCoords.CellSize, zHigh0 * WorldCoords.HeightStep, vz0 * WorldCoords.CellSize);
                Vector3 p1Low  = new Vector3(vx1 * WorldCoords.CellSize, zLow1 * WorldCoords.HeightStep, vz1 * WorldCoords.CellSize);
                Vector3 p1High = new Vector3(vx1 * WorldCoords.CellSize, zHigh1 * WorldCoords.HeightStep, vz1 * WorldCoords.CellSize);

                Vector2 uv0Low  = CalcBakedUv(vx0, vz0, zLow0);
                Vector2 uv0High = CalcBakedUv(vx0, vz0, zHigh0);
                Vector2 uv1Low  = CalcBakedUv(vx1, vz1, zLow1);
                Vector2 uv1High = CalcBakedUv(vx1, vz1, zHigh1);

                EmitTri(p0Low, uv0Low, p0High, uv0High, p1High, uv1High, maskCol);
                EmitTri(p0Low, uv0Low, p1High, uv1High, p1Low, uv1Low, maskCol);
            }

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = map.GetCell(x, y);
                    Color mask = new Color(0f, 0f, 0f, cell.Terrain == TerrainKind.OutOfBounds ? 0f : 1f);

                    // If cell has an elevated High Bridge, ground/water stays at canyon/river level while BridgeMesh sits on top!
                    int groundBaseLvl = cell.HasBridge && cell.HeightLevel >= 4 ? cell.HeightLevel - 4 : cell.HeightLevel;
                    int groundRamp = cell.HasBridge ? 0 : cell.RampType;
                    Ra2RampGeometry.GetCornerOffsets(groundRamp, out int oTL, out int oTR, out int oBR, out int oBL, out int split);

                    float zTL = groundBaseLvl + oTL;
                    float zTR = groundBaseLvl + oTR;
                    float zBR = groundBaseLvl + oBR;
                    float zBL = groundBaseLvl + oBL;

                    // Corner positions in Unity XZ: BL=(x,y), TL=(x,y+1), TR=(x+1,y+1), BR=(x+1,y)
                    Vector3 vBL = new Vector3(x * WorldCoords.CellSize, zBL * WorldCoords.HeightStep, y * WorldCoords.CellSize);
                    Vector3 vTL = new Vector3(x * WorldCoords.CellSize, zTL * WorldCoords.HeightStep, (y + 1) * WorldCoords.CellSize);
                    Vector3 vTR = new Vector3((x + 1) * WorldCoords.CellSize, zTR * WorldCoords.HeightStep, (y + 1) * WorldCoords.CellSize);
                    Vector3 vBR = new Vector3((x + 1) * WorldCoords.CellSize, zBR * WorldCoords.HeightStep, y * WorldCoords.CellSize);

                    Vector2 uvBL = CalcBakedUv(x, y, zBL);
                    Vector2 uvTL = CalcBakedUv(x, y + 1, zTL);
                    Vector2 uvTR = CalcBakedUv(x + 1, y + 1, zTR);
                    Vector2 uvBR = CalcBakedUv(x + 1, y, zBR);

                    if (split == 1) // SplitX along BL-TR diagonal
                    {
                        EmitTri(vBL, uvBL, vTL, uvTL, vTR, uvTR, mask);
                        EmitTri(vBL, uvBL, vTR, uvTR, vBR, uvBR, mask);
                    }
                    else // SplitY along TL-BR diagonal
                    {
                        EmitTri(vBL, uvBL, vTL, uvTL, vBR, uvBR, mask);
                        EmitTri(vTL, uvTL, vTR, uvTR, vBR, uvBR, mask);
                    }

                    // Cliff wall skirts along East (+x) and North (+y) edges when adjacent ground levels jump
                    if (x + 1 < map.Width)
                    {
                        var nb = map.GetCell(x + 1, y);
                        int nbBase = nb.HasBridge && nb.HeightLevel >= 4 ? nb.HeightLevel - 4 : nb.HeightLevel;
                        Ra2RampGeometry.GetCornerOffsets(nb.HasBridge ? 0 : nb.RampType, out int nTL, out _, out _, out int nBL, out _);
                        float nzTL = nbBase + nTL;
                        float nzBL = nbBase + nBL;
                        if (zTR > nzTL || zBR > nzBL)
                            EmitCliffWall(x + 1, y + 1, nzTL, zTR, x + 1, y, nzBL, zBR, mask);
                        else if (nzTL > zTR || nzBL > zBR)
                            EmitCliffWall(x + 1, y, zBR, nzBL, x + 1, y + 1, zTR, nzTL, mask);
                    }
                    if (y + 1 < map.Height)
                    {
                        var nb = map.GetCell(x, y + 1);
                        int nbBase = nb.HasBridge && nb.HeightLevel >= 4 ? nb.HeightLevel - 4 : nb.HeightLevel;
                        Ra2RampGeometry.GetCornerOffsets(nb.HasBridge ? 0 : nb.RampType, out _, out _, out int nBR, out int nBL, out _);
                        float nzBR = nbBase + nBR;
                        float nzBL = nbBase + nBL;
                        if (zTL > nzBL || zTR > nzBR)
                            EmitCliffWall(x, y + 1, nzBL, zTL, x + 1, y + 1, nzBR, zTR, mask);
                        else if (nzBL > zTL || nzBR > zTR)
                            EmitCliffWall(x + 1, y + 1, zTR, nzBR, x, y + 1, zTL, nzBL, mask);
                    }

                    // Subtle translucent 3D sun-glint water overlay strictly on actual Water cells
                    if (cell.Terrain == TerrainKind.Water && !cell.HasBridge)
                    {
                        float wy = zBL * WorldCoords.HeightStep + 0.04f;
                        int wi = wVerts.Count;
                        wVerts.Add(new Vector3(vBL.x, wy, vBL.z));
                        wVerts.Add(new Vector3(vTL.x, wy, vTL.z));
                        wVerts.Add(new Vector3(vTR.x, wy, vTR.z));
                        wVerts.Add(new Vector3(vBR.x, wy, vBR.z));
                        wTris.Add(wi); wTris.Add(wi + 1); wTris.Add(wi + 2);
                        wTris.Add(wi); wTris.Add(wi + 2); wTris.Add(wi + 3);
                    }

                    // 3D Bridge Deck Slab on HasBridge cells!
                    if (cell.HasBridge)
                    {
                        float deckTop = cell.HeightLevel * WorldCoords.HeightStep + 0.06f;
                        float deckBot = deckTop - 0.28f;
                        AddSolidBox(bVerts, bNorms, bCols, bTris,
                            new Vector3((x + 0.5f) * WorldCoords.CellSize, (deckTop + deckBot) * 0.5f, (y + 0.5f) * WorldCoords.CellSize),
                            new Vector3(WorldCoords.CellSize, 0.28f, WorldCoords.CellSize),
                            new Color(0.44f, 0.39f, 0.32f, 1f));
                    }
                }
            }

            var mesh = new Mesh { name = "Ra2Authentic3DTerrainMesh", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var terrainGo = new GameObject("TerrainMesh");
            terrainGo.transform.SetParent(transform, false);
            terrainGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = terrainGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreateAuthenticBakedMaterial(map);
            mr.receiveShadows = true;
            terrainGo.AddComponent<MeshCollider>().sharedMesh = mesh;

            if (bVerts.Count > 0)
            {
                var bMesh = new Mesh { name = "Ra2BridgeMesh", indexFormat = IndexFormat.UInt32 };
                bMesh.SetVertices(bVerts);
                bMesh.SetNormals(bNorms);
                bMesh.SetColors(bCols);
                bMesh.SetTriangles(bTris, 0);
                var bridgeGo = new GameObject("Bridges3D");
                bridgeGo.transform.SetParent(transform, false);
                bridgeGo.AddComponent<MeshFilter>().sharedMesh = bMesh;
                bridgeGo.AddComponent<MeshRenderer>().sharedMaterial = CreateLitMaterial(new Color(0.46f, 0.42f, 0.36f, 1f), 0.3f);
                bridgeGo.AddComponent<MeshCollider>().sharedMesh = bMesh;
            }

            if (wVerts.Count > 0)
            {
                var wMesh = new Mesh { name = "Ra2WaterGlintMesh", indexFormat = IndexFormat.UInt32 };
                wMesh.SetVertices(wVerts);
                wMesh.SetTriangles(wTris, 0);
                wMesh.RecalculateNormals();
                var waterGo = new GameObject("RiverWaterSurface");
                waterGo.transform.SetParent(transform, false);
                waterGo.AddComponent<MeshFilter>().sharedMesh = wMesh;
                var wMat = CreateWaterMaterial(map.Theater);
                if (wMat.HasProperty("_DeepColor")) wMat.SetColor("_DeepColor", new Color(0.08f, 0.28f, 0.48f, 0.32f));
                if (wMat.HasProperty("_ShallowColor")) wMat.SetColor("_ShallowColor", new Color(0.20f, 0.54f, 0.76f, 0.28f));
                waterGo.AddComponent<MeshRenderer>().sharedMaterial = wMat;
            }

            BuildMapPropsAndPreplacedStructures(map);
        }

        private void BuildMapPropsAndPreplacedStructures(GridMap map)
        {
            if ((map.Props == null || map.Props.Count == 0) && (map.PreplacedStructures == null || map.PreplacedStructures.Count == 0))
                return;

            var propsRoot = new GameObject("MapPropsAndStructures");
            propsRoot.transform.SetParent(transform, false);

            Color foliageCol = map.Theater == "SNOW" ? new Color(0.76f, 0.86f, 0.88f)
                : map.Theater == "DESERT" ? new Color(0.36f, 0.50f, 0.22f)
                : new Color(0.18f, 0.42f, 0.16f);
            Material trunkMat = CreateLitMaterial(new Color(0.36f, 0.24f, 0.14f), 0.15f);
            Material leafMat = CreateLitMaterial(foliageCol, 0.22f);
            Material oreTreeMat = CreateLitMaterial(new Color(0.92f, 0.62f, 0.14f), 0.65f);
            Material lampMat = CreateLitMaterial(new Color(0.55f, 0.58f, 0.62f), 0.55f);
            Material wallMat = CreateLitMaterial(new Color(0.58f, 0.56f, 0.52f), 0.25f);
            Material civWallMat = CreateLitMaterial(new Color(0.72f, 0.69f, 0.64f), 0.30f);
            Material civGlassMat = CreateLitMaterial(new Color(0.16f, 0.28f, 0.42f), 0.78f);

            for (int i = 0; i < map.Props.Count; i++)
            {
                var p = map.Props[i];
                Vector3 pos = WorldCoords.CellToWorld(map, p.X, p.Y);

                if (p.Kind == 2) // Urban Streetlight / Traffic Light
                {
                    var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(pole.GetComponent<Collider>());
                    pole.transform.SetParent(propsRoot.transform, false);
                    pole.transform.position = pos + Vector3.up * 1.1f;
                    pole.transform.localScale = new Vector3(0.12f, 1.1f, 0.12f);
                    pole.GetComponent<MeshRenderer>().sharedMaterial = lampMat;
                }
                else if (p.Kind == 3) // Concrete Wall / Sandbag
                {
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(wall.GetComponent<Collider>());
                    wall.transform.SetParent(propsRoot.transform, false);
                    wall.transform.position = pos + Vector3.up * 0.45f;
                    wall.transform.localScale = new Vector3(1.85f, 0.9f, 1.85f);
                    wall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
                }
                else // Tree or Blossom Ore Tree
                {
                    var treeGo = new GameObject(p.TypeId);
                    treeGo.transform.SetParent(propsRoot.transform, false);
                    treeGo.transform.position = pos;

                    var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(trunk.GetComponent<Collider>());
                    trunk.transform.SetParent(treeGo.transform, false);
                    trunk.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                    trunk.transform.localScale = new Vector3(0.32f, 0.65f, 0.32f);
                    trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

                    var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(canopy.GetComponent<Collider>());
                    canopy.transform.SetParent(treeGo.transform, false);
                    float hVar = 1.4f + ((p.X * 31 + p.Y * 17) % 5) * 0.16f;
                    canopy.transform.localPosition = new Vector3(0f, 1.55f, 0f);
                    canopy.transform.localScale = new Vector3(1.35f, hVar, 1.35f);
                    canopy.GetComponent<MeshRenderer>().sharedMaterial = p.Kind == 1 ? oreTreeMat : leafMat;
                }
            }

            // Spawn 3D Civilian / Neutral Tech Structures on their exact [Structures] footprints!
            for (int i = 0; i < map.PreplacedStructures.Count; i++)
            {
                var s = map.PreplacedStructures[i];
                float cx = (s.X + s.FootprintX * 0.5f) * WorldCoords.CellSize;
                float cz = (s.Y + s.FootprintY * 0.5f) * WorldCoords.CellSize;
                float baseY = map.SampleExactCellLevel(s.X + s.FootprintX * 0.5f, s.Y + s.FootprintY * 0.5f) * WorldCoords.HeightStep;
                float sx = s.FootprintX * WorldCoords.CellSize * 0.88f;
                float sz = s.FootprintY * WorldCoords.CellSize * 0.88f;
                float bldgH = Mathf.Clamp((s.FootprintX + s.FootprintY) * 0.85f + (i % 3) * 0.7f, 2.2f, 5.6f);

                var bGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bGo.name = s.TypeId + "_" + i;
                bGo.transform.SetParent(propsRoot.transform, false);
                bGo.transform.position = new Vector3(cx, baseY + bldgH * 0.5f, cz);
                bGo.transform.localScale = new Vector3(sx, bldgH, sz);
                bGo.GetComponent<MeshRenderer>().sharedMaterial = civWallMat;

                var roofBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(roofBand.GetComponent<Collider>());
                roofBand.transform.SetParent(bGo.transform, false);
                roofBand.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                roofBand.transform.localScale = new Vector3(1.02f, 0.32f, 1.02f);
                roofBand.GetComponent<MeshRenderer>().sharedMaterial = civGlassMat;
            }
        }

        private Material CreateAuthenticBakedMaterial(GridMap map)
        {
            Shader s = Shader.Find("ReCnc/Ra2TerrainLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(s);

            if (!string.IsNullOrEmpty(map.BakedTerrainTextureRelPath))
            {
                string fullPath = Path.Combine(Application.streamingAssetsPath, "Ra2Converted", map.BakedTerrainTextureRelPath);
                if (File.Exists(fullPath))
                {
                    byte[] pngBytes = File.ReadAllBytes(fullPath);
                    _loadedBakedMapTex = new Texture2D(2, 2, TextureFormat.RGBA32, true)
                    {
                        filterMode = FilterMode.Bilinear,
                        anisoLevel = 8,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    _loadedBakedMapTex.LoadImage(pngBytes);
                    if (mat.HasProperty("_UseBakedMap")) mat.SetFloat("_UseBakedMap", 1f);
                    if (mat.HasProperty("_BakedMap")) mat.SetTexture("_BakedMap", _loadedBakedMapTex);
                    mat.mainTexture = _loadedBakedMapTex;
                    return mat;
                }
            }

            return CreateSplatTerrainMaterial(map.Theater);
        }

        private void BuildFallbackProceduralTerrain(GridMap map)
        {
            int vw = map.Width + 1;
            int vh = map.Height + 1;
            var verts = new Vector3[vw * vh];
            var cols = new Color[vw * vh];
            var uvs = new Vector2[vw * vh];
            var tris = new int[map.Width * map.Height * 6];
            bool hasWater = false;

            for (int cy = 0; cy < vh; cy++)
            {
                for (int cx = 0; cx < vw; cx++)
                {
                    int idx = cy * vw + cx;
                    float h = map.SampleExactCellLevel(cx, cy) * WorldCoords.HeightStep;
                    verts[idx] = new Vector3(cx * WorldCoords.CellSize, h, cy * WorldCoords.CellSize);
                    uvs[idx] = new Vector2(cx / (float)map.Width, cy / (float)map.Height);

                    var c = map.GetCell(Mathf.Clamp(cx, 0, map.Width - 1), Mathf.Clamp(cy, 0, map.Height - 1));
                    if (c.Terrain == TerrainKind.Water) hasWater = true;
                    cols[idx] = new Color(
                        c.Terrain == TerrainKind.Road ? 1f : 0f,
                        c.Terrain == TerrainKind.Beach || c.Terrain == TerrainKind.Water ? 1f : 0f,
                        c.Terrain == TerrainKind.Rock ? 1f : 0f,
                        1f);
                }
            }

            int tPtr = 0;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    int i00 = y * vw + x, i10 = i00 + 1, i01 = (y + 1) * vw + x, i11 = i01 + 1;
                    tris[tPtr++] = i00; tris[tPtr++] = i01; tris[tPtr++] = i11;
                    tris[tPtr++] = i00; tris[tPtr++] = i11; tris[tPtr++] = i10;
                }
            }

            var mesh = new Mesh { name = "Ra2FallbackTerrainMesh", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var terrainGo = new GameObject("TerrainMesh");
            terrainGo.transform.SetParent(transform, false);
            terrainGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrainGo.AddComponent<MeshRenderer>().sharedMaterial = CreateSplatTerrainMaterial(map.Theater);
            terrainGo.AddComponent<MeshCollider>().sharedMesh = mesh;

            if (hasWater)
            {
                var waterGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                waterGo.name = "RiverWaterSurface";
                Destroy(waterGo.GetComponent<Collider>());
                waterGo.transform.SetParent(transform, false);
                waterGo.transform.position = new Vector3(map.Width * WorldCoords.CellSize * 0.5f, 1.35f * WorldCoords.HeightStep, map.Height * WorldCoords.CellSize * 0.5f);
                waterGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                waterGo.transform.localScale = new Vector3(map.Width * WorldCoords.CellSize, map.Height * WorldCoords.CellSize, 1f);
                waterGo.GetComponent<MeshRenderer>().sharedMaterial = CreateWaterMaterial(map.Theater);
            }
        }

        private static void AddSolidBox(List<Vector3> verts, List<Vector3> norms, List<Color> cols, List<int> tris, Vector3 center, Vector3 size, Color col)
        {
            Vector3 h = size * 0.5f;
            // Top face
            int idx = verts.Count;
            verts.Add(center + new Vector3(-h.x, h.y, -h.z));
            verts.Add(center + new Vector3(-h.x, h.y,  h.z));
            verts.Add(center + new Vector3( h.x, h.y,  h.z));
            verts.Add(center + new Vector3( h.x, h.y, -h.z));
            for (int i = 0; i < 4; i++) { norms.Add(Vector3.up); cols.Add(col); }
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
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
                float r = 0.18f + (i % 2) * 0.24f;
                nugget.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * r, 0.08f, Mathf.Sin(angle * Mathf.Deg2Rad) * r);
                nugget.transform.localRotation = Quaternion.Euler(15f, angle, 25f);
                nugget.transform.localScale = new Vector3(0.38f, 0.22f, 0.32f);
                nugget.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return root;
        }

        private static Material CreateSplatTerrainMaterial(string theater)
        {
            Shader s = Shader.Find("ReCnc/Ra2TerrainLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(s);
            if (mat.HasProperty("_UseBakedMap")) mat.SetFloat("_UseBakedMap", 0f);
            Texture2D primaryTex = GetOrBuildNoiseTexture("TempGrass", new Color(0.24f, 0.42f, 0.17f), new Color(0.38f, 0.56f, 0.24f), 9f, 0.26f);
            Texture2D sandTex    = GetOrBuildNoiseTexture("TempSand",  new Color(0.68f, 0.59f, 0.40f), new Color(0.82f, 0.73f, 0.52f), 12f, 0.18f);
            Texture2D roadTex    = GetOrBuildNoiseTexture("RoadAsph",  new Color(0.28f, 0.29f, 0.31f), new Color(0.37f, 0.38f, 0.41f), 14f, 0.20f);
            Texture2D rockTex    = GetOrBuildNoiseTexture("CliffRock", new Color(0.35f, 0.34f, 0.32f), new Color(0.52f, 0.50f, 0.47f), 10f, 0.30f);
            if (mat.HasProperty("_PrimaryMap"))  mat.SetTexture("_PrimaryMap", primaryTex);
            if (mat.HasProperty("_DirtSandMap")) mat.SetTexture("_DirtSandMap", sandTex);
            if (mat.HasProperty("_RoadMap"))     mat.SetTexture("_RoadMap", roadTex);
            if (mat.HasProperty("_RockMap"))     mat.SetTexture("_RockMap", rockTex);
            return mat;
        }

        private static Material CreateWaterMaterial(string theater)
        {
            Shader ws = Shader.Find("ReCnc/Ra2WaterLit");
            return ws != null ? new Material(ws) : CreateLitMaterial(new Color(0.11f, 0.38f, 0.62f, 0.85f), 0.85f);
        }

        private static Texture2D GetOrBuildNoiseTexture(string key, Color colDark, Color colLight, float baseFreq, float detailAmp)
        {
            Texture2D tex;
            if (_proceduralTexCache.TryGetValue(key, out tex) && tex != null) return tex;

            const int sz = 128;
            tex = new Texture2D(sz, sz, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var pixels = new Color[sz * sz];
            for (int y = 0; y < sz; y++)
            {
                float v = y / (float)sz;
                for (int x = 0; x < sz; x++)
                {
                    float u = x / (float)sz;
                    float n1 = Mathf.PerlinNoise(u * baseFreq, v * baseFreq);
                    float n2 = Mathf.PerlinNoise(u * baseFreq * 2f + 3.1f, v * baseFreq * 2f + 1.7f) * detailAmp;
                    pixels[y * sz + x] = Color.Lerp(colDark, colLight, Mathf.Clamp01(n1 * 0.78f + n2));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            _proceduralTexCache[key] = tex;
            return tex;
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
