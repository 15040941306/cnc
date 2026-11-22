using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReCnc.Unity.View
{
    public static class Ra2PbrMaterials
    {
        private static Dictionary<string, Material> _sharedMaterials;

        public static Material ResolveSlotMaterial(string rawSlotName, Color factionColor, out bool isTeamSlot)
        {
            EnsureInitialized();
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

            _sharedMaterials["Mat_TeamPaint"]    = BuildUrpLit(new Color(0.20f, 0.54f, 0.98f), 0.35f, 0.62f, Color.black, 0f);
            _sharedMaterials["Mat_BrushedSteel"] = BuildUrpLit(new Color(0.74f, 0.77f, 0.81f), 0.82f, 0.68f, Color.black, 0f);
            _sharedMaterials["Mat_DarkSteel"]    = BuildUrpLit(new Color(0.26f, 0.28f, 0.32f), 0.76f, 0.56f, Color.black, 0f);
            _sharedMaterials["Mat_TreadRubber"]  = BuildUrpLit(new Color(0.13f, 0.14f, 0.15f), 0.12f, 0.22f, Color.black, 0f);
            _sharedMaterials["Mat_Concrete"]     = BuildUrpLit(new Color(0.52f, 0.53f, 0.50f), 0.05f, 0.18f, Color.black, 0f);
            _sharedMaterials["Mat_SovietBrick"]  = BuildUrpLit(new Color(0.50f, 0.34f, 0.26f), 0.08f, 0.25f, Color.black, 0f);
            _sharedMaterials["Mat_GoldBrass"]    = BuildUrpLit(new Color(0.95f, 0.74f, 0.22f), 0.88f, 0.76f, Color.black, 0f);
            _sharedMaterials["Mat_HazardYellow"] = BuildUrpLit(new Color(0.96f, 0.76f, 0.10f), 0.20f, 0.52f, Color.black, 0f);
            _sharedMaterials["Mat_GlassDark"]    = BuildUrpLit(new Color(0.09f, 0.20f, 0.32f), 0.90f, 0.88f, new Color(0.05f, 0.14f, 0.25f), 0.5f);
            _sharedMaterials["Mat_GlowCyan"]     = BuildUrpLit(new Color(0.38f, 0.96f, 1.00f), 0.10f, 0.85f, new Color(0.32f, 0.94f, 1.00f), 2.4f);
            _sharedMaterials["Mat_GlowPlasma"]   = BuildUrpLit(new Color(0.58f, 0.82f, 1.00f), 0.15f, 0.85f, new Color(0.48f, 0.76f, 1.00f), 2.2f);
            _sharedMaterials["Mat_GlowRed"]      = BuildUrpLit(new Color(1.00f, 0.24f, 0.18f), 0.10f, 0.80f, new Color(1.00f, 0.20f, 0.14f), 2.2f);
            _sharedMaterials["Mat_Skin"]         = BuildUrpLit(new Color(0.84f, 0.65f, 0.52f), 0.00f, 0.32f, Color.black, 0f);
            _sharedMaterials["Mat_AlliedCloth"]  = BuildUrpLit(new Color(0.30f, 0.36f, 0.32f), 0.05f, 0.25f, Color.black, 0f);
            _sharedMaterials["Mat_SovietCoat"]   = BuildUrpLit(new Color(0.46f, 0.37f, 0.24f), 0.05f, 0.22f, Color.black, 0f);
            _sharedMaterials["Mat_WoodStock"]    = BuildUrpLit(new Color(0.38f, 0.21f, 0.11f), 0.05f, 0.30f, Color.black, 0f);
        }

        private static Material BuildUrpLit(Color baseCol, float metallic, float smoothness, Color emissionCol, float emissionIntensity)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(s);
            SetLitColor(mat, baseCol);
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
            return mat;
        }

        public static void SetLitColor(Material mat, Color col)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            mat.color = col;
        }
    }
}
