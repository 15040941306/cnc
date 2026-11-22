using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReCnc.Unity.Assets
{
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
        public List<VoxelModelAssetEntry> Models = new List<VoxelModelAssetEntry>();
        public List<SpriteAssetEntry> Sprites = new List<SpriteAssetEntry>();
        public List<SpriteAssetEntry> Cameos = new List<SpriteAssetEntry>();
        public List<AudioAssetEntry> Audios = new List<AudioAssetEntry>();

        private Dictionary<string, VoxelModelAssetEntry> _modelMap;
        private Dictionary<string, Sprite> _spriteMap;
        private Dictionary<string, Sprite> _cameoMap;
        private Dictionary<string, AudioClip> _audioMap;

        public void EnsureIndexed()
        {
            if (_modelMap != null) return;
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
                if (!string.IsNullOrEmpty(Cameos[i].Id) && Cameos[i].Sprite != null) _cameoMap[Cameos[i].Id] = Cameos[i].Sprite;
            }

            _audioMap = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Audios.Count; i++)
            {
                if (!string.IsNullOrEmpty(Audios[i].Id) && Audios[i].Clip != null) _audioMap[Audios[i].Id] = Audios[i].Clip;
            }
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
