#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// DEBE estar dentro de una carpeta llamada "Editor".
/// Configura solo los PNG de Resources/EthernetMinigame para pixel art:
/// Sprite, filtro Point (sin suavizado) y sin compresión.
/// </summary>
public class EthernetSpriteImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("/Resources/EthernetMinigame/")) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
    }
}
#endif