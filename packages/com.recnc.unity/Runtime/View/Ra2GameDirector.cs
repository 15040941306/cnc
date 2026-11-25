using System.Collections.Generic;
using System.IO;
using ReCnc.Core.Data;
using ReCnc.Core.Simulation;
using ReCnc.Unity.Assets;
using ReCnc.Unity.CameraControl;
using ReCnc.Unity.World;
using UnityEngine;

namespace ReCnc.Unity.View
{
    public class Ra2GameDirector : MonoBehaviour
    {
        public Ra2AssetRegistry AssetRegistry;
        public string MapFileName = "maps/skirmish_valley.json";
        public string PlayerSide = "Allied";
        public int StartingCredits = 10000;
        public bool Pure3DStructuresMode = false;
        public bool IsInSkirmishLobby = true;

        public List<SkirmishMapEntryData> AvailableMaps { get; private set; } = new List<SkirmishMapEntryData>();
        public GameSimulation Simulation { get; private set; }
        public Terrain3DBuilder TerrainBuilder { get; private set; }

        private readonly Dictionary<int, Entity3DView> _entityViews = new Dictionary<int, Entity3DView>();
        private readonly Dictionary<int, GameObject> _projectileViews = new Dictionary<int, GameObject>();
        private readonly List<SimEvent> _eventBuffer = new List<SimEvent>();
        private readonly List<int> _staleIds = new List<int>();

        private Material _voxelTemplateMat;
        private Material _shellMat;
        private Material _laserMat;
        private Material _teslaMat;
        private AudioSource _evaAudioSource;
        private float _oreSyncTimer;

