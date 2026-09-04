// One-shot utility that assigns the 1024 master to the default icon group
// (m_BuildTargetIcons with an empty m_BuildTarget in ProjectSettings.asset). The
// default icon is what macOS, Windows, and WebGL builds use, and what every
// platform falls back to when it has no slot set of its own. bootstrap.sh copied
// the block with the master's pinned GUID (a7c31f5b9d2e4c68a1f3b5d7e9c2a4f6);
// this re-asserts it when the file is ever replaced. No platform module is
// involved, so unlike SetIOSIcons/SetAndroidIcons it needs no guard. Run headless:
//   Unity -batchmode -quit -projectPath . -executeMethod SetDefaultIcon.Apply

using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>Assigns Assets/Icons/heatonca_icon_1024.png to the default icon group.</summary>
public static class SetDefaultIcon
{
    /// <summary>The transparent-margin macOS-style master (1024x1024 RGBA).</summary>
    public const string MasterPath = "Assets/Icons/heatonca_icon_1024.png";

    /// <summary>Entry point (menu and -executeMethod).</summary>
    [MenuItem("Tools/HeatonCA/Set Default Icon")]
    public static void Apply()
    {
        var master = AssetDatabase.LoadAssetAtPath<Texture2D>(MasterPath);
        if (master == null)
        {
            throw new Exception($"SetDefaultIcon: missing texture at {MasterPath}");
        }

        // The Unknown (default) group has one slot per size Unity reports; every
        // slot gets the master and Unity rescales.
        int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Unknown, IconKind.Any);
        var icons = new Texture2D[sizes.Length];
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i] = master;
        }

        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, icons, IconKind.Any);
        AssetDatabase.SaveAssets();
        Console.WriteLine($"SetDefaultIcon: {icons.Length} default icon slot(s) assigned from {MasterPath}.");
    }
}
