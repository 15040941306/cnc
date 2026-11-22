using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Ra2Converter.Formats;
using ReCnc.Core.Data;

namespace Ra2Converter;

public sealed class AssetConversionPipeline
{
    private static readonly string[] TargetVehicles =
    {
        // Core playable roster + iconic RA2/YR vehicles
        "AMCV", "MCV", "CMIN", "MTNK", "FV", "SREF", "MGTK", "BFRT", "ORCA",
        "SMCV", "HARV", "HTNK", "APOC", "TTNK", "V3", "ZEP",
        "PCV", "YTNK"
    };

    private static readonly string[] TargetBuildingsAndInfantry =
    {
        "GACNST", "GAPOWR", "GAREFN", "GAPILE", "GAWEAP", "GAAIRC", "GATECH", "GAPILL", "ATESLA",
        "NACNST", "NAPOWR", "NAREFN", "NAHAND", "NAWEAP", "NATECH", "TESLA",
        "E1", "GGI", "E2"
    };

    private static readonly string[] TargetAudioClips =
    {
        // Allied/General EVA announcements
        "ceva001", "ceva006", "ceva048", "ceva051", "ceva052", "ceva055", "ceva058", "ceva062",
        // Weapon & combat sounds
        "vgriatta", "vrhiatta", "vapoatta", "vpriatta", "bpriatta", "btesatta", "bpilatta", "igiatta",
        "gexp14a", "gexpbara", "uplace", "uselbuil", "utab",
        // Unit select/move voices
        "vgriselc", "vgrimova", "vrhiselc", "vrhimova", "vaposelc", "vapomova",
        "igiselc", "igimova", "iconselec", "iconmova", "vharvsea", "vharvmoa", "vmcvselc", "vmcvmova", "vkirselc"
    };

    private readonly Ra2VirtualFileSystem _vfs;
    private readonly string _unityAssetsGeneratedDir;
    private readonly string _streamingOutDir;

    public AssetConversionPipeline(string rawAssetsDir, string unityProjectDir)
    {
        _vfs = new Ra2VirtualFileSystem(rawAssetsDir);
        _unityAssetsGeneratedDir = Path.Combine(unityProjectDir, "Assets", "_ReCncGenerated");
        _streamingOutDir = Path.Combine(unityProjectDir, "Assets", "StreamingAssets", "Ra2Converted");
    }