        private void Awake()
        {
            _shellMat = Terrain3DBuilder.CreateLitMaterial(new Color(1.0f, 0.78f, 0.25f), 0.8f);
            _laserMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.45f, 0.92f, 1.0f, 0.95f) };
            _teslaMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.75f, 0.85f, 1.0f, 1.0f) };

            _evaAudioSource = gameObject.AddComponent<AudioSource>();
            _evaAudioSource.spatialBlend = 0f;
            _evaAudioSource.volume = 0.85f;
        }

        private void Start()
        {
            // Bright hemisphere daylight ambient lighting so metallic silver & brass surfaces gleam like original RA2!
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.76f, 0.88f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.54f, 0.60f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.34f, 0.30f, 1f);

            if (AssetRegistry == null)
            {
                AssetRegistry = Resources.Load<Ra2AssetRegistry>("Ra2AssetRegistry");
            }
            if (AssetRegistry != null)
            {
                Ra2PbrMaterials.BindRegistryTextures(AssetRegistry);
            }

            Shader voxelShader = (AssetRegistry != null && AssetRegistry.VoxelTeamShader != null)
                ? AssetRegistry.VoxelTeamShader
                : (Shader.Find("ReCnc/Ra2VoxelTeamLit") ?? Shader.Find("Universal Render Pipeline/Lit"));
            _voxelTemplateMat = new Material(voxelShader);

            LoadAvailableMapsCatalog();
            if (!IsInSkirmishLobby)
            {
                InitializeMatch();
            }
        }

        private void LoadAvailableMapsCatalog()
        {
            AvailableMaps.Clear();
            var catalog = TryLoadJsonFromStreamingAssets<SkirmishMapsCatalogData>("maps_index.json");
            if (catalog != null && catalog.Maps != null && catalog.Maps.Count > 0)
            {
                AvailableMaps.AddRange(catalog.Maps);
            }
            else
            {
                AvailableMaps.Add(new SkirmishMapEntryData
                {
                    MapId = "skirmish_valley",
                    DisplayName = "河谷争锋 (内置演示图)",
                    Theater = "TEMPERATE",
                    Width = 64,
                    Height = 64,
                    MinPlayers = 2,
                    MaxPlayers = 2,
                    JsonRelPath = "maps/skirmish_valley.json",
                    IsOfficialRa2Map = false
                });
            }
        }

        public void StartSkirmish(SkirmishMapEntryData selectedMap, string playerSide, int startingCredits)
        {
            if (selectedMap != null && !string.IsNullOrEmpty(selectedMap.JsonRelPath))
            {
                MapFileName = selectedMap.JsonRelPath;
            }
            PlayerSide = string.IsNullOrEmpty(playerSide) ? "Allied" : playerSide;
            StartingCredits = startingCredits;
            IsInSkirmishLobby = false;
            InitializeMatch();
        }

        public void OpenSkirmishLobby()
        {
            IsInSkirmishLobby = true;
        }

        private void ClearAllMatchObjects()
        {
            foreach (var kv in _entityViews)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            _entityViews.Clear();

            foreach (var kv in _projectileViews)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            _projectileViews.Clear();

            if (TerrainBuilder != null)
            {
                Destroy(TerrainBuilder.gameObject);
                TerrainBuilder = null;
            }
        }

        public void InitializeMatch()
        {
            ClearAllMatchObjects();

            RulesBundleData importedRules = TryLoadJsonFromStreamingAssets<RulesBundleData>("rules_bundle.json");
            var catalog = RulesCatalog.CreateWithDefaults(importedRules);

            MapBundleData mapBundle = TryLoadJsonFromStreamingAssets<MapBundleData>(MapFileName)
                ?? DefaultMapFactory.CreateSkirmishArena(64, 64);

            Simulation = GameSimulation.CreateStandardSkirmish(catalog, mapBundle, PlayerSide, StartingCredits);

            if (TerrainBuilder != null) Destroy(TerrainBuilder.gameObject);
            var terrainGo = new GameObject("Ra2Terrain3D");
            terrainGo.transform.SetParent(transform, false);
            TerrainBuilder = terrainGo.AddComponent<Terrain3DBuilder>();
            TerrainBuilder.BuildTerrain(Simulation.Map);

            // Jump camera to player's starting MCV & align to authentic RA2 -45 deg isometric yaw
            var camCtrl = FindObjectOfType<RtsCamera3DController>();
            if (camCtrl != null)
            {
                camCtrl.DefaultYawDegrees = Simulation.Map.IsNativeIsometricGrid ? -45f : 45f;
                camCtrl.YawDegrees = camCtrl.DefaultYawDegrees;
                camCtrl.PitchDegrees = 36f;
                camCtrl.WorldMaxBounds = new Vector2(Simulation.Map.Width * WorldCoords.CellSize - 2f, Simulation.Map.Height * WorldCoords.CellSize - 2f);
                for (int i = 0; i < Simulation.Entities.Count; i++)
                {
                    if (Simulation.Entities[i].FactionId == 0)
                    {
                        camCtrl.JumpTo(WorldCoords.SimToWorld(Simulation.Map, Simulation.Entities[i].Position));
                        break;
                    }
                }
            }

            SyncAllViews(0f);
        }

        public void ToggleStructureVisualMode()
        {
            Pure3DStructuresMode = !Pure3DStructuresMode;
            foreach (var kv in _entityViews)
            {
                if (kv.Value != null) kv.Value.SetLegacyVisualMode(Pure3DStructuresMode);
            }
        }

        public Entity3DView GetEntityView(int entityId)
        {
            Entity3DView v;
            return _entityViews.TryGetValue(entityId, out v) ? v : null;
        }

        public void PlayUnitCommandFeedback(EntityState unit, bool isAttack, bool isSelect)
        {
            if (AssetRegistry == null || unit == null) return;
            List<string> list = isSelect ? unit.Def.VoiceSelect : (isAttack ? unit.Def.VoiceAttack : unit.Def.VoiceMove);
            if (list != null && list.Count > 0)
            {
                PlayClip2D(list[0], 0.7f);
            }
        }

        public void PlayClip2D(string soundId, float volume = 0.8f)
        {
            if (AssetRegistry == null || string.IsNullOrEmpty(soundId)) return;
            var clip = AssetRegistry.GetAudio(soundId);
            if (clip != null && _evaAudioSource != null)
            {
                _evaAudioSource.PlayOneShot(clip, volume);
            }
        }

        private void Update()
        {
            if (IsInSkirmishLobby || Simulation == null) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Simulation.Step(dt);

            ProcessSimulationEvents();
            SyncAllViews(dt);

            _oreSyncTimer += dt;
            if (_oreSyncTimer >= 0.5f)
            {
                _oreSyncTimer = 0f;
                TerrainBuilder.SyncOreVisuals(Simulation.Map);
            }
        }

        private void ProcessSimulationEvents()
        {
            Simulation.DrainEvents(_eventBuffer);
            for (int i = 0; i < _eventBuffer.Count; i++)
            {
                var ev = _eventBuffer[i];
                switch (ev.Kind)
                {
                    case SimEventKind.WeaponFired:
                    {
                        Entity3DView shooterView;
                        if (_entityViews.TryGetValue(ev.SourceEntityId, out shooterView) && shooterView != null)
                        {
                            shooterView.TriggerRecoil();
                        }
                        PlayWorldSound(ev.Tag, WorldCoords.SimToWorld(Simulation.Map, ev.Position), 0.55f);
                        break;
                    }
                    case SimEventKind.ProjectileImpact:
                    {
                        SpawnImpactFlash(WorldCoords.SimToWorld(Simulation.Map, ev.Position) + Vector3.up * 0.45f, 0.55f, new Color(1f, 0.65f, 0.15f));
                        break;
                    }
                    case SimEventKind.EntityDestroyed:
                    {
                        Vector3 pos = WorldCoords.SimToWorld(Simulation.Map, ev.Position);
                        SpawnImpactFlash(pos + Vector3.up * 0.8f, 1.85f, new Color(1f, 0.4f, 0.1f));
                        PlayWorldSound("gexp14a", pos, 0.75f);
                        break;
                    }
                    case SimEventKind.BuildingPlaced:
                    {
                        if (ev.FactionId == 0) PlayClip2D("uplace", 0.85f);
                        break;
                    }
                    case SimEventKind.EvaAnnouncement:
                    {
                        if (ev.FactionId == 0) PlayClip2D(ev.Tag, 0.9f);
                        break;
                    }
                }
            }
        }

        private void PlayWorldSound(string soundId, Vector3 pos, float volume)
        {
            if (AssetRegistry == null || string.IsNullOrEmpty(soundId)) return;
            var clip = AssetRegistry.GetAudio(soundId);
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, pos, volume);
            }
        }

        private void SpawnImpactFlash(Vector3 pos, float maxScale, Color col)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (maxScale * 0.35f);
            var mat = new Material(Shader.Find("Sprites/Default")) { color = col };
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var fx = go.AddComponent<TransientBurstVfx>();
            fx.MaxScale = maxScale;
            fx.BaseColor = col;
            fx.MatInstance = mat;
        }

        private void SyncAllViews(float dt)
        {
            Camera mainCam = Camera.main;
            var entities = Simulation.Entities;
            var activeIds = new HashSet<int>();

            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (!e.IsAlive) continue;
                activeIds.Add(e.Id);

                Entity3DView view;
                if (!_entityViews.TryGetValue(e.Id, out view) || view == null)
                {
                    var go = new GameObject(e.Def.Id + "_" + e.Id);
                    go.transform.SetParent(transform, false);
                    view = go.AddComponent<Entity3DView>();
                    view.Initialize(e, AssetRegistry, _voxelTemplateMat, Pure3DStructuresMode);
                    _entityViews[e.Id] = view;
                }

                view.SyncFromSim(Simulation.Map, mainCam, dt);
            }

            _staleIds.Clear();
            foreach (var kv in _entityViews)
            {
                if (!activeIds.Contains(kv.Key)) _staleIds.Add(kv.Key);
            }
            for (int i = 0; i < _staleIds.Count; i++)
            {
                int id = _staleIds[i];
                if (_entityViews[id] != null) Destroy(_entityViews[id].gameObject);
                _entityViews.Remove(id);
            }

            SyncProjectiles();
        }

        private void SyncProjectiles()
        {
            var projs = Simulation.Projectiles;
            var activeProjIds = new HashSet<int>();

            for (int i = 0; i < projs.Count; i++)
            {
                var p = projs[i];
                activeProjIds.Add(p.Id);

                GameObject pGo;
                if (!_projectileViews.TryGetValue(p.Id, out pGo) || pGo == null)
                {
                    pGo = CreateProjectileVisual(p);
                    _projectileViews[p.Id] = pGo;
                }

                Vector3 startW = WorldCoords.SimToWorld(Simulation.Map, p.StartPos) + Vector3.up * 0.9f;
                Vector3 endW = WorldCoords.SimToWorld(Simulation.Map, p.TargetPos) + Vector3.up * 0.7f;

                if (p.Weapon.ProjectileStyle == "Laser" || p.Weapon.ProjectileStyle == "Tesla")
                {
                    var lr = pGo.GetComponent<LineRenderer>();
                    if (lr != null)
                    {
                        lr.SetPosition(0, startW + Vector3.up * 0.6f);
                        lr.SetPosition(1, endW);
                    }
                }
                else
                {
                    float arc = p.Weapon.ProjectileStyle == "Shell" ? Mathf.Sin(p.Progress * Mathf.PI) * 1.35f : 0f;
                    Vector3 cur = Vector3.Lerp(startW, endW, p.Progress) + Vector3.up * arc;
                    pGo.transform.position = cur;
                }
            }

            _staleIds.Clear();
            foreach (var kv in _projectileViews)
            {
                if (!activeProjIds.Contains(kv.Key)) _staleIds.Add(kv.Key);
            }
            for (int i = 0; i < _staleIds.Count; i++)
            {
                int id = _staleIds[i];
                if (_projectileViews[id] != null) Destroy(_projectileViews[id]);
                _projectileViews.Remove(id);
            }
        }

        private GameObject CreateProjectileVisual(ProjectileState p)
        {
            if (p.Weapon.ProjectileStyle == "Laser" || p.Weapon.ProjectileStyle == "Tesla")
            {
                var beamGo = new GameObject("Beam_" + p.Id);
                beamGo.transform.SetParent(transform, false);
                var lr = beamGo.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.widthMultiplier = p.Weapon.ProjectileStyle == "Tesla" ? 0.18f : 0.14f;
                lr.sharedMaterial = p.Weapon.ProjectileStyle == "Tesla" ? _teslaMat : _laserMat;
                return beamGo;
            }

            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(shell.GetComponent<Collider>());
            shell.name = "Projectile_" + p.Id;
            shell.transform.SetParent(transform, false);
            float s = p.Weapon.ProjectileStyle == "Bullet" ? 0.14f : 0.26f;
            shell.transform.localScale = new Vector3(s, s, s);
            shell.GetComponent<MeshRenderer>().sharedMaterial = _shellMat;
            return shell;
        }

        private static T TryLoadJsonFromStreamingAssets<T>(string relPath) where T : class
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, "Ra2Converted", relPath);
            if (!File.Exists(fullPath)) return null;
            string json = File.ReadAllText(fullPath);
            return JsonUtility.FromJson<T>(json);
        }
    }

    public class TransientBurstVfx : MonoBehaviour
    {
        public float MaxScale = 1f;
        public Color BaseColor = Color.yellow;
        public Material MatInstance;
        private float _t;

        private void Update()
        {
            _t += Time.deltaTime * 4.2f;
            if (_t >= 1f)
            {
                if (MatInstance != null) Destroy(MatInstance);
                Destroy(gameObject);
                return;
            }
            float s = Mathf.Lerp(MaxScale * 0.25f, MaxScale, _t);
            transform.localScale = new Vector3(s, s, s);
            if (MatInstance != null)
            {
                var c = BaseColor;
                c.a = 1f - _t;
                MatInstance.color = c;
            }
        }
    }
}
