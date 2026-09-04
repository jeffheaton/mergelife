// Patches the built .app's Info.plist (the dynaface-unity / heaton-life-unity
// pattern) so a Mac App Store submission carries the right declarations, replaces
// the privacy manifest, then re-signs ad hoc.
//
// A build-report callback rather than a [PostProcessBuild] attribute so that a
// failed edit can FAIL THE BUILD (BuildFailedException): every edit below is a
// review item, and a build that silently skipped one is indistinguishable from a
// good one until App Review says otherwise. Packaging/package-macos-appstore.sh
// re-checks the same keys before signing.
#if UNITY_STANDALONE_OSX
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// macOS post-build: export compliance, no ATS exception, Game Mode opt-out,
/// the Finder copyright line, the four-reason privacy manifest, and an ad-hoc
/// re-sign so the freshly built app still launches locally.
/// </summary>
public class macOSPostBuild : IPostprocessBuildWithReport
{
    /// <summary>Finder's Get Info line (App Store Connect has its own copyright field).</summary>
    public const string HumanReadableCopyright = "© 2018-2026 Jeff Heaton";

    /// <summary>Runs after the default post-processors.</summary>
    public int callbackOrder => 100;

    /// <summary>Applies every edit; throws <see cref="BuildFailedException"/> when one fails.</summary>
    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneOSX)
        {
            return;
        }

        string buildPath = report.summary.outputPath;

        // PlistBuddy/codesign only exist on a Mac host; a cross-build from another OS
        // must make these plist edits in its own packaging step instead.
        if (Application.platform != RuntimePlatform.OSXEditor)
        {
            Debug.LogWarning("[macOSPostBuild] Non-macOS host - Info.plist not patched " +
                             "(ITSAppUsesNonExemptEncryption / NSAppTransportSecurity / GCSupportsGameMode / " +
                             "NSHumanReadableCopyright) and PrivacyInfo.xcprivacy not replaced.");
            return;
        }

        string plist = Path.Combine(buildPath, "Contents/Info.plist");
        if (!File.Exists(plist))
        {
            throw new BuildFailedException($"[macOSPostBuild] Info.plist not found at {plist}");
        }

        // Pre-answers App Store Connect's export-compliance question.
        PlistBuddy(plist, "Delete :ITSAppUsesNonExemptEncryption", ignoreErrors: true);
        PlistBuddy(plist, "Add :ITSAppUsesNonExemptEncryption bool false");

        // Unity's mac template ships an allow-arbitrary-HTTP ATS exception; the app makes
        // no HTTP requests, so drop it.
        PlistBuddy(plist, "Delete :NSAppTransportSecurity", ignoreErrors: true);

        // macOS game-engine heuristics treat a full-screen Unity player as a game and
        // flash the "Game Mode on" pill. HeatonCA ships as a utility, and a non-game
        // presenting Game Mode support risks App Review rejection - opt out
        // explicitly (dynaface's hard-won lesson).
        PlistBuddy(plist, "Delete :GCSupportsGameMode", ignoreErrors: true);
        PlistBuddy(plist, "Add :GCSupportsGameMode bool false");

        // Finder's Get Info line (App Store Connect has its own copyright field).
        PlistBuddy(plist, "Delete :NSHumanReadableCopyright", ignoreErrors: true);
        // Escaped quotes: Run() wraps the command in quotes for ProcessStartInfo, whose
        // argument parser would otherwise split this value at its own quotes.
        PlistBuddy(plist, "Add :NSHumanReadableCopyright string \\\"" + HumanReadableCopyright + "\\\"");

        // The privacy manifest (2026-08-22 Mac App Store audit). The built app's
        // Contents/Resources/PrivacyInfo.xcprivacy is Unity's base manifest (empty
        // collected-data list) merged with the Insights module's
        // (Tools/XCode/com.unity.modules.insights.xcprivacy, pulled in by the built-in
        // unityanalytics module), which declares SIX collected data types - User ID,
        // product interaction, performance, crash, diagnostic and usage data, all
        // "linked to the user" - the generic manifest for apps running Unity services.
        // This app runs none and collects nothing, and App Store Connect checks the
        // manifest against the privacy label. Neither manifest declares the
        // required-reason APIs the player actually uses: UnityPlayer.dylib
        // and the Mono libraries import NSUserDefaults (PlayerPrefs),
        // mach_absolute_time (the clock), statvfs/statfs (disk space) and
        // stat/fstat/lstat (file timestamps) - undeclared uses App Store Connect has
        // refused since May 2024 (ITMS-91053). Replace it with the truth: the same
        // four categories and reasons iOSPostBuild declares, no tracking, no data.
        string manifest = Path.Combine(buildPath, "Contents/Resources/PrivacyInfo.xcprivacy");
        try
        {
            File.WriteAllText(manifest, PrivacyManifest);
        }
        catch (IOException e)
        {
            throw new BuildFailedException($"[macOSPostBuild] could not write {manifest}: {e.Message}");
        }

        // Editing Info.plist breaks the seal of the ad-hoc signature Unity applied, and
        // Apple-silicon macOS refuses to launch a broken-seal binary - re-sign ad hoc so
        // the fresh build still runs locally. Store packaging replaces this with the real
        // Apple Distribution signature.
        int code = Run("/usr/bin/codesign", $"--force --deep --sign - \"{buildPath}\"");
        if (code != 0)
        {
            throw new BuildFailedException(
                $"[macOSPostBuild] codesign --force --deep --sign - failed ({code}); the app would not launch locally");
        }

        Debug.Log($"[macOSPostBuild] patched {plist}, replaced {manifest}, re-signed ad hoc");
    }

    // Reasons: CA92.1 = user defaults readable only by this app; 35F9.1 = elapsed
    // time between events inside the app; E174.1 = checking for enough disk space
    // before writing; C617.1 = timestamps of files inside the app's own container.
    private const string PrivacyManifest =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
	<key>NSPrivacyTracking</key>
	<false/>
	<key>NSPrivacyTrackingDomains</key>
	<array/>
	<key>NSPrivacyCollectedDataTypes</key>
	<array/>
	<key>NSPrivacyAccessedAPITypes</key>
	<array>
		<dict>
			<key>NSPrivacyAccessedAPIType</key>
			<string>NSPrivacyAccessedAPICategoryUserDefaults</string>
			<key>NSPrivacyAccessedAPITypeReasons</key>
			<array><string>CA92.1</string></array>
		</dict>
		<dict>
			<key>NSPrivacyAccessedAPIType</key>
			<string>NSPrivacyAccessedAPICategorySystemBootTime</string>
			<key>NSPrivacyAccessedAPITypeReasons</key>
			<array><string>35F9.1</string></array>
		</dict>
		<dict>
			<key>NSPrivacyAccessedAPIType</key>
			<string>NSPrivacyAccessedAPICategoryDiskSpace</string>
			<key>NSPrivacyAccessedAPITypeReasons</key>
			<array><string>E174.1</string></array>
		</dict>
		<dict>
			<key>NSPrivacyAccessedAPIType</key>
			<string>NSPrivacyAccessedAPICategoryFileTimestamp</string>
			<key>NSPrivacyAccessedAPITypeReasons</key>
			<array><string>C617.1</string></array>
		</dict>
	</array>
</dict>
</plist>
";

    /// <summary>Runs one PlistBuddy command; a non-zero exit fails the build unless <paramref name="ignoreErrors"/>.</summary>
    private static void PlistBuddy(string plist, string command, bool ignoreErrors = false)
    {
        int code = Run("/usr/libexec/PlistBuddy", $"-c \"{command}\" \"{plist}\"");
        if (code != 0 && !ignoreErrors)
        {
            throw new BuildFailedException(
                $"[macOSPostBuild] PlistBuddy failed ({code}): {command} - " +
                "the app would ship without this Info.plist edit");
        }
    }

    /// <summary>Runs a tool to completion and returns its exit code.</summary>
    private static int Run(string file, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(file, args)
        {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };
        using var p = System.Diagnostics.Process.Start(psi);
        p.WaitForExit();
        return p.ExitCode;
    }
}
#endif
