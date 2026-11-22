using System.Collections.Generic;
using ReCnc.Core.Data;
using ReCnc.Core.Simulation;
using ReCnc.Unity.Assets;
using ReCnc.Unity.World;
using UnityEngine;

namespace ReCnc.Unity.View
{
    public class EntitySelectableTag : MonoBehaviour
    {
        public int EntityId;
    }

    public class Entity3DView : MonoBehaviour
    {
        public int EntityId { get; private set; }
        public EntityState BoundState { get; private set; }

        private Transform _bodyRoot;
        private Transform _turretPivot;
        private Transform _barrelPivot;
        private Vector3 _barrelRestPos;
        private float _recoilOffset;

        private GameObject _spriteHybridRoot;
        private GameObject _blockout3DRoot;
        private Transform _billboardTransform;
        private GameObject _selectionRing;

        private readonly List<Renderer> _teamRenderers = new List<Renderer>();
        private MaterialPropertyBlock _mpb;
        private Color _teamColor;
        private bool _isSelected;

        public void Initialize(
            EntityState state,
            Ra2AssetRegistry registry,
            Material sharedVoxelTemplateMat,
            bool usePure3DForStructures)
        {
            EntityId = state.Id;
            BoundState = state;
            _mpb = new MaterialPropertyBlock();
            _teamColor = state.FactionId == 0
                ? new Color(0.20f, 0.54f, 1.0f, 1f)   // Classic RA2 Allied Blue
                : new Color(0.92f, 0.16f, 0.16f, 1f); // Classic RA2 Soviet Crimson

            var tag = gameObject.AddComponent<EntitySelectableTag>();
            tag.EntityId = state.Id;

            var col = gameObject.AddComponent<BoxCollider>();
            float sx = state.Def.IsBuilding ? state.Def.FootprintX * WorldCoords.CellSize * 0.9f : 1.5f;
            float sz = state.Def.IsBuilding ? state.Def.FootprintY * WorldCoords.CellSize * 0.9f : 1.5f;
            float sy = Mathf.Max(1.1f, state.Def.Height);
            col.size = new Vector3(sx, sy, sz);
            col.center = new Vector3(0f, sy * 0.5f, 0f);

            _bodyRoot = new GameObject("BodyRoot").transform;
            _bodyRoot.SetParent(transform, false);

            var voxelEntry = registry != null ? registry.GetModel(state.Def.Id) : null;
            if (voxelEntry != null && voxelEntry.BodyModelAsset != null)
            {
                BuildRealVoxelHierarchy(voxelEntry, sharedVoxelTemplateMat);
            }
            else if (state.Def.IsBuilding || state.Def.Role == "Infantry")
            {
                BuildDualModeStructureOrInfantry(state, registry);
                SetPure3DMode(usePure3DForStructures);
            }
            else
            {
                BuildProceduralVehicleFallback(state.Def);
            }

            BuildSelectionRing(Mathf.Max(sx, sz) * 0.58f);
            ApplyTeamProperties();
        }

        public void TriggerRecoil()
        {
            _recoilOffset = 0.22f;
        }

        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;
            _isSelected = selected;
            if (_selectionRing != null) _selectionRing.SetActive(selected);
            ApplyTeamProperties();
        }

        public void SetPure3DMode(bool pure3D)
        {
            if (_spriteHybridRoot != null && _blockout3DRoot != null)
            {
                _spriteHybridRoot.SetActive(!pure3D);
                _blockout3DRoot.SetActive(pure3D);
            }
        }