    public ConvertedAssetManifest RunAll()
    {
        Directory.CreateDirectory(_unityAssetsGeneratedDir);
        Directory.CreateDirectory(_streamingOutDir);

        Console.WriteLine($"[1/6] 已挂载 {_vfs.Mounts.Count} 个 MIX 归档（含嵌套子包）");

        // 1. Parse CSF Strings (ra2md.csf -> fallback ra2.csf)
        var strings = LoadLocalizedStrings();
        Console.WriteLine($"[2/6] 解析本地化字典完成: {strings.Count} 条词条");

        // 2. Parse rulesmd.ini / rules.ini & artmd.ini / art.ini
        var rulesIni = LoadIni("rulesmd.ini") ?? LoadIni("rules.ini") ?? new IniDocument();
        var artIni = LoadIni("artmd.ini") ?? LoadIni("art.ini") ?? new IniDocument();
        var soundIni = LoadIni("soundmd.ini") ?? LoadIni("sound.ini") ?? new IniDocument();
        var evaIni = LoadIni("evamd.ini") ?? LoadIni("eva.ini") ?? new IniDocument();
        _ = soundIni;
        _ = evaIni;

        var rulesBundle = BuildEnrichedRulesBundle(rulesIni, artIni, strings);
        var jsonOpts = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        File.WriteAllText(Path.Combine(_streamingOutDir, "rules_bundle.json"), JsonSerializer.Serialize(rulesBundle, jsonOpts));
        Console.WriteLine($"[3/6] 导出规则配置 rules_bundle.json: {rulesBundle.Actors.Count} 个单位/建筑, {rulesBundle.Weapons.Count} 种武器");

        // 3. Load core palettes
        var unitPal = LoadPalette("unittem.pal", 16, 31) ?? LoadPalette("unitsno.pal", 16, 31);
        var isoPal = LoadPalette("isotem.pal", 16, 31) ?? unitPal;
        var cameoPal = LoadPalette("cameomd.pal", -1, -1) ?? LoadPalette("cameo.pal", -1, -1) ?? unitPal;

        var manifest = new ConvertedAssetManifest
        {
            GeneratedAtUtc = DateTime.UtcNow.ToString("O")
        };

        // 4. Convert 3D VXL + HVA models
        string paletteRelAssetPath = "Assets/_ReCncGenerated/Palettes/unittem_lut.png";
        string paletteAbsPath = Path.Combine(_unityAssetsGeneratedDir, "Palettes", "unittem_lut.png");
        if (unitPal != null)
        {
            PngWriter.SaveRgba32(paletteAbsPath, 256, 2, unitPal.BuildVoxelLut256x2());
        }

        ConvertAllVoxelModels(rulesIni, artIni, unitPal, paletteRelAssetPath, manifest);
        Console.WriteLine($"[4/6] 转换 3D 体素模型 (.vxl+.hva -> .obj): 成功导出 {manifest.Models.Count} 套载具模型");

        // 5. Convert Cameo icons & Building/Infantry SHP reference/billboard sprites
        if (cameoPal != null)
        {
            ConvertCameoIcons(rulesBundle, artIni, cameoPal, manifest);
        }
        if (isoPal != null && unitPal != null)
        {
            ConvertBuildingAndInfantrySprites(artIni, isoPal, unitPal, manifest);
        }
        Console.WriteLine($"[5/6] 转换 2D 图像 (.shp -> .png): {manifest.Cameos.Count} 个建造图标, {manifest.Sprites.Count} 张建筑/步兵立绘");

        // 6. Convert Audio (.bag + .idx -> .wav) & Skirmish Map
        ConvertAudioClips(manifest);
        ExportSkirmishMaps(manifest, jsonOpts);
        Console.WriteLine($"[6/6] 转换音频与地图: {manifest.Audios.Count} 条 WAV 语音/音效, {manifest.Maps.Count} 张 3D 地图数据");

        File.WriteAllText(Path.Combine(_streamingOutDir, "asset_manifest.json"), JsonSerializer.Serialize(manifest, jsonOpts));
        return manifest;
    }

