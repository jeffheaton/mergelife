#if UNITY_IOS
// iOS Xcode-project patches (the dynaface-unity / heaton-life-unity pattern).
// Guarded like the house file: UnityEditor.iOS.Xcode ships with the iOS build
// support module, and an unguarded using would break every unrelated build on a
// machine without it. Runs at order 100, after NativeGallery's own post-build
// (order 1), so the photo-library purpose string below is the one that ships.
using System;
using System.IO;
using HeatonCA.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// Patches the generated Xcode project after every iOS build: Info.plist keys
/// App Store Connect and the Files app need, the dSYM settings App Store
/// validation requires, automatic signing with the team, and the app-level
/// privacy manifest.
/// </summary>
public static class iOSPostBuild
{
    /// <summary>
    /// Apple developer team, read from <see cref="ProjectIdentity.AppleTeamEnv"/>.
    /// Empty when that variable is unset, which is what a fresh Xcode project
    /// carries: the simulator gate is unaffected, and a device or store build
    /// stops with Xcode's own "requires a development team" until it is exported.
    /// </summary>
    public static string DevelopmentTeam =>
        Environment.GetEnvironmentVariable(ProjectIdentity.AppleTeamEnv) ?? string.Empty;

    /// <summary>
    /// Shown by iOS the first time the app adds a PNG to the photo library
    /// (NativeGallery.SaveImageToGallery, PngExporter). Add-only access: the app
    /// never reads the library.
    /// </summary>
    public const string PhotoLibraryAddUsageDescription =
        "HeatonCA saves the PNG images you export to your photo library.";

    /// <summary>Entry point registered with Unity's post-build callbacks.</summary>
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        var plistPath = Path.Combine(buildPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // Pre-answers App Store Connect's export-compliance question.
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

        // Exported snapshots also land under Application.persistentDataPath, which on
        // iOS is the app's Documents container - invisible to the user unless the app
        // publishes it. Together these two keys surface Documents as
        // "On My iPhone/iPad -> HeatonCA" in the Files app, where the PNGs can be
        // opened, shared, and copied out.
        plist.root.SetBoolean("UIFileSharingEnabled", true);
        plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);

        // Save PNG on iOS goes through NativeGallery into the photo library; iOS
        // shows this text on the one-time permission prompt and refuses to launch
        // the API without it. NativeGallery writes a generic sentence from
        // ProjectSettings/NativeGallery.json at order 1; this runs later and wins.
        plist.root.SetString("NSPhotoLibraryAddUsageDescription", PhotoLibraryAddUsageDescription);

        // Add-only is the whole story: PngExporter calls only
        // NativeGallery.SaveImageToGallery, and NativeGallery pins
        // PermissionFreeMode = true, so on iOS 14+ it asks for PHAccessLevelAddOnly
        // and never reads the library. NativeGallery's order-1 post-build sets the
        // read key anyway, with its stock placeholder ("The app requires access to
        // Photos to interact with it."), which App Store review rejects as
        // insufficient -- and no honest replacement exists for a resource the app
        // does not touch. Drop the key instead; the prompt the user actually sees
        // comes from NSPhotoLibraryAddUsageDescription above.
        plist.root.values.Remove("NSPhotoLibraryUsageDescription");

        plist.WriteToFile(plistPath);

        // App Store validation requires a dSYM for UnityRuntime.framework.
        // Unity doesn't set dwarf-with-dsym on its generated targets by default,
        // so patch both targets after every build.
        var pbxPath = PBXProject.GetPBXProjectPath(buildPath);
        var pbx     = new PBXProject();
        pbx.ReadFromFile(pbxPath);

        string mainTarget      = pbx.GetUnityMainTargetGuid();
        string frameworkTarget = pbx.GetUnityFrameworkTargetGuid();

