using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using HeatonCA.SelfCheck;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// The one call the simulator screen answers that the four-member view contract
    /// cannot express: the kiosk look the old web viewer's <c>?controls=off</c>
    /// embeds asked for. Implemented by <c>SimulatorView</c>.
    ///
    /// Everything else the controller needs from that screen is already part of it
    /// -- <c>Open(rule, autoStart)</c> loads a rule (the PyQt <c>display_rule</c>
    /// path) and <see cref="IAppView.ApplyLayout"/> re-cuts the lattice, which the
    /// controller calls again once a resize has settled.
    /// </summary>
    public interface ISimulatorScreen
    {
        /// <summary>
        /// Show or hide every control around the lattice -- the kiosk look the old
        /// web viewer's <c>?controls=off</c> embeds used. True is the app default.
        /// </summary>
        /// <param name="visible">False to leave only the lattice on screen.</param>
        void SetChromeVisible(bool visible);
    }

    /// <summary>
    /// The extra call the rule-decoder screen answers: which rule to decode.
    /// <c>ShowRuleDecoder</c> always names a rule, so unlike
    /// <see cref="ISimulatorScreen"/> this is not optional -- <c>RuleDecoderView</c>
    /// implements it, and the controller calls it as soon as the screen is up.
    /// </summary>
    public interface IRuleDecoderScreen
    {
        /// <summary>Decode and display <paramref name="rule"/>.</summary>
        /// <param name="rule">Rule in any form <c>RuleParser</c> accepts.</param>
        void OpenRule(string rule);
    }

    /// <summary>
    /// Root coordinator (the heaton-life-unity / dynaface pattern): bootstraps the
    /// camera, canvas, safe area, and event system in code, builds every screen
    /// once, owns the services those screens share, and is the app's only
    /// <see cref="INavigator"/>. No simulation, texture, or file logic lives here.
    ///
    /// The scene's "App" object binds to this script by GUID
    /// (Assets/Scripts/AppController.cs.meta, 7d3a9c41e5f24b8a9c6d1e0f2a4b6c8d), and
    /// <see cref="Bootstrap"/> covers scenes that have no such object. Everything
    /// the controller creates is parented under its own GameObject, so destroying
    /// "App" -- which the PlayMode harness does between tests -- takes the whole UI
    /// with it.
    /// </summary>
    public sealed class AppController : MonoBehaviour, INavigator
    {
        private const string LogTag = "[HeatonCA]";
        private const string SelfCheckFlag = "-selfcheck";

        /// <summary>
        /// Seconds the canvas must hold still before the simulator re-cuts its
        /// lattice. PyQt debounced its resize event by 300 ms; a window drag or a
        /// rotation animation otherwise re-cuts on every intermediate size.
        /// </summary>
        public const float ResizeSettleSeconds = 0.3f;

        /// <summary>Seconds a <see cref="Status"/> toast stays up before it hides itself.</summary>
        public const float StatusSeconds = 3f;

        /// <summary>Widest a dialog card or a toast gets, in canvas units.</summary>
        private const float OverlayMaxWidth = 620f;

        /// <summary>Margin kept between an overlay and the edge of the safe area.</summary>
        private const float OverlayMargin = 24f;

        /// <summary>The live controller, or null before boot / after teardown.</summary>
        public static AppController Instance { get; private set; }

        /// <summary>
        /// The app is fully code-built, so it can boot into any scene: if the open
        /// scene has no App object (empty scene, wrong scene, the test runner's
        /// scene), spawn one on play. Makes "press Play" work regardless of which
        /// scene the editor has open. The shipped scene already carries an App,
        /// whose Awake ran before this hook fires.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null && FindAnyObjectByType<AppController>() == null)
            {
                new GameObject("App", typeof(AppController));
            }
        }

        /// <summary>Full report of the determinism self-check run at boot.</summary>
        public string SelfCheckReport { get; private set; } = string.Empty;

        /// <summary>True when every embedded determinism check reproduced its pin.</summary>
        public bool SelfCheckPassed { get; private set; }

        /// <summary>
        /// Persistent-storage root for the app's own files (finds, snapshots), or
        /// null for the platform default. Set by <see cref="UseStorageRoot"/>.
        /// </summary>
        public string StorageRoot { get; private set; }

        /// <summary>
        /// The safe-area rect every screen parents into (clear of the notch and home
        /// indicator; the full-bleed backdrop stays behind it).
        /// </summary>
        public RectTransform SafeArea { get; private set; }

        /// <summary>The long-lived services every screen shares.</summary>
        public AppServices Services { get; private set; }

        /// <summary>The screen on display; <see cref="ScreenId.Home"/> at boot.</summary>
        public ScreenId CurrentScreen { get; private set; } = ScreenId.Home;

        /// <summary>
        /// The navigation stack, Home first and the visible screen last. Read-only:
        /// it changes only through the <see cref="INavigator"/> calls.
        /// </summary>
        public IReadOnlyList<ScreenId> BackStack => _backStack;

        /// <summary>The Home screen, for the PlayMode layout checks.</summary>
        public HomeView Home => _home;

        /// <summary>
        /// The Home corner badge ("v" + Application.version). Lives on the Home
        /// screen; exposed here because it is the app's one version readout outside
        /// the About page.
        /// </summary>
        public Text VersionBadge => _home != null ? _home.VersionBadge : null;

        /// <summary>True while a modal alert or confirmation is up.</summary>
        public bool DialogVisible => _dialogOverlay != null && _dialogOverlay.activeSelf;

        /// <summary>The toast line currently up, or null when none is.</summary>
        public string StatusText { get; private set; }

        /// <summary>
        /// False in the WebGL kiosk embeds (<c>?controls=off</c>): the simulator
        /// hides its chrome and shows only the lattice. True everywhere else.
        /// </summary>
        public bool ControlsVisible { get; private set; } = true;

        private readonly List<ScreenId> _backStack = new List<ScreenId>();
        private readonly Dictionary<ScreenId, IAppView> _views = new Dictionary<ScreenId, IAppView>();
        private readonly HashSet<ScreenId> _laidOut = new HashSet<ScreenId>();

        private HomeView _home;
        private GalleryView _gallery;
        private SimulatorView _simulator;
        private RuleDecoderView _decoder;
        private EvolveView _evolve;
        private SettingsView _settings;
        private AboutView _about;

        private CanvasScaler _canvasScaler;
        private Vector2Int _lastScreenSize;
        private float _settleTimer;
        private bool _portrait;
        private bool _phone;
        private bool _screenKeptAwake;

        private GameObject _dialogOverlay;
        private RectTransform _dialogCard;
        private Text _dialogTitle;
        private Text _dialogBody;
        private Text _dialogOkLabel;
        private Text _dialogCancelLabel;
        private GameObject _dialogCancelGo;
        private TaskCompletionSource<bool> _dialogTcs;

        private RectTransform _statusPanel;
        private Text _statusLabel;
        private float _statusTimer;

        private void Awake()
        {
            Instance = this;
            // Idle power: without a cap a static screen renders at unbounded FPS and
            // spins the fans. VSync governs desktop; targetFrameRate governs
            // platforms that ignore vsync (mobile).
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
            CreateCamera();
            RectTransform canvasRoot = UIBuilder.CreateCanvas(out Canvas canvas);
            canvasRoot.SetParent(transform, false);
            _canvasScaler = canvas.GetComponent<CanvasScaler>();
            RectTransform background = UIBuilder.CreatePanel(
                canvasRoot, "Background", UIBuilder.BackgroundColor);
            UIBuilder.Stretch(background);
            SafeArea = UIBuilder.CreatePanel(canvasRoot, "SafeArea");
            UIBuilder.Stretch(SafeArea);
            SafeArea.gameObject.AddComponent<SafeAreaPanel>();
            // Before the screens: a view that throws must still leave the determinism
            // verdict in the device log, because the -selfcheck gates read it there.
            RunSelfCheck();
            BuildServices();
            _portrait = Screen.height > Screen.width;
            _phone = UIBuilder.IsPhoneLayout;
            BuildViews();
            // Built after the screens so both overlays are later siblings and sort
            // above every view; each also raises itself on show.
            BuildDialogOverlay(SafeArea);
            BuildStatusToast(SafeArea);
            CreateEventSystem();
            ShowHome();
            // Runtime platform check, not a compile-time define: AppController is not
            // one of the plan's seam files. WebGlBridge is a no-op elsewhere.
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                WebGlBridge.InstallUnloadHook();
                string query = WebGlBridge.GetQueryString();
                Debug.Log(LogTag + " url " + query);
                ApplyUrl(query);
            }
            if (!Application.isEditor && HasCommandLineFlag(SelfCheckFlag))
            {
                // Device gates: `HeatonCA -batchmode -nographics -selfcheck` exits
                // with the verdict once the report is in the log.
                Application.Quit(SelfCheckPassed ? 0 : 1);
            }
        }

        private void Start()
        {
            // Native desktop menu bars: a no-op on every platform without one, and
            // the seam file carries the only platform conditionals.
            NativeMenus.Install(this);
        }

        private void Update()
        {
            if (Services == null)
            {
                return; // a screen's constructor threw during Awake; nothing to drive
            }
            // Menu clicks arrive inside the OS message pump; the seam records them
            // and this poll runs them here, in the game loop.
            NativeMenus.Poll();
            float dt = Time.unscaledDeltaTime;
            // The search is session-long: it keeps breeding while the user browses
            // other screens, so it is pumped here and not from the evolve screen
            // (EvolveView.PumpsSearch is off in the app for exactly this reason).
            Services.Evolve.Tick();
            // At most one thumbnail render per frame, wherever the finds page is.
            Services.Thumbnails.Tick();
            // The on-screen lattice: stepped here, drawn by the simulator, which
            // consumes Host.Dirty in its own Tick immediately below.
            Services.Host.Tick(dt);
            if (_views.TryGetValue(CurrentScreen, out IAppView visible))
            {
                visible.Tick(dt);
            }
            WatchScreenSize(dt);
            ScopeScreenSleep();
            TickStatus(dt);
            HandleBackKey();
        }

        private void OnDestroy()
        {
            NativeMenus.Uninstall();
            // Stop before tearing down: a search left running would keep worker
            // threads alive past the controller that owns them (the PlayMode suite
            // destroys and rebuilds the app between tests).
            if (Services != null && Services.Evolve != null && Services.Evolve.Running)
            {
                Services.Evolve.RequestStop();
            }
            Services?.Host?.Unload();
            Services?.Blitter?.Dispose();
            Services?.Thumbnails?.Dispose();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            NativeMenus.Uninstall();
            PersistAll();
        }

        private void OnApplicationPause(bool paused)
        {
            if (Services == null || Services.Evolve == null)
            {
                return;
            }
            if (paused)
            {
                // Mobile lifecycle: backgrounding may be the last thing that runs,
                // and a suspended search resumes with its population intact.
                Services.Evolve.Suspend();
                PersistAll();
            }
            else
            {
                Services.Evolve.Resume();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer
                && Services != null && Services.Evolve != null)
            {
                // The browser has already stopped ticking a background tab; this only
                // lets the evolve screen say why its counters stopped.
                Services.Evolve.BrowserPaused = !focused;
            }
            if (!focused)
            {
                PersistAll();
            }
        }

        /// <summary>
        /// Called from the WebGL page's beforeunload hook via
        /// SendMessage("App", "OnBeforeUnload"): browsers never fire
        /// OnApplicationQuit, so this is the last chance to bank the finds log and
        /// flush preferences.
        /// </summary>
        public void OnBeforeUnload()
        {
            PersistAll();
        }

        /// <summary>
        /// Redirect all persistent storage to another root: the finds log, whatever
        /// else the store holds, and the snapshot folder. The PlayMode suite points
        /// this at a throwaway folder so tests never touch the user's real data;
        /// null or an empty root goes back to the platform default.
        /// </summary>
        /// <param name="root">Absolute folder to store under, or null for the default.</param>
        public void UseStorageRoot(string root)
        {
            StorageRoot = root;
            IAppStore store = string.IsNullOrEmpty(root)
                ? AppStore.CreateDefault()
                : new FileStore(root);
            if (Services != null)
            {
                Services.Store = store;
                Services.Evolve?.SetStore(store);
            }
            PngExporter.SnapshotsRoot = string.IsNullOrEmpty(root)
                ? null
                : Path.Combine(root, PngExporter.SnapshotsFolder);
        }

        /// <summary>
        /// Apply the web viewer's launch parameters (<c>?rule=</c>, <c>?size=</c>,
        /// <c>?controls=</c>). Called at boot on WebGL with
        /// <c>WebGlBridge.GetQueryString()</c>, and by the PlayMode suite with a
        /// query of its own -- the one hook that lets a desktop test drive the
        /// browser's launch path. Junk parses to an empty result and changes
        /// nothing.
        /// </summary>
        /// <param name="query">A URL, a <c>location.search</c> string, or a bare query.</param>
        /// <returns>What was parsed, so a caller can log or assert on it.</returns>
        public UrlParams ApplyUrl(string query)
        {
            UrlParams parameters = UrlParams.Parse(query);
            if (parameters.CellSize.HasValue)
            {
                // The old viewer's "size" is this app's cell size. It is written
                // through AppSettings, which persists it: a browser embed that names
                // a size keeps it for that origin, exactly as the viewer's own
                // per-page zoom did.
                AppSettings.CellSize = parameters.CellSize.Value;
                Debug.Log(
                    LogTag + " url size "
                    + parameters.CellSize.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (parameters.Controls.HasValue)
            {
                SetControlsVisible(parameters.Controls.Value);
                Debug.Log(LogTag + " url controls " + (parameters.Controls.Value ? "on" : "off"));
            }
            if (!string.IsNullOrEmpty(parameters.Rule))
            {
                Debug.Log(LogTag + " url rule " + parameters.Rule);
                ShowSimulator(parameters.Rule, autoStart: true);
            }
            return parameters;
        }

        /// <summary>
        /// Show or hide the simulator's chrome for every visit from here on -- the
        /// kiosk look of the <c>?controls=off</c> embeds.
        /// </summary>
        /// <param name="visible">False to leave only the lattice on screen.</param>
        public void SetControlsVisible(bool visible)
        {
            ControlsVisible = visible;
            ApplyChrome();
        }

        /// <summary>
        /// Tell the simulator what the kiosk flag says. Re-applied on every visit,
        /// because <see cref="ControlsVisible"/> is a property of the session (the
        /// launch URL), not of one trip to that screen.
        /// </summary>
        private void ApplyChrome()
        {
            ((ISimulatorScreen)_simulator)?.SetChromeVisible(ControlsVisible);
        }

        /// <summary>
        /// The screen behind <paramref name="id"/>, or null when it has not been
        /// built (which only happens if a screen's constructor failed). The PlayMode
        /// suite reads the visibility flags through this.
        /// </summary>
        /// <param name="id">Which screen to look up.</param>
        /// <returns>That screen's view, or null.</returns>
        public IAppView ViewFor(ScreenId id) =>
            _views.TryGetValue(id, out IAppView view) ? view : null;

        // ---- navigation ---------------------------------------------------------
        //
        // One stack, Home at the bottom, every screen in it at most once: pushing a
        // screen that is already on the stack pops back to it instead of stacking a
        // second copy, so Simulator -> Rule -> "Open in Simulator" cannot grow
        // without bound and Back always shortens the path.

        /// <inheritdoc/>
        public void ShowHome()
        {
            _backStack.Clear();
            _backStack.Add(ScreenId.Home);
            Present(ScreenId.Home);
        }

        /// <inheritdoc/>
        public void ShowGallery() => Push(ScreenId.Gallery);

        /// <inheritdoc/>
        public void ShowSimulator(string rule = null, bool autoStart = false)
        {
            if (_simulator == null)
            {
                Push(ScreenId.Simulator);
                return;
            }
            // A named rule (the gallery, the decoder, ?rule=) replaces whatever is
            // running rather than stacking a second lattice: PyQt's display_rule. A
            // bare visit -- the Home button, which is PyQt's splash switching back to
            // the tab -- keeps the world that is already there.
            bool reopen = !string.IsNullOrEmpty(rule)
                || autoStart
                || string.IsNullOrEmpty(_simulator.Rule);
            if (reopen && _simulator.Visible)
            {
                _simulator.Hide();
            }
            Push(ScreenId.Simulator);
            ApplyChrome();
            if (reopen)
            {
                // After Show: the canvas is sized by then, so the grid is cut for the
                // rect the lattice will actually occupy.
                _simulator.Open(rule, autoStart);
            }
        }

        /// <inheritdoc/>
        public void ShowRuleDecoder(string rule)
        {
            Push(ScreenId.RuleDecoder);
            _decoder?.OpenRule(rule);
        }

        /// <inheritdoc/>
        public void ShowEvolve() => Push(ScreenId.Evolve);

        /// <inheritdoc/>
        public void ShowSettings() => Push(ScreenId.Settings);

        /// <inheritdoc/>
        public void ShowAbout() => Push(ScreenId.About);

        /// <inheritdoc/>
        public void Back()
        {
            // Home is the root: the app never backs out of its own first screen, on
            // desktop or on Android.
            if (_backStack.Count <= 1)
            {
                return;
            }
            _backStack.RemoveAt(_backStack.Count - 1);
            Present(_backStack[_backStack.Count - 1]);
        }

        private void Push(ScreenId id)
        {
            int existing = _backStack.IndexOf(id);
            if (existing >= 0)
            {
                _backStack.RemoveRange(existing + 1, _backStack.Count - existing - 1);
            }
            else
            {
                _backStack.Add(id);
            }
            Present(id);
        }

        private void Present(ScreenId id)
        {
            foreach (KeyValuePair<ScreenId, IAppView> entry in _views)
            {
                if (entry.Key != id && entry.Value.Visible)
                {
                    entry.Value.Hide();
                }
            }
            CurrentScreen = id;
            // A toast belongs to the screen that raised it.
            Status(null);
            if (!_views.TryGetValue(id, out IAppView view))
            {
                return;
            }
            if (_laidOut.Add(id))
            {
                view.ApplyLayout(_portrait, _phone);
            }
            view.Show();
        }

        // ---- modal dialogs and the status toast ---------------------------------
        //
        // The house rule: a technical failure the user must acknowledge is a modal
        // alert, anything destructive asks first with a confirm, and routine
        // feedback ("copied", "saved foo.png") is a transient toast that steals no
        // focus. Both dialogs resolve from a button click through a
        // TaskCompletionSource, never a blocking wait -- WebGL has no worker thread
        // to block.

        /// <inheritdoc/>
        public Task ShowAlertAsync(string title, string message) =>
            ShowDialogAsync(title, message, AppStrings.Ok, cancel: null);

        /// <inheritdoc/>
        public Task<bool> ShowConfirmAsync(
            string title,
            string message,
            string ok = AppStrings.Ok,
            string cancel = AppStrings.Cancel) =>
            ShowDialogAsync(title, message, ok, cancel);

        /// <inheritdoc/>
        public void Status(string text)
        {
            if (_statusPanel == null)
            {
                return;
            }
            if (string.IsNullOrEmpty(text))
            {
                StatusText = null;
                _statusTimer = 0f;
                _statusPanel.gameObject.SetActive(false);
                return;
            }
            StatusText = text;
            _statusLabel.text = text;
            _statusTimer = StatusSeconds;
            _statusPanel.SetAsLastSibling();
            _statusPanel.gameObject.SetActive(true);
        }

        /// <inheritdoc/>
        public void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // Called synchronously inside the click handler so popup blockers
                // treat the new tab as user-initiated; Application.OpenURL is blocked
                // in browsers.
                WebGlBridge.OpenInNewTab(url);
                return;
            }
            Application.OpenURL(url);
        }

        private Task<bool> ShowDialogAsync(string title, string message, string ok, string cancel)
        {
            // One at a time: a second request resolves false rather than stacking a
            // modal over the one whose answer somebody is already awaiting.
            if (_dialogOverlay == null || _dialogTcs != null)
            {
                return Task.FromResult(false);
            }
            _dialogTitle.text = title ?? string.Empty;
            _dialogBody.text = message ?? string.Empty;
            _dialogOkLabel.text = ok ?? AppStrings.Ok;
            _dialogCancelLabel.text = cancel ?? string.Empty;
            _dialogCancelGo.SetActive(cancel != null);
            _dialogTcs = new TaskCompletionSource<bool>();
            _dialogOverlay.transform.SetAsLastSibling();
            _dialogOverlay.SetActive(true);
            return _dialogTcs.Task;
        }

        private void ResolveDialog(bool result)
        {
            _dialogOverlay?.SetActive(false);
            TaskCompletionSource<bool> pending = _dialogTcs;
            _dialogTcs = null;
            pending?.TrySetResult(result);
        }

        private void TickStatus(float dt)
        {
            if (_statusTimer <= 0f)
            {
                return;
            }
            _statusTimer -= dt;
            if (_statusTimer <= 0f)
            {
                Status(null);
            }
        }

        // ---- frame work ---------------------------------------------------------

        private void WatchScreenSize(float dt)
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _lastScreenSize && size.x > 0 && size.y > 0)
            {
                _lastScreenSize = size;
                // Density first: rotation changes the physical width, and every
                // layout below reads UIBuilder.ReferenceWidth through the scaler.
                UIBuilder.ApplyReference(_canvasScaler);
                _portrait = size.y > size.x;
                _phone = UIBuilder.IsPhoneLayout;
                // Every screen is now laid out for a page that no longer exists --
                // even without a change of mode, a desktop window resize moves every
                // rect. The visible one is fixed immediately and the rest as they are
                // shown, which is also what re-cuts a lattice sized for the old page.
                _laidOut.Clear();
                if (_views.TryGetValue(CurrentScreen, out IAppView visible))
                {
                    _laidOut.Add(CurrentScreen);
                    visible.ApplyLayout(_portrait, _phone);
                }
                LayoutOverlays();
                // Restart the debounce: the lattice re-cut waits for the last size.
                _settleTimer = ResizeSettleSeconds;
                return;
            }
            if (_settleTimer <= 0f)
            {
                return;
            }
            _settleTimer -= dt;
            if (_settleTimer > 0f)
            {
                return;
            }
            _settleTimer = 0f;
            // The settled pass: the canvas has held still for 0.3 s, so this is the
            // size worth re-cutting a lattice for (PyQt debounced the same event by
            // 300 ms). ApplyLayout is that re-cut -- the simulator fits its lattice
            // to the page there, and does nothing when the grid already matches, so
            // the intermediate sizes of a window drag cost a measurement and no more.
            if (_views.TryGetValue(CurrentScreen, out IAppView settled))
            {
                settled.ApplyLayout(_portrait, _phone);
            }
            // Again here because the canvas rect is zero until uGUI has laid it out
            // once, which has certainly happened by now; the pass on the size change
            // itself can land before that and leave the overlays at their built width.
            LayoutOverlays();
        }

        /// <summary>
        /// Keep the screen awake while there is something to watch. Mobile only:
        /// desktop screensavers are the user's business, and the browser has its own
        /// rules. EvolveHost scopes the same flag around a search; this pass runs
        /// after its Tick, so whichever of the two still wants the screen awake has
        /// the last word.
        /// </summary>
        private void ScopeScreenSleep()
        {
            if (!Application.isMobilePlatform)
            {
                return;
            }
            bool keepAwake = (Services.Host != null && Services.Host.Playing)
                || (Services.Evolve != null && Services.Evolve.Running);
            if (keepAwake == _screenKeptAwake)
            {
                return;
            }
            _screenKeptAwake = keepAwake;
            Screen.sleepTimeout = keepAwake ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        }

        /// <summary>
        /// Escape (desktop, editor) and the Android hardware back button -- which the
        /// Input System reports as the keyboard's escape key -- both mean Back. A
        /// modal takes it first and reads it as a dismissal, so a dialog can never
        /// trap the user on a screen they cannot leave.
        /// </summary>
        private void HandleBackKey()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }
            if (DialogVisible)
            {
                ResolveDialog(false);
                return;
            }
            Back();
        }

        private void PersistAll()
        {
            try
            {
                Services?.Evolve?.PersistFinds();
                Services?.Store?.Flush();
            }
            catch (Exception exception)
            {
                // A failed flush must not take the app down on the way out; the store
                // itself reports the detail.
                Debug.LogWarning(LogTag + " could not flush storage: " + exception.Message);
            }
            PlayerPrefs.Save();
        }

        /// <summary>
        /// The determinism gate: every build, including the Editor, proves the engine
        /// math held on this platform and scripting backend before the user can trust
        /// a simulation or an evolve run. File-free and fast (~100 ms); the report
        /// lands in the device log, where the gate scripts read the PASS / FAIL token.
        /// </summary>
        private void RunSelfCheck()
        {
            SelfCheckPassed = DeterminismSelfCheck.Run(out string report);
            SelfCheckReport = report;
            if (SelfCheckPassed)
            {
                Debug.Log(AppStrings.SelfCheckPass + "\n" + report);
            }
            else
            {
                Debug.LogError(AppStrings.SelfCheckFail + "\n" + report);
            }
        }

        // ---- construction -------------------------------------------------------

        private void BuildServices()
        {
            Services = new AppServices
            {
                Host = new SimulationHost(),
                Blitter = new FrameBlitter(),
                Evolve = new EvolveHost(),
                Store = AppStore.CreateDefault(),
                Thumbnails = new FindsThumbnailer(),
                SelfCheckReport = () => SelfCheckReport,
                SelfCheckPassed = () => SelfCheckPassed,
            };
            Services.Evolve.SetStore(Services.Store);
        }

        private void BuildViews()
        {
            _home = new HomeView(SafeArea, this, Services);
            _gallery = new GalleryView(SafeArea, this, Services);
            _simulator = new SimulatorView(SafeArea, this, Services);
            _decoder = new RuleDecoderView(SafeArea, this, Services);
            _evolve = new EvolveView(SafeArea, this, Services);
            _settings = new SettingsView(SafeArea, this, Services);
            _about = new AboutView(SafeArea, this, Services);
            _views[ScreenId.Home] = _home;
            _views[ScreenId.Gallery] = _gallery;
            _views[ScreenId.Simulator] = _simulator;
            _views[ScreenId.RuleDecoder] = _decoder;
            _views[ScreenId.Evolve] = _evolve;
            _views[ScreenId.Settings] = _settings;
            _views[ScreenId.About] = _about;
            foreach (KeyValuePair<ScreenId, IAppView> entry in _views)
            {
                // Start every screen down, without going through Hide(): a screen
                // that leaves its root active after building has not been *shown*, so
                // running its leave-the-screen work (persisting a log, releasing a
                // preview) at boot would be wrong -- and on the evolve screen it would
                // write the finds log before the storage redirect is even in place.
                if (entry.Value.Root != null)
                {
                    entry.Value.Root.SetActive(false);
                }
            }
        }

        private void BuildDialogOverlay(RectTransform parent)
        {
            // The scrim swallows taps: a modal must not be dismissable by a stray tap
            // on the screen behind it, because the caller is awaiting an answer.
            var overlay = new GameObject("DialogOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(parent, false);
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            UIBuilder.Stretch((RectTransform)overlay.transform);
            _dialogOverlay = overlay;

            _dialogCard = UIBuilder.CreatePanel(overlay.transform, "Card", UIBuilder.PanelColor);
            _dialogCard.anchorMin = new Vector2(0.5f, 0.5f);
            _dialogCard.anchorMax = new Vector2(0.5f, 0.5f);
            _dialogCard.pivot = new Vector2(0.5f, 0.5f);
            _dialogCard.sizeDelta = new Vector2(OverlayMaxWidth, 300f);

            _dialogTitle = UIBuilder.CreateText(
                _dialogCard, "Title", string.Empty, 28, UIBuilder.TextColor, TextAnchor.UpperCenter);
            _dialogTitle.fontStyle = FontStyle.Bold;
            _dialogTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            var titleRt = (RectTransform)_dialogTitle.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -20f);
            titleRt.sizeDelta = new Vector2(-40f, 44f);

            _dialogBody = UIBuilder.CreateText(
                _dialogCard, "Body", string.Empty, 20, UIBuilder.TextDimColor, TextAnchor.UpperLeft);
            // A dialog body is prose: it wraps inside the card instead of running out
            // past its edge, but never truncates -- an exception string must be read.
            _dialogBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _dialogBody.verticalOverflow = VerticalWrapMode.Overflow;
            UIBuilder.Stretch((RectTransform)_dialogBody.transform, 26f, 96f, 26f, 72f);

            Button ok = UIBuilder.CreateButton(
                _dialogCard, "Ok", AppStrings.Ok, 22, () => ResolveDialog(true));
            var okRt = (RectTransform)ok.transform;
            okRt.anchorMin = new Vector2(0.72f, 0f);
            okRt.anchorMax = new Vector2(0.72f, 0f);
            okRt.pivot = new Vector2(0.5f, 0f);
            okRt.anchoredPosition = new Vector2(0f, 22f);
            okRt.sizeDelta = new Vector2(200f, 52f);
            _dialogOkLabel = ok.GetComponentInChildren<Text>();

            Button cancel = UIBuilder.CreateButton(
                _dialogCard, "Cancel", AppStrings.Cancel, 22, () => ResolveDialog(false));
            var cancelRt = (RectTransform)cancel.transform;
            cancelRt.anchorMin = new Vector2(0.28f, 0f);
            cancelRt.anchorMax = new Vector2(0.28f, 0f);
            cancelRt.pivot = new Vector2(0.5f, 0f);
            cancelRt.anchoredPosition = new Vector2(0f, 22f);
            cancelRt.sizeDelta = new Vector2(200f, 52f);
            _dialogCancelLabel = cancel.GetComponentInChildren<Text>();
            _dialogCancelGo = cancel.gameObject;

            overlay.SetActive(false);
        }

        private void BuildStatusToast(RectTransform parent)
        {
            _statusPanel = UIBuilder.CreatePanel(parent, "StatusToast", UIBuilder.PanelColor);
            _statusPanel.anchorMin = new Vector2(0.5f, 0f);
            _statusPanel.anchorMax = new Vector2(0.5f, 0f);
            _statusPanel.pivot = new Vector2(0.5f, 0f);
            _statusPanel.anchoredPosition = new Vector2(0f, 28f);
            _statusPanel.sizeDelta = new Vector2(OverlayMaxWidth, 46f);
            _statusLabel = UIBuilder.CreateText(
                _statusPanel, "Label", string.Empty, 20, UIBuilder.TextColor,
                TextAnchor.MiddleCenter);
            _statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusLabel.verticalOverflow = VerticalWrapMode.Truncate;
            UIBuilder.Stretch((RectTransform)_statusLabel.transform, 14f, 2f, 14f, 2f);
            _statusPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Fit the dialog card and the toast to the safe area. Both are fixed-width
        /// by design, so on a phone canvas (about 470 units wide) they would run off
        /// both edges without this pass.
        /// </summary>
        private void LayoutOverlays()
        {
            float available = SafeArea != null ? SafeArea.rect.width : 0f;
            if (available <= 0f)
            {
                return;
            }
            float width = Mathf.Min(OverlayMaxWidth, available - 2f * OverlayMargin);
            if (width <= 0f)
            {
                return;
            }
            if (_dialogCard != null)
            {
                _dialogCard.sizeDelta = new Vector2(width, _dialogCard.sizeDelta.y);
            }
            if (_statusPanel != null)
            {
                _statusPanel.sizeDelta = new Vector2(width, _statusPanel.sizeDelta.y);
            }
        }

        private static bool HasCommandLineFlag(string flag)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void CreateCamera()
        {
            if (Camera.main != null)
            {
                return;
            }
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UIBuilder.BackgroundColor;
            camera.cullingMask = 0; // overlay canvas only; nothing in world space
        }

        private void CreateEventSystem()
        {
            // Qualified: this file has `using System`, so bare Object is ambiguous.
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }
            var go = new GameObject(
                "EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(transform, false);
        }
    }
}