    private Dictionary<string, string> LoadLocalizedStrings()
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        byte[]? ra2Csf = _vfs.TryReadFile("ra2.csf");
        if (ra2Csf != null)
        {
            foreach (var kv in CsfStringTable.Parse(ra2Csf)) merged[kv.Key] = kv.Value;
        }
        byte[]? yrCsf = _vfs.TryReadFile("ra2md.csf");
        if (yrCsf != null)
        {
            foreach (var kv in CsfStringTable.Parse(yrCsf)) merged[kv.Key] = kv.Value;
        }
        return merged;
    }

    private IniDocument? LoadIni(string filename)
    {
        byte[]? bytes = _vfs.TryReadFile(filename);
        return bytes != null ? IniDocument.Parse(bytes) : null;
    }

    private PalPalette? LoadPalette(string filename, int remapStart, int remapEnd)
    {
        byte[]? bytes = _vfs.TryReadFile(filename);
        return bytes is { Length: >= 768 } ? PalPalette.ParseSixBitPal(bytes, remapStart, remapEnd) : null;
    }

    private RulesBundleData BuildEnrichedRulesBundle(
        IniDocument rulesIni,
        IniDocument artIni,
        Dictionary<string, string> csf)
    {
        var catalog = RulesCatalog.CreateWithDefaults(null);
        var bundle = new RulesBundleData { SourceEdition = "yuris_revenge" };

        foreach (var baseActor in catalog.AllActors)
        {
            var sec = rulesIni.GetSection(baseActor.Id);
            if (sec != null)
            {
                baseActor.Cost = sec.GetInt("Cost", baseActor.Cost);
                baseActor.MaxHp = sec.GetInt("Strength", baseActor.MaxHp);
                baseActor.Armor = sec.GetString("Armor", baseActor.Armor) ?? baseActor.Armor;
                baseActor.Sight = Math.Clamp(sec.GetFloat("Sight", baseActor.Sight), 3f, 14f);
                int rawSpeed = sec.GetInt("Speed", -1);
                if (rawSpeed > 0 && !baseActor.IsBuilding)
                {
                    baseActor.Speed = Math.Clamp(rawSpeed * 0.62f, 1.8f, 7.5f);
                }
                baseActor.Power = sec.GetInt("Power", baseActor.Power);
                baseActor.Powered = sec.GetBool("Powered", baseActor.Powered);
                string? uiName = sec.GetString("UIName");
                if (!string.IsNullOrEmpty(uiName))
                {
                    baseActor.UiNameKey = uiName;
                    if (csf.TryGetValue(uiName, out string? localized) && !string.IsNullOrWhiteSpace(localized))
                    {
                        bool hasCjk = false;
                        foreach (char ch in localized) { if (ch > 0x2E80) { hasCjk = true; break; } }
                        if (hasCjk || string.IsNullOrEmpty(baseActor.Name))
                        {
                            baseActor.Name = localized;
                        }
                    }
                }
                string? imageOverride = sec.GetString("Image");
                if (!string.IsNullOrEmpty(imageOverride))
                {
                    baseActor.ImageId = imageOverride;
                }
                baseActor.VoiceSelect = sec.GetCsvList("VoiceSelect");
                baseActor.VoiceMove = sec.GetCsvList("VoiceMove");
                baseActor.VoiceAttack = sec.GetCsvList("VoiceAttack");
            }

            string artKey = string.IsNullOrEmpty(baseActor.ImageId) ? baseActor.Id : baseActor.ImageId;
            var artSec = artIni.GetSection(artKey) ?? artIni.GetSection(baseActor.Id);
            if (artSec != null)
            {
                string? cameo = artSec.GetString("Cameo") ?? artSec.GetString("AltCameo");
                if (!string.IsNullOrEmpty(cameo))
                {
                    baseActor.CameoIcon = cameo;
                }
            }

            bundle.Actors.Add(baseActor);
        }

        // Also store relevant localized strings
        foreach (var a in bundle.Actors)
        {
            if (!string.IsNullOrEmpty(a.UiNameKey) && csf.TryGetValue(a.UiNameKey, out string? text))
            {
                bundle.Strings.Add(new LocalizedEntryData { Key = a.UiNameKey, Value = text });
            }
        }

        // Preserve default balanced weapons enriched with any INI Range/Damage hints
        string[] weaponIds = { "M60", "105mm", "120mm", "120mmx", "Comet", "PrismShot", "CoilBolt", "Vulcan2" };
        foreach (string wid in weaponIds)
        {
            var w = catalog.GetWeapon(wid);
            if (w != null)
            {
                var wSec = rulesIni.GetSection(wid);
                if (wSec != null)
                {
                    w.Damage = Math.Max(10, wSec.GetInt("Damage", w.Damage));
                    float rof = wSec.GetFloat("ROF", 0f);
                    if (rof > 5f) w.ReloadSeconds = Math.Clamp(rof / 30f, 0.35f, 3.5f);
                }
                bundle.Weapons.Add(w);
            }
        }

        return bundle;
    }

    private void ConvertAllVoxelModels(
        IniDocument rulesIni,
        IniDocument artIni,
        PalPalette? unitPal,
        string sharedPaletteAssetPath,
        ConvertedAssetManifest manifest)
    {
        foreach (string unitId in TargetVehicles)
        {
            var rulesSec = rulesIni.GetSection(unitId);
            string rulesImg = rulesSec?.GetString("Image", unitId) ?? unitId;
            var artSec = artIni.GetSection(rulesImg) ?? artIni.GetSection(unitId);
            string baseImage = artSec?.GetString("Image", rulesImg) ?? rulesImg;

            // Special RA2 VXL stem aliases (e.g. AMCV -> MCV, APOC -> 4TNK in RA2!)
            string vxlStem = unitId.ToUpperInvariant() switch
            {
                "AMCV" => "MCV",
                "APOC" when _vfs.TryReadFile($"{baseImage}.vxl") == null => "4TNK",
                _ => baseImage
            };
            byte[]? bodyVxlBytes = _vfs.TryReadFile($"{vxlStem}.vxl");
            if (bodyVxlBytes == null && !vxlStem.Equals(unitId, StringComparison.OrdinalIgnoreCase))
            {
                vxlStem = unitId;
                bodyVxlBytes = _vfs.TryReadFile($"{vxlStem}.vxl");
            }
            if (bodyVxlBytes == null) continue;

            try
            {
                var bodyVxl = VxlDocument.Parse(bodyVxlBytes);
                byte[]? bodyHvaBytes = _vfs.TryReadFile($"{vxlStem}.hva");
                var bodyHva = bodyHvaBytes != null ? HvaDocument.Parse(bodyHvaBytes) : null;

                string unitDir = Path.Combine(_unityAssetsGeneratedDir, "Models", unitId);
                Directory.CreateDirectory(unitDir);

                // Copy/save local palette texture next to OBJ so standard MTL & Unity OBJ importer resolve it cleanly
                string localTexName = "palette_lut.png";
                var palToUse = unitPal ?? bodyVxl.EmbeddedPalette;
                PngWriter.SaveRgba32(Path.Combine(unitDir, localTexName), 256, 2, palToUse.BuildVoxelLut256x2());

                string mtlName = $"{unitId}.mtl";
                string matId = $"Mat_{unitId}";
                VxlObjExporter.WriteMtl(Path.Combine(unitDir, mtlName), matId, localTexName);

                string bodyObjFile = $"{unitId}_body.obj";
                var (bodyMin, bodyMax, _) = VxlObjExporter.ExportToObj(
                    bodyVxl, bodyHva, Path.Combine(unitDir, bodyObjFile), mtlName, matId, groundAlignBottom: true);

                var record = new ConvertedModelRecord
                {
                    UnitId = unitId,
                    BodyObjPath = $"Assets/_ReCncGenerated/Models/{unitId}/{bodyObjFile}",
                    PaletteTexPath = string.IsNullOrEmpty(sharedPaletteAssetPath)
                        ? $"Assets/_ReCncGenerated/Models/{unitId}/{localTexName}"
                        : $"Assets/_ReCncGenerated/Models/{unitId}/{localTexName}"
                };

                // Optional Turret VXL (<stem>tur.vxl)
                byte[]? turVxlBytes = _vfs.TryReadFile($"{vxlStem}tur.vxl");
                Vector3 turMax = bodyMax;
                if (turVxlBytes != null)
                {
                    var turVxl = VxlDocument.Parse(turVxlBytes);
                    byte[]? turHvaBytes = _vfs.TryReadFile($"{vxlStem}tur.hva");
                    var turHva = turHvaBytes != null ? HvaDocument.Parse(turHvaBytes) : null;
                    string turObjFile = $"{unitId}_turret.obj";
                    var (_, tMax, tTris) = VxlObjExporter.ExportToObj(
                        turVxl, turHva, Path.Combine(unitDir, turObjFile), mtlName, matId, groundAlignBottom: true);
                    if (tTris > 0)
                    {
                        turMax = tMax;
                        record.TurretObjPath = $"Assets/_ReCncGenerated/Models/{unitId}/{turObjFile}";
                        record.TurretOffsetY = Math.Max(0.15f, bodyMax.Y * 0.78f);
                    }
                }

                // Optional Barrel VXL (<stem>barl.vxl)
                byte[]? barlVxlBytes = _vfs.TryReadFile($"{vxlStem}barl.vxl");
                if (barlVxlBytes != null)
                {
                    var barlVxl = VxlDocument.Parse(barlVxlBytes);
                    byte[]? barlHvaBytes = _vfs.TryReadFile($"{vxlStem}barl.hva");
                    var barlHva = barlHvaBytes != null ? HvaDocument.Parse(barlHvaBytes) : null;
                    string barlObjFile = $"{unitId}_barrel.obj";
                    var (_, _, bTris) = VxlObjExporter.ExportToObj(
                        barlVxl, barlHva, Path.Combine(unitDir, barlObjFile), mtlName, matId, groundAlignBottom: true);
                    if (bTris > 0)
                    {
                        record.BarrelObjPath = $"Assets/_ReCncGenerated/Models/{unitId}/{barlObjFile}";
                        record.BarrelOffsetY = Math.Max(0.08f, (turMax.Y > 0 ? turMax.Y : bodyMax.Y) * 0.35f);
                        record.BarrelOffsetZ = 0.22f;
                    }
                }

                manifest.Models.Add(record);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WARN] VXL conversion skipped for {unitId}: {ex.Message}");
            }
        }
    }

    private void ConvertCameoIcons(RulesBundleData rules, IniDocument artIni, PalPalette cameoPal, ConvertedAssetManifest manifest)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var actor in rules.Actors)
        {
            if (string.IsNullOrEmpty(actor.CameoIcon) || !seen.Add(actor.CameoIcon)) continue;

            byte[]? shpBytes = _vfs.TryReadFile($"{actor.CameoIcon}.shp");
            if (shpBytes == null)
            {
                // Try art.ini Cameo lookup
                var artSec = artIni.GetSection(actor.Id);
                string? alt = artSec?.GetString("Cameo") ?? artSec?.GetString("AltCameo");
                if (!string.IsNullOrEmpty(alt)) shpBytes = _vfs.TryReadFile($"{alt}.shp");
            }
            if (shpBytes == null) continue;

            try
            {
                var shp = ShpTsDocument.Parse(shpBytes);
                if (shp.Frames.Count == 0) continue;
                var pixels = shp.RenderFrameToCanvas(0, cameoPal, null, false, out int w, out int h);
                string outRel = $"Assets/_ReCncGenerated/Cameos/{actor.Id}.png";
                string outAbs = Path.Combine(_unityAssetsGeneratedDir, "Cameos", $"{actor.Id}.png");
                PngWriter.SaveRgba32(outAbs, w, h, pixels);
                manifest.Cameos.Add(new ConvertedSpriteRecord
                {
                    Id = actor.Id,
                    PngPath = outRel,
                    Width = w,
                    Height = h,
                    FrameCount = 1
                });
            }
            catch
            {
                // Skip malformed SHP
            }
        }
    }

    private void ConvertBuildingAndInfantrySprites(
        IniDocument artIni,
        PalPalette isoPal,
        PalPalette unitPal,
        ConvertedAssetManifest manifest)
    {
        foreach (string id in TargetBuildingsAndInfantry)
        {
            bool isInfantry = id is "E1" or "E2" or "GGI";
            var artSec = artIni.GetSection(id);
            string stem = artSec?.GetString("Image", id) ?? id;

            byte[]? shpBytes = FindTheaterShp(stem);
            if (shpBytes == null && !stem.Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                shpBytes = FindTheaterShp(id);
            }
            if (shpBytes == null) continue;

            try
            {
                var shp = ShpTsDocument.Parse(shpBytes);
                if (shp.Frames.Count == 0) continue;

                // Pick first non-empty frame
                int frameIdx = 0;
                for (int i = 0; i < shp.Frames.Count; i++)
                {
                    if (shp.Frames[i].CropWidth > 4 && shp.Frames[i].CropHeight > 4)
                    {
                        frameIdx = i;
                        break;
                    }
                }

                // Military structures and infantry in RA2 both use unittem.pal (isotem.pal is for .tem terrain tiles)
                bool isSoviet = id.StartsWith("N", StringComparison.OrdinalIgnoreCase) || id is "TESLA" or "E2";
                Rgba32 factionTint = isSoviet ? new Rgba32(225, 48, 48, 255) : new Rgba32(65, 145, 255, 255);
                var pal = unitPal;

                var pixels = shp.RenderFrameToCanvas(frameIdx, pal, factionTint, autoTrimTransparent: true, out int w, out int h);
                string outRel = $"Assets/_ReCncGenerated/Sprites/{id}.png";
                string outAbs = Path.Combine(_unityAssetsGeneratedDir, "Sprites", $"{id}.png");
                PngWriter.SaveRgba32(outAbs, w, h, pixels);

                manifest.Sprites.Add(new ConvertedSpriteRecord
                {
                    Id = id,
                    PngPath = outRel,
                    Width = w,
                    Height = h,
                    FrameCount = shp.Frames.Count
                });
            }
            catch
            {
                // Ignore unsupported compression variants
            }
        }
    }

    private byte[]? FindTheaterShp(string stem)
    {
        // Try exact stem first, then RA2 Temperate ('T' at index 1) and Generic ('G' at index 1)
        byte[]? exact = _vfs.TryReadFile($"{stem}.shp");
        if (exact != null) return exact;

        if (stem.Length >= 2)
        {
            string tempName = $"{stem[0]}T{stem.Substring(2)}.shp";
            byte[]? temp = _vfs.TryReadFile(tempName);
            if (temp != null) return temp;

            string genName = $"{stem[0]}G{stem.Substring(2)}.shp";
            byte[]? gen = _vfs.TryReadFile(genName);
            if (gen != null) return gen;
        }
        return null;
    }

    private void ConvertAudioClips(ConvertedAssetManifest manifest)
    {
        var banks = new List<AudioBagBank>();
        byte[]? idxMd = _vfs.TryReadFile("audiomd.idx");
        byte[]? bagMd = _vfs.TryReadFile("audiomd.bag");
        if (idxMd != null && bagMd != null) banks.Add(new AudioBagBank(idxMd, bagMd));

        byte[]? idxBase = _vfs.TryReadFile("audio.idx");
        byte[]? bagBase = _vfs.TryReadFile("audio.bag");
        if (idxBase != null && bagBase != null) banks.Add(new AudioBagBank(idxBase, bagBase));

        if (banks.Count == 0) return;

        foreach (string clipName in TargetAudioClips)
        {
            foreach (var bank in banks)
            {
                string outAbs = Path.Combine(_unityAssetsGeneratedDir, "Audio", $"{clipName}.wav");
                if (bank.TryExportWav(clipName, outAbs, out int sr))
                {
                    manifest.Audios.Add(new ConvertedAudioRecord
                    {
                        Id = clipName,
                        WavPath = $"Assets/_ReCncGenerated/Audio/{clipName}.wav",
                        SampleRate = sr
                    });
                    break;
                }
            }
        }
    }

    private void ExportSkirmishMaps(ConvertedAssetManifest manifest, JsonSerializerOptions jsonOpts)
    {
        string mapsDir = Path.Combine(_streamingOutDir, "maps");
        Directory.CreateDirectory(mapsDir);

        // Always export clean balanced default skirmish map
        var defaultMap = DefaultMapFactory.CreateSkirmishArena(64, 64);
        string defaultPath = Path.Combine(mapsDir, "skirmish_valley.json");
        File.WriteAllText(defaultPath, JsonSerializer.Serialize(defaultMap, jsonOpts));
        manifest.Maps.Add("maps/skirmish_valley.json");

        // Also check official multiplayer map archives for an authentic RA2 map sample
        string[] candidateMapNames = { "mp01t2.map", "mp02t2.map", "mp03t4.map", "eb1.yrm", "mp08t2.map" };
        foreach (string mapFile in candidateMapNames)
        {
            byte[]? bytes = _vfs.TryReadFile(mapFile);
            if (bytes == null) continue;
            string id = Path.GetFileNameWithoutExtension(mapFile);
            var converted = Ra2MapExtractor.TryConvertSkirmishMap(id, $"官方地图 ({id.ToUpperInvariant()})", bytes);
            if (converted != null)
            {
                File.WriteAllText(Path.Combine(mapsDir, $"{id}.json"), JsonSerializer.Serialize(converted, jsonOpts));
                manifest.Maps.Add($"maps/{id}.json");
                break;
            }
        }
    }
}
