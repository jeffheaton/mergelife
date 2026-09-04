// Applies HeatonCA's store identity to the ProjectSettings that
// tools/bootstrap.sh seeded from heaton-life-unity, then re-reads the YAML to
// confirm the values that have no public PlayerSettings API. Idempotent by
// construction: every setter runs through Set(), which compares first and
// assigns only on a difference, so a second run reports changed=0 and leaves
// git diff empty.
//
//   Unity -batchmode -quit -projectPath unity/heaton-ca \
//         -executeMethod HeatonCA.Editor.ProjectIdentity.Apply
//   Unity -batchmode -quit -projectPath unity/heaton-ca \
//         -executeMethod HeatonCA.Editor.ProjectIdentity.CreateWebGLBuildProfile
//
// Environment (read only when set; otherwise the checked-in value is kept):
//   HEATONCA_BUILD_NUMBER          macOS and iOS buildNumber (shared App Store
//                                  Connect counter; must exceed the live value)
//   HEATONCA_ANDROID_VERSION_CODE  Android bundleVersionCode (positive integer)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace HeatonCA.Editor
{
    /// <summary>
    /// Store identity for HeatonCA 2.0.0 (App Store record 6469583429 and the
    /// new Google Play listing). Every constant here is what the Phase 0 gate
    /// greps for in ProjectSettings.asset after <see cref="Apply"/> runs.
    /// </summary>
    public static class ProjectIdentity
    {
        /// <summary>PlayerSettings.companyName.</summary>
        public const string CompanyName = "Jeff Heaton";

        /// <summary>PlayerSettings.productName (no space; also the .app / .exe name).</summary>
        public const string ProductName = "HeatonCA";

        /// <summary>Marketing version shared by every platform.</summary>
        public const string BundleVersion = "2.0.0";

        /// <summary>iOS and macOS bundle identifier (the existing App Store record).</summary>
        public const string AppleIdentifier = "com.heatonresearch.heaton-ca";

        /// <summary>Android application id; hyphens are illegal in Android package names.</summary>
        public const string AndroidIdentifier = "com.heatonresearch.heatonca";

        /// <summary>
        /// Android manifest <c>android:appCategory</c>: empty means none. Unity
        /// defaults new projects to "game" and heaton-life-unity shipped with it;
        /// HeatonCA is not a game, and Android's own guidance is to declare a
        /// category only when the app fits one well (none of accessibility, audio,
        /// image, maps, news, productivity, social, or video does). The Play
        /// listing category is set in the Play Console and is unrelated to this.
        /// Assigning an empty string clears <c>useAndroidAppCategory</c>, which is
        /// what the obsolete <c>androidIsGame = false</c> did.
        /// </summary>
        public const string AndroidAppCategory = "";

        /// <summary>Minimum iOS version.</summary>
        public const string IOSTargetVersion = "15.0";

        /// <summary>Minimum macOS version.</summary>
        public const string MacOSTargetVersion = "12.0";

        /// <summary>WebGL template, resolved from Assets/WebGLTemplates/HeatonCA.</summary>
        public const string WebGLTemplate = "PROJECT:HeatonCA";

        /// <summary>WebGL initial heap in MB.</summary>
        public const int WebGLInitialMemoryMB = 32;

        /// <summary>WebGL maximum heap in MB.</summary>
        public const int WebGLMaximumMemoryMB = 2048;

        /// <summary>Mac App Store category bootstrap left in the YAML (no public API).</summary>
        public const string MacAppStoreCategory = "public.app-category.utilities";

        /// <summary>Environment variable holding the shared Apple build number.</summary>
        public const string BuildNumberEnv = "HEATONCA_BUILD_NUMBER";

        /// <summary>Environment variable holding the Android version code.</summary>
        public const string AndroidVersionCodeEnv = "HEATONCA_ANDROID_VERSION_CODE";

        /// <summary>
        /// Environment variable holding the Apple developer team that signs a device
        /// or store build. This is account identity, not app identity: the repository
        /// is public, so the team is never checked in. <see cref="Apply"/> keeps
        /// ProjectSettings' appleDeveloperTeamID empty and iOSPostBuild reads this
        /// variable when it patches the generated Xcode project, which keeps a
        /// personal team out of every tracked file and every future diff. Unset is a
        /// working state: WebGL, Windows, macOS and Android builds sign without a
        /// team, and so does the iOS simulator gate (CODE_SIGNING_ALLOWED=NO).
        /// </summary>
        public const string AppleTeamEnv = "HEATONCA_APPLE_TEAM_ID";

        /// <summary>Where the WebGL build profile lives, next to the copied macOS/iOS/Android ones.</summary>
        public const string WebGLBuildProfilePath = "Assets/Settings/Build Profiles/WebGL.asset";

        /// <summary>
        /// Unity's platform id for WebGL ("Web" in the Build Profiles window), the
        /// same kind of value as m_PlatformId in the copied profiles. It is the string
        /// literal UnityEditor.WebGL.Extensions.dll embeds (6000.5.0f1) and is
        /// cross-checked against GetInstalledPlatformModules() before use.
        /// </summary>
        public const string WebGLPlatformGuid = "84a3bb9e7420477f885e98145999eb20";

        private const string Tag = "[ProjectIdentity]";
        private const string ProjectSettingsFile = "ProjectSettings/ProjectSettings.asset";
        private const string QualitySettingsFile = "ProjectSettings/QualitySettings.asset";
        private const string UnityConnectSettingsFile = "ProjectSettings/UnityConnectSettings.asset";

        // App Store build numbers are dotted integers (CFBundleVersion); a stray
        // git hash or an empty string here would only fail at upload time.
        private static readonly Regex BuildNumberPattern = new Regex(@"^[0-9]+(\.[0-9]+){0,2}$");

        /// <summary>
        /// Applies every identity setting that has a public API, saves, then
        /// re-reads the YAML and logs a MISMATCH line for anything (API-backed or
        /// not) that differs from what bootstrap and this method intend. Never
        /// edits the YAML directly. The final line is
        /// <c>[ProjectIdentity] DONE changed=N mismatches=M</c>.
        /// </summary>
        public static void Apply()
        {
            int changed = 0;

            changed += Set("companyName", PlayerSettings.companyName, CompanyName,
                           v => PlayerSettings.companyName = v);
            changed += Set("productName", PlayerSettings.productName, ProductName,
                           v => PlayerSettings.productName = v);
            changed += Set("bundleVersion", PlayerSettings.bundleVersion, BundleVersion,
                           v => PlayerSettings.bundleVersion = v);

            changed += SetIdentifier(NamedBuildTarget.iOS, AppleIdentifier);
            changed += SetIdentifier(NamedBuildTarget.Standalone, AppleIdentifier);
            changed += SetIdentifier(NamedBuildTarget.Android, AndroidIdentifier);

            // Account identity is never checked in: the signing team reaches the build
            // from AppleTeamEnv when iOSPostBuild patches the Xcode project, so this
            // field stays empty in the tracked YAML no matter who builds.
            changed += Set("iOS.appleDeveloperTeamID", PlayerSettings.iOS.appleDeveloperTeamID, "",
                           v => PlayerSettings.iOS.appleDeveloperTeamID = v);

            changed += ApplyBuildNumbers();
            changed += ApplyAndroidVersionCode();

            // PlayerSettings.Android.androidIsGame is obsolete in 6000.5 (CS0618, "use
            // appCategory"); its getter is appCategory == "game" and its setter only
            // toggles useAndroidAppCategory, so this is the same state without the warning.
            changed += Set("Android.appCategory", PlayerSettings.Android.appCategory, AndroidAppCategory,
                           v => PlayerSettings.Android.appCategory = v);
            changed += Set("iOS.targetOSVersionString", PlayerSettings.iOS.targetOSVersionString, IOSTargetVersion,
                           v => PlayerSettings.iOS.targetOSVersionString = v);
            changed += Set("macOS.targetOSVersion", PlayerSettings.macOS.targetOSVersion, MacOSTargetVersion,
                           v => PlayerSettings.macOS.targetOSVersion = v);

            changed += SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            changed += SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            changed += Set("WebGL.compressionFormat", PlayerSettings.WebGL.compressionFormat, WebGLCompressionFormat.Gzip,
                           v => PlayerSettings.WebGL.compressionFormat = v);
            changed += Set("WebGL.decompressionFallback", PlayerSettings.WebGL.decompressionFallback, true,
                           v => PlayerSettings.WebGL.decompressionFallback = v);
            changed += Set("WebGL.threadsSupport", PlayerSettings.WebGL.threadsSupport, false,
                           v => PlayerSettings.WebGL.threadsSupport = v);
            changed += Set("WebGL.exceptionSupport", PlayerSettings.WebGL.exceptionSupport, WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
                           v => PlayerSettings.WebGL.exceptionSupport = v);
            changed += Set("WebGL.template", PlayerSettings.WebGL.template, WebGLTemplate,
                           v => PlayerSettings.WebGL.template = v);
            changed += Set("WebGL.initialMemorySize", PlayerSettings.WebGL.initialMemorySize, WebGLInitialMemoryMB,
                           v => PlayerSettings.WebGL.initialMemorySize = v);
            changed += Set("WebGL.maximumMemorySize", PlayerSettings.WebGL.maximumMemorySize, WebGLMaximumMemoryMB,
                           v => PlayerSettings.WebGL.maximumMemorySize = v);

            // heaton-life's splash logo GUID does not exist in this project; an
            // empty list is the intended state, not a missing asset.
            var logos = PlayerSettings.SplashScreen.logos;
            if (logos == null || logos.Length != 0)
            {
                PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[0];
                changed++;
                Debug.Log($"{Tag} SplashScreen.logos: {(logos == null ? 0 : logos.Length)} entries -> 0");
            }
            else
            {
                Debug.Log($"{Tag} SplashScreen.logos already empty");
            }

            // Signing is env-driven at build time (CIBuild); nothing is checked in.
            changed += Set("Android.keystoreName", PlayerSettings.Android.keystoreName, "",
                           v => PlayerSettings.Android.keystoreName = v);
            changed += Set("Android.keyaliasName", PlayerSettings.Android.keyaliasName, "",
                           v => PlayerSettings.Android.keyaliasName = v);

            AssetDatabase.SaveAssets();

            int mismatches = CheckYaml();
            Debug.Log($"{Tag} DONE changed={changed} mismatches={mismatches}");
        }

        /// <summary>
        /// Creates <see cref="WebGLBuildProfilePath"/> next to the copied macOS, iOS,
        /// and Android profiles through the 6000.5 factory
        /// <c>BuildProfile.CreateBuildProfile(GUID, string, UnityAction&lt;BuildProfile&gt;)</c>,
        /// which writes <c>Assets/Settings/Build Profiles/{name}.asset</c>. Idempotent:
        /// an existing asset is left alone (the factory would otherwise mint "WebGL 1").
        /// The platform comes from <c>GetInstalledPlatformModules()</c>, matched by
        /// <see cref="WebGLPlatformGuid"/> first and by display name ("Web" / "WebGL")
        /// as a fallback; without the WebGL module this logs a TODO and does nothing.
        /// The new profile keeps the global scene list (overrideGlobalScenes false)
        /// like the copied ones, so CIBuild.WebGL builds the same scene with or
        /// without it.
        /// </summary>
        public static void CreateWebGLBuildProfile()
        {
            if (BuildProfile.GetBuildProfileAtPath(WebGLBuildProfilePath) != null)
            {
                Debug.Log($"{Tag} {WebGLBuildProfilePath} already exists; nothing to do");
                return;
            }

            if (!TryFindWebGLPlatform(out GUID platform, out string displayName))
            {
                Debug.LogWarning($"{Tag} TODO: no installed platform module matches WebGL (guid {WebGLPlatformGuid} "
                                 + "or display name \"Web\"); install the WebGL build support module and re-run, or add "
                                 + $"{WebGLBuildProfilePath} from File > Build Profiles > Add Build Profile > Web. "
                                 + "CIBuild.WebGL builds from the global scene list without it.");
                return;
            }

            var profile = BuildProfile.CreateBuildProfile(platform, "WebGL", OnWebGLBuildProfileReady);
            if (profile == null)
            {
                Debug.LogError($"{Tag} BuildProfile.CreateBuildProfile returned null for \"{displayName}\" ({platform})");
                return;
            }

            AssetDatabase.SaveAssets();
            string path = AssetDatabase.GetAssetPath(profile);
            Debug.Log($"{Tag} created build profile \"{profile.name}\" at {path} for platform \"{displayName}\" "
                      + $"({platform}); overrideGlobalScenes={profile.overrideGlobalScenes}");
            if (path != WebGLBuildProfilePath)
            {
                Debug.LogWarning($"{Tag} expected the profile at {WebGLBuildProfilePath}; move it there and update CIBuild if it should be used");
            }
        }

        /// <summary>
        /// Finds the installed WebGL platform: by <see cref="WebGLPlatformGuid"/>, else
        /// by the Build Profiles display name. False when the module is not installed.
        /// </summary>
        private static bool TryFindWebGLPlatform(out GUID platform, out string displayName)
        {
            var wanted = new GUID(WebGLPlatformGuid);
            var installed = BuildProfile.GetInstalledPlatformModules();

            foreach (var info in installed)
            {
                if (info.platformGuid == wanted)
                {
                    platform = info.platformGuid;
                    displayName = info.displayName;
                    return true;
                }
            }

            foreach (var info in installed)
            {
                if (string.Equals(info.displayName, "Web", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(info.displayName, "WebGL", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"{Tag} WebGL platform matched by display name \"{info.displayName}\" "
                                     + $"({info.platformGuid}), not by guid {WebGLPlatformGuid}; update WebGLPlatformGuid");
                    platform = info.platformGuid;
                    displayName = info.displayName;
                    return true;
                }
            }

            platform = default;
            displayName = null;
            return false;
        }

        /// <summary>Static so it survives a domain reload, as CreateBuildProfile requires of its callback.</summary>
        private static void OnWebGLBuildProfileReady(BuildProfile profile)
        {
            Debug.Log($"{Tag} build profile \"{profile.name}\" ready");
        }

        /// <summary>Compares, assigns on a difference, logs either way; returns 1 when something changed.</summary>
        private static int Set<T>(string label, T current, T desired, Action<T> assign)
        {
            if (EqualityComparer<T>.Default.Equals(current, desired))
            {
                Debug.Log($"{Tag} {label} already {Describe(desired)}");
                return 0;
            }

            assign(desired);
            Debug.Log($"{Tag} {label}: {Describe(current)} -> {Describe(desired)}");
            return 1;
        }

        private static int SetIdentifier(NamedBuildTarget target, string identifier)
        {
            return Set($"applicationIdentifier[{target.TargetName}]",
                       PlayerSettings.GetApplicationIdentifier(target), identifier,
                       v => PlayerSettings.SetApplicationIdentifier(target, v));
        }

        private static int SetScriptingBackend(NamedBuildTarget target, ScriptingImplementation backend)
        {
            return Set($"scriptingBackend[{target.TargetName}]",
                       PlayerSettings.GetScriptingBackend(target), backend,
                       v => PlayerSettings.SetScriptingBackend(target, v));
        }

        /// <summary>
        /// iOS and macOS share one App Store Connect build counter, so both take
        /// the same value from <see cref="BuildNumberEnv"/>. Unset leaves the
        /// checked-in numbers alone (a dev build); an unparsable value is an error
        /// and also leaves them alone.
        /// </summary>
        private static int ApplyBuildNumbers()
        {
            string value = Environment.GetEnvironmentVariable(BuildNumberEnv);
            if (string.IsNullOrEmpty(value))
            {
                Debug.Log($"{Tag} {BuildNumberEnv} unset; buildNumber untouched "
                          + $"(macOS={PlayerSettings.macOS.buildNumber}, iOS={PlayerSettings.iOS.buildNumber})");
                return 0;
            }

            if (!BuildNumberPattern.IsMatch(value))
            {
                Debug.LogError($"{Tag} {BuildNumberEnv}=\"{value}\" is not a dotted integer; buildNumber untouched");
                return 0;
            }

            int changed = 0;
            changed += Set("macOS.buildNumber", PlayerSettings.macOS.buildNumber, value,
                           v => PlayerSettings.macOS.buildNumber = v);
            changed += Set("iOS.buildNumber", PlayerSettings.iOS.buildNumber, value,
                           v => PlayerSettings.iOS.buildNumber = v);
            return changed;
        }

        private static int ApplyAndroidVersionCode()
        {
            string value = Environment.GetEnvironmentVariable(AndroidVersionCodeEnv);
            if (string.IsNullOrEmpty(value))
            {
                Debug.Log($"{Tag} {AndroidVersionCodeEnv} unset; Android.bundleVersionCode untouched "
                          + $"({PlayerSettings.Android.bundleVersionCode})");
                return 0;
            }

            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int code) || code <= 0)
            {
                Debug.LogError($"{Tag} {AndroidVersionCodeEnv}=\"{value}\" is not a positive integer; "
                               + "Android.bundleVersionCode untouched");
                return 0;
            }

            return Set("Android.bundleVersionCode", PlayerSettings.Android.bundleVersionCode, code,
                       v => PlayerSettings.Android.bundleVersionCode = v);
        }

        /// <summary>
        /// Re-reads the saved YAML with System.IO and logs one line per key:
        /// <c>OK</c> or <c>MISMATCH</c>. Covers both the API-backed values above
        /// (as written to disk) and the values that only bootstrap can set
        /// (macAppStoreCategory, cloudProjectId, overrideDefaultApplicationIdentifier,
        /// the WebGL default quality level, Unity Connect / analytics switches).
        /// Returns the mismatch count; never edits a file.
        /// </summary>
        private static int CheckYaml()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            int mismatches = 0;

            string ps = Path.Combine(root, ProjectSettingsFile);
            mismatches += Expect(ps, "companyName", 2, CompanyName);
            mismatches += Expect(ps, "productName", 2, ProductName);
            mismatches += Expect(ps, "bundleVersion", 2, BundleVersion);
            mismatches += ExpectChild(ps, "applicationIdentifier", 2, "Android", AndroidIdentifier);
            mismatches += ExpectChild(ps, "applicationIdentifier", 2, "Standalone", AppleIdentifier);
            mismatches += ExpectChild(ps, "applicationIdentifier", 2, "iPhone", AppleIdentifier);
            mismatches += Expect(ps, "appleDeveloperTeamID", 2, "");
            mismatches += Expect(ps, "overrideDefaultApplicationIdentifier", 2, "1");
            mismatches += Expect(ps, "AndroidIsGame", 2, "0");
            mismatches += Expect(ps, "useAndroidAppCategory", 2, "0");
            mismatches += Expect(ps, "iOSTargetOSVersionString", 2, IOSTargetVersion);
            // 1 = arm64. Unity defaults to 0 (x86_64), which produces a simulator app that
            // cannot install on an Apple Silicon Mac ("Failed to find matching arch"), so the
            // iOS simulator determinism gate could never run. Verified 2026-09-02.
            mismatches += Expect(ps, "iOSSimulatorArchitecture", 2, "1");
            mismatches += Expect(ps, "macOSTargetOSVersion", 2, MacOSTargetVersion);
            mismatches += Expect(ps, "macAppStoreCategory", 2, MacAppStoreCategory);
            mismatches += Expect(ps, "cloudProjectId", 2, "");
            mismatches += Expect(ps, "m_SplashScreenLogos", 2, "[]");
            mismatches += Expect(ps, "AndroidKeystoreName", 2, "");
            mismatches += Expect(ps, "AndroidKeyaliasName", 2, "");
            mismatches += ExpectChild(ps, "scriptingBackend", 2, "Android", "1");
            mismatches += Expect(ps, "webGLCompressionFormat", 2, "1");
            mismatches += Expect(ps, "webGLDecompressionFallback", 2, "1");
            mismatches += Expect(ps, "webGLThreadsSupport", 2, "0");
            mismatches += Expect(ps, "webGLExceptionSupport", 2, "1");
            mismatches += Expect(ps, "webGLTemplate", 2, WebGLTemplate);
            mismatches += Expect(ps, "webGLInitialMemorySize", 2, WebGLInitialMemoryMB.ToString(CultureInfo.InvariantCulture));
            mismatches += Expect(ps, "webGLMaximumMemorySize", 2, WebGLMaximumMemoryMB.ToString(CultureInfo.InvariantCulture));
            mismatches += Expect(ps, "runInBackground", 2, "0");
            mismatches += Expect(ps, "submitAnalytics", 2, "0");

            // Quality level 0 ("Mobile") excludes Standalone; bootstrap moved WebGL to 1 ("PC").
            string qs = Path.Combine(root, QualitySettingsFile);
            mismatches += ExpectChild(qs, "m_PerPlatformDefaultQuality", 2, "WebGL", "1");

            string uc = Path.Combine(root, UnityConnectSettingsFile);
            mismatches += Expect(uc, "m_Enabled", 2, "0");
            mismatches += ExpectChild(uc, "InsightsSettings", 2, "m_EngineDiagnosticsEnabled", "0");

            return mismatches;
        }

        /// <summary>Checks a scalar <c>key: value</c> line at the given indentation.</summary>
        private static int Expect(string file, string key, int indent, string expected)
        {
            string actual = ReadScalar(file, key, indent);
            return Report(Path.GetFileName(file), key, expected, actual);
        }

        /// <summary>Checks <c>child: value</c> nested directly under a <c>parent:</c> block.</summary>
        private static int ExpectChild(string file, string parent, int indent, string child, string expected)
        {
            string actual = ReadChildScalar(file, parent, indent, child);
            return Report(Path.GetFileName(file), parent + "." + child, expected, actual);
        }

        private static int Report(string file, string key, string expected, string actual)
        {
            if (actual == null)
            {
                Debug.LogError($"{Tag} MISMATCH {file} {key}: expected {Describe(expected)}, key not found");
                return 1;
            }

            if (actual != expected)
            {
                Debug.LogError($"{Tag} MISMATCH {file} {key}: expected {Describe(expected)}, found {Describe(actual)}");
                return 1;
            }

            Debug.Log($"{Tag} OK {file} {key} = {Describe(actual)}");
            return 0;
        }

        /// <summary>Value of the first <c>key:</c> line at exactly <paramref name="indent"/> spaces, or null.</summary>
        private static string ReadScalar(string file, string key, int indent)
        {
            if (!File.Exists(file))
            {
                return null;
            }

            string prefix = new string(' ', indent) + key + ":";
            foreach (string line in File.ReadLines(file))
            {
                if (line.StartsWith(prefix, StringComparison.Ordinal) && IsKeyBoundary(line, prefix.Length))
                {
                    return line.Substring(prefix.Length).Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Value of <c>child:</c> within the block that follows <c>parent:</c>;
        /// the block ends at the first line indented no deeper than the parent.
        /// </summary>
        private static string ReadChildScalar(string file, string parent, int indent, string child)
        {
            if (!File.Exists(file))
            {
                return null;
            }

            string parentPrefix = new string(' ', indent) + parent + ":";
            string childPrefix = new string(' ', indent + 2) + child + ":";
            bool inBlock = false;
            foreach (string line in File.ReadLines(file))
            {
                if (!inBlock)
                {
                    inBlock = line.StartsWith(parentPrefix, StringComparison.Ordinal)
                              && IsKeyBoundary(line, parentPrefix.Length);
                    continue;
                }

                if (LeadingSpaces(line) <= indent)
                {
                    return null;
                }

                if (line.StartsWith(childPrefix, StringComparison.Ordinal) && IsKeyBoundary(line, childPrefix.Length))
                {
                    return line.Substring(childPrefix.Length).Trim();
                }
            }

            return null;
        }

        /// <summary>True when the text after <c>key:</c> is a value or nothing (so "m_Enabled:" does not match "m_EnabledX:").</summary>
        private static bool IsKeyBoundary(string line, int end)
        {
            return line.Length == end || line[end] == ' ' || line[end] == '\r';
        }

        private static int LeadingSpaces(string line)
        {
            int n = 0;
            while (n < line.Length && line[n] == ' ')
            {
                n++;
            }

            return n;
        }

        private static string Describe<T>(T value)
        {
            if (value == null)
            {
                return "(null)";
            }

            if (value is string s)
            {
                return "\"" + s + "\"";
            }

            if (value is IFormattable f)
            {
                return f.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }
    }
}