        pbx.SetBuildProperty(mainTarget,      "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");
        pbx.SetBuildProperty(frameworkTarget, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

        // Signing. Player Settings deliberately carry no team (ProjectIdentity.AppleTeamEnv),
        // so the generated project is the only place it is set -- and it has to be set on
        // BOTH signed targets: UnityFramework is embedded in the app and used to inherit
        // the team from Player Settings, which no longer supplies one.
        // CODE_SIGN_IDENTITY is deliberately NOT pinned: under automatic signing Xcode
        // resolves it per build configuration, and hardcoding "Apple Development"
        // fights the Release/archive path that needs the distribution identity.
        string team = DevelopmentTeam;
        if (team.Length == 0)
        {
            Debug.LogWarning($"[iOSPostBuild] {ProjectIdentity.AppleTeamEnv} is unset, so DEVELOPMENT_TEAM "
                             + "is empty. Simulator builds are unaffected; export it to sign for a device "
                             + "or the App Store.");
        }

        pbx.SetBuildProperty(mainTarget,      "CODE_SIGN_STYLE", "Automatic");
        pbx.SetBuildProperty(frameworkTarget, "CODE_SIGN_STYLE", "Automatic");
        pbx.SetBuildProperty(mainTarget,      "DEVELOPMENT_TEAM", team);
        pbx.SetBuildProperty(frameworkTarget, "DEVELOPMENT_TEAM", team);

        // Xcode doesn't automatically copy UnityRuntime.framework.dSYM into the
        // xcarchive's dSYMs folder, which fails App Store validation; find it
        // wherever Xcode put it and copy it there (dynaface's script, verbatim).
        string copyDsymScript =
            "DSYM=$(find \"${BUILD_DIR}\" \"${OBJROOT}\" \"${CONFIGURATION_BUILD_DIR}\"" +
            " -name 'UnityRuntime.framework.dSYM' -maxdepth 15 2>/dev/null | head -1)\n" +
            "if [ -z \"$DSYM\" ]; then\n" +
            "  DSYM=$(find \"${SRCROOT}\" -name 'UnityRuntime.framework.dSYM' -maxdepth 15 2>/dev/null | head -1)\n" +
            "fi\n" +
            "if [ -n \"$DSYM\" ] && [ -d \"$DSYM\" ]; then\n" +
            "  ditto \"$DSYM\" \"${DWARF_DSYM_FOLDER_PATH}/UnityRuntime.framework.dSYM\"\n" +
            "else\n" +
            "  echo \"warning: [dSYM] UnityRuntime.framework.dSYM not found anywhere\"\n" +
            "fi\n";
        pbx.AddShellScriptBuildPhase(mainTarget, "Copy UnityRuntime dSYM", "/bin/sh", copyDsymScript);

        // Required-reason API declarations (Apple; App Store Connect has refused
        // uploads without them since May 2024 - ITMS-91053). Unity's own
        // UnityFramework/PrivacyInfo.xcprivacy declares only the file-timestamp
        // family, yet `nm -u UnityRuntime` shows the engine imports NSUserDefaults
        // (PlayerPrefs - AppSettings), mach_absolute_time / systemUptime (its clock)
        // and statvfs (disk space), all on Apple's list. This app-level manifest
        // declares those with the approved reasons and states the privacy label's
        // whole truth: no tracking, nothing collected. Reasons: CA92.1 = user
        // defaults readable only by this app; 35F9.1 = elapsed time between events
        // inside the app; E174.1 = checking for enough disk space before writing;
        // C617.1 = timestamps of files inside the app's own container. Re-audit the
        // import list on every Unity upgrade.
        var privacy = new PlistDocument();
        privacy.root.SetBoolean("NSPrivacyTracking", false);
        privacy.root.CreateArray("NSPrivacyTrackingDomains");
        privacy.root.CreateArray("NSPrivacyCollectedDataTypes");
        PlistElementArray apis = privacy.root.CreateArray("NSPrivacyAccessedAPITypes");
        void Declare(string category, params string[] reasons)
        {
            PlistElementDict entry = apis.AddDict();
            entry.SetString("NSPrivacyAccessedAPIType", category);
            PlistElementArray list = entry.CreateArray("NSPrivacyAccessedAPITypeReasons");
            foreach (string reason in reasons)
            {
                list.AddString(reason);
            }
        }

        Declare("NSPrivacyAccessedAPICategoryUserDefaults", "CA92.1");
        Declare("NSPrivacyAccessedAPICategorySystemBootTime", "35F9.1");
        Declare("NSPrivacyAccessedAPICategoryDiskSpace", "E174.1");
        Declare("NSPrivacyAccessedAPICategoryFileTimestamp", "C617.1");
        const string privacyFile = "PrivacyInfo.xcprivacy";
        privacy.WriteToFile(Path.Combine(buildPath, privacyFile));
        string privacyGuid = pbx.FindFileGuidByProjectPath(privacyFile)
            ?? pbx.AddFile(privacyFile, privacyFile);
        pbx.AddFileToBuild(mainTarget, privacyGuid); // a bundle resource of the app target

        pbx.WriteToFile(pbxPath);
    }
}
#endif
