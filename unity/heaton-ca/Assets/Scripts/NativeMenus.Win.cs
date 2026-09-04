// The Windows half of the NativeMenus seam. Compiled into the Windows
// standalone player only, so a machine without the Windows Build Support
// module never compiles a line of it.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
namespace HeatonCAApp
{
    /// <summary>
    /// Windows menu bar: File &gt; Settings... / Exit and Help &gt; Tutorial /
    /// HeatonCA Manual / About HeatonCA.
    ///
    /// The mechanism — a Win32 menu on Unity's own window, a window-procedure
    /// subclass, and commands deferred out of the message pump — lives in
    /// <see cref="NativeMenuBarWin"/>, which is a big file for one reason: it
    /// is all P/Invoke, and P/Invoke that goes wrong takes the process down
    /// rather than throwing. This file is the seam, and stays three lines so
    /// the platform boundary is obvious from either side.
    /// </summary>
    public static partial class NativeMenus
    {
        /// <summary>
        /// Attach the bar and subclass the player window.
        /// </summary>
        /// <param name="nav">The navigator menu items act through.</param>
        static partial void InstallPlatform(INavigator nav) => NativeMenuBarWin.Install(nav);

        /// <summary>
        /// Run a deferred menu command and keep the bar in step with fullscreen.
        /// </summary>
        static partial void PollPlatform() => NativeMenuBarWin.Poll();

        /// <summary>
        /// Restore Unity's window procedure and drop the bar.
        /// </summary>
        static partial void UninstallPlatform() => NativeMenuBarWin.Uninstall();
    }
}
#endif