        public void SyncFromSim(ReCnc.Core.Map.GridMap map, Camera mainCam, float dt)
        {
            if (BoundState == null || !BoundState.IsAlive) return;

            transform.position = WorldCoords.SimToWorld(map, BoundState.Position);

            if (!BoundState.Def.IsBuilding && _bodyRoot != null)
            {
                _bodyRoot.localRotation = Quaternion.Euler(0f, BoundState.BodyYawDeg, 0f);
            }

            if (_turretPivot != null)
            {
                _turretPivot.rotation = Quaternion.Euler(0f, BoundState.TurretYawDeg, 0f);
            }

            if (_barrelPivot != null)
            {
                if (_recoilOffset > 0.001f)
                {
                    _recoilOffset = Mathf.MoveTowards(_recoilOffset, 0f, dt * 0.85f);
                }
                _barrelPivot.localPosition = _barrelRestPos - new Vector3(0f, 0f, _recoilOffset);
            }

            if (_billboardTransform != null && mainCam != null && _billboardTransform.gameObject.activeInHierarchy)
            {
                // Face camera yaw + slight tilt so isometric RA2 building/infantry art looks natural in 3D space!
                Vector3 camEuler = mainCam.transform.eulerAngles;
                _billboardTransform.rotation = Quaternion.Euler(camEuler.x * 0.45f, camEuler.y, 0f);
            }
        }

        private void BuildRealVoxelHierarchy(VoxelModelAssetEntry entry, Material templateMat)
        {
            var bodyInst = Instantiate(entry.BodyModelAsset, _bodyRoot, false);
            bodyInst.transform.localPosition = Vector3.zero;
            bodyInst.transform.localRotation = Quaternion.identity;
            bodyInst.transform.localScale = Vector3.one * 1.18f;
            AssignVoxelMaterialRecursive(bodyInst, entry.PaletteLutTexture, templateMat);

            if (entry.TurretModelAsset != null)
            {
                _turretPivot = new GameObject("TurretPivot").transform;
                _turretPivot.SetParent(transform, false);
                _turretPivot.localPosition = entry.TurretOffset * 1.18f;

                var turInst = Instantiate(entry.TurretModelAsset, _turretPivot, false);
                turInst.transform.localPosition = Vector3.zero;
                turInst.transform.localScale = Vector3.one * 1.18f;
                AssignVoxelMaterialRecursive(turInst, entry.PaletteLutTexture, templateMat);

                if (entry.BarrelModelAsset != null)
                {
                    _barrelPivot = new GameObject("BarrelPivot").transform;
                    _barrelPivot.SetParent(_turretPivot, false);
                    _barrelRestPos = entry.BarrelOffset * 1.18f;
                    _barrelPivot.localPosition = _barrelRestPos;

                    var barlInst = Instantiate(entry.BarrelModelAsset, _barrelPivot, false);
                    barlInst.transform.localPosition = Vector3.zero;
                    barlInst.transform.localScale = Vector3.one * 1.18f;
                    AssignVoxelMaterialRecursive(barlInst, entry.PaletteLutTexture, templateMat);
                }
            }
        }

