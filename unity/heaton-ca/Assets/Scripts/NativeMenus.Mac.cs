// The macOS half of the NativeMenus seam. Compiled into the Mac standalone
// player only: in the Editor (and on iOS, Android and WebGL) the partial
// methods below have no body at all and the calls in AppController vanish.
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// macOS menu bar: About HeatonCA / Settings... (Cmd-,) in the app menu and
    /// HeatonCA Manual / Tutorial in a Help menu, built by the native bundle
    /// <c>Assets/Plugins/macOS/HeatonCAMac.bundle</c> (source and rebuild script
    /// in <c>Source~</c> beside it).
    ///
    /// Two rules shape this file, both paid for elsewhere first:
    ///
    /// 1. The bundle calls back through a registered function pointer, never
    ///    <c>UnitySendMessage</c> — a Standalone player does not export that
    ///    symbol, so a bundle that calls it fails to load at all. The delegate
    ///    handed to the plugin is a static method carrying
    ///    <c>[AOT.MonoPInvokeCallback]</c> and is rooted in a static field for
    ///    the life of the process: native code holds the raw pointer, and a
    ///    collected delegate is a native crash rather than an exception.
    /// 2. The callback fires inside AppKit's event dispatch, mid-frame. It only
    ///    records which item was clicked; <see cref="PollPlatform"/>, called
    ///    from <c>AppController.Update</c>, runs the command in the game loop
    ///    where navigation and dialogs are legal.
    ///
    /// The plugin is a build artifact that can legitimately be missing (a
    /// checkout where <c>Source~/build.sh</c> has not been run), so the first
    /// native call is guarded: if the bundle will not load, the app logs a
    /// warning and runs with no menu bar rather than throwing out of
    /// <c>Start</c>.
    /// </summary>
    public static partial class NativeMenus
    {
        /// <summary>
        /// The plugin's file name without extension, which is how Mono resolves
        /// <c>HeatonCAMac.bundle</c> inside <c>Contents/PlugIns</c>.
        /// </summary>
        private const string Library = "HeatonCAMac";

        /// <summary>
        /// Menu word with no on-screen counterpart, so it has no place in
        /// <c>AppStrings</c>: macOS spells the exit command "Quit".
        /// </summary>
        private const string QuitWord = "Quit";

        /// <summary>
        /// Suffix marking a menu item that opens a screen rather than acting at
        /// once. Three ASCII periods, not U+2026: every string this app puts on
        /// screen is ASCII apart from the three Greek letters in the rule
        /// decoder, and an ellipsis crossing the P/Invoke boundary would be the
        /// only non-ASCII byte in the whole native seam.
        /// </summary>
        private const string OpensScreenSuffix = "...";

        // Command names, matched by the switch in OnMenuCommand. They are the
        // literals HeatonCAMac.mm passes to the callback: the two halves of
        // this pair live in different languages and must be edited together.
        private const string CommandAbout = "About";
        private const string CommandSettings = "Settings";
        private const string CommandQuit = "Quit";
        private const string CommandManual = "Manual";
        private const string CommandTutorial = "Tutorial";

        /// <summary>
        /// Shape of the plugin's callback. Cdecl because the bundle declares a
        /// plain C function pointer; the two <c>IntPtr</c>s are UTF-8 C strings
        /// (command name, and an argument the current items never use).
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MenuCallback(IntPtr methodPtr, IntPtr argPtr);

        [DllImport(Library)]
        private static extern void HeatonCA_RegisterCallback(MenuCallback callback);

        [DllImport(Library)]
        private static extern void HeatonCA_InstallMenuBar(
            string aboutTitle,
            string settingsTitle,
            string quitTitle,
            string tutorialTitle,
            string manualTitle);

        [DllImport(Library)]
        private static extern void HeatonCA_UninstallMenuBar();

        [DllImport(Library)]
        private static extern void HeatonCA_RevealInFinder(string path);

        /// <summary>
        /// Rooted so the collector never moves or frees it: the plugin keeps the
        /// marshaled function pointer until <see cref="UninstallPlatform"/>
        /// clears it, and nothing else references the delegate instance.
        /// </summary>
        private static MenuCallback _callback;

        /// <summary>Where menu items act; null until installed.</summary>
        private static INavigator _nav;

        /// <summary>
        /// The command recorded by the last click, or null. Written by
        /// <see cref="OnMenuCommand"/> and consumed by
        /// <see cref="PollPlatform"/> — both on the main thread (AppKit
        /// dispatches menu actions there), so this is a deferral to a safe
        /// point in the frame, not a thread handoff.
        /// </summary>
        private static string _pending;

        /// <summary>
        /// False once a native call has failed to bind, which stops every later
        /// call: a missing or unloadable bundle should cost one warning, not one
        /// exception per frame.
        /// </summary>
        private static bool _nativeUsable = true;

        /// <summary>
        /// Build the menu bar and point it at <paramref name="nav"/>. Idempotent:
        /// a second call while installed does nothing.
        /// </summary>
        /// <param name="nav">The navigator menu items act through.</param>
        static partial void InstallPlatform(INavigator nav)
        {
            if (nav == null || _nav != null || !_nativeUsable)
            {
                return;
            }
            _nav = nav;
            _callback = OnMenuCommand;
            try
            {
                HeatonCA_RegisterCallback(_callback);
                HeatonCA_InstallMenuBar(
                    AppStrings.AboutTitle + " " + AppStrings.AppName,
                    AppStrings.SettingsTitle + OpensScreenSuffix,
                    QuitWord + " " + AppStrings.AppName,
                    AppStrings.AboutTutorial,
                    AppStrings.AppName + " " + AppStrings.AboutManual);
            }
            catch (Exception exception)
            {
                // DllNotFoundException (the bundle is absent or the wrong
                // architecture) or EntryPointNotFoundException (a stale bundle
                // built before this file's exports). Neither is worth failing a
                // launch over: every menu command has an on-screen route too.
                _nativeUsable = false;
                _nav = null;
                // _callback is deliberately NOT cleared: if the registration
                // call is the one that succeeded, the plugin already holds the
                // pointer, and unrooting the delegate would hand the collector
                // a live native reference.
                Debug.LogWarning("[HeatonCA] native menu bar unavailable: " + exception.Message);
            }
        }

        /// <summary>
        /// Run the command recorded since the previous frame, if any. macOS
        /// hides and shows the menu bar itself in fullscreen, so unlike Windows
        /// there is nothing to re-attach here.
        /// </summary>
        static partial void PollPlatform()
        {
            string command = _pending;
            if (command == null || _nav == null)
            {
                return;
            }
            _pending = null;
            switch (command)
            {
                case CommandAbout:
                    _nav.ShowAbout();
                    break;
                case CommandSettings:
                    _nav.ShowSettings();
                    break;
                case CommandQuit:
                    // Quit through Unity, so the normal exit path runs:
                    // OnApplicationQuit persists settings and stops the search.
                    Application.Quit();
                    break;
                case CommandManual:
                    _nav.OpenUrl(AppLinks.ManualUrl);
                    break;
                case CommandTutorial:
                    _nav.OpenUrl(AppLinks.TutorialUrl);
                    break;
            }
        }

        /// <summary>
        /// Take the items back out of the player's menu bar and — the part that
        /// matters — clear the plugin's copy of the callback pointer before the
        /// scripting runtime shuts down. A click that reached a dead delegate
        /// would take the process down with it.
        /// </summary>
        static partial void UninstallPlatform()
        {
            if (_nav == null)
            {
                return; // never installed, or already uninstalled
            }
            _nav = null;
            _pending = null;
            if (!_nativeUsable)
            {
                return;
            }
            try
            {
                HeatonCA_UninstallMenuBar();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[HeatonCA] native menu teardown failed: " + exception.Message);
            }
            // _callback stays rooted deliberately: the plugin dropped its
            // pointer above, but nothing is gained by making the delegate
            // collectable a few milliseconds before the process exits, and a
            // race there would be fatal rather than noisy.
        }

        /// <summary>
        /// Select <paramref name="path"/> in a Finder window — the sandbox-safe
        /// way to show a just-saved snapshot. Offered here because the Mac App
        /// Store build cannot spawn <c>/usr/bin/open</c>: launching a helper
        /// process is denied inside the sandbox, while the plugin's
        /// <c>NSWorkspace</c> call is a request Finder fulfills from its own
        /// process. <c>PngExporter.Reveal</c> calls this first and falls back to
        /// <c>open -R</c> only when the bundle did not load, which cannot happen
        /// in a store build.
        /// </summary>
        /// <param name="path">Absolute path of an existing file.</param>
        /// <returns>True when the request reached Finder.</returns>
        internal static bool RevealInFinder(string path)
        {
            if (string.IsNullOrEmpty(path) || !_nativeUsable)
            {
                return false;
            }
            try
            {
                HeatonCA_RevealInFinder(path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[HeatonCA] reveal failed: " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// The plugin's entry back into managed code. Static and attributed so
        /// the AOT compilers emit a real, stable native entry point — this is
        /// what makes it safe for the bundle to call through the registered raw
        /// pointer at any time. It must never let an exception escape into
        /// AppKit, and it must not act: it records the command for
        /// <see cref="PollPlatform"/> and returns.
        /// </summary>
        /// <param name="methodPtr">UTF-8 command name.</param>
        /// <param name="argPtr">UTF-8 argument; unused by the current items.</param>
        [AOT.MonoPInvokeCallback(typeof(MenuCallback))]
        private static void OnMenuCommand(IntPtr methodPtr, IntPtr argPtr)
        {
            try
            {
                _pending = Marshal.PtrToStringUTF8(methodPtr);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[HeatonCA] menu callback failed: " + exception.Message);
            }
        }
    }
}
#endif
