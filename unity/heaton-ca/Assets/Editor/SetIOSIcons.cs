// One-shot utility that fills the iOS icon slots from Assets/Icons/iOS/. iOS
// strips the alpha channel from app icons, so the transparent macOS-style
// master (a green tile floating in a margin) can't ship as-is - and flattening
// it over white put a white picture-frame around every home-screen icon. These
// are FULL-BLEED instead: the tile cropped to its bounds and scaled to fill
// the canvas, rounded-corner gaps flooded with the tile's modal opaque color
// (140, 239, 156 - the HeatonCA green; recompute if the art changes); iOS
// applies its own squircle mask. Without explicit slots, every iOS icon falls
// back to the transparent default and goes black. Regenerate from the master
// with Pillow (python/.venv) if the art changes:
//   m = Image.open('Assets/Icons/heatonca_icon_1024.png').convert('RGBA')
//   tile = m.crop(m.getbbox()).resize(m.size, Image.LANCZOS)
//   flat = Image.alpha_composite(Image.new('RGBA', m.size, (140, 239, 156, 255)), tile).convert('RGB')
//   flat.save('Assets/Icons/iOS/app_store_1024.png')
//   for s in (180, 167, 152, 120, 76):
//       flat.resize((s, s), Image.LANCZOS).save(f'Assets/Icons/iOS/app_{s}.png')
// Run headless:
//   Unity -batchmode -quit -projectPath . -buildTarget iOS -executeMethod SetIOSIcons.Apply
//
// Guarded like iOSPostBuild: UnityEditor.iOS ships with the iOS build support
// module, so an editor that lacks it (a Windows box installs only the modules it
// can build) fails to compile this file and takes every other build down with it.
// The guard costs nothing - the define is on exactly when the active target is
// iOS, which is the only way this is ever invoked.
#if UNITY_IOS
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.iOS;
using UnityEngine;

/// <summary>Assigns the full-bleed HeatonCA icon set to every iOS icon kind.</summary>
public static class SetIOSIcons
{
    /// <summary>Folder holding app_{size}.png and app_store_1024.png.</summary>
    public const string IconFolder = "Assets/Icons/iOS";

    /// <summary>Entry point (menu and -executeMethod).</summary>
    [MenuItem("Tools/HeatonCA/Set iOS Icons")]
    public static void Apply()
    {
        var appStore = Load($"{IconFolder}/app_store_1024.png");

        // Application slots get exact-size art (180/120 iPhone, 167/152/76 iPad);
        // the smaller kinds (spotlight/settings/notification) and App Store get
        // the 1024 - Unity rescales.
        var icons = PlayerSettings.GetPlatformIcons(
            NamedBuildTarget.iOS, iOSPlatformIconKind.Application);
        foreach (var icon in icons)
        {
            icon.SetTextures(Load($"{IconFolder}/app_{icon.width}.png"));
        }

        PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, iOSPlatformIconKind.Application, icons);

        SetKind(iOSPlatformIconKind.Spotlight, appStore);
        SetKind(iOSPlatformIconKind.Settings, appStore);
        SetKind(iOSPlatformIconKind.Notification, appStore);
        SetKind(iOSPlatformIconKind.Marketing, appStore);

        AssetDatabase.SaveAssets();
        Console.WriteLine("SetIOSIcons: application/spotlight/settings/notification/marketing icons assigned.");
    }

    private static Texture2D Load(string path)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            throw new Exception($"SetIOSIcons: missing texture at {path}");
        }

        return tex;
    }

    // Every size slot of the kind gets the same source texture; Unity rescales.
    private static void SetKind(PlatformIconKind kind, params Texture2D[] textures)
    {
        var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
        foreach (var icon in icons)
        {
            icon.SetTextures(textures);
        }

        PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
    }
}
#endif
