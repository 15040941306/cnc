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

        // 6. Convert Audio (.bag + .idx -> .wav), Ore/Drill Atlases & Skirmish Maps
        ConvertAudioClips(manifest);
        if (unitPal != null && isoPal != null)
        {
            ExportOreAndDrillAtlases(unitPal, isoPal);
        }
        ExportSkirmishMaps(strings, rulesIni, artIni, manifest, jsonOpts);
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

    private void ExportOreAndDrillAtlases(PalPalette unitPal, PalPalette isoPal)
    {
        string outDir = Path.Combine(_streamingOutDir, "terrain_tex");
        Directory.CreateDirectory(outDir);

        // 1. Gold Ore Atlas (12 stages x 4 variations = 720 x 240, 60x60 per cell)
        Export12StageMineralAtlas(
            new[] { "tib01.tem", "tib02.tem", "tib03.tem", "tib04.tem" },
            unitPal,
            new Rgba32(252, 212, 38, 255),
            Path.Combine(outDir, "ore_gold_atlas.png"));

        // 2. Colorful Gems Atlas (12 stages x 4 variations = 720 x 240, 60x60 per cell)
        Export12StageMineralAtlas(
            new[] { "gem01.tem", "gem02.tem", "gem03.tem", "gem04.tem" },
            unitPal,
            null,
            Path.Combine(outDir, "ore_gem_atlas.png"));

        // 3. Animated Ore Spawner Drill Atlas (tibtre01.tem: 11 frames x 1 row = 924 x 56)
        byte[]? drillBytes = _vfs.TryReadFile("tibtre01.tem") ?? _vfs.TryReadFile("tibtre01.sno");
        if (drillBytes != null)
        {
            var drillShp = ShpTsDocument.Parse(drillBytes);
            int fw = drillShp.Width, fh = drillShp.Height;
            int animFrames = Math.Min(11, drillShp.Frames.Count / 2);
            if (fw > 0 && fh > 0 && animFrames > 0)
            {
                int atlasW = fw * animFrames;
                var atlas = new Rgba32[atlasW * fh];
                for (int f = 0; f < animFrames; f++)
                {
                    var colPx = drillShp.RenderFrameToCanvas(f, isoPal, null, false, out _, out _);
                    var shdPx = (f + animFrames < drillShp.Frames.Count)
                        ? drillShp.RenderFrameToCanvas(f + animFrames, isoPal, null, false, out _, out _)
                        : null;
                    BlitFrameWithShadow(colPx, shdPx, fw, fh, f * fw, 0, atlas, atlasW);
                }
                PngWriter.SaveRgba32(Path.Combine(outDir, "ore_drill_atlas.png"), atlasW, fh, atlas);
            }
        }
    }

    private void Export12StageMineralAtlas(string[] shpNames, PalPalette pal, Rgba32? remapColor, string outPngPath)
    {
        const int cellW = 60, cellH = 60, stages = 12;
        int rows = shpNames.Length;
        int atlasW = cellW * stages;
        int atlasH = cellH * rows;
        var atlas = new Rgba32[atlasW * atlasH];

        for (int r = 0; r < rows; r++)
        {
            byte[]? bytes = _vfs.TryReadFile(shpNames[r]) ?? _vfs.TryReadFile(shpNames[0]);
            if (bytes == null) continue;
            var shp = ShpTsDocument.Parse(bytes);
            if (shp.Width != cellW || shp.Height != cellH) continue;

            for (int s = 0; s < stages && s < shp.Frames.Count; s++)
            {
                var colPx = shp.RenderFrameToCanvas(s, pal, remapColor, false, out _, out _);
                var shdPx = (s + 12 < shp.Frames.Count)
                    ? shp.RenderFrameToCanvas(s + 12, pal, null, false, out _, out _)
                    : null;
                BlitFrameWithShadow(colPx, shdPx, cellW, cellH, s * cellW, r * cellH, atlas, atlasW);
            }
        }

        PngWriter.SaveRgba32(outPngPath, atlasW, atlasH, atlas);
    }

    private static void BlitFrameWithShadow(Rgba32[] colPx, Rgba32[]? shdPx, int fw, int fh, int dstX, int dstY, Rgba32[] atlas, int atlasW)
    {
        for (int y = 0; y < fh; y++)
        {
            int srcRow = y * fw;
            int dstRow = (dstY + y) * atlasW + dstX;
            for (int x = 0; x < fw; x++)
            {
                if (colPx[srcRow + x].A > 0)
                {
                    atlas[dstRow + x] = colPx[srcRow + x];
                }
                else if (shdPx != null && shdPx[srcRow + x].A > 0)
                {
                    atlas[dstRow + x] = new Rgba32(0, 0, 0, 108);
                }
            }
        }
    }

    public Dictionary<string, TheaterTilesetAtlas> BuildTheaterAtlases()
    {
        PalPalette fallbackPal = LoadPalette("isotem.pal", -1, -1) ?? PalPalette.ParseSixBitPal(new byte[768], -1, -1);
        TheaterTilesetAtlas Make(string name, string mdIni, string baseIni, string palFile, string ext)
        {
            var ini = LoadIni(mdIni) ?? (!string.IsNullOrEmpty(baseIni) ? LoadIni(baseIni) : null);
            var pal = LoadPalette(palFile, -1, -1) ?? fallbackPal;
            return new TheaterTilesetAtlas(name, ini, pal, _vfs, ext);
        }

        return new Dictionary<string, TheaterTilesetAtlas>(StringComparer.OrdinalIgnoreCase)
        {
            ["TEMPERATE"] = Make("TEMPERATE", "temperatmd.ini", "temperat.ini", "isotem.pal", "tem"),
            ["SNOW"]      = Make("SNOW",      "snowmd.ini",     "snow.ini",     "isosno.pal", "sno"),
            ["URBAN"]     = Make("URBAN",     "urbanmd.ini",    "urban.ini",    "isourb.pal", "urb"),
            ["NEWURBAN"]  = Make("NEWURBAN",  "urbannmd.ini",   "urbanmd.ini",  "isoubn.pal", "ubn"),
            ["DESERT"]    = Make("DESERT",    "desertmd.ini",   "",             "isodes.pal", "des"),
            ["LUNAR"]     = Make("LUNAR",     "lunarmd.ini",    "",             "isolun.pal", "lun"),
        };
    }

    public SkirmishMapEntryData? ConvertSingleExternalMap(string mapFilePath)
    {
        Directory.CreateDirectory(Path.Combine(_streamingOutDir, "maps"));
        Directory.CreateDirectory(Path.Combine(_streamingOutDir, "previews"));
        Directory.CreateDirectory(Path.Combine(_streamingOutDir, "terrain_tex"));

        var csf = LoadLocalizedStrings();
        var rulesIni = LoadIni("rulesmd.ini") ?? LoadIni("rules.ini");
        var artIni = LoadIni("artmd.ini") ?? LoadIni("art.ini");
        var atlases = BuildTheaterAtlases();
        var jsonOpts = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

        byte[] mapBytes = File.ReadAllBytes(mapFilePath);
        string safeId = Path.GetFileNameWithoutExtension(mapFilePath).ToLowerInvariant();
        var mapIni = IniDocument.Parse(mapBytes);
        string basicName = mapIni.GetSection("Basic")?.GetString("Name", "") ?? "";
        string displayName = !string.IsNullOrWhiteSpace(basicName) ? $"{basicName} (导入)" : $"{Path.GetFileNameWithoutExtension(mapFilePath)} (导入)";

        var entry = WriteConvertedMapFiles(safeId, displayName, 2, 8, mapBytes, atlases, rulesIni, artIni, csf, jsonOpts);
        if (entry == null) return null;

        string indexPath = Path.Combine(_streamingOutDir, "maps_index.json");
        SkirmishMapsCatalogData catalog = File.Exists(indexPath)
            ? (JsonSerializer.Deserialize<SkirmishMapsCatalogData>(File.ReadAllText(indexPath), jsonOpts) ?? new SkirmishMapsCatalogData())
            : new SkirmishMapsCatalogData();

        catalog.Maps.RemoveAll(m => string.Equals(m.MapId, safeId, StringComparison.OrdinalIgnoreCase));
        catalog.Maps.Insert(0, entry);
        File.WriteAllText(indexPath, JsonSerializer.Serialize(catalog, jsonOpts));
        return entry;
    }

    private SkirmishMapEntryData? WriteConvertedMapFiles(
        string safeId,
        string displayName,
        int minP,
        int maxP,
        byte[] mapBytes,
        Dictionary<string, TheaterTilesetAtlas> atlases,
        IniDocument? rulesIni,
        IniDocument? artIni,
        Dictionary<string, string> csf,
        JsonSerializerOptions jsonOpts)
    {
        var extracted = Ra2MapExtractor.TryExtractFullMap(safeId, displayName, mapBytes, atlases, rulesIni, artIni, csf);
        if (extracted == null) return null;

        var converted = extracted.Bundle;
        string relJson = $"maps/{safeId}.json";
        File.WriteAllText(Path.Combine(_streamingOutDir, relJson), JsonSerializer.Serialize(converted, jsonOpts));

        if (extracted.BakedTerrainPixels != null && extracted.BakedTerrainW > 0 && extracted.BakedTerrainH > 0)
        {
            int saveW = extracted.BakedTerrainPixels.Length == (extracted.BakedTerrainW / 2) * (extracted.BakedTerrainH / 2)
                ? extracted.BakedTerrainW / 2
                : extracted.BakedTerrainW;
            int saveH = saveW == extracted.BakedTerrainW ? extracted.BakedTerrainH : extracted.BakedTerrainH / 2;
            PngWriter.SaveRgba32(
                Path.Combine(_streamingOutDir, converted.BakedTerrainTextureRelPath),
                saveW,
                saveH,
                extracted.BakedTerrainPixels);
        }

        string previewRel = "";
        if (extracted.PreviewPixels != null && extracted.PreviewW > 0 && extracted.PreviewH > 0)
        {
            previewRel = $"previews/{safeId}.png";
            PngWriter.SaveRgba32(Path.Combine(_streamingOutDir, previewRel), extracted.PreviewW, extracted.PreviewH, extracted.PreviewPixels);
        }

        return new SkirmishMapEntryData
        {
            MapId = safeId,
            DisplayName = displayName,
            Theater = converted.Theater,
            Width = converted.Width,
            Height = converted.Height,
            PlayableWidth = converted.LocalWidth,
            PlayableHeight = converted.LocalHeight,
            MinPlayers = minP,
            MaxPlayers = Math.Max(maxP, converted.Waypoints.Count),
            JsonRelPath = relJson,
            PreviewAssetPath = previewRel,
            IsOfficialRa2Map = true
        };
    }

    private void ExportSkirmishMaps(
        Dictionary<string, string> csf,
        IniDocument? rulesIni,
        IniDocument? artIni,
        ConvertedAssetManifest manifest,
        JsonSerializerOptions jsonOpts)
    {
        Directory.CreateDirectory(Path.Combine(_streamingOutDir, "maps"));
        Directory.CreateDirectory(Path.Combine(_streamingOutDir, "previews"));
        Directory.CreateDirectory(Path.Combine(_streamingOutDir, "terrain_tex"));

        var catalog = new SkirmishMapsCatalogData();
        var atlases = BuildTheaterAtlases();

        var pktDocs = new List<IniDocument>();
        if (LoadIni("missionsmd.pkt") is { } pktMd) pktDocs.Add(pktMd);
        if (LoadIni("missions.pkt") is { } pktBase) pktDocs.Add(pktBase);

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const int maxExportedMaps = 24;

        foreach (var pkt in pktDocs)
        {
            var multiSec = pkt.GetSection("MultiMaps");
            if (multiSec == null) continue;

            foreach (var kv in multiSec.OrderedEntries)
            {
                if (catalog.Maps.Count >= maxExportedMaps) break;
                string mapStem = kv.Value.Trim();
                if (string.IsNullOrEmpty(mapStem) || !seenIds.Add(mapStem)) continue;

                var infoSec = pkt.GetSection(mapStem);
                string modes = (infoSec?.GetString("GameMode", "standard") ?? "standard").ToLowerInvariant();
                if (!modes.Contains("standard") && !modes.Contains("battle") && !modes.Contains("megawealth"))
                    continue;

                byte[]? mapBytes = _vfs.TryReadFile($"{mapStem}.map")
                    ?? _vfs.TryReadFile($"{mapStem}.yrm")
                    ?? _vfs.TryReadFile($"{mapStem}.mpr");
                if (mapBytes == null) continue;

                string descKey = infoSec?.GetString("Description", "") ?? "";
                string displayName = !string.IsNullOrEmpty(descKey) && csf.TryGetValue(descKey, out string? locName) && !string.IsNullOrWhiteSpace(locName)
                    ? locName.Trim()
                    : mapStem;

                int minP = Math.Max(2, infoSec?.GetInt("MinPlayers", 2) ?? 2);
                int maxP = Math.Max(minP, infoSec?.GetInt("MaxPlayers", 2) ?? 2);
                string safeId = mapStem.ToLowerInvariant();

                var entry = WriteConvertedMapFiles(safeId, displayName, minP, maxP, mapBytes, atlases, rulesIni, artIni, csf, jsonOpts);
                if (entry != null)
                {
                    manifest.Maps.Add(entry.JsonRelPath);
                    catalog.Maps.Add(entry);
                }
            }
        }

        // Built-in fallback demo map
        var defaultMap = DefaultMapFactory.CreateSkirmishArena(64, 64);
        string defaultRel = "maps/skirmish_valley.json";
        File.WriteAllText(Path.Combine(_streamingOutDir, defaultRel), JsonSerializer.Serialize(defaultMap, jsonOpts));
        manifest.Maps.Add(defaultRel);
        catalog.Maps.Add(new SkirmishMapEntryData
        {
            MapId = "skirmish_valley",
            DisplayName = "河谷争锋 (内置对称演示图)",
            Theater = "TEMPERATE",
            Width = defaultMap.Width,
            Height = defaultMap.Height,
            PlayableWidth = defaultMap.Width,
            PlayableHeight = defaultMap.Height,
            MinPlayers = 2,
            MaxPlayers = 2,
            JsonRelPath = defaultRel,
            PreviewAssetPath = "",
            IsOfficialRa2Map = false
        });

        File.WriteAllText(Path.Combine(_streamingOutDir, "maps_index.json"), JsonSerializer.Serialize(catalog, jsonOpts));
    }
}
