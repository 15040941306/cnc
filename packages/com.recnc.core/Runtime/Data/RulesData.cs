using System;
using System.Collections.Generic;

namespace ReCnc.Core.Data
{
    [Serializable]
    public class RulesBundleData
    {
        public string SourceEdition = "yuris_revenge";
        public List<ActorDefData> Actors = new List<ActorDefData>();
        public List<WeaponDefData> Weapons = new List<WeaponDefData>();
        public List<LocalizedEntryData> Strings = new List<LocalizedEntryData>();
    }

    [Serializable]
    public class LocalizedEntryData
    {
        public string Key;
        public string Value;
    }

    [Serializable]
    public class ActorDefData
    {
        public string Id;
        public string Name;
        public string UiNameKey;
        public string Side;          // "Allied", "Soviet", "Yuri", "Neutral"
        public string Category;      // "MainBuilding", "DefenseBuilding", "Infantry", "Vehicle"
        public string Role;          // "Building", "Mcv", "Harvester", "Infantry", "Tank"
        public bool IsBuilding;
        public int Cost = 500;
        public int MaxHp = 400;
        public string Armor = "light";
        public float Speed = 4.0f;
        public float Sight = 6.0f;
        public int Power = 0;        // >0 produces power, <0 consumes power
        public bool Powered;         // disabled on low power (e.g. Prism Tower / Tesla Coil)
        public int FootprintX = 1;
        public int FootprintY = 1;
        public float Height = 1.5f;
        public string ImageId;
        public string CameoIcon;
        public bool HasTurret;
        public bool HasBarrel;
        public string PrimaryWeapon;
        public string DeploysInto;
        public string FreeUnit;
        public List<string> Prerequisites = new List<string>();
        public List<string> VoiceSelect = new List<string>();
        public List<string> VoiceMove = new List<string>();
        public List<string> VoiceAttack = new List<string>();
    }

    [Serializable]
    public class WeaponDefData
    {
        public string Id;
        public int Damage = 50;
        public float Range = 5.5f;
        public float ReloadSeconds = 1.5f;
        public float ProjectileSpeed = 18.0f;
        public float SplashRadius = 0.3f;
        public string Warhead = "AP";
        public string ProjectileStyle = "Shell"; // "Bullet", "Shell", "Laser", "Tesla", "Missile"
        public string ReportSound;
        public float VsNone = 1.0f;
        public float VsFlak = 1.0f;
        public float VsPlate = 1.0f;
        public float VsLight = 1.0f;
        public float VsMedium = 1.0f;
        public float VsHeavy = 1.0f;
        public float VsWood = 1.0f;
        public float VsSteel = 1.0f;
        public float VsConcrete = 1.0f;
    }

    [Serializable]
    public class ConvertedAssetManifest
    {
        public string GeneratedAtUtc;
        public List<ConvertedModelRecord> Models = new List<ConvertedModelRecord>();
        public List<ConvertedSpriteRecord> Sprites = new List<ConvertedSpriteRecord>();
        public List<ConvertedSpriteRecord> Cameos = new List<ConvertedSpriteRecord>();
        public List<ConvertedAudioRecord> Audios = new List<ConvertedAudioRecord>();
        public List<string> Maps = new List<string>();
    }

    [Serializable]
    public class ConvertedModelRecord
    {
        public string UnitId;
        public string BodyObjPath;
        public string TurretObjPath;
        public string BarrelObjPath;
        public string PaletteTexPath;
        public float TurretOffsetX;
        public float TurretOffsetY;
        public float TurretOffsetZ;
        public float BarrelOffsetX;
        public float BarrelOffsetY;
        public float BarrelOffsetZ;
    }

    [Serializable]
    public class ConvertedSpriteRecord
    {
        public string Id;
        public string PngPath;
        public int Width;
        public int Height;
        public int FrameCount;
    }

    [Serializable]
    public class ConvertedAudioRecord
    {
        public string Id;
        public string WavPath;
        public int SampleRate;
    }

