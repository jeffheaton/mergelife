// Import rules for the app's art, enforced at import time so a stray click in the
// Inspector (or a fresh clone importing with Unity's defaults) can never ship a
// blurred, compressed, or mipmapped gallery tile. The EditMode GalleryAssetsTests
// check the same properties on the imported textures.
//
//   Assets/Resources/Gallery/  the 30 curated 300x192 previews: Point filter,
//                              uncompressed, no mipmaps, no power-of-two rescale,
//                              sRGB, max 512, clamp - pixel-exact like PyQt.
//   Assets/Resources/Art/      title art: max 1024, normal compression, no
//                              mipmaps, no rescale, bilinear.
//   Assets/Icons/              app-icon masters: uncompressed, no mipmaps, alpha
//                              is transparency, no rescale (180/167/152/76 are not
//                              powers of two; ToNearest would resample them twice).
//
// GetVersion() is bumped whenever a rule changes: Unity reimports every texture
// whose postprocessor version differs, so a rule edit takes effect on the next
// gate run without touching each .meta by hand. Per-platform overrides are
// cleared for the standard targets so the default block below is what ships.

using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>Texture import rules for the Gallery, Art, and Icons folders.</summary>
public class ArtImportSettings : AssetPostprocessor
{
    /// <summary>Gallery previews (project-relative prefix, forward slashes).</summary>
    public const string GalleryFolder = "Assets/Resources/Gallery/";

    /// <summary>Title art.</summary>
    public const string ArtFolder = "Assets/Resources/Art/";

    /// <summary>App icon masters and the iOS/Android slot sets.</summary>
    public const string IconsFolder = "Assets/Icons/";

    /// <summary>Bump when a rule below changes so the affected textures reimport.</summary>
    public const uint RulesVersion = 1;

    // Platforms whose per-texture override could otherwise hide the default rule
    // (the names the importer serializes: Standalone, iOS, Android, WebGL).
    private static readonly string[] OverridablePlatforms =
    {
        NamedBuildTarget.Standalone.TargetName,
        NamedBuildTarget.iOS.TargetName,
        NamedBuildTarget.Android.TargetName,
        NamedBuildTarget.WebGL.TargetName,
    };

    /// <summary>Version of the rules; a change triggers a reimport of matching assets.</summary>
    public override uint GetVersion()
    {
        return RulesVersion;
    }

    private void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;

        if (assetPath.StartsWith(GalleryFolder, StringComparison.Ordinal))
        {
            ApplyGallery(importer);
        }
        else if (assetPath.StartsWith(ArtFolder, StringComparison.Ordinal))
        {
            ApplyArt(importer);
        }
        else if (assetPath.StartsWith(IconsFolder, StringComparison.Ordinal))
        {
            ApplyIcons(importer);
        }
    }

    /// <summary>Pixel-exact 300x192 previews: the tile must look like PyQt's QPixmap.</summary>
    private static void ApplyGallery(TextureImporter importer)
    {
        importer.textureType        = TextureImporterType.Default;
        importer.filterMode         = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled      = false;
        importer.npotScale          = TextureImporterNPOTScale.None;
        importer.sRGBTexture        = true;
        importer.maxTextureSize     = 512;
        importer.wrapMode           = TextureWrapMode.Clamp;
        ClearOverrides(importer);
    }

    /// <summary>Title art: full-size bilinear, compressed normally.</summary>
    private static void ApplyArt(TextureImporter importer)
    {
        importer.maxTextureSize     = 1024;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.mipmapEnabled      = false;
        importer.npotScale          = TextureImporterNPOTScale.None;
        importer.filterMode         = FilterMode.Bilinear;
        ClearOverrides(importer);
    }

    /// <summary>Icon sources: lossless, alpha preserved as transparency, never resampled.</summary>
    private static void ApplyIcons(TextureImporter importer)
    {
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled       = false;
        importer.alphaIsTransparency = true;
        importer.npotScale           = TextureImporterNPOTScale.None;
        ClearOverrides(importer);
    }

    private static void ClearOverrides(TextureImporter importer)
    {
        foreach (string platform in OverridablePlatforms)
        {
            importer.ClearPlatformTextureSettings(platform);
        }
    }
}
