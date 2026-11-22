using UnityEditor;
using UnityEngine;

namespace ReCnc.Unity.Editor
{
    public class Ra2AssetPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_ReCncGenerated/RemasteredModels/")) return;

            var modelImporter = (ModelImporter)assetImporter;
            modelImporter.importCameras = false;
            modelImporter.importLights = false;
            modelImporter.importAnimation = false;
            modelImporter.animationType = ModelImporterAnimationType.None;
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_ReCncGenerated/")) return;

            var importer = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("palette_lut.png") || assetPath.EndsWith("unittem_lut.png"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
            else if (assetPath.Contains("/Cameos/") || assetPath.Contains("/Sprites/"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = assetPath.Contains("/Sprites/") ? FilterMode.Point : FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
        }
    }
}
