using System;
using System.Collections.Generic;
using ReCnc.Unity.Assets;
using UnityEngine;

namespace ReCnc.Unity.View
{
    public static class Ra2PbrMaterials
    {
        private static Dictionary<string, Material> _sharedMaterials;
        private static Ra2AssetRegistry _boundRegistry;

        public static void BindRegistryTextures(Ra2AssetRegistry registry)
        {
            if (registry == null || _boundRegistry == registry) return;
            _boundRegistry = registry;
            _sharedMaterials = null;
            EnsureInitialized();
        }

        public static Material ResolveSlotMaterial(string rawSlotName, Color factionColor, Ra2AssetRegistry registry, out bool isTeamSlot)
        {
            if (registry != null && _boundRegistry != registry)
            {
                BindRegistryTextures(registry);
            }
            else
            {
                EnsureInitialized();
            }

            isTeamSlot = false;
            string key = NormalizeSlotName(rawSlotName);
            if (key == "Mat_TeamPaint")
            {
                isTeamSlot = true;
                var teamMat = new Material(_sharedMaterials["Mat_TeamPaint"]);
                SetLitColor(teamMat, factionColor);
                return teamMat;
            }

            Material mat;
            if (_sharedMaterials.TryGetValue(key, out mat))
            {
                return mat;
            }
            return _sharedMaterials["Mat_BrushedSteel"];
        }

        private static string NormalizeSlotName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Mat_BrushedSteel";
            int spaceIdx = raw.IndexOf(' ');
            if (spaceIdx > 0) raw = raw.Substring(0, spaceIdx);
            int dotIdx = raw.IndexOf('.');
            if (dotIdx > 0) raw = raw.Substring(0, dotIdx);
            return raw.Trim();
        }

        private static void EnsureInitialized()
        {
            if (_sharedMaterials != null) return;
            _sharedMaterials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            // Balanced dielectric/semi-metallic values + high-res Albedo & Normal textures so surfaces look crisp & bright!
            Reg("Mat_TeamPaint",       Color.white,                        0.28f, 0.58f, Color.black, 0f);
            Reg("Mat_BrushedSteel",    new Color(0.94f, 0.96f, 1.00f),     0.38f, 0.64f, Color.black, 0f);
            Reg("Mat_CorrugatedSteel", new Color(0.95f, 0.97f, 1.00f),     0.34f, 0.60f, Color.black, 0f);
            Reg("Mat_DarkSteel",       new Color(0.88f, 0.90f, 0.94f),     0.42f, 0.52f, Color.black, 0f);
            Reg("Mat_TreadRubber",     new Color(0.85f, 0.85f, 0.88f),     0.18f, 0.28f, Color.black, 0f);
            Reg("Mat_Concrete",        new Color(0.96f, 0.95f, 0.92f),     0.05f, 0.20f, Color.black, 0f);
            Reg("Mat_SovietBrick",     new Color(0.98f, 0.96f, 0.94f),     0.06f, 0.24f, Color.black, 0f);
            Reg("Mat_GoldBrass",       new Color(1.00f, 0.96f, 0.88f),     0.48f, 0.72f, Color.black, 0f);
            Reg("Mat_HazardYellow",    Color.white,                        0.15f, 0.48f, Color.black, 0f);
            Reg("Mat_ShutterDoor",     Color.white,                        0.38f, 0.50f, Color.black, 0f);
            Reg("Mat_SovietGate",      Color.white,                        0.25f, 0.45f, Color.black, 0f);
            Reg("Mat_AlliedCrest",     Color.white,                        0.22f, 0.55f, Color.black, 0f);
            Reg("Mat_GlassDark",       new Color(0.10f, 0.26f, 0.46f),     0.65f, 0.88f, new Color(0.06f, 0.20f, 0.40f), 0.6f);
            Reg("Mat_GlowCyan",        new Color(0.38f, 0.96f, 1.00f),     0.10f, 0.85f, new Color(0.30f, 0.92f, 1.00f), 2.4f);
            Reg("Mat_GlowPlasma",      new Color(0.55f, 0.84f, 1.00f),     0.15f, 0.85f, new Color(0.45f, 0.78f, 1.00f), 2.2f);
            Reg("Mat_GlowGreen",       new Color(0.22f, 0.96f, 0.64f),     0.10f, 0.82f, new Color(0.16f, 0.90f, 0.54f), 2.0f);
            Reg("Mat_GlowRed",         new Color(1.00f, 0.24f, 0.18f),     0.10f, 0.80f, new Color(1.00f, 0.20f, 0.14f), 2.2f);
            Reg("Mat_Skin",            new Color(0.85f, 0.66f, 0.53f),     0.00f, 0.34f, Color.black, 0f);
            Reg("Mat_AlliedCloth",     new Color(0.30f, 0.38f, 0.33f),     0.04f, 0.24f, Color.black, 0f);
            Reg("Mat_SovietCoat",      new Color(0.48f, 0.38f, 0.24f),     0.04f, 0.22f, Color.black, 0f);
            Reg("Mat_WoodStock",       new Color(0.42f, 0.23f, 0.11f),     0.04f, 0.32f, Color.black, 0f);
        }

        private static void Reg(string slotName, Color fallbackTint, float metallic, float smoothness, Color emissionCol, float emissionIntensity)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(s);

            PbrTexturePairEntry texPair = _boundRegistry != null ? _boundRegistry.GetPbrTextures(slotName) : null;
            if (texPair != null && texPair.AlbedoMap != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texPair.AlbedoMap);
                mat.mainTexture = texPair.AlbedoMap;
                SetLitColor(mat, fallbackTint);
            }
            else
            {
                // Fallback colors when textures are absent
                Color noTexCol = slotName == "Mat_DarkSteel" ? new Color(0.28f, 0.31f, 0.35f)
                    : slotName == "Mat_TreadRubber" ? new Color(0.15f, 0.16f, 0.18f)
                    : slotName == "Mat_SovietBrick" ? new Color(0.54f, 0.38f, 0.30f)
                    : slotName == "Mat_Concrete" ? new Color(0.56f, 0.56f, 0.52f)
                    : slotName == "Mat_GoldBrass" ? new Color(0.95f, 0.76f, 0.26f)
                    : fallbackTint;
                SetLitColor(mat, noTexCol);
            }

            if (texPair != null && texPair.NormalMap != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", texPair.NormalMap);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.9f);
            }

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emissionIntensity > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", emissionCol * emissionIntensity);
                }
            }

            _sharedMaterials[slotName] = mat;
        }

        public static void SetLitColor(Material mat, Color col)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            mat.color = col;
        }
    }
}
