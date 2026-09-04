// Headless build entry points (the dynaface-unity / heaton-life-unity pattern):
//   Unity -batchmode -quit -projectPath . -buildTarget OSXUniversal -executeMethod CIBuild.MacOS
//   Unity -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod CIBuild.Windows
//   Unity -batchmode -quit -projectPath . -buildTarget iOS -executeMethod CIBuild.IOS
//   Unity -batchmode -quit -projectPath . -buildTarget iOS -executeMethod CIBuild.IOSSimulator
//   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod CIBuild.Android
//   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod CIBuild.AndroidPlayStore
//   Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod CIBuild.WebGL
// Builds the scenes enabled in Build Settings with the player settings checked into
// the project. macOS: macOSPostBuild's plist patch + ad-hoc re-sign run as normal
// post-process steps, then Packaging/package-macos-appstore.sh signs and packages.
// iOS: produces the Xcode project (iOSPostBuild patches it); archive/submit from
// Xcode. Android: .apk for emulator/device testing, .aab for Google Play. WebGL:
// the gzip-compressed player for Packaging/web/publish-webgl.sh. Windows: built on
// the Windows VM and shipped as a zip.
//
// Environment (every variable is optional for a dev build):
//   HEATONCA_BUILD_NUMBER          iOS and macOS buildNumber (ONE App Store Connect
//                                  counter shared by both platforms; must exceed the
//                                  highest build ever uploaded). Applied to both
//                                  targets whenever set, whatever is being built, so
//                                  the About page's build stamp agrees everywhere.
//   HEATONCA_ANDROID_VERSION_CODE  Android bundleVersionCode (its own counter).
//   HEATONCA_STORE=1               store mode for MacOS/IOS: the build FAILS when
//                                  HEATONCA_BUILD_NUMBER is unset instead of logging
//                                  DEV BUILD NUMBER. AndroidPlayStore is always store
//                                  mode (version code and release signing required).
//   HEATONCA_ANDROID_KEYSTORE      release signing (all four together):
//   HEATONCA_ANDROID_KEYSTORE_PASS   export HEATONCA_ANDROID_KEYSTORE=$HOME/secrets/heatonca-upload.keystore
//   HEATONCA_ANDROID_KEYALIAS        export HEATONCA_ANDROID_KEYSTORE_PASS=...
//   HEATONCA_ANDROID_KEYALIAS_PASS   export HEATONCA_ANDROID_KEYALIAS=upload
//                                    export HEATONCA_ANDROID_KEYALIAS_PASS=...
//
// Global namespace on purpose: tools/unity-gate.sh and the CI workflow call
// -executeMethod CIBuild.<Method>. No #if here: every PlayerSettings knob this file
// touches (iOS SDK, WebGL, Android signing) lives in UnityEditor.CoreModule, so the
// file compiles on a machine that lacks a platform module.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Headless player builds for every HeatonCA target. Each public method is an
/// <c>-executeMethod</c> entry point; all of them exit the editor with code 1 on
/// any <see cref="BuildResult"/> other than <see cref="BuildResult.Succeeded"/>
/// after printing the build summary.
/// </summary>
public static class CIBuild
{
    /// <summary>Shared iOS + macOS build number (App Store Connect counter).</summary>
    public const string BuildNumberEnv = "HEATONCA_BUILD_NUMBER";

    /// <summary>Android bundleVersionCode.</summary>
    public const string AndroidVersionCodeEnv = "HEATONCA_ANDROID_VERSION_CODE";

    /// <summary>Set to 1 to make the Apple builds fail without a build number.</summary>
    public const string StoreEnv = "HEATONCA_STORE";

    /// <summary>Path of the Android upload keystore.</summary>
    public const string AndroidKeystoreEnv = "HEATONCA_ANDROID_KEYSTORE";

    /// <summary>Password of the Android upload keystore.</summary>
    public const string AndroidKeystorePassEnv = "HEATONCA_ANDROID_KEYSTORE_PASS";

    /// <summary>Alias of the upload key inside the keystore.</summary>
    public const string AndroidKeyaliasEnv = "HEATONCA_ANDROID_KEYALIAS";

    /// <summary>Password of the upload key alias.</summary>
    public const string AndroidKeyaliasPassEnv = "HEATONCA_ANDROID_KEYALIAS_PASS";

    /// <summary>macOS player bundle, relative to the project root.</summary>
    public const string MacOSOutput = "build/macos/HeatonCA.app";

    /// <summary>Windows x64 player executable, relative to the project root.</summary>
    public const string WindowsOutput = "build/windows-x64/HeatonCA.exe";

