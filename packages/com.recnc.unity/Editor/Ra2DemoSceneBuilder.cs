using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ReCnc.Core.Data;
using ReCnc.Unity.Assets;
using ReCnc.Unity.CameraControl;
using ReCnc.Unity.InputControl;
using ReCnc.Unity.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReCnc.Unity.Editor
{
    public static class Ra2DemoSceneBuilder
    {
        private const string RegistryAssetPath = "Assets/_ReCncGenerated/Ra2AssetRegistry.asset";
        private const string DemoScenePath = "Assets/Scenes/Ra2PlayableDemo.unity";

        private const string PendingBuildSessionKey = "ReCnc.PendingSceneBuildAfterExitPlayMode";

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(PendingBuildSessionKey, false))
            {
                SessionState.SetBool(PendingBuildSessionKey, false);
                EditorApplication.delayCall += () =>
                {
                    ExecuteBuildSceneInEditMode(autoEnterPlayMode: true);
                };
            }
        }

        [MenuItem("ReCnc/1. 一键装配资源并打开可玩 3D Demo 场景 (Build & Open 3D Demo)", priority = 10)]
        public static void BuildAndOpenPlayableDemoScene()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEngine.Debug.Log("[ReCnc] 检测到当前处于运行模式 (Play Mode)，正在自动退出运行模式并重建 3D Demo 场景...");
                SessionState.SetBool(PendingBuildSessionKey, true);
                EditorApplication.isPlaying = false;
                return;
            }

            ExecuteBuildSceneInEditMode(autoEnterPlayMode: false);
        }

        private static void ExecuteBuildSceneInEditMode(bool autoEnterPlayMode)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Force reimport of generated palette LUTs and sprites if needed so Point filter & Sprite settings apply
            if (Directory.Exists("Assets/_ReCncGenerated"))
            {
                AssetDatabase.ImportAsset("Assets/_ReCncGenerated", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            }

            var registry = BuildOrUpdateAssetRegistry();
            CreateDemoScene(registry);

            UnityEngine.Debug.Log("[ReCnc] 红警2 3D 可玩 Demo 场景装配完成！");
            if (autoEnterPlayMode)
            {
                EditorApplication.isPlaying = true;
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "ReCnc 3D Demo 已就绪",
                    "已自动绑定所有转换后的 3D 载具模型、建造图标、立绘与音效，并打开场景 Ra2PlayableDemo.unity！\n\n现在直接点击顶部 ▶ Play 按钮即可开始试玩：\n• 选中盟军基地车，双击或按 [G] 展开建造厂\n• 右侧栏建造发电厂 -> 矿石精炼厂 -> 兵营 / 战车工厂\n• 按 [V] 可实时切换建筑的「原版立绘」与「纯3D白模」模式！",
                    "开始体验");
            }
        }

        [MenuItem("ReCnc/2. 重新运行离线转换器 (Run Ra2Converter)", priority = 20)]
        public static void RunOfflineConverterFromEditor()
        {
            string projectDir = Directory.GetCurrentDirectory();
            string repoRoot = Path.GetFullPath(Path.Combine(projectDir, "..", ".."));
            string converterProj = Path.Combine(repoRoot, "tools", "Ra2Converter", "Ra2Converter.csproj");
            if (!File.Exists(converterProj))
            {
                UnityEngine.Debug.LogError("[ReCnc] 未找到 Ra2Converter.csproj: " + converterProj);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/local/share/dotnet/dotnet",
                Arguments = "run --project \"" + converterProj + "\"",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (!File.Exists(psi.FileName)) psi.FileName = "dotnet";

            using (var proc = Process.Start(psi))
            {
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                UnityEngine.Debug.Log("[Ra2Converter]\n" + stdout);
                if (!string.IsNullOrEmpty(stderr)) UnityEngine.Debug.LogWarning(stderr);
            }

            BuildAndOpenPlayableDemoScene();
        }

        public static Ra2AssetRegistry BuildOrUpdateAssetRegistry()
        {
            Directory.CreateDirectory("Assets/_ReCncGenerated");

            var registry = AssetDatabase.LoadAssetAtPath<Ra2AssetRegistry>(RegistryAssetPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<Ra2AssetRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            }

            registry.VoxelTeamShader = Shader.Find("ReCnc/Ra2VoxelTeamLit");
            registry.Models.Clear();
            registry.Sprites.Clear();
            registry.Cameos.Clear();
            registry.Audios.Clear();

            string manifestPath = Path.Combine(Application.streamingAssetsPath, "Ra2Converted", "asset_manifest.json");
            if (File.Exists(manifestPath))
            {
                var manifest = JsonUtility.FromJson<ConvertedAssetManifest>(File.ReadAllText(manifestPath));
                if (manifest != null)
                {
                    for (int i = 0; i < manifest.Models.Count; i++)
                    {
                        var m = manifest.Models[i];
                        var bodyGo = !string.IsNullOrEmpty(m.BodyObjPath) ? AssetDatabase.LoadAssetAtPath<GameObject>(m.BodyObjPath) : null;
                        if (bodyGo == null) continue;

                        registry.Models.Add(new VoxelModelAssetEntry
                        {
                            UnitId = m.UnitId,
                            BodyModelAsset = bodyGo,
                            TurretModelAsset = !string.IsNullOrEmpty(m.TurretObjPath) ? AssetDatabase.LoadAssetAtPath<GameObject>(m.TurretObjPath) : null,
                            BarrelModelAsset = !string.IsNullOrEmpty(m.BarrelObjPath) ? AssetDatabase.LoadAssetAtPath<GameObject>(m.BarrelObjPath) : null,
                            PaletteLutTexture = !string.IsNullOrEmpty(m.PaletteTexPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(m.PaletteTexPath) : null,
                            TurretOffset = new Vector3(m.TurretOffsetX, m.TurretOffsetY, m.TurretOffsetZ),
                            BarrelOffset = new Vector3(m.BarrelOffsetX, m.BarrelOffsetY, m.BarrelOffsetZ)
                        });
                    }

                    for (int i = 0; i < manifest.Sprites.Count; i++)
                    {
                        var s = manifest.Sprites[i];
                        var spr = LoadSprite(s.PngPath);
                        if (spr != null) registry.Sprites.Add(new SpriteAssetEntry { Id = s.Id, Sprite = spr });
                    }

                    for (int i = 0; i < manifest.Cameos.Count; i++)
                    {
                        var c = manifest.Cameos[i];
                        var spr = LoadSprite(c.PngPath);
                        if (spr != null) registry.Cameos.Add(new SpriteAssetEntry { Id = c.Id, Sprite = spr });
                    }

                    for (int i = 0; i < manifest.Audios.Count; i++)
                    {
                        var a = manifest.Audios[i];
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(a.WavPath);
                        if (clip != null) registry.Audios.Add(new AudioAssetEntry { Id = a.Id, Clip = clip });
                    }
                }
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            return registry;
        }

        private static Sprite LoadSprite(string assetPath)
        {
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (spr != null) return spr;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                spr = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }
            return spr;
        }

        private static void CreateDemoScene(Ra2AssetRegistry registry)
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Directional Sun Light with soft real-time shadows
            var lightGo = new GameObject("Directional Sun Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.97f, 0.90f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

            // 2. Main RTS 3D Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.13f, 0.18f);
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 300f;
            cam.fieldOfView = 42f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<RtsCamera3DController>();

            // 3. Game Director & Input/HUD Controller
            var directorGo = new GameObject("Ra2GameDirector");
            var director = directorGo.AddComponent<Ra2GameDirector>();
            director.AssetRegistry = registry;
            director.PlayerSide = "Allied";

            var hud = directorGo.AddComponent<RtsInputAndHudController>();
            hud.Director = director;

            EditorSceneManager.SaveScene(scene, DemoScenePath);

            // Register scene in Build Settings
            var existingScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            existingScenes.RemoveAll(s => s.path == DemoScenePath);
            existingScenes.Insert(0, new EditorBuildSettingsScene(DemoScenePath, true));
            EditorBuildSettings.scenes = existingScenes.ToArray();
        }
    }
}