    public class RulesCatalog
    {
        private readonly Dictionary<string, ActorDefData> _actors = new Dictionary<string, ActorDefData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, WeaponDefData> _weapons = new Dictionary<string, WeaponDefData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ActorDefData> _orderedActors = new List<ActorDefData>();

        public IReadOnlyList<ActorDefData> AllActors { get { return _orderedActors; } }

        public static RulesCatalog CreateWithDefaults(RulesBundleData imported)
        {
            var catalog = new RulesCatalog();
            catalog.LoadBuiltInDefaults();
            if (imported != null)
            {
                catalog.MergeImported(imported);
            }
            return catalog;
        }

        public ActorDefData GetActor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            ActorDefData def;
            return _actors.TryGetValue(id, out def) ? def : null;
        }

        public WeaponDefData GetWeapon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            WeaponDefData def;
            return _weapons.TryGetValue(id, out def) ? def : null;
        }

        public string Localize(string key, string fallback)
        {
            if (!string.IsNullOrEmpty(key))
            {
                string val;
                if (_strings.TryGetValue(key, out val) && !string.IsNullOrEmpty(val))
                {
                    return val;
                }
            }
            return fallback;
        }

        public void MergeImported(RulesBundleData bundle)
        {
            if (bundle.Strings != null)
            {
                for (int i = 0; i < bundle.Strings.Count; i++)
                {
                    var s = bundle.Strings[i];
                    if (!string.IsNullOrEmpty(s.Key) && !string.IsNullOrEmpty(s.Value))
                    {
                        _strings[s.Key] = s.Value;
                    }
                }
            }

            if (bundle.Weapons != null)
            {
                for (int i = 0; i < bundle.Weapons.Count; i++)
                {
                    var w = bundle.Weapons[i];
                    if (!string.IsNullOrEmpty(w.Id))
                    {
                        _weapons[w.Id] = w;
                    }
                }
            }

            if (bundle.Actors != null)
            {
                for (int i = 0; i < bundle.Actors.Count; i++)
                {
                    var a = bundle.Actors[i];
                    if (string.IsNullOrEmpty(a.Id)) continue;
                    string localized = Localize(a.UiNameKey, null);
                    if (string.IsNullOrEmpty(a.Name) && !string.IsNullOrEmpty(localized))
                    {
                        a.Name = localized;
                    }
                    RegisterActor(a);
                }
            }
        }

        private void RegisterActor(ActorDefData def)
        {
            ActorDefData existing;
            if (_actors.TryGetValue(def.Id, out existing))
            {
                int idx = _orderedActors.IndexOf(existing);
                if (idx >= 0) _orderedActors[idx] = def;
            }
            else
            {
                _orderedActors.Add(def);
            }
            _actors[def.Id] = def;
        }

        private void RegisterWeapon(WeaponDefData def)
        {
            _weapons[def.Id] = def;
        }

        private void LoadBuiltInDefaults()
        {
            RegisterWeapon(new WeaponDefData { Id = "M60", Damage = 18, Range = 5.0f, ReloadSeconds = 0.65f, ProjectileSpeed = 28f, SplashRadius = 0.1f, ProjectileStyle = "Bullet", ReportSound = "igiatta", VsNone = 1.0f, VsFlak = 0.9f, VsPlate = 0.8f, VsLight = 0.4f, VsHeavy = 0.15f, VsConcrete = 0.15f });
            RegisterWeapon(new WeaponDefData { Id = "105mm", Damage = 65, Range = 5.8f, ReloadSeconds = 1.65f, ProjectileSpeed = 18f, SplashRadius = 0.45f, ProjectileStyle = "Shell", ReportSound = "vgriatta", VsNone = 0.35f, VsFlak = 0.4f, VsLight = 1.0f, VsMedium = 1.0f, VsHeavy = 1.0f, VsWood = 0.75f, VsSteel = 0.8f, VsConcrete = 0.6f });
            RegisterWeapon(new WeaponDefData { Id = "120mm", Damage = 90, Range = 5.8f, ReloadSeconds = 1.85f, ProjectileSpeed = 18f, SplashRadius = 0.5f, ProjectileStyle = "Shell", ReportSound = "vrhiatta", VsNone = 0.35f, VsLight = 1.0f, VsMedium = 1.0f, VsHeavy = 1.0f, VsWood = 0.8f, VsSteel = 0.85f, VsConcrete = 0.65f });
            RegisterWeapon(new WeaponDefData { Id = "120mmx", Damage = 160, Range = 6.2f, ReloadSeconds = 2.1f, ProjectileSpeed = 19f, SplashRadius = 0.75f, ProjectileStyle = "Shell", ReportSound = "vapoatta", VsNone = 0.5f, VsLight = 1.0f, VsMedium = 1.0f, VsHeavy = 1.1f, VsWood = 0.9f, VsSteel = 0.95f, VsConcrete = 0.85f });
            RegisterWeapon(new WeaponDefData { Id = "Comet", Damage = 110, Range = 8.5f, ReloadSeconds = 2.4f, ProjectileSpeed = 40f, SplashRadius = 0.6f, ProjectileStyle = "Laser", ReportSound = "vpriatta", VsNone = 1.1f, VsLight = 0.9f, VsHeavy = 0.8f, VsWood = 1.2f, VsSteel = 1.0f, VsConcrete = 1.0f });
            RegisterWeapon(new WeaponDefData { Id = "PrismShot", Damage = 135, Range = 8.0f, ReloadSeconds = 2.6f, ProjectileSpeed = 42f, SplashRadius = 0.4f, ProjectileStyle = "Laser", ReportSound = "bpriatta", VsNone = 1.2f, VsLight = 1.0f, VsHeavy = 0.95f, VsConcrete = 0.8f });
            RegisterWeapon(new WeaponDefData { Id = "CoilBolt", Damage = 150, Range = 7.5f, ReloadSeconds = 2.7f, ProjectileSpeed = 42f, SplashRadius = 0.4f, ProjectileStyle = "Tesla", ReportSound = "btesatta", VsNone = 1.2f, VsLight = 1.1f, VsHeavy = 1.05f, VsConcrete = 0.8f });
            RegisterWeapon(new WeaponDefData { Id = "Vulcan2", Damage = 25, Range = 6.0f, ReloadSeconds = 0.55f, ProjectileSpeed = 30f, SplashRadius = 0.15f, ProjectileStyle = "Bullet", ReportSound = "bpilatta", VsNone = 1.2f, VsFlak = 1.1f, VsLight = 0.5f, VsHeavy = 0.2f, VsConcrete = 0.2f });

            // Allied Structures
            RegisterActor(new ActorDefData { Id = "GACNST", Name = "盟军建造厂", UiNameKey = "Name:GACNST", Side = "Allied", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 3000, MaxHp = 1000, Armor = "concrete", Sight = 8f, FootprintX = 4, FootprintY = 4, Height = 2.6f, ImageId = "GACNST", CameoIcon = "CNSTICON" });
            RegisterActor(new ActorDefData { Id = "GAPOWR", Name = "发电厂", UiNameKey = "Name:GAPOWR", Side = "Allied", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 800, MaxHp = 750, Armor = "wood", Sight = 5f, Power = 200, FootprintX = 2, FootprintY = 2, Height = 2.4f, ImageId = "GAPOWR", CameoIcon = "POWRICON", Prerequisites = new List<string> { "GACNST" } });
            RegisterActor(new ActorDefData { Id = "GAREFN", Name = "矿石精炼厂", UiNameKey = "Name:GAREFN", Side = "Allied", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 2000, MaxHp = 1000, Armor = "wood", Sight = 6f, Power = -50, FootprintX = 4, FootprintY = 3, Height = 2.5f, ImageId = "GAREFN", CameoIcon = "REFNICON", FreeUnit = "CMIN", Prerequisites = new List<string> { "GACNST", "GAPOWR" } });
            RegisterActor(new ActorDefData { Id = "GAPILE", Name = "盟军兵营", UiNameKey = "Name:GAPILE", Side = "Allied", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 500, MaxHp = 500, Armor = "steel", Sight = 5f, Power = -10, FootprintX = 2, FootprintY = 2, Height = 2.0f, ImageId = "GAPILE", CameoIcon = "BARRICON", Prerequisites = new List<string> { "GACNST", "GAPOWR" } });
            RegisterActor(new ActorDefData { Id = "GAWEAP", Name = "盟军战车工厂", UiNameKey = "Name:GAWEAP", Side = "Allied", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 2000, MaxHp = 1000, Armor = "wood", Sight = 6f, Power = -25, FootprintX = 5, FootprintY = 3, Height = 2.8f, ImageId = "GAWEAP", CameoIcon = "WEAPICON", Prerequisites = new List<string> { "GACNST", "GAREFN" } });
            RegisterActor(new ActorDefData { Id = "GAAIRC", Name = "空指部", UiNameKey = "Name:GAAIRC", Side = "Allied", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 1000, MaxHp = 600, Armor = "steel", Sight = 9f, Power = -50, FootprintX = 3, FootprintY = 2, Height = 2.6f, ImageId = "GAAIRC", CameoIcon = "RADRICON", Prerequisites = new List<string> { "GACNST", "GAREFN" } });
            RegisterActor(new ActorDefData { Id = "GATECH", Name = "盟军作战实验室", UiNameKey = "Name:GATECH", Side = "Allied", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 2000, MaxHp = 600, Armor = "wood", Sight = 6f, Power = -100, FootprintX = 3, FootprintY = 2, Height = 3.2f, ImageId = "GATECH", CameoIcon = "TECHICON", Prerequisites = new List<string> { "GAWEAP", "GAAIRC" } });
            RegisterActor(new ActorDefData { Id = "GAPILL", Name = "机枪碉堡", UiNameKey = "Name:GAPILL", Side = "Allied", Category = "DefenseBuilding", Role = "Building", IsBuilding = true, Cost = 500, MaxHp = 400, Armor = "steel", Sight = 6f, FootprintX = 1, FootprintY = 1, Height = 1.2f, ImageId = "GAPILL", CameoIcon = "PILLICON", PrimaryWeapon = "Vulcan2", Prerequisites = new List<string> { "GAPILE" } });
            RegisterActor(new ActorDefData { Id = "ATESLA", Name = "光棱塔", UiNameKey = "Name:ATESLA", Side = "Allied", Category = "DefenseBuilding", Role = "Building", IsBuilding = true, Cost = 1500, MaxHp = 600, Armor = "steel", Sight = 8.5f, Power = -75, Powered = true, FootprintX = 1, FootprintY = 1, Height = 3.0f, ImageId = "ATESLA", CameoIcon = "atesicon", PrimaryWeapon = "PrismShot", Prerequisites = new List<string> { "GAAIRC" } });

            // Allied Units
            RegisterActor(new ActorDefData { Id = "E1", Name = "美国大兵", UiNameKey = "Name:E1", Side = "Allied", Category = "Infantry", Role = "Infantry", Cost = 200, MaxHp = 125, Armor = "none", Speed = 2.6f, Sight = 5.5f, Height = 1.1f, ImageId = "E1", CameoIcon = "GIICON", PrimaryWeapon = "M60", Prerequisites = new List<string> { "GAPILE" } });
            RegisterActor(new ActorDefData { Id = "GGI", Name = "重装大兵", UiNameKey = "Name:GGI", Side = "Allied", Category = "Infantry", Role = "Infantry", Cost = 400, MaxHp = 160, Armor = "flak", Speed = 2.3f, Sight = 6.0f, Height = 1.15f, ImageId = "GGI", CameoIcon = "GGIICON", PrimaryWeapon = "105mm", Prerequisites = new List<string> { "GAPILE" } });
            RegisterActor(new ActorDefData { Id = "AMCV", Name = "盟军基地车", UiNameKey = "Name:AMCV", Side = "Allied", Category = "Vehicle", Role = "Mcv", Cost = 3000, MaxHp = 1000, Armor = "heavy", Speed = 2.4f, Sight = 6f, Height = 1.8f, ImageId = "MGTK", CameoIcon = "MCVICON", DeploysInto = "GACNST", Prerequisites = new List<string> { "GAWEAP" } });
            RegisterActor(new ActorDefData { Id = "CMIN", Name = "超时空采矿车", UiNameKey = "Name:CMIN", Side = "Allied", Category = "Vehicle", Role = "Harvester", Cost = 1400, MaxHp = 1000, Armor = "medium", Speed = 3.2f, Sight = 5f, Height = 1.6f, ImageId = "CMIN", CameoIcon = "CMINICON", Prerequisites = new List<string> { "GAWEAP", "GAREFN" } });
            RegisterActor(new ActorDefData { Id = "MTNK", Name = "灰熊坦克", UiNameKey = "Name:MTNK", Side = "Allied", Category = "Vehicle", Role = "Tank", Cost = 700, MaxHp = 300, Armor = "heavy", Speed = 4.2f, Sight = 6f, Height = 1.4f, ImageId = "MTNK", CameoIcon = "MTNKICON", HasTurret = true, HasBarrel = true, PrimaryWeapon = "105mm", Prerequisites = new List<string> { "GAWEAP" } });
            RegisterActor(new ActorDefData { Id = "FV", Name = "多功能步兵车", UiNameKey = "Name:FV", Side = "Allied", Category = "Vehicle", Role = "Tank", Cost = 600, MaxHp = 200, Armor = "light", Speed = 5.4f, Sight = 6.5f, Height = 1.35f, ImageId = "FV", CameoIcon = "FVICON", HasTurret = true, PrimaryWeapon = "Vulcan2", Prerequisites = new List<string> { "GAWEAP" } });
            RegisterActor(new ActorDefData { Id = "SREF", Name = "光棱坦克", UiNameKey = "Name:SREF", Side = "Allied", Category = "Vehicle", Role = "Tank", Cost = 1200, MaxHp = 150, Armor = "light", Speed = 3.0f, Sight = 8.5f, Height = 1.55f, ImageId = "SREF", CameoIcon = "SREFICON", HasTurret = true, PrimaryWeapon = "Comet", Prerequisites = new List<string> { "GAWEAP", "GATECH" } });

            // Soviet Structures & Units (for AI opponent & optional Soviet play)
            RegisterActor(new ActorDefData { Id = "NACNST", Name = "苏军建造厂", UiNameKey = "Name:NACNST", Side = "Soviet", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 3000, MaxHp = 1000, Armor = "concrete", Sight = 8f, FootprintX = 4, FootprintY = 4, Height = 2.6f, ImageId = "NACNST", CameoIcon = "NCNSTICO" });
            RegisterActor(new ActorDefData { Id = "NAPOWR", Name = "磁能反应炉", UiNameKey = "Name:NAPOWR", Side = "Soviet", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 600, MaxHp = 750, Armor = "wood", Sight = 5f, Power = 150, FootprintX = 2, FootprintY = 2, Height = 2.4f, ImageId = "NAPOWR", CameoIcon = "NPOWRICO", Prerequisites = new List<string> { "NACNST" } });
            RegisterActor(new ActorDefData { Id = "NAREFN", Name = "苏联矿石精炼厂", UiNameKey = "Name:NAREFN", Side = "Soviet", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 2000, MaxHp = 1000, Armor = "wood", Sight = 6f, Power = -50, FootprintX = 4, FootprintY = 3, Height = 2.5f, ImageId = "NAREFN", CameoIcon = "NREFNICO", FreeUnit = "HARV", Prerequisites = new List<string> { "NACNST", "NAPOWR" } });
            RegisterActor(new ActorDefData { Id = "NAHAND", Name = "苏联兵营", UiNameKey = "Name:NAHAND", Side = "Soviet", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 500, MaxHp = 500, Armor = "steel", Sight = 5f, Power = -10, FootprintX = 2, FootprintY = 2, Height = 2.2f, ImageId = "NAHAND", CameoIcon = "HANDICON", Prerequisites = new List<string> { "NACNST", "NAPOWR" } });
            RegisterActor(new ActorDefData { Id = "NAWEAP", Name = "苏联战车工厂", UiNameKey = "Name:NAWEAP", Side = "Soviet", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 2000, MaxHp = 1000, Armor = "wood", Sight = 6f, Power = -25, FootprintX = 5, FootprintY = 3, Height = 2.8f, ImageId = "NAWEAP", CameoIcon = "NWEAPICO", Prerequisites = new List<string> { "NACNST", "NAREFN" } });
            RegisterActor(new ActorDefData { Id = "NATECH", Name = "苏联作战实验室", UiNameKey = "Name:NATECH", Side = "Soviet", Category = "MainBuilding", Role = "Building", IsBuilding = true, Cost = 2000, MaxHp = 600, Armor = "wood", Sight = 6f, Power = -100, FootprintX = 3, FootprintY = 2, Height = 3.2f, ImageId = "NATECH", CameoIcon = "NTECHICO", Prerequisites = new List<string> { "NAWEAP" } });
            RegisterActor(new ActorDefData { Id = "TESLA", Name = "磁暴线圈", UiNameKey = "Name:TESLA", Side = "Soviet", Category = "DefenseBuilding", Role = "Building", IsBuilding = true, Cost = 1500, MaxHp = 600, Armor = "steel", Sight = 7.8f, Power = -75, Powered = true, FootprintX = 1, FootprintY = 1, Height = 3.0f, ImageId = "TESLA", CameoIcon = "TSLAICON", PrimaryWeapon = "CoilBolt", Prerequisites = new List<string> { "NAHAND" } });
            RegisterActor(new ActorDefData { Id = "E2", Name = "动员兵", UiNameKey = "Name:E2", Side = "Soviet", Category = "Infantry", Role = "Infantry", Cost = 100, MaxHp = 125, Armor = "none", Speed = 2.5f, Sight = 5.0f, Height = 1.1f, ImageId = "E2", CameoIcon = "E2ICON", PrimaryWeapon = "M60", Prerequisites = new List<string> { "NAHAND" } });
            RegisterActor(new ActorDefData { Id = "SMCV", Name = "苏联基地车", UiNameKey = "Name:SMCV", Side = "Soviet", Category = "Vehicle", Role = "Mcv", Cost = 3000, MaxHp = 1000, Armor = "heavy", Speed = 2.4f, Sight = 6f, Height = 1.8f, ImageId = "SMCV", CameoIcon = "SMCVICON", DeploysInto = "NACNST", Prerequisites = new List<string> { "NAWEAP" } });
            RegisterActor(new ActorDefData { Id = "HARV", Name = "武装采矿车", UiNameKey = "Name:HARV", Side = "Soviet", Category = "Vehicle", Role = "Harvester", Cost = 1400, MaxHp = 1000, Armor = "heavy", Speed = 3.0f, Sight = 5.5f, Height = 1.7f, ImageId = "HARV", CameoIcon = "HARVICON", HasTurret = true, PrimaryWeapon = "M60", Prerequisites = new List<string> { "NAWEAP", "NAREFN" } });
            RegisterActor(new ActorDefData { Id = "HTNK", Name = "犀牛坦克", UiNameKey = "Name:HTNK", Side = "Soviet", Category = "Vehicle", Role = "Tank", Cost = 900, MaxHp = 400, Armor = "heavy", Speed = 3.6f, Sight = 6f, Height = 1.45f, ImageId = "HTNK", CameoIcon = "HTNKICON", HasTurret = true, HasBarrel = true, PrimaryWeapon = "120mm", Prerequisites = new List<string> { "NAWEAP" } });
            RegisterActor(new ActorDefData { Id = "APOC", Name = "天启坦克", UiNameKey = "Name:APOC", Side = "Soviet", Category = "Vehicle", Role = "Tank", Cost = 1750, MaxHp = 800, Armor = "heavy", Speed = 2.6f, Sight = 6.5f, Height = 1.7f, ImageId = "APOC", CameoIcon = "APOCICON", HasTurret = true, HasBarrel = true, PrimaryWeapon = "120mmx", Prerequisites = new List<string> { "NAWEAP", "NATECH" } });
        }
    }
}
