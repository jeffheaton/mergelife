// One-shot utility that fills the Android launcher-icon slots from
// Assets/Icons/Android/ (generated from Assets/Icons/heatonca_icon_1024.png;
// see the Pillow recipe in the heaton-life-unity history). Without these, an
// Android build ships Unity's default cube icon. Run headless:
//   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod SetAndroidIcons.Apply
//
// Adaptive layers: index 0 = background (the icon's own tile green, 140,239,156 -
// the art is a CA pattern, so an edge-cropped solid works), index 1 = foreground
// (the rounded-square art at 50% of the 108dp canvas, the zero-clipping bound
// inside the 72dp mask). Round gets a pre-composed green disk; Legacy is the raw
// rounded-square art.
//
// Guarded for the same reason as SetIOSIcons: UnityEditor.Android comes with the
// Android build support module, and an editor without it cannot compile this file
// - which would break every unrelated build on that machine.
#if UNITY_ANDROID
using System;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

/// <summary>Assigns the adaptive, round, and legacy HeatonCA launcher icons.</summary>
public static class SetAndroidIcons
{
    /// <summary>Folder holding adaptive_bg, adaptive_fg, round, and legacy PNGs.</summary>
    public const string IconFolder = "Assets/Icons/Android";

    /// <summary>Entry point (menu and -executeMethod).</summary>
    [MenuItem("Tools/HeatonCA/Set Android Icons")]
    public static void Apply()
    {
        var bg     = Load($"{IconFolder}/adaptive_bg.png");
        var fg     = Load($"{IconFolder}/adaptive_fg.png");
        var round  = Load($"{IconFolder}/round.png");
        var legacy = Load($"{IconFolder}/legacy.png");

        SetKind(AndroidPlatformIconKind.Adaptive, bg, fg);

        // 6000.5 marks Round and Legacy obsolete (CS0618: "use Adaptive instead")
        // because every device at minSdk 26 renders adaptive icons. The kinds still
        // serialize and ship, and the ProjectSettings block copied from
        // heaton-life-unity carries all three, so keep re-asserting them.
#pragma warning disable 0618
        SetKind(AndroidPlatformIconKind.Round,    round);
        SetKind(AndroidPlatformIconKind.Legacy,   legacy);
#pragma warning restore 0618

        AssetDatabase.SaveAssets();
        Console.WriteLine("SetAndroidIcons: adaptive/round/legacy Android icons assigned.");
    }

    private static Texture2D Load(string path)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            throw new Exception($"SetAndroidIcons: missing texture at {path}");
        }

        return tex;
    }

    // Every size slot of the kind gets the same source textures; Unity rescales.
    private static void SetKind(PlatformIconKind kind, params Texture2D[] textures)
    {
        var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
        foreach (var icon in icons)
        {
            icon.SetTextures(textures);
        }

        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
    }
}
#endif