    /// <summary>iOS device Xcode project folder.</summary>
    public const string IOSOutput = "build/ios";

    /// <summary>iOS Simulator Xcode project folder.</summary>
    public const string IOSSimulatorOutput = "build/ios-sim";

    /// <summary>Android package for emulator/device testing.</summary>
    public const string AndroidApkOutput = "build/android/HeatonCA.apk";

    /// <summary>Android App Bundle for Google Play.</summary>
    public const string AndroidAabOutput = "build/android/HeatonCA.aab";

    /// <summary>WebGL player folder.</summary>
    public const string WebGLOutput = "build/webgl";

    /// <summary>WebGL template, resolved from Assets/WebGLTemplates/HeatonCA.</summary>
    public const string WebGLTemplate = "PROJECT:HeatonCA";

    // App Store build numbers are dotted integers (CFBundleVersion); a stray git
    // hash or an empty string here would only fail at upload time.
    private static readonly Regex BuildNumberPattern = new Regex(@"^[0-9]+(\.[0-9]+){0,2}$");

    /// <summary>macOS player (Mono) at <see cref="MacOSOutput"/>; store mode via <see cref="StoreEnv"/>.</summary>
    public static void MacOS()
    {
        ApplyAppleBuildNumber("MacOS", IsStoreMode());
        Finish(Build(BuildTarget.StandaloneOSX, MacOSOutput));
    }

    /// <summary>
    /// Windows x64 player (Mono) at <see cref="WindowsOutput"/>. Windows has no store
    /// build counter, so the build number is only stamped when the env var is set and
    /// its absence never fails the build.
    /// </summary>
    public static void Windows()
    {
        ApplyAppleBuildNumber("Windows", storeMode: false);
        Finish(Build(BuildTarget.StandaloneWindows64, WindowsOutput));
    }

    /// <summary>iOS device Xcode project at <see cref="IOSOutput"/>; store mode via <see cref="StoreEnv"/>.</summary>
    public static void IOS()
    {
        ApplyAppleBuildNumber("IOS", IsStoreMode());

        // A crashed IOSSimulator run could have left SimulatorSDK persisted in
        // ProjectSettings (batchmode -quit saves PlayerSettings changes); a device
        // build must never inherit it.
        if (PlayerSettings.iOS.sdkVersion != iOSSdkVersion.DeviceSDK)
        {
            Log($"CIBuild IOS: iOS.sdkVersion was {PlayerSettings.iOS.sdkVersion}; resetting to DeviceSDK");
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        }

        Finish(Build(BuildTarget.iOS, IOSOutput));
    }

    /// <summary>
    /// iOS Simulator Xcode project at <see cref="IOSSimulatorOutput"/> for
    /// tools/ios-sim-selfcheck.sh. Switches the SDK to the Simulator for the build
    /// and restores <see cref="iOSSdkVersion.DeviceSDK"/> in a finally block so the
    /// checked-in setting survives even a failed build. Never a store build.
    /// </summary>
    public static void IOSSimulator()
    {
        ApplyAppleBuildNumber("IOSSimulator", storeMode: false);

        bool ok;
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
        Log("CIBuild IOSSimulator: iOS.sdkVersion = SimulatorSDK");
        try
        {
            ok = Build(BuildTarget.iOS, IOSSimulatorOutput);
        }
        finally
        {
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            AssetDatabase.SaveAssets();
            Log("CIBuild IOSSimulator: iOS.sdkVersion restored to DeviceSDK");
        }

        Finish(ok);
    }

    /// <summary>
    /// Android .apk at <see cref="AndroidApkOutput"/> for emulator/device testing.
    /// Debug-signed unless the four signing variables are set; the version code is
    /// applied when set and only required under <see cref="StoreEnv"/>.
    /// </summary>
    public static void Android()
    {
        BuildAndroid("Android", appBundle: false, AndroidApkOutput);
    }

    /// <summary>
    /// Google Play submission artifact at <see cref="AndroidAabOutput"/>. Unlike
    /// dynaface there is no giant asset pack to split out (the whole app is a few
    /// MB), so the .aab ships as a single base module. Always store mode: Play
    /// rejects debug-signed bundles and re-used version codes, so the build fails
    /// early when the signing variables or the version code are unset.
    /// </summary>
    public static void AndroidPlayStore()
    {
        BuildAndroid("AndroidPlayStore", appBundle: true, AndroidAabOutput);
    }

