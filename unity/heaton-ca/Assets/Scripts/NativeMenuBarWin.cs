// The Win32 menu bar for the Windows standalone player. Compiled into that
// player only; the Editor, macOS, mobile and WebGL never see a line of it,
// which is what lets the project build on a machine with no Windows module.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// Native Windows menu bar — the Windows counterpart of
    /// <c>HeatonCAMac.bundle</c>, ported from heaton-life's
    /// <c>NativeMenuBarWin</c>. Same commands as the Mac bar, laid out the way
    /// Windows lays them out: no app menu, so Exit ends File and About ends
    /// Help, and every item carries a mnemonic (<c>&amp;File</c> gives Alt+F).
    ///
    /// <code>
    ///   File                    Help
    ///    +- Settings...          +- Tutorial
    ///    +- ---------            +- HeatonCA Manual
    ///    +- Exit                 +- ---------
    ///                            +- About HeatonCA
    /// </code>
    ///
    /// Unlike macOS this needs no compiled plugin: the Win32 menu API lives in
    /// user32.dll and is P/Invoked directly. Two mechanisms are involved.
    /// <c>CreateMenu</c>/<c>AppendMenuW</c>/<c>SetMenu</c> attach the bar to
    /// Unity's window; <c>SetWindowLongPtr(GWLP_WNDPROC)</c> subclasses that
    /// window so menu clicks (<c>WM_COMMAND</c>) reach managed code, and every
    /// other message is forwarded to Unity's own window procedure through
    /// <c>CallWindowProc</c>.
    ///
    /// Three rules keep it from crashing, all inherited from the port:
    /// <list type="bullet">
    /// <item><description><c>WM_COMMAND</c> arrives inside Unity's message
    /// pump, mid-frame. Handlers must not run there — showing a screen or a
    /// modal from inside the pump re-enters it — so the window procedure only
    /// records the command id and <see cref="Poll"/>, called from
    /// <c>AppController.Update</c>, executes it in the game loop.</description></item>
    /// <item><description>The window-procedure delegate is pinned in a
    /// <see cref="GCHandle"/> for as long as the subclass is installed. The OS
    /// calls through its raw function pointer at any time; a collected delegate
    /// is a native crash. <see cref="Uninstall"/> restores Unity's original
    /// procedure first and only then frees the handle, so afterwards no native
    /// code holds a managed pointer.</description></item>
    /// <item><description>A menu bar only renders on a windowed (titled)
    /// window. <see cref="Poll"/> detaches it while the player is fullscreen
    /// and re-attaches it on the way back.</description></item>
    /// </list>
    /// </summary>
    internal static class NativeMenuBarWin
    {
        // Command ids carried in WM_COMMAND's low word. Unity's window defines
        // no menu items of its own, so any private range works; ids outside the
        // range below are forwarded to Unity untouched.
        private const int IdFileSettings = 0x1001;
        private const int IdFileExit = 0x1002;
        private const int IdHelpTutorial = 0x1003;
        private const int IdHelpManual = 0x1004;
        private const int IdHelpAbout = 0x1005;

        /// <summary>
        /// Top of the dispatch range — keep it on the HIGHEST id above, or a new
        /// item's clicks fall through to Unity and it silently does nothing.
        /// </summary>
        private const int IdLastCommand = IdHelpAbout;

        /// <summary>No pending command. Zero is not a valid WM_COMMAND id here.</summary>
        private const int IdNone = 0;

        private const uint MF_STRING = 0x0000;
        private const uint MF_POPUP = 0x0010;
        private const uint MF_SEPARATOR = 0x0800;
        private const uint WM_COMMAND = 0x0111;
        private const int GWLP_WNDPROC = -4;

        /// <summary>Unity's player window class, matched when the window must be found by hand.</summary>
        private const string PlayerWindowClass = "UnityWndClass";

        /// <summary>
        /// Suffix marking an item that opens a screen rather than acting at
        /// once; ASCII periods for the same reason as the macOS side.
        /// </summary>
        private const string OpensScreenSuffix = "...";

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr CreateMenu();

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool SetMenu(IntPtr hWnd, IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool DrawMenuBar(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(
            uint threadId, EnumWindowsDelegate callback, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenuW(
            IntPtr hMenu, uint flags, UIntPtr idOrSubmenu, string label);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassNameW(IntPtr hWnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr newValue);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static extern IntPtr CallWindowProc(
            IntPtr prevProc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static IntPtr _hwnd;
        private static IntPtr _menuBar;
        private static IntPtr _originalWndProc;
        private static bool _menuAttached;

        /// <summary>Where menu items act; null until installed.</summary>
        private static INavigator _nav;

        // The OS holds the marshaled function pointer for as long as the
        // subclass is installed, so the delegate must stay alive exactly that
        // long. The GCHandle roots it independently of this field ever being
        // reassigned; Uninstall frees it only after Unity's procedure is back.
        private static WndProcDelegate _wndProc;
        private static GCHandle _wndProcHandle;

        // Written by OnWndProc, consumed by Poll. Both run on the main thread
        // (Unity pumps window messages from the player loop), so no
        // synchronization is needed — this is a deferral, not a thread handoff.
        private static int _pendingCommand;

        private static IntPtr _enumFound;

        /// <summary>
        /// Attach the bar and subclass the player window. Called once from the
        /// seam's Install. If the window handle is not available yet (the app
        /// launched without focus, or is still creating its window),
        /// <see cref="Poll"/> retries until it is. Idempotent after the first
        /// success.
        /// </summary>
        /// <param name="nav">The navigator menu items act through.</param>
        public static void Install(INavigator nav)
        {
            if (nav == null || _hwnd != IntPtr.Zero)
            {
                return;
            }
            _nav = nav;

            IntPtr hwnd = FindPlayerWindow();
            if (hwnd == IntPtr.Zero)
            {
                return; // retried from Poll
            }
            _hwnd = hwnd;

            BuildMenus();

            _wndProc = OnWndProc;
            _wndProcHandle = GCHandle.Alloc(_wndProc);
            _originalWndProc = SetWindowLongPtr(
                _hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProc));

            AttachMenu(Screen.fullScreenMode == FullScreenMode.Windowed);
        }

        /// <summary>
        /// Run the command recorded since the previous frame and keep the bar in
        /// step with fullscreen. Called every frame from the seam's Poll.
        /// </summary>
        public static void Poll()
        {
            if (_nav == null)
            {
                return; // uninstalled, or never installed
            }
            if (_hwnd == IntPtr.Zero)
            {
                Install(_nav);
                if (_hwnd == IntPtr.Zero)
                {
                    return;
                }
            }

            bool wantMenu = Screen.fullScreenMode == FullScreenMode.Windowed;
            if (wantMenu != _menuAttached)
            {
                AttachMenu(wantMenu);
            }

            int command = _pendingCommand;
            if (command == IdNone)
            {
                return;
            }
            _pendingCommand = IdNone;

            switch (command)
            {
                case IdFileSettings:
                    _nav.ShowSettings();
                    break;
                case IdFileExit:
                    // The normal exit path: OnApplicationQuit persists settings
                    // and stops the search.
                    Application.Quit();
                    break;
                case IdHelpTutorial:
                    _nav.OpenUrl(AppLinks.TutorialUrl);
                    break;
                case IdHelpManual:
                    _nav.OpenUrl(AppLinks.ManualUrl);
                    break;
                case IdHelpAbout:
                    _nav.ShowAbout();
                    break;
            }
        }

        /// <summary>
        /// Restore Unity's window procedure, drop the menu, and release the
        /// pinned delegate — before the window is destroyed and the scripting
        /// runtime shuts down. Restoring the procedure first means no window
        /// message can reach managed code afterwards, which is what makes
        /// freeing the delegate safe. Idempotent.
        /// </summary>
        public static void Uninstall()
        {
            _nav = null;
            _pendingCommand = IdNone;
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            if (_originalWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _originalWndProc);
                _originalWndProc = IntPtr.Zero;
            }
            SetMenu(_hwnd, IntPtr.Zero);
            if (_menuBar != IntPtr.Zero)
            {
                DestroyMenu(_menuBar); // recursively destroys the popup submenus
                _menuBar = IntPtr.Zero;
            }
            if (_wndProcHandle.IsAllocated)
            {
                _wndProcHandle.Free();
            }
            _wndProc = null;
            _menuAttached = false;
            _hwnd = IntPtr.Zero;
        }

        /// <summary>
        /// Build the two popups and the bar that holds them. Every visible word
        /// comes from <c>AppStrings</c> / the product name; the ampersands are
        /// Win32 mnemonic markers and are not drawn.
        /// </summary>
        private static void BuildMenus()
        {
            IntPtr fileMenu = CreatePopupMenu();
            AppendMenuW(fileMenu, MF_STRING, (UIntPtr)IdFileSettings,
                "&" + AppStrings.SettingsTitle + OpensScreenSuffix);
            AppendMenuW(fileMenu, MF_SEPARATOR, UIntPtr.Zero, null);
            AppendMenuW(fileMenu, MF_STRING, (UIntPtr)IdFileExit, "E&xit");

            IntPtr helpMenu = CreatePopupMenu();
            AppendMenuW(helpMenu, MF_STRING, (UIntPtr)IdHelpTutorial,
                "&" + AppStrings.AboutTutorial);
            AppendMenuW(helpMenu, MF_STRING, (UIntPtr)IdHelpManual,
                AppStrings.AppName + " &" + AppStrings.AboutManual);
            AppendMenuW(helpMenu, MF_SEPARATOR, UIntPtr.Zero, null);
            AppendMenuW(helpMenu, MF_STRING, (UIntPtr)IdHelpAbout,
                "&" + AppStrings.AboutTitle + " " + AppStrings.AppName);

            _menuBar = CreateMenu();
            AppendMenuW(_menuBar, MF_POPUP, (UIntPtr)(ulong)(long)fileMenu, "&File");
            AppendMenuW(_menuBar, MF_POPUP, (UIntPtr)(ulong)(long)helpMenu, "&Help");
        }

        private static void AttachMenu(bool attach)
        {
            SetMenu(_hwnd, attach ? _menuBar : IntPtr.Zero);
            DrawMenuBar(_hwnd);
            _menuAttached = attach;
        }

        private static IntPtr FindPlayerWindow()
        {
            // Normal launch: the player window is focused, and GetActiveWindow
            // returns the calling thread's active window.
            IntPtr hwnd = GetActiveWindow();
            if (hwnd != IntPtr.Zero)
            {
                return hwnd;
            }

            // Launched or still loading without focus: enumerate the main
            // thread's windows and match the player window's class.
            _enumFound = IntPtr.Zero;
            EnumThreadWindows(GetCurrentThreadId(), OnEnumWindow, IntPtr.Zero);
            return _enumFound;
        }

        [AOT.MonoPInvokeCallback(typeof(EnumWindowsDelegate))]
        private static bool OnEnumWindow(IntPtr hWnd, IntPtr lParam)
        {
            var className = new StringBuilder(64);
            GetClassNameW(hWnd, className, className.Capacity);
            if (className.ToString() != PlayerWindowClass)
            {
                return true; // keep enumerating
            }
            _enumFound = hWnd;
            return false;
        }

        [AOT.MonoPInvokeCallback(typeof(WndProcDelegate))]
        private static IntPtr OnWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_COMMAND)
            {
                int id = unchecked((int)(long)wParam) & 0xFFFF;
                if (id >= IdFileSettings && id <= IdLastCommand)
                {
                    _pendingCommand = id; // executed by Poll inside the game loop
                    return IntPtr.Zero;
                }
            }
            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }
    }
}
#endif
