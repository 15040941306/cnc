using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReCnc.Unity.Assets
{
    [Serializable]
    public class RemasteredModelAssetEntry
    {
        public string ActorId;
        public GameObject FbxPrefab;
    }

    [Serializable]
    public class PbrTexturePairEntry
    {
        public string MaterialSlotName;
        public Texture2D AlbedoMap;
        public Texture2D NormalMap;
    }

    [Serializable]
    public class VoxelModelAssetEntry
    {
        public string UnitId;
        public GameObject BodyModelAsset;
        public GameObject TurretModelAsset;
        public GameObject BarrelModelAsset;
        public Texture2D PaletteLutTexture;
        public Vector3 TurretOffset;
        public Vector3 BarrelOffset;
    }

    [Serializable]
    public class SpriteAssetEntry
    {
        public string Id;
        public Sprite Sprite;
    }

    [Serializable]
    public class AudioAssetEntry
    {
        public string Id;
        public AudioClip Clip;
    }

    public class Ra2AssetRegistry : ScriptableObject
    {
        public Shader VoxelTeamShader;
        public List<RemasteredModelAssetEntry> RemasteredModels = new List<RemasteredModelAssetEntry>();
        public List<PbrTexturePairEntry> PbrTextures = new List<PbrTexturePairEntry>();
        public List<VoxelModelAssetEntry> Models = new List<VoxelModelAssetEntry>();
        public List<SpriteAssetEntry> Sprites = new List<SpriteAssetEntry>();
        public List<SpriteAssetEntry> Cameos = new List<SpriteAssetEntry>();
        public List<AudioAssetEntry> Audios = new List<AudioAssetEntry>();

        private Dictionary<string, GameObject> _remasteredMap;
        private Dictionary<string, PbrTexturePairEntry> _pbrTexMap;
        private Dictionary<string, VoxelModelAssetEntry> _modelMap;
        private Dictionary<string, Sprite> _spriteMap;
        private Dictionary<string, Sprite> _cameoMap;
        private Dictionary<string, AudioClip> _audioMap;

        public void InvalidateCache()
        {
            _remasteredMap = null;
            _pbrTexMap = null;
            _modelMap = null;
            _spriteMap = null;
            _cameoMap = null;
            _audioMap = null;
        }

        public void EnsureIndexed()
        {
            if (_modelMap != null) return;

            _remasteredMap = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < RemasteredModels.Count; i++)
            {
                var r = RemasteredModels[i];
                if (!string.IsNullOrEmpty(r.ActorId) && r.FbxPrefab != null)
                {
                    _remasteredMap[r.ActorId] = r.FbxPrefab;
                }
            }

            _pbrTexMap = new Dictionary<string, PbrTexturePairEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < PbrTextures.Count; i++)
            {
                var t = PbrTextures[i];
                if (!string.IsNullOrEmpty(t.MaterialSlotName))
                {
                    _pbrTexMap[t.MaterialSlotName] = t;
                }
            }

            _modelMap = new Dictionary<string, VoxelModelAssetEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Models.Count; i++)
            {
                if (!string.IsNullOrEmpty(Models[i].UnitId)) _modelMap[Models[i].UnitId] = Models[i];
            }

            _spriteMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Sprites.Count; i++)
            {
                if (!string.IsNullOrEmpty(Sprites[i].Id) && Sprites[i].Sprite != null) _spriteMap[Sprites[i].Id] = Sprites[i].Sprite;
            }

            _cameoMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Cameos.Count; i++)
            {
                if (!string.IsNullOrEmpty(Cameos[i].Id) && Sprites != null && Cameos[i].Sprite != null) _cameoMap[Cameos[i].Id] = Cameos[i].Sprite;
            }

            _audioMap = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Audios.Count; i++)
            {
                if (!string.IsNullOrEmpty(Audios[i].Id) && Audios[i].Clip != null) _audioMap[Audios[i].Id] = Audios[i].Clip;
            }
        }

        public GameObject GetRemasteredModel(string actorId)
        {
            EnsureIndexed();
            GameObject prefab;
            return !string.IsNullOrEmpty(actorId) && _remasteredMap.TryGetValue(actorId, out prefab) ? prefab : null;
        }

        public PbrTexturePairEntry GetPbrTextures(string slotName)
        {
            EnsureIndexed();
            PbrTexturePairEntry entry;
            return !string.IsNullOrEmpty(slotName) && _pbrTexMap.TryGetValue(slotName, out entry) ? entry : null;
        }

        public VoxelModelAssetEntry GetModel(string unitId)
        {
            EnsureIndexed();
            VoxelModelAssetEntry entry;
            return !string.IsNullOrEmpty(unitId) && _modelMap.TryGetValue(unitId, out entry) ? entry : null;
        }

        public Sprite GetSprite(string id)
        {
            EnsureIndexed();
            Sprite s;
            return !string.IsNullOrEmpty(id) && _spriteMap.TryGetValue(id, out s) ? s : null;
        }

        public Sprite GetCameo(string id)
        {
            EnsureIndexed();
            Sprite s;
            return !string.IsNullOrEmpty(id) && _cameoMap.TryGetValue(id, out s) ? s : null;
        }

        public AudioClip GetAudio(string id)
        {
            EnsureIndexed();
            AudioClip clip;
            return !string.IsNullOrEmpty(id) && _audioMap.TryGetValue(id, out clip) ? clip : null;
        }
    }
}