    /// <summary>
    /// WebGL player at <see cref="WebGLOutput"/>. Re-applies the WebGL knobs
    /// ProjectIdentity set (gzip with the decompression fallback so any static host
    /// serves it, no threads, explicitly-thrown exceptions only, the HeatonCA
    /// template) before building, so a stray editor session cannot ship a build
    /// that needs Content-Encoding headers or a footer the template removed.
    /// </summary>
    public static void WebGL()
    {
        ApplyAppleBuildNumber("WebGL", storeMode: false);

        PlayerSettings.WebGL.compressionFormat     = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.threadsSupport        = false;
        PlayerSettings.WebGL.exceptionSupport      = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.template              = WebGLTemplate;
        Log("CIBuild WebGL: compressionFormat=Gzip decompressionFallback=true threadsSupport=false "
            + $"exceptionSupport=ExplicitlyThrownExceptionsOnly template={WebGLTemplate}");

        Finish(Build(BuildTarget.WebGL, WebGLOutput));
    }

    // Both Android flavors set every knob they depend on, so neither inherits stray
    // state from the other (batchmode -quit can persist PlayerSettings changes).
    private static void BuildAndroid(string entry, bool appBundle, string locationPathName)
    {
        EditorUserBuildSettings.buildAppBundle        = appBundle;
        PlayerSettings.Android.splitApplicationBinary = false;

        bool storeMode = appBundle || IsStoreMode();
        ApplyAndroidVersionCode(entry, storeMode);
        ApplyAndroidSigning(entry, appBundle);

        Finish(Build(BuildTarget.Android, locationPathName));
    }

    /// <summary>
    /// Release signing from the environment. All four variables set: sign with the
    /// upload key. None set: debug-sign (fine for the emulator; fatal for the .aab).
    /// Some set: fail, because a half-configured keystore would only fail inside
    /// Gradle with a far less useful message.
    /// </summary>
    private static void ApplyAndroidSigning(string entry, bool appBundle)
    {
        string keystore    = Environment.GetEnvironmentVariable(AndroidKeystoreEnv);
        string storePass   = Environment.GetEnvironmentVariable(AndroidKeystorePassEnv);
        string alias       = Environment.GetEnvironmentVariable(AndroidKeyaliasEnv);
        string aliasPass   = Environment.GetEnvironmentVariable(AndroidKeyaliasPassEnv);

        var missing = new List<string>();
        if (string.IsNullOrEmpty(keystore))
        {
            missing.Add(AndroidKeystoreEnv);
        }

        if (string.IsNullOrEmpty(storePass))
        {
            missing.Add(AndroidKeystorePassEnv);
        }

        if (string.IsNullOrEmpty(alias))
        {
            missing.Add(AndroidKeyaliasEnv);
        }

        if (string.IsNullOrEmpty(aliasPass))
        {
            missing.Add(AndroidKeyaliasPassEnv);
        }

        if (missing.Count == 4)
        {
            if (appBundle)
            {
                Fail($"CIBuild {entry}: release signing is required for a Google Play .aab but "
                     + $"{AndroidKeystoreEnv}, {AndroidKeystorePassEnv}, {AndroidKeyaliasEnv} and "
                     + $"{AndroidKeyaliasPassEnv} are unset. Play rejects debug-signed bundles; export "
                     + "the four variables (see the header of Assets/Editor/CIBuild.cs) and rerun, or "
                     + "build CIBuild.Android for a debug-signed .apk.");
            }

            PlayerSettings.Android.useCustomKeystore = false;
            Log($"CIBuild {entry}: {AndroidKeystoreEnv} unset - building a DEBUG-SIGNED package "
                + "(local/emulator testing only).");
            return;
        }

        if (missing.Count > 0)
        {
            Fail($"CIBuild {entry}: Android signing is half configured; unset: {string.Join(", ", missing)}. "
                 + "Set all four HEATONCA_ANDROID_* variables for a release build, or none for a debug-signed one.");
        }

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName      = keystore;
        PlayerSettings.Android.keystorePass      = storePass;
        PlayerSettings.Android.keyaliasName      = alias;
        PlayerSettings.Android.keyaliasPass      = aliasPass;
        Log($"CIBuild {entry}: release signing with keystore {keystore} alias {alias}");
    }

