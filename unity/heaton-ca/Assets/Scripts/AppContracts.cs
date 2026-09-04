using System;
using System.Threading.Tasks;
using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// The app's screens, in the order the Home column lists them (Home first
    /// because it is the root of the back stack, so <c>ScreenId.Home == 0</c>).
    /// Used for navigation bookkeeping and analytics-free logging only; the
    /// views themselves are addressed through <see cref="INavigator"/>.
    /// </summary>
    public enum ScreenId
    {
        /// <summary>Title art, version badge, and the button column. Back-stack root.</summary>
        Home,

        /// <summary>The 30-rule curated gallery (PyQt <c>tab_gallery.py</c>).</summary>
        Gallery,

        /// <summary>The live lattice with its toolbar (PyQt's "Rule Viewer").</summary>
        Simulator,

        /// <summary>The eight-sub-rule decoder table (PyQt's "Rule" tab).</summary>
        RuleDecoder,

        /// <summary>The genetic-algorithm search and its finds page.</summary>
        Evolve,

        /// <summary>Cell size, animation speed, and the FPS/steps overlay toggle.</summary>
        Settings,

        /// <summary>Version, citation, links, and the determinism self-check verdict.</summary>
        About,
    }

    /// <summary>
    /// Everything a view may ask the app to do. Implemented once, by
    /// <c>AppController</c> (WP3.1); views hold this interface and never reach
    /// for the controller singleton, so a view can be built in a test against a
    /// recording stub. The navigator owns the back stack, the modal overlays,
    /// and the toast, so a view never creates a screen or a dialog itself.
    ///
    /// Every call is main-thread only and returns immediately: the two async
    /// calls hand back a Task completed by a later frame's button click (a
    /// <c>TaskCompletionSource</c>, not a thread), which is why they are safe
    /// on WebGL where there are no worker threads.
    /// </summary>
    public interface INavigator
    {
        /// <summary>
        /// Show Home, the back-stack root. Clears the stack: Home is where
        /// <see cref="Back"/> stops, so arriving here starts a fresh path.
        /// </summary>
        void ShowHome();

        /// <summary>Show the 30-rule gallery. Tapping a tile calls
        /// <see cref="ShowSimulator"/> with that rule and <c>autoStart</c> true.</summary>
        void ShowGallery();

        /// <summary>
        /// Show the simulator. Any simulator already on screen is hidden and
        /// torn down first, then rebuilt for <paramref name="rule"/> — the PyQt
        /// <c>display_rule</c> semantics, where opening a rule replaces the
        /// running one rather than stacking a second lattice.
        /// </summary>
        /// <param name="rule">
        /// Rule to load, in any form <c>RuleParser</c> accepts; null keeps the
        /// simulator's current rule (or <c>GalleryCatalog.DefaultRule</c> on the
        /// first visit).
        /// </param>
        /// <param name="autoStart">
        /// True starts the animation as soon as the lattice is seeded — what the
        /// gallery and the WebGL <c>?rule=</c> parameter do; false leaves it
        /// paused on generation 0 for the Home and toolbar routes.
        /// </param>
        void ShowSimulator(string rule = null, bool autoStart = false);

        /// <summary>
        /// Show the decoder table for <paramref name="rule"/> (the eight
        /// sub-rules of <c>MergeLife.DecodeRule</c>). Pushed on top of whatever
        /// screen asked, so <see cref="Back"/> returns there — usually the
        /// simulator, whose Rule button opens this for the rule it is running.
        /// </summary>
        /// <param name="rule">The rule to decode; canonical or any parsable form.</param>
        void ShowRuleDecoder(string rule);

        /// <summary>
        /// Show the evolve screen. A search already running keeps running and
        /// its readouts simply reappear — leaving the screen never stops the GA
        /// (Home shows an "Evolving..." chip instead).
        /// </summary>
        void ShowEvolve();

        /// <summary>Show settings (cell size, animation speed, overlay toggle).</summary>
        void ShowSettings();

        /// <summary>Show the About screen (version, citation, links, self-check verdict).</summary>
        void ShowAbout();

        /// <summary>
        /// Pop one entry off the back stack and show what is underneath. A no-op
        /// at Home, which is the root: the app never backs out of its own first
        /// screen. Escape (desktop and editor) and the Android hardware back
        /// button both route here, as do the on-screen back buttons.
        /// </summary>
        void Back();

        /// <summary>
        /// Show a modal one-button alert over the current screen and complete
        /// when the user dismisses it. Awaiting it never blocks a frame: the
        /// returned Task completes from the button's click handler.
        /// </summary>
        /// <param name="title">Dialog heading.</param>
        /// <param name="message">Body text.</param>
        /// <returns>A Task that completes once the alert is dismissed.</returns>
        Task ShowAlertAsync(string title, string message);

        /// <summary>
        /// Show a modal two-button confirmation over the current screen — the
        /// gate in front of every destructive action (Delete a find, Clear all).
        /// </summary>
        /// <param name="title">Dialog heading.</param>
        /// <param name="message">Body text, phrased so "yes" is unambiguous.</param>
        /// <param name="ok">
        /// Confirm button label; defaults to <see cref="AppStrings.Ok"/> ("OK").
        /// The default is spelled as that constant, not a literal, so the button
        /// text still lives in AppStrings — a <c>const string</c> is a
        /// compile-time constant, which is what a default parameter value needs.
        /// </param>
        /// <param name="cancel">
        /// Dismiss button label, and the answer a dismissal gives; defaults to
        /// <see cref="AppStrings.Cancel"/> ("Cancel").
        /// </param>
        /// <returns>
        /// A Task yielding true when the user chose <paramref name="ok"/>, false
        /// for <paramref name="cancel"/> or any other dismissal.
        /// </returns>
        Task<bool> ShowConfirmAsync(
            string title,
            string message,
            string ok = AppStrings.Ok,
            string cancel = AppStrings.Cancel);

        /// <summary>
        /// Show a transient toast — a non-modal line that fades on its own and
        /// steals no focus. For results the user does not have to acknowledge
        /// ("Saved to Photos", "Copied"), never for errors that need a decision;
        /// those use <see cref="ShowAlertAsync"/>. A second call replaces the
        /// line that is up rather than queueing behind it.
        /// </summary>
        /// <param name="text">The line to show; null or empty hides the toast.</param>
        void Status(string text);

        /// <summary>
        /// Open <paramref name="url"/> outside the app. On WebGL this goes
        /// through the <c>WebGlBridge</c> new-tab jslib call, invoked
        /// synchronously inside the click handler so popup blockers treat it as
        /// user-initiated (<c>Application.OpenURL</c> is blocked there);
        /// everywhere else it is <c>Application.OpenURL</c>. Only the addresses
        /// in <see cref="AppLinks"/> are ever passed.
        /// </summary>
        /// <param name="url">Absolute http(s) URL.</param>
        void OpenUrl(string url);
    }

    /// <summary>
    /// One screen. Views are plain C# classes (not MonoBehaviours) that build
    /// their UI into a parent RectTransform in their constructor, exactly like
    /// the heaton-life-unity views, and every one of them takes the same fixed
    /// constructor <c>(RectTransform parent, INavigator nav, AppServices
    /// services)</c> so the controller can create them uniformly.
    ///
    /// The controller owns the lifetime: it constructs a view once, then drives
    /// it through <see cref="Show"/>, <see cref="Hide"/>, <see cref="Tick"/>,
    /// and <see cref="ApplyLayout"/>. Views never poll <c>Screen</c> for
    /// rotation or read <c>Time.deltaTime</c> themselves.
    /// </summary>
    public interface IAppView
    {
        /// <summary>
        /// The view's own GameObject, parented under the safe-area rect.
        /// Destroying it removes the whole screen, which is how the PlayMode
        /// harness tears the app down between tests.
        /// </summary>
        GameObject Root { get; }

        /// <summary>True while this screen is the one on display.</summary>
        bool Visible { get; }

        /// <summary>
        /// Bring the screen up. Called on every navigation to this view, not
        /// only the first, so a view refreshes whatever may have changed while
        /// it was hidden (finds count, settings, the current rule).
        /// </summary>
        void Show();

        /// <summary>
        /// Take the screen down. Called before another view is shown; a view
        /// releases per-visit resources here (preview textures, timers) but
        /// keeps its built UI for the next <see cref="Show"/>.
        /// </summary>
        void Hide();

        /// <summary>
        /// Per-frame update, called by the controller only while the view is
        /// visible.
        /// </summary>
        /// <param name="dt">
        /// Seconds since the previous frame (unscaled — nothing in this app uses
        /// <c>Time.timeScale</c>).
        /// </param>
        void Tick(float dt);

        /// <summary>
        /// Re-lay the screen for a new orientation or form factor. Called once
        /// at construction and again from the controller's resize watch, which
        /// fires on rotation and on desktop window resizes.
        /// </summary>
        /// <param name="portrait">True when the screen is taller than it is wide.</param>
        /// <param name="phone">
        /// <c>UIBuilder.IsPhoneLayout</c> — true for phone-sized screens, where
        /// toolbars wrap to two rows and tables become card lists.
        /// </param>
        void ApplyLayout(bool portrait, bool phone);
    }

    /// <summary>
    /// The long-lived services a view may use, created once by
    /// <c>AppController</c> and handed to every view through the fixed
    /// constructor. Passing this bag rather than a controller singleton keeps
    /// views testable: a test can build one with its own <c>FileStore</c> in a
    /// temporary folder and never touch the user's real data.
    ///
    /// The controller owns and disposes everything in here; views borrow.
    /// </summary>
    public sealed class AppServices
    {
        /// <summary>
        /// Drives the on-screen lattice: play/pause, steps per second, and the
        /// per-tick cell budget. Shared, because only one simulation is on
        /// screen at a time (the evolve preview runs its own).
        /// </summary>
        public SimulationHost Host { get; set; }

        /// <summary>
        /// Uploads engine RGB frames into the texture behind the simulator's
        /// RawImage, and encodes PNGs for Save PNG.
        /// </summary>
        public FrameBlitter Blitter { get; set; }

        /// <summary>
        /// The genetic-algorithm search and its persisted finds. Lives for the
        /// whole session because a search keeps running while the user browses
        /// other screens.
        /// </summary>
        public EvolveHost Evolve { get; set; }

        /// <summary>
        /// Persistent key/value text storage (files on device, PlayerPrefs on
        /// WebGL). The finds log is the only production writer today.
        /// </summary>
        public IAppStore Store { get; set; }

        /// <summary>
        /// Cached 100x100 thumbnails of found rules, rendered at most one per
        /// frame so the finds page never stalls a frame building sixty of them.
        /// </summary>
        public FindsThumbnailer Thumbnails { get; set; }

        /// <summary>
        /// Returns the full determinism self-check report captured at boot, for
        /// the About screen's detail text. A function, not a string, so the
        /// report is read when shown rather than copied at construction.
        /// </summary>
        public Func<string> SelfCheckReport { get; set; }

        /// <summary>
        /// Returns the boot self-check verdict, which About renders as
        /// "Determinism self-check: PASS" or "... FAIL".
        /// </summary>
        public Func<bool> SelfCheckPassed { get; set; }
    }

    /// <summary>
    /// Shared plumbing for every screen: it holds the constructor arguments,
    /// the root GameObject, and the default show/hide behavior, so a concrete
    /// view is only its own layout and logic. Derived views build their UI into
    /// <see cref="Parent"/> and assign <see cref="Root"/> before returning from
    /// their constructor.
    ///
    /// The three constructor parameters are the fixed view constructor the
    /// controller depends on; <c>AppContractsTests</c> fails any view that
    /// declares a different one.
    /// </summary>
    public abstract class AppViewBase : IAppView
    {
        /// <summary>
        /// Records the constructor arguments. Derived views call this first,
        /// then build their UI under <see cref="Parent"/>.
        /// </summary>
        /// <param name="parent">
        /// The safe-area rect (or a test's stand-in) this screen builds into.
        /// </param>
        /// <param name="nav">The one navigator; never null in the running app.</param>
        /// <param name="services">The shared service bag; never null in the running app.</param>
        protected AppViewBase(RectTransform parent, INavigator nav, AppServices services)
        {
            Parent = parent;
            Nav = nav;
            Services = services;
        }

        /// <summary>The RectTransform this view builds its UI into.</summary>
        protected RectTransform Parent { get; }

        /// <summary>The navigator every button on this screen talks to.</summary>
        protected INavigator Nav { get; }

        /// <summary>The shared services this screen may use.</summary>
        protected AppServices Services { get; }

        /// <summary>
        /// This view's root GameObject. Derived constructors assign it as soon
        /// as they have created their panel; until then the view counts as
        /// hidden and <see cref="Show"/> is a no-op rather than a crash.
        /// </summary>
        public GameObject Root { get; protected set; }

        /// <summary>True while the root object is active — the screen on display.</summary>
        public virtual bool Visible => Root != null && Root.activeSelf;

        /// <summary>
        /// Activates <see cref="Root"/>. Override to refresh contents on every
        /// visit, calling <c>base.Show()</c> first.
        /// </summary>
        public virtual void Show()
        {
            if (Root != null)
            {
                Root.SetActive(true);
            }
        }

        /// <summary>
        /// Deactivates <see cref="Root"/>. Override to release per-visit
        /// resources, calling <c>base.Hide()</c> first.
        /// </summary>
        public virtual void Hide()
        {
            if (Root != null)
            {
                Root.SetActive(false);
            }
        }

        /// <summary>
        /// Per-frame work while visible. The base screen is static, so this does
        /// nothing; views with animation or live readouts override it.
        /// </summary>
        /// <param name="dt">Unscaled seconds since the previous frame.</param>
        public virtual void Tick(float dt)
        {
        }

        /// <summary>
        /// Re-lay for a new orientation or form factor. The base screen has no
        /// layout of its own, so this does nothing.
        /// </summary>
        /// <param name="portrait">True when the screen is taller than it is wide.</param>
        /// <param name="phone">True for phone-sized screens (<c>UIBuilder.IsPhoneLayout</c>).</param>
        public virtual void ApplyLayout(bool portrait, bool phone)
        {
        }
    }
}
