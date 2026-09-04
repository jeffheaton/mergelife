namespace HeatonCAApp
{
    /// <summary>
    /// The desktop native-menu seam. macOS and Windows players carry a real
    /// menu bar (About HeatonCA / Settings... / Quit and Help &gt; Tutorial on
    /// the Mac; File &gt; Settings... / Exit and Help &gt; Tutorial / About on
    /// Windows); every other target has no menu bar at all, and the on-screen
    /// buttons are the only route.
    ///
    /// This file is the whole API the rest of the app sees, and it is free of
    /// platform conditionals on purpose: <c>AppController</c> calls
    /// <see cref="Install"/> at boot, <see cref="Poll"/> every frame, and
    /// <see cref="Uninstall"/> on teardown without knowing which platform it is
    /// on. The bodies arrive in the P2 partial files
    /// <c>NativeMenus.Mac.cs</c> and <c>NativeMenus.Win.cs</c> (WP4.5), each
    /// wrapped in <c>#if UNITY_STANDALONE_OSX / UNITY_STANDALONE_WIN &amp;&amp;
    /// !UNITY_EDITOR</c>. Where no such implementation is compiled in, the
    /// partial methods below have no body and the compiler erases the calls
    /// entirely — so on iOS, Android, WebGL, and in the Editor these three
    /// entry points cost nothing at all.
    ///
    /// Why <see cref="Poll"/> exists: a native menu click arrives inside the
    /// OS message pump, mid-frame, where a handler must not run (it may open a
    /// modal dialog that re-enters the pump). The platform files record the
    /// command and this poll, called from <c>AppController.Update</c>, runs it
    /// in the game loop. Menu actions reach the app through the
    /// <see cref="INavigator"/> passed to <see cref="Install"/>, so the menu
    /// bar navigates exactly like an on-screen button.
    /// </summary>
    public static partial class NativeMenus
    {
        /// <summary>
        /// Build and attach the platform's menu bar, routing its items to
        /// <paramref name="nav"/>. Called once at boot; a no-op on platforms
        /// with no native menu bar.
        /// </summary>
        /// <param name="nav">
        /// The navigator menu items act through (About, Settings, Tutorial).
        /// </param>
        public static void Install(INavigator nav) => InstallPlatform(nav);

        /// <summary>
        /// Run any menu command recorded since the previous frame, and re-attach
        /// or detach the bar when the player enters or leaves fullscreen. Called
        /// every frame from <c>AppController.Update</c>; a no-op where no menu
        /// bar is installed.
        /// </summary>
        public static void Poll() => PollPlatform();

        /// <summary>
        /// Detach the menu bar and release every native handle it holds, before
        /// the scripting runtime shuts down. Called from the controller's
        /// teardown; a no-op where no menu bar is installed.
        /// </summary>
        public static void Uninstall() => UninstallPlatform();

        /// <summary>
        /// Platform half of <see cref="Install"/>. Implemented by
        /// <c>NativeMenus.Mac.cs</c> / <c>NativeMenus.Win.cs</c>; absent
        /// elsewhere, which erases the call.
        /// </summary>
        /// <param name="nav">The navigator menu items act through.</param>
        static partial void InstallPlatform(INavigator nav);

        /// <summary>
        /// Platform half of <see cref="Poll"/>. Implemented by
        /// <c>NativeMenus.Mac.cs</c> / <c>NativeMenus.Win.cs</c>; absent
        /// elsewhere, which erases the call.
        /// </summary>
        static partial void PollPlatform();

        /// <summary>
        /// Platform half of <see cref="Uninstall"/>. Implemented by
        /// <c>NativeMenus.Mac.cs</c> / <c>NativeMenus.Win.cs</c>; absent
        /// elsewhere, which erases the call.
        /// </summary>
        static partial void UninstallPlatform();
    }
}
