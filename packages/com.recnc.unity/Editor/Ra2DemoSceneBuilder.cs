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
        private const string RegistryAssetPath = "Assets/_ReCncGenerated/Resources/Ra2AssetRegistry.asset";
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
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                EnsureRegistryReadyBeforePlay();
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(PendingBuildSessionKey, false))
            {
                SessionState.SetBool(PendingBuildSessionKey, false);
                EditorApplication.delayCall += () =>
                {
                    ExecuteBuildSceneInEditMode(autoEnterPlayMode: true, interactiveDialog: false);
                };
            }
        }

        private static void EnsureRegistryReadyBeforePlay()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Ra2AssetRegistry>(RegistryAssetPath);
            bool hasOriginal = existing != null && existing.Models != null && existing.Models.Count > 0;
            bool hasRemastered = existing != null && existing.RemasteredModels != null && existing.RemasteredModels.Count > 0;
            bool hasPbrTextures = existing != null && existing.PbrTextures != null && existing.PbrTextures.Count > 0;
            bool remasteredFilesOnDisk = Directory.Exists("Assets/_ReCncGenerated/RemasteredModels");
            bool pbrTexOnDisk = Directory.Exists("Assets/_ReCncGenerated/RemasteredTextures");

            int aiFilesCount = Directory.Exists("Assets/_ReCncGenerated/RemasteredTextures/AiBaked")
                ? Directory.GetFiles("Assets/_ReCncGenerated/RemasteredTextures/AiBaked", "*_albedo.png").Length
                : 0;
            int boundAiCount = 0;
            if (existing != null && existing.PbrTextures != null)
            {
                for (int i = 0; i < existing.PbrTextures.Count; i++)
                {
                    if (existing.PbrTextures[i].MaterialSlotName != null && existing.PbrTextures[i].MaterialSlotName.StartsWith("AiBaked_"))
                        boundAiCount++;
                }
            }

            if (hasOriginal && (hasRemastered || !remasteredFilesOnDisk) && (hasPbrTextures || !pbrTexOnDisk) && boundAiCount == aiFilesCount)
            {
                return;
            }

            if (!Directory.Exists("Assets/_ReCncGenerated/Models"))
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
                string ra2MixA = Path.Combine(repoRoot, "raw_assets", "ra2.mix");
                string ra2MixB = Path.Combine(repoRoot, "raw_assets", "RA2.MIX");
                if (File.Exists(ra2MixA) || File.Exists(ra2MixB))
                {
                    UnityEngine.Debug.Log("[ReCnc] 检测到 raw_assets 但尚未离线转换，正在自动调用 Ra2Converter...");
                    TryInvokeConverterProcess();
                }
            }

            if (!Directory.Exists("Assets/_ReCncGenerated/RemasteredModels"))
            {
                TryInvokeBlenderRemasterProcess();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (Directory.Exists("Assets/_ReCncGenerated"))
            {
                BuildOrUpdateAssetRegistry();
            }
        }

        public static void PrepareProjectForBatchMode()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (Directory.Exists("Assets/_ReCncGenerated"))
            {
                AssetDatabase.ImportAsset("Assets/_ReCncGenerated", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            }
            var reg = BuildOrUpdateAssetRegistry();
            UnityEngine.Debug.Log($"[ReCnc BatchVerify] Remastered={reg.RemasteredModels.Count}, VXL={reg.Models.Count}, Cameos={reg.Cameos.Count}, Sprites={reg.Sprites.Count}, Audios={reg.Audios.Count}");
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

            ExecuteBuildSceneInEditMode(autoEnterPlayMode: false, interactiveDialog: true);
        }

        private static void ExecuteBuildSceneInEditMode(bool autoEnterPlayMode, bool interactiveDialog)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (Directory.Exists("Assets/_ReCncGenerated"))
            {
                AssetDatabase.ImportAsset("Assets/_ReCncGenerated", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            }

            var registry = BuildOrUpdateAssetRegistry();
            CreateDemoScene(registry);

            UnityEngine.Debug.Log($"[ReCnc] 3D Demo 场景装配完成（已绑定 {registry.RemasteredModels.Count} 套 Blender 3D 翻新精模）！");
            if (autoEnterPlayMode)
            {
                EditorApplication.isPlaying = true;
            }
            else if (interactiveDialog)
            {
                EditorUtility.DisplayDialog(
                    "ReCnc 3D 翻新精模已就绪",
                    $"已自动绑定 {registry.RemasteredModels.Count} 套 Blender 3D 翻新精模、{registry.Cameos.Count} 个建造图标与音效，并打开场景 Ra2PlayableDemo.unity！\n\n点击顶部 ▶ Play 即可体验（按 [V] 可实时对比切换「Blender 3D翻新精模」与「原版复古模式」）！",
                    "开始体验");
            }
        }

        [MenuItem("ReCnc/2. 重新运行离线转换器 (Run Ra2Converter)", priority = 20)]
        public static void RunOfflineConverterFromEditor()
        {
            if (TryInvokeConverterProcess())
            {
                BuildAndOpenPlayableDemoScene();
            }
        }

        [MenuItem("ReCnc/3. 重新生成 Blender 3D 翻新精模 (Rebuild Blender 3D Models)", priority = 30)]
        public static void RunBlenderRemasterFromEditor()
        {
            if (TryInvokeBlenderRemasterProcess())
            {
                BuildAndOpenPlayableDemoScene();
            }
        }

        [MenuItem("ReCnc/4. 导入外部红警2地图文件 (Import .map / .yrm / .mpr)", priority = 40)]
        public static void ImportExternalRa2MapFromDialog()
        {
            string selected = EditorUtility.OpenFilePanel("选择《红色警戒2 / 尤里的复仇》地图文件", "", "map,yrm,mpr");
            if (string.IsNullOrEmpty(selected)) return;

            if (TryInvokeConverterProcess("-- --map \"" + selected + "\""))
            {
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "红警2地图导入成功",
                    "已将地图 " + Path.GetFileName(selected) + " 1:1 烘焙转换并加入遭遇战地图列表顶部！\n\n进入运行模式 (Play) 即可在「创建遭遇战」界面首行直接选择游玩。",
                    "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("地图导入失败", "请查看 Console 日志确认 raw_assets 中存在红警2地貌 .mix 包。", "关闭");
            }
        }

        private static bool TryInvokeBlenderRemasterProcess()
        {
            string projectDir = Directory.GetCurrentDirectory();
            string repoRoot = Path.GetFullPath(Path.Combine(projectDir, "..", ".."));
            string scriptPath = Path.Combine(repoRoot, "tools", "blender_remaster", "build_all_models.py");
            if (!File.Exists(scriptPath)) return false;

            string blenderExe = null;
            string[] candidates =
            {
                "/Applications/Blender.app/Contents/MacOS/Blender",
                @"C:\Program Files\Blender Foundation\Blender 5.3\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 4.2\blender.exe"
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    blenderExe = candidates[i];
                    break;
                }
            }
            if (string.IsNullOrEmpty(blenderExe)) return false;

            var psi = new ProcessStartInfo
            {
                FileName = blenderExe,
                Arguments = "--background --factory-startup --python \"" + scriptPath + "\"",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            {
                if (proc == null) return false;
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                UnityEngine.Debug.Log("[Blender Remaster]\n" + stdout);
                if (!string.IsNullOrEmpty(stderr)) UnityEngine.Debug.LogWarning(stderr);
                return proc.ExitCode == 0;
            }
        }

        private static bool TryInvokeConverterProcess(string extraArgs = "")
        {
            string projectDir = Directory.GetCurrentDirectory();
            string repoRoot = Path.GetFullPath(Path.Combine(projectDir, "..", ".."));
            string converterProj = Path.Combine(repoRoot, "tools", "Ra2Converter", "Ra2Converter.csproj");
            if (!File.Exists(converterProj))
            {
                UnityEngine.Debug.LogError("[ReCnc] 未找到 Ra2Converter.csproj: " + converterProj);
                return false;
            }

            string dotnetExe = "dotnet";
            string[] candidates =
            {
                "/usr/local/share/dotnet/dotnet",
                "/opt/homebrew/bin/dotnet",
                @"C:\Program Files\dotnet\dotnet.exe"
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    dotnetExe = candidates[i];
                    break;
                }
            }

            string cmdArgs = "run --project \"" + converterProj + "\"" + (string.IsNullOrEmpty(extraArgs) ? "" : " " + extraArgs);
            var psi = new ProcessStartInfo
            {
                FileName = dotnetExe,
                Arguments = cmdArgs,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            {
                if (proc == null) return false;
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                UnityEngine.Debug.Log("[Ra2Converter]\n" + stdout);
                if (!string.IsNullOrEmpty(stderr)) UnityEngine.Debug.LogWarning(stderr);
                return proc.ExitCode == 0;
            }
        }

        public static Ra2AssetRegistry BuildOrUpdateAssetRegistry()
        {
            Directory.CreateDirectory("Assets/_ReCncGenerated/Resources");

            var registry = AssetDatabase.LoadAssetAtPath<Ra2AssetRegistry>(RegistryAssetPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<Ra2AssetRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            }

            registry.VoxelTeamShader = Shader.Find("ReCnc/Ra2VoxelTeamLit");
            registry.RemasteredModels.Clear();
            registry.PbrTextures.Clear();
            registry.Models.Clear();
            registry.Sprites.Clear();
            registry.Cameos.Clear();
            registry.Audios.Clear();

            // 0. Bind procedural high-res PBR Albedo + Normal texture maps
            BindPbrSlot(registry, "Mat_BrushedSteel",    "tex_steel_plates.png",  "tex_steel_plates_n.png");
            BindPbrSlot(registry, "Mat_CorrugatedSteel", "tex_corrugated.png",    "tex_corrugated_n.png");
            BindPbrSlot(registry, "Mat_TeamPaint",       "tex_team_armor.png",    "tex_team_armor_n.png");
            BindPbrSlot(registry, "Mat_DarkSteel",       "tex_dark_mech.png",     "tex_dark_mech_n.png");
            BindPbrSlot(registry, "Mat_Concrete",        "tex_concrete.png",      "tex_concrete_n.png");
            BindPbrSlot(registry, "Mat_SovietBrick",     "tex_soviet_brick.png",  "tex_soviet_brick_n.png");
            BindPbrSlot(registry, "Mat_GoldBrass",       "tex_gold_fluted.png",   "tex_gold_fluted_n.png");
            BindPbrSlot(registry, "Mat_HazardYellow",    "tex_hazard.png",        null);
            BindPbrSlot(registry, "Mat_TreadRubber",     "tex_tread.png",         "tex_tread_n.png");
            BindPbrSlot(registry, "Mat_SovietGate",      "tex_soviet_gate.png",   null);
            BindPbrSlot(registry, "Mat_AlliedCrest",     "tex_allied_crest.png",  null);
            BindPbrSlot(registry, "Mat_ShutterDoor",     "tex_shutter_door.png",  null != null ? null : "tex_shutter_door_n.png");

            // Bind any local Hunyuan3D AI-baked textures (RemasteredTextures/AiBaked/<ActorId>_albedo.png)
            if (Directory.Exists("Assets/_ReCncGenerated/RemasteredTextures/AiBaked"))
            {
                string[] aiPngs = Directory.GetFiles("Assets/_ReCncGenerated/RemasteredTextures/AiBaked", "*_albedo.png");
                for (int i = 0; i < aiPngs.Length; i++)
                {
                    string assetPath = aiPngs[i].Replace('\\', '/');
                    string fileStem = Path.GetFileNameWithoutExtension(assetPath); // <ID>_albedo
                    string actorId = fileStem.Substring(0, fileStem.Length - "_albedo".Length);
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (tex != null)
                    {
                        registry.PbrTextures.Add(new PbrTexturePairEntry
                        {
                            MaterialSlotName = "AiBaked_" + actorId,
                            AlbedoMap = tex
                        });
                    }
                }
            }

            // 1. Bind all 28 Blender 3D Remastered FBX models
            string remManifestPath = Path.Combine(Application.streamingAssetsPath, "Ra2Converted", "remastered_manifest.json");
            if (File.Exists(remManifestPath))
            {
                var remManifest = JsonUtility.FromJson<RemasteredManifestData>(File.ReadAllText(remManifestPath));
                if (remManifest != null && remManifest.Models != null)
                {
                    for (int i = 0; i < remManifest.Models.Count; i++)
                    {
                        var rm = remManifest.Models[i];
                        var fbxGo = !string.IsNullOrEmpty(rm.FbxAssetPath) ? AssetDatabase.LoadAssetAtPath<GameObject>(rm.FbxAssetPath) : null;
                        if (fbxGo != null)
                        {
                            registry.RemasteredModels.Add(new RemasteredModelAssetEntry
                            {
                                ActorId = rm.ActorId,
                                FbxPrefab = fbxGo
                            });
                        }
                    }
                }
            }
            else if (Directory.Exists("Assets/_ReCncGenerated/RemasteredModels"))
            {
                string[] fbxFiles = Directory.GetFiles("Assets/_ReCncGenerated/RemasteredModels", "*.fbx");
                for (int i = 0; i < fbxFiles.Length; i++)
                {
                    string assetPath = fbxFiles[i].Replace('\\', '/');
                    var fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (fbxGo != null)
                    {
                        registry.RemasteredModels.Add(new RemasteredModelAssetEntry
                        {
                            ActorId = Path.GetFileNameWithoutExtension(assetPath),
                            FbxPrefab = fbxGo
                        });
                    }
                }
            }

            // 2. Bind converted original RA2 VXL, Sprites, Cameos & Audio
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

            registry.InvalidateCache();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            return registry;
        }

        private static void BindPbrSlot(Ra2AssetRegistry registry, string slotName, string albedoFile, string normalFile)
        {
            const string baseDir = "Assets/_ReCncGenerated/RemasteredTextures/";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(baseDir + albedoFile);
            if (albedo == null) return;

            Texture2D normal = null;
            if (!string.IsNullOrEmpty(normalFile))
            {
                string normPath = baseDir + normalFile;
                var imp = AssetImporter.GetAtPath(normPath) as TextureImporter;
                if (imp != null && imp.textureType != TextureImporterType.NormalMap)
                {
                    imp.textureType = TextureImporterType.NormalMap;
                    imp.SaveAndReimport();
                }
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);
            }

            registry.PbrTextures.Add(new PbrTexturePairEntry
            {
                MaterialSlotName = slotName,
                AlbedoMap = albedo,
                NormalMap = normal
            });
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

            var lightGo = new GameObject("Directional Sun Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.97f, 0.90f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

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

            var directorGo = new GameObject("Ra2GameDirector");
            var director = directorGo.AddComponent<Ra2GameDirector>();
            director.AssetRegistry = registry;
            director.PlayerSide = "Allied";

            var hud = directorGo.AddComponent<RtsInputAndHudController>();
            hud.Director = director;

            EditorSceneManager.SaveScene(scene, DemoScenePath);

            var existingScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            existingScenes.RemoveAll(s => s.path == DemoScenePath);
            existingScenes.Insert(0, new EditorBuildSettingsScene(DemoScenePath, true));
            EditorBuildSettings.scenes = existingScenes.ToArray();
        }
    }
}