    /// <summary>
    /// iOS and macOS share one App Store Connect build counter, so both take the
    /// same value from <see cref="BuildNumberEnv"/>. Unset: a dev build keeps the
    /// checked-in numbers and logs DEV BUILD NUMBER; a store build fails. An
    /// unparsable value always fails (it would otherwise surface only at upload).
    /// </summary>
    private static void ApplyAppleBuildNumber(string entry, bool storeMode)
    {
        string value = Environment.GetEnvironmentVariable(BuildNumberEnv);
        if (string.IsNullOrEmpty(value))
        {
            if (storeMode)
            {
                Fail($"CIBuild {entry}: {StoreEnv}=1 but {BuildNumberEnv} is unset. Read the highest build "
                     + "number on App Store Connect (iOS and macOS share it), export a higher one, and rerun.");
            }

            Log($"CIBuild {entry}: DEV BUILD NUMBER - {BuildNumberEnv} unset; keeping "
                + $"macOS.buildNumber={PlayerSettings.macOS.buildNumber} iOS.buildNumber={PlayerSettings.iOS.buildNumber}");
            return;
        }

        if (!BuildNumberPattern.IsMatch(value))
        {
            Fail($"CIBuild {entry}: {BuildNumberEnv}=\"{value}\" is not a dotted integer (CFBundleVersion).");
        }

        PlayerSettings.macOS.buildNumber = value;
        PlayerSettings.iOS.buildNumber   = value;
        Log($"CIBuild {entry}: macOS.buildNumber = iOS.buildNumber = {value} (from {BuildNumberEnv})");
    }

    /// <summary>
    /// Android's own counter from <see cref="AndroidVersionCodeEnv"/>: applied when
    /// set, DEV BUILD NUMBER when unset for a dev build, fatal when unset for a
    /// store build or when not a positive integer.
    /// </summary>
    private static void ApplyAndroidVersionCode(string entry, bool storeMode)
    {
        string value = Environment.GetEnvironmentVariable(AndroidVersionCodeEnv);
        if (string.IsNullOrEmpty(value))
        {
            if (storeMode)
            {
                Fail($"CIBuild {entry}: {AndroidVersionCodeEnv} is unset; a Google Play upload needs a version "
                     + "code higher than every previous upload. Export it and rerun.");
            }

            Log($"CIBuild {entry}: DEV BUILD NUMBER - {AndroidVersionCodeEnv} unset; keeping "
                + $"Android.bundleVersionCode={PlayerSettings.Android.bundleVersionCode}");
            return;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int code) || code <= 0)
        {
            Fail($"CIBuild {entry}: {AndroidVersionCodeEnv}=\"{value}\" is not a positive integer.");
        }

        PlayerSettings.Android.bundleVersionCode = code;
        Log($"CIBuild {entry}: Android.bundleVersionCode = {code} (from {AndroidVersionCodeEnv})");
    }

    /// <summary>True when <see cref="StoreEnv"/> is 1 or true (case-insensitive).</summary>
    private static bool IsStoreMode()
    {
        string value = Environment.GetEnvironmentVariable(StoreEnv);
        return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the enabled Build Settings scenes for <paramref name="target"/> and
    /// returns true on <see cref="BuildResult.Succeeded"/>; any other result prints
    /// the summary in the house format and returns false. Callers decide when to
    /// exit so that a finally block (IOSSimulator) can run first.
    /// </summary>
    private static bool Build(BuildTarget target, string locationPathName)
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled)
                                                    .Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Fail($"CIBuild {target}: no enabled scenes in EditorBuildSettings; run "
                 + "-executeMethod HeatonCA.Editor.SceneBootstrap.Run first.");
        }

        Log($"CIBuild {target}: building {string.Join(", ", scenes)} -> {locationPathName}");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = locationPathName,
            target           = target,
            options          = BuildOptions.None,
        });

        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            Error($"CIBuild {target}: {summary.result} "
                  + $"({summary.totalErrors} errors, {summary.totalWarnings} warnings)");
            return false;
        }

        Log($"CIBuild {target}: Succeeded -> {summary.outputPath} "
            + $"({summary.totalSize} bytes, {summary.totalTime.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture)} s, "
            + $"{summary.totalWarnings} warnings)");
        return true;
    }

    /// <summary>Exits the editor with code 1 when the build did not succeed.</summary>
    private static void Finish(bool succeeded)
    {
        if (!succeeded)
        {
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Logs to stdout and the editor log (the -logFile the gate reads).</summary>
    private static void Log(string message)
    {
        Console.WriteLine(message);
        Debug.Log(message);
    }

    /// <summary>Logs to stderr and the editor log without exiting.</summary>
    private static void Error(string message)
    {
        Console.Error.WriteLine(message);
        Debug.LogError(message);
    }

    /// <summary>
    /// Reports a fatal configuration problem and exits the editor with code 1. The
    /// throw after Exit only matters when the method is invoked from an interactive
    /// editor, where Exit is deferred; it keeps the build from starting either way.
    /// </summary>
    private static void Fail(string message)
    {
        Error(message);
        EditorApplication.Exit(1);
        throw new BuildFailedException(message);
    }
}
