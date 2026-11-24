using System.Collections.Generic;
using ReCnc.Core.Data;
using ReCnc.Core.Simulation;
using ReCnc.Unity.Assets;
using ReCnc.Unity.World;
using UnityEngine;
using UnityEngine.Rendering;

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

        // Remastered 3D FBX nodes (Mode 0 - Default!)
        private GameObject _remasteredRoot;
        private Transform _remBodyYawNode;
        private Transform _remTurretPivot;
        private Transform _remBarrelPivot;
        private Transform _remSpinPivot;
        private Vector3 _remBarrelRestPos;

        // Classic VXL/SHP nodes (Mode 1 - Legacy comparison toggle!)
        private GameObject _legacyRoot;
        private Transform _legBodyYawNode;
        private Transform _legTurretPivot;
        private Transform _legBarrelPivot;
        private Vector3 _legBarrelRestPos;
        private Transform _billboardTransform;

        private float _recoilOffset;
        private GameObject _selectionRing;

        private readonly List<Renderer> _voxelTeamRenderers = new List<Renderer>();
        private readonly List<Material> _pbrTeamMaterials = new List<Material>();
        private MaterialPropertyBlock _mpb;
        private Color _teamColor;
        private bool _isSelected;
        private bool _hasRemastered;

        public void Initialize(
            EntityState state,
            Ra2AssetRegistry registry,
            Material sharedVoxelTemplateMat,
            bool useLegacyVisuals)
        {
            EntityId = state.Id;
            BoundState = state;
            _mpb = new MaterialPropertyBlock();
            _teamColor = state.FactionId == 0
                ? new Color(0.18f, 0.52f, 0.98f, 1f)   // Allied Blue
                : new Color(0.88f, 0.15f, 0.15f, 1f); // Soviet Crimson

            var tag = gameObject.AddComponent<EntitySelectableTag>();
            tag.EntityId = state.Id;

            var col = gameObject.AddComponent<BoxCollider>();
            float sx = state.Def.IsBuilding ? state.Def.FootprintX * WorldCoords.CellSize * 0.9f : 1.5f;
            float sz = state.Def.IsBuilding ? state.Def.FootprintY * WorldCoords.CellSize * 0.9f : 1.5f;
            float sy = Mathf.Max(1.2f, state.Def.Height);
            col.size = new Vector3(sx, sy, sz);
            col.center = new Vector3(0f, sy * 0.5f, 0f);

            // 1. Build Remastered PBR 3D Hierarchy from Blender FBX if available
            GameObject remPrefab = registry != null ? registry.GetRemasteredModel(state.Def.Id) : null;
            if (remPrefab != null)
            {
                BuildRemasteredHierarchy(remPrefab, registry);
                _hasRemastered = true;
            }

            // 2. Build Legacy VXL/SHP/Procedural Hierarchy (used when toggling [V] or as fallback)
            BuildLegacyHierarchy(state, registry, sharedVoxelTemplateMat);

            SetLegacyVisualMode(useLegacyVisuals);
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

        public void SetLegacyVisualMode(bool useLegacy)
        {
            if (_hasRemastered && _remasteredRoot != null && _legacyRoot != null)
            {
                _remasteredRoot.SetActive(!useLegacy);
                _legacyRoot.SetActive(useLegacy);
            }
            else if (_legacyRoot != null)
            {
                _legacyRoot.SetActive(true);
            }
        }

        public void SyncFromSim(ReCnc.Core.Map.GridMap map, Camera mainCam, float dt)
        {
            if (BoundState == null || !BoundState.IsAlive) return;

            Vector3 worldPos = WorldCoords.SimToWorld(map, BoundState.Position);

            // Subtle 3D running stride bob for infantry while moving!
            if (BoundState.Def.Role == "Infantry" && BoundState.Path.Count > 0)
            {
                float phase = Time.time * 15f + EntityId * 1.7f;
                worldPos.y += Mathf.Abs(Mathf.Sin(phase)) * 0.09f;
            }
            transform.position = worldPos;

            if (_recoilOffset > 0.001f)
            {
                _recoilOffset = Mathf.MoveTowards(_recoilOffset, 0f, dt * 0.9f);
            }

            // Sync Remastered PBR 3D Hierarchy
            if (_remasteredRoot != null && _remasteredRoot.activeSelf)
            {
                if (!BoundState.Def.IsBuilding && _remBodyYawNode != null)
                {
                    float sway = (BoundState.Def.Role == "Infantry" && BoundState.Path.Count > 0)
                        ? Mathf.Sin(Time.time * 15f + EntityId * 1.7f) * 6f
                        : 0f;
                    _remBodyYawNode.localRotation = Quaternion.Euler(0f, BoundState.BodyYawDeg, sway);
                }

                if (_remTurretPivot != null)
                {
                    _remTurretPivot.rotation = Quaternion.Euler(0f, BoundState.TurretYawDeg, 0f);
                }

                if (_remBarrelPivot != null)
                {
                    _remBarrelPivot.localPosition = _remBarrelRestPos - new Vector3(0f, 0f, _recoilOffset);
                }

                if (_remSpinPivot != null)
                {
                    _remSpinPivot.Rotate(0f, 72f * dt, 0f, Space.Self);
                }
            }

            // Sync Legacy VXL/SHP Hierarchy
            if (_legacyRoot != null && _legacyRoot.activeSelf)
            {
                if (!BoundState.Def.IsBuilding && _legBodyYawNode != null)
                {
                    _legBodyYawNode.localRotation = Quaternion.Euler(0f, BoundState.BodyYawDeg, 0f);
                }

                if (_legTurretPivot != null)
                {
                    _legTurretPivot.rotation = Quaternion.Euler(0f, BoundState.TurretYawDeg, 0f);
                }

                if (_legBarrelPivot != null)
                {
                    _legBarrelPivot.localPosition = _legBarrelRestPos - new Vector3(0f, 0f, _recoilOffset);
                }

                if (_billboardTransform != null && mainCam != null)
                {
                    Vector3 camEuler = mainCam.transform.eulerAngles;
                    _billboardTransform.rotation = Quaternion.Euler(camEuler.x * 0.45f, camEuler.y, 0f);
                }
            }
        }

        private void BuildRemasteredHierarchy(GameObject fbxPrefab, Ra2AssetRegistry registry)
        {
            _remasteredRoot = new GameObject("Remastered3D");
            _remasteredRoot.transform.SetParent(transform, false);

            _remBodyYawNode = new GameObject("YawRoot").transform;
            _remBodyYawNode.SetParent(_remasteredRoot.transform, false);

            var modelInst = Instantiate(fbxPrefab, _remBodyYawNode, false);
            modelInst.name = fbxPrefab.name;
            modelInst.transform.localPosition = Vector3.zero;
            modelInst.transform.localRotation = Quaternion.identity;
            modelInst.transform.localScale = Vector3.one;

            var renderers = modelInst.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;

                var srcMats = r.sharedMaterials;
                var newMats = new Material[srcMats.Length];
                for (int m = 0; m < srcMats.Length; m++)
                {
                    Material srcMat = srcMats[m];
                    string slotName = srcMat != null ? srcMat.name : "Mat_BrushedSteel";
                    if (slotName.StartsWith("Mat_AiBaked"))
                    {
                        PbrTexturePairEntry bakedTex = registry != null ? registry.GetPbrTextures("AiBaked_" + BoundState.Def.Id) : null;
                        Texture mainTex = (bakedTex != null && bakedTex.AlbedoMap != null)
                            ? bakedTex.AlbedoMap
                            : (srcMat != null ? srcMat.mainTexture : null);
                        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        var aiMat = new Material(urpLit);
                        Ra2PbrMaterials.SetLitColor(aiMat, Color.white);
                        if (mainTex != null)
                        {
                            if (aiMat.HasProperty("_BaseMap")) aiMat.SetTexture("_BaseMap", mainTex);
                            aiMat.mainTexture = mainTex;
                        }
                        if (aiMat.HasProperty("_Metallic")) aiMat.SetFloat("_Metallic", 0.18f);
                        if (aiMat.HasProperty("_Smoothness")) aiMat.SetFloat("_Smoothness", 0.36f);
                        newMats[m] = aiMat;
                        continue;
                    }

                    bool isTeam;
                    Material resolved = Ra2PbrMaterials.ResolveSlotMaterial(slotName, _teamColor, registry, out isTeam);
                    newMats[m] = resolved;
                    if (isTeam) _pbrTeamMaterials.Add(resolved);
                }
                r.sharedMaterials = newMats;
            }

            // Locate articulated child nodes exported by Blender
            _remTurretPivot = FindDeepChild(modelInst.transform, "TurretPivot");
            if (_remTurretPivot != null)
            {
                _remBarrelPivot = FindDeepChild(_remTurretPivot, "BarrelPivot");
                if (_remBarrelPivot != null)
                {
                    _remBarrelRestPos = _remBarrelPivot.localPosition;
                }
            }
            _remSpinPivot = FindDeepChild(modelInst.transform, "SpinPivot");
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == targetName) return c;
                var found = FindDeepChild(c, targetName);
                if (found != null) return found;
            }
            return null;
        }

        private void BuildLegacyHierarchy(EntityState state, Ra2AssetRegistry registry, Material voxelTemplateMat)
        {
            _legacyRoot = new GameObject("LegacyVisuals");
            _legacyRoot.transform.SetParent(transform, false);

            _legBodyYawNode = new GameObject("BodyRoot").transform;
            _legBodyYawNode.SetParent(_legacyRoot.transform, false);

            var voxelEntry = registry != null ? registry.GetModel(state.Def.Id) : null;
            if (voxelEntry != null && voxelEntry.BodyModelAsset != null)
            {
                var bodyInst = Instantiate(voxelEntry.BodyModelAsset, _legBodyYawNode, false);
                bodyInst.transform.localScale = Vector3.one * 1.18f;
                AssignVoxelMaterialRecursive(bodyInst, voxelEntry.PaletteLutTexture, voxelTemplateMat);

                if (voxelEntry.TurretModelAsset != null)
                {
                    _legTurretPivot = new GameObject("TurretPivot").transform;
                    _legTurretPivot.SetParent(_legacyRoot.transform, false);
                    _legTurretPivot.localPosition = voxelEntry.TurretOffset * 1.18f;

                    var turInst = Instantiate(voxelEntry.TurretModelAsset, _legTurretPivot, false);
                    turInst.transform.localScale = Vector3.one * 1.18f;
                    AssignVoxelMaterialRecursive(turInst, voxelEntry.PaletteLutTexture, voxelTemplateMat);

                    if (voxelEntry.BarrelModelAsset != null)
                    {
                        _legBarrelPivot = new GameObject("BarrelPivot").transform;
                        _legBarrelPivot.SetParent(_legTurretPivot, false);
                        _legBarrelRestPos = voxelEntry.BarrelOffset * 1.18f;
                        _legBarrelPivot.localPosition = _legBarrelRestPos;

                        var barlInst = Instantiate(voxelEntry.BarrelModelAsset, _legBarrelPivot, false);
                        barlInst.transform.localScale = Vector3.one * 1.18f;
                        AssignVoxelMaterialRecursive(barlInst, voxelEntry.PaletteLutTexture, voxelTemplateMat);
                    }
                }
                return;
            }

            Sprite spr = registry != null ? registry.GetSprite(state.Def.Id) : null;
            if (spr != null)
            {
                float wx = state.Def.FootprintX * WorldCoords.CellSize * 0.92f;
                float wz = state.Def.FootprintY * WorldCoords.CellSize * 0.92f;
                if (state.Def.IsBuilding)
                {
                    var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(pad.GetComponent<Collider>());
                    pad.transform.SetParent(_legacyRoot.transform, false);
                    pad.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    pad.transform.localScale = new Vector3(wx, 0.2f, wz);
                    pad.GetComponent<MeshRenderer>().sharedMaterial = Terrain3DBuilder.CreateLitMaterial(new Color(0.24f, 0.26f, 0.28f), 0.25f);
                }

                var bbGo = new GameObject("ClassicSpriteBillboard");
                bbGo.transform.SetParent(_legacyRoot.transform, false);
                float scale = state.Def.IsBuilding
                    ? (Mathf.Max(wx, wz) * 1.18f) / Mathf.Max(0.1f, spr.rect.width / spr.pixelsPerUnit)
                    : 1.25f / Mathf.Max(0.1f, spr.rect.height / spr.pixelsPerUnit);
                float yOff = state.Def.IsBuilding ? ((spr.rect.height / spr.pixelsPerUnit) * scale * 0.42f + 0.15f) : 0.65f;
                bbGo.transform.localPosition = new Vector3(0f, yOff, 0f);
                bbGo.transform.localScale = new Vector3(scale, scale, scale);
                var sr = bbGo.AddComponent<SpriteRenderer>();
                sr.sprite = spr;
                sr.shadowCastingMode = ShadowCastingMode.On;
                _billboardTransform = bbGo.transform;
            }
            else
            {
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(fallback.GetComponent<Collider>());
                fallback.transform.SetParent(_legBodyYawNode, false);
                fallback.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                fallback.GetComponent<MeshRenderer>().sharedMaterial = Terrain3DBuilder.CreateLitMaterial(_teamColor, 0.5f);
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
                renderers[i].shadowCastingMode = ShadowCastingMode.On;
                renderers[i].receiveShadows = true;
                _voxelTeamRenderers.Add(renderers[i]);
            }
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
            var ringMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.25f, 1.0f, 0.38f, 0.95f) };
            lr.sharedMaterial = ringMat;
            _selectionRing.SetActive(false);
        }

        private void ApplyTeamProperties()
        {
            Color activeTeamCol = _isSelected
                ? Color.Lerp(_teamColor, Color.white, 0.25f)
                : _teamColor;

            for (int i = 0; i < _pbrTeamMaterials.Count; i++)
            {
                if (_pbrTeamMaterials[i] != null)
                {
                    Ra2PbrMaterials.SetLitColor(_pbrTeamMaterials[i], activeTeamCol);
                }
            }

            for (int i = 0; i < _voxelTeamRenderers.Count; i++)
            {
                var r = _voxelTeamRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_TeamColor", _teamColor);
                _mpb.SetFloat("_SelectedBoost", _isSelected ? 1f : 0f);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