        private void AssignVoxelMaterialRecursive(GameObject root, Texture2D lut, Material templateMat)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var instanceMat = new Material(templateMat);
            if (lut != null)
            {
                instanceMat.SetTexture("_MainTex", lut);
                instanceMat.mainTexture = lut;
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = instanceMat;
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[i].receiveShadows = true;
                _teamRenderers.Add(renderers[i]);
            }
        }

        private void BuildDualModeStructureOrInfantry(EntityState state, Ra2AssetRegistry registry)
        {
            Sprite spr = registry != null ? registry.GetSprite(state.Def.Id) : null;

            _spriteHybridRoot = new GameObject("HybridSprite3D");
            _spriteHybridRoot.transform.SetParent(transform, false);

            _blockout3DRoot = new GameObject("PureBlockout3D");
            _blockout3DRoot.transform.SetParent(transform, false);

            Material teamMat = Terrain3DBuilder.CreateLitMaterial(_teamColor, 0.45f);
            Material steelMat = Terrain3DBuilder.CreateLitMaterial(new Color(0.62f, 0.65f, 0.69f), 0.55f);
            Material darkMat = Terrain3DBuilder.CreateLitMaterial(new Color(0.24f, 0.26f, 0.28f), 0.25f);

            if (state.Def.IsBuilding)
            {
                float wx = state.Def.FootprintX * WorldCoords.CellSize * 0.92f;
                float wz = state.Def.FootprintY * WorldCoords.CellSize * 0.92f;

                // 3D Concrete foundation pad shared visually under hybrid sprite
                var pad = CreatePart(PrimitiveType.Cube, _spriteHybridRoot.transform, new Vector3(0f, 0.1f, 0f), new Vector3(wx, 0.2f, wz), darkMat);
                _ = pad;

                if (spr != null)
                {
                    var bbGo = new GameObject("BuildingSpriteBillboard");
                    bbGo.transform.SetParent(_spriteHybridRoot.transform, false);
                    float targetWorldWidth = Mathf.Max(wx, wz) * 1.18f;
                    float spriteWorldW = spr.rect.width / spr.pixelsPerUnit;
                    float scale = spriteWorldW > 0.01f ? (targetWorldWidth / spriteWorldW) : 2.2f;
                    bbGo.transform.localPosition = new Vector3(0f, (spr.rect.height / spr.pixelsPerUnit) * scale * 0.42f + 0.15f, 0f);
                    bbGo.transform.localScale = new Vector3(scale, scale, scale);

                    var sr = bbGo.AddComponent<SpriteRenderer>();
                    sr.sprite = spr;
                    sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    _billboardTransform = bbGo.transform;
                }

                // Pure 3D Architectural Blockout Model (Mode B)
                BuildArchitectural3DBuilding(_blockout3DRoot.transform, state.Def, wx, wz, teamMat, steelMat, darkMat);
            }
            else
            {
                // Infantry
                if (spr != null)
                {
                    var bbGo = new GameObject("InfantryBillboard");
                    bbGo.transform.SetParent(_spriteHybridRoot.transform, false);
                    float spriteH = spr.rect.height / spr.pixelsPerUnit;
                    float scale = spriteH > 0.01f ? (1.25f / spriteH) : 2.5f;
                    bbGo.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                    bbGo.transform.localScale = new Vector3(scale, scale, scale);
                    var sr = bbGo.AddComponent<SpriteRenderer>();
                    sr.sprite = spr;
                    sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    _billboardTransform = bbGo.transform;
                }

                // Pure 3D Low-Poly Soldier
                CreatePart(PrimitiveType.Capsule, _blockout3DRoot.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.42f, 0.52f, 0.42f), teamMat);
                CreatePart(PrimitiveType.Sphere, _blockout3DRoot.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.34f, 0.34f, 0.34f), steelMat);
                CreatePart(PrimitiveType.Cube, _blockout3DRoot.transform, new Vector3(0.18f, 0.68f, 0.28f), new Vector3(0.10f, 0.10f, 0.55f), darkMat);
            }

            if (spr == null)
            {
                _spriteHybridRoot.SetActive(false);
                _blockout3DRoot.SetActive(true);
            }
        }

        private void BuildArchitectural3DBuilding(
            Transform root,
            ActorDefData def,
            float wx,
            float wz,
            Material teamMat,
            Material steelMat,
            Material darkMat)
        {
            CreatePart(PrimitiveType.Cube, root, new Vector3(0f, 0.15f, 0f), new Vector3(wx, 0.3f, wz), darkMat);

            string idUpper = def.Id.ToUpperInvariant();
            if (idUpper.Contains("POWR"))
            {
                CreatePart(PrimitiveType.Cube, root, new Vector3(-wx * 0.18f, 0.9f, 0f), new Vector3(wx * 0.48f, 1.5f, wz * 0.7f), steelMat);
                CreatePart(PrimitiveType.Cylinder, root, new Vector3(wx * 0.22f, 1.35f, wz * 0.18f), new Vector3(0.95f, 1.25f, 0.95f), teamMat);
                CreatePart(PrimitiveType.Cylinder, root, new Vector3(wx * 0.22f, 1.15f, -wz * 0.22f), new Vector3(0.85f, 1.05f, 0.85f), teamMat);
            }
            else if (idUpper.Contains("REFN"))
            {
                CreatePart(PrimitiveType.Cube, root, new Vector3(0f, 1.15f, wz * 0.16f), new Vector3(wx * 0.82f, 2.0f, wz * 0.52f), steelMat);
                CreatePart(PrimitiveType.Cylinder, root, new Vector3(-wx * 0.28f, 1.6f, wz * 0.2f), new Vector3(1.1f, 1.5f, 1.1f), teamMat);
                CreatePart(PrimitiveType.Cube, root, new Vector3(0f, 0.35f, -wz * 0.28f), new Vector3(wx * 0.55f, 0.35f, wz * 0.36f), teamMat);
            }
            else if (idUpper.Contains("WEAP"))
            {
                CreatePart(PrimitiveType.Cube, root, new Vector3(0f, 1.35f, 0.15f), new Vector3(wx * 0.86f, 2.4f, wz * 0.78f), steelMat);
                CreatePart(PrimitiveType.Cube, root, new Vector3(0f, 2.65f, 0.15f), new Vector3(wx * 0.65f, 0.35f, wz * 0.65f), teamMat);
                CreatePart(PrimitiveType.Cube, root, new Vector3(0f, 0.9f, -wz * 0.40f), new Vector3(wx * 0.42f, 1.5f, 0.15f), darkMat);
            }
            else if (idUpper.Contains("TESLA") || idUpper.Contains("ATESLA"))
            {
                CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 1.4f, 0f), new Vector3(0.65f, 1.3f, 0.65f), steelMat);
                var emitter = CreatePart(PrimitiveType.Sphere, root, new Vector3(0f, 2.85f, 0f), new Vector3(0.75f, 0.75f, 0.75f), teamMat);
                _turretPivot = emitter.transform;
            }
            else
            {
                float h = Mathf.Max(1.4f, def.Height);
                CreatePart(PrimitiveType.Cube, root, new Vector3(0f, h * 0.45f, 0f), new Vector3(wx * 0.78f, h * 0.8f, wz * 0.78f), steelMat);
                CreatePart(PrimitiveType.Cube, root, new Vector3(-wx * 0.15f, h * 0.92f, 0f), new Vector3(wx * 0.42f, h * 0.3f, wz * 0.52f), teamMat);
            }
        }

        private void BuildProceduralVehicleFallback(ActorDefData def)
        {
            Material teamMat = Terrain3DBuilder.CreateLitMaterial(_teamColor, 0.5f);
            Material darkMat = Terrain3DBuilder.CreateLitMaterial(new Color(0.2f, 0.22f, 0.24f), 0.3f);
            CreatePart(PrimitiveType.Cube, _bodyRoot, new Vector3(0f, 0.4f, 0f), new Vector3(1.3f, 0.55f, 1.85f), teamMat);
            if (def.HasTurret)
            {
                _turretPivot = new GameObject("TurretPivot").transform;
                _turretPivot.SetParent(transform, false);
                _turretPivot.localPosition = new Vector3(0f, 0.75f, 0f);
                CreatePart(PrimitiveType.Cube, _turretPivot, Vector3.zero, new Vector3(0.85f, 0.38f, 0.95f), teamMat);
                CreatePart(PrimitiveType.Cylinder, _turretPivot, new Vector3(0f, 0.05f, 0.85f), new Vector3(0.14f, 0.65f, 0.14f), darkMat).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private static GameObject CreatePart(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private void BuildSelectionRing(float radius)
        {
            _selectionRing = new GameObject("SelectionRing");
            _selectionRing.transform.SetParent(transform, false);
            _selectionRing.transform.localPosition = new Vector3(0f, 0.08f, 0f);

            var lr = _selectionRing.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.widthMultiplier = 0.09f;
            const int segs = 28;
            lr.positionCount = segs;
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            var ringMat = new Material(Shader.Find("Sprites/Default"));
            ringMat.color = new Color(0.25f, 1.0f, 0.38f, 0.95f);
            lr.sharedMaterial = ringMat;
            _selectionRing.SetActive(false);
        }

        private void ApplyTeamProperties()
        {
            for (int i = 0; i < _teamRenderers.Count; i++)
            {
                var r = _teamRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_TeamColor", _teamColor);
                _mpb.SetFloat("_SelectedBoost", _isSelected ? 1f : 0f);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
