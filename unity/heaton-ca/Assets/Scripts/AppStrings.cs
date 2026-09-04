using System.Collections.Generic;

namespace HeatonCAApp
{
    /// <summary>
    /// Every user-visible string in the app, in American English. Views and
    /// services never hard-code UI text; they read it from here so the spelling
    /// check, the PyQt parity review, and any future localization pass have one
    /// file to audit.
    ///
    /// Character set: plain ASCII everywhere except the three Greek letters
    /// alpha (U+03B1), beta (U+03B2), and gamma (U+03B3) in the rule-decoder
    /// headers, which carry the ASCII fallbacks
    /// <see cref="DecoderHeaderHighAscii"/>, <see cref="DecoderHeaderPercentAscii"/>,
    /// and <see cref="DecoderHeaderIndexAscii"/> for fonts that lack them.
    /// Everything else stays inside Latin-1 so LegacyRuntime.ttf renders it on
    /// every platform, including WebGL.
    ///
    /// Constants whose name ends in "Format" are <c>string.Format</c> templates.
    /// Callers format their numbers with <c>CultureInfo.InvariantCulture</c>, so
    /// the readouts match the locale-free PyQt originals.
    /// </summary>
    public static class AppStrings
    {
        #region Product identity

        /// <summary>Product name as displayed (no space, like the store listing).</summary>
        public const string AppName = "HeatonCA";

        /// <summary>Prefix of the version badge in the Home corner: "v" + Application.version.</summary>
        public const string VersionBadgePrefix = "v";

        #endregion

        #region Home

        /// <summary>Home screen title (the PyQt splash tab's heading).</summary>
        public const string HomeTitle = "Welcome to HeatonCA";

        /// <summary>Home button: the 30-rule gallery.</summary>
        public const string HomeGallery = "Gallery";

        /// <summary>Home button: the simulator (PyQt's "Rule Viewer", renamed).</summary>
        public const string HomeSimulator = "Simulator";

        /// <summary>Home button: the evolver.</summary>
        public const string HomeEvolve = "Evolve";

        /// <summary>Home button: settings.</summary>
        public const string HomeSettings = "Settings";

        /// <summary>Home button: about.</summary>
        public const string HomeAbout = "About";

        /// <summary>
        /// Chip shown on Home while a search runs in the background, so leaving
        /// the Evolve screen never hides the fact that the CPU is busy.
        /// </summary>
        public const string HomeEvolvingChip = "Evolving...";

        #endregion

        #region Simulator

        /// <summary>Simulator screen title.</summary>
        public const string SimulatorTitle = "Simulator";

        /// <summary>Simulator toolbar: begin stepping (disables Start and Step).</summary>
        public const string SimStart = "Start";

        /// <summary>Simulator toolbar: stop stepping (re-enables Start and Step).</summary>
        public const string SimStop = "Stop";

        /// <summary>Simulator toolbar: advance one generation; only while stopped.</summary>
        public const string SimStep = "Step";

        /// <summary>Simulator toolbar: re-seed the lattice and zero the step count.</summary>
        public const string SimReset = "Reset";

        /// <summary>Simulator toolbar: open the rule decoder for the current rule.</summary>
        public const string SimRule = "Rule";

        /// <summary>Simulator toolbar: replace the rule with MergeLife.RandomRule.</summary>
        public const string SimRandom = "Random";

        /// <summary>Simulator toolbar: export the current lattice as a PNG.</summary>
        public const string SimSavePng = "Save PNG";

        /// <summary>Simulator toolbar: put the current rule on the clipboard.</summary>
        public const string SimCopyRule = "Copy rule";

        /// <summary>Placeholder inside the empty rule InputField.</summary>
        public const string SimRulePlaceholder = "Rule (8 groups of 4 hex digits)";

        /// <summary>
        /// Held for four seconds under the rule field when the typed text is not
        /// a rule. The typed text is kept so it can be corrected in place.
        /// </summary>
        public const string SimInvalidRule = "Invalid rule: need 32 hex digits (8 groups of 4).";

        /// <summary>
        /// Corner overlay when "Display FPS/Steps" is on: {0} is the generation
        /// count formatted "N0", {1} the measured frames per second.
        /// </summary>
        public const string SimOverlayFormat = "Steps: {0}, FPS: {1}";

        /// <summary>
        /// Second overlay line when CellGeometry raised the cell size to stay
        /// inside the cell cap on a very large canvas; {0} is the size it used.
        /// </summary>
        public const string SimCellSizeRaisedFormat = "cell size raised to {0} to fit this screen";

        /// <summary>Status toast after a desktop or Editor PNG export; {0} is the file name.</summary>
        public const string SimStatusSavedFormat = "saved {0}";

        /// <summary>Status toast after an iOS or Android export into the photo library.</summary>
        public const string SimStatusSavedToPhotos = "saved to Photos";

        /// <summary>Status toast after the rule went to the clipboard.</summary>
        public const string SimStatusCopied = "copied";

        /// <summary>Status toast after a WebGL export handed the PNG to the browser.</summary>
        public const string SimStatusDownloadStarted = "download started";

        #endregion

        #region Gallery

        /// <summary>Gallery screen title.</summary>
        public const string GalleryTitle = "Gallery";

        #endregion

        #region Rule decoder

        /// <summary>Rule decoder screen title (PyQt's "Rule" tab).</summary>
        public const string RuleDecoderTitle = "Rule";

        /// <summary>Heading above the decode table; {0} is the canonical rule.</summary>
        public const string RuleDecoderRuleFormat = "Rule: {0}";

        /// <summary>Decode column 1: the promoted upper limit. Contains alpha (U+03B1).</summary>
        public const string DecoderHeaderHigh = "High (α)";

        /// <summary>Decode column 2: the neighbor-sum range this sub-rule owns.</summary>
        public const string DecoderHeaderRange = "Range";

        /// <summary>Decode column 3: the swatch and name of the color merged toward.</summary>
        public const string DecoderHeaderKeyColor = "Key Color";

        /// <summary>Decode column 4: the merge percentage. Contains beta (U+03B2).</summary>
        public const string DecoderHeaderPercent = "Percent (β)";

        /// <summary>Decode column 5: the raw color index and its name. Contains gamma (U+03B3).</summary>
        public const string DecoderHeaderIndex = "Index (γ)";

        /// <summary>Decode column 6: the raw range octet, hex and signed decimal.</summary>
        public const string DecoderHeaderOctet1 = "Octet-1";

        /// <summary>Decode column 7: the raw percent octet, hex and signed decimal.</summary>
        public const string DecoderHeaderOctet2 = "Octet-2";

        /// <summary>ASCII stand-in for <see cref="DecoderHeaderHigh"/> when the font lacks alpha.</summary>
        public const string DecoderHeaderHighAscii = "High (alpha)";

        /// <summary>ASCII stand-in for <see cref="DecoderHeaderPercent"/> when the font lacks beta.</summary>
        public const string DecoderHeaderPercentAscii = "Percent (beta)";

        /// <summary>ASCII stand-in for <see cref="DecoderHeaderIndex"/> when the font lacks gamma.</summary>
        public const string DecoderHeaderIndexAscii = "Index (gamma)";

        /// <summary>Color index 0. Its label is drawn in white; every other name is drawn in black.</summary>
        public const string ColorBlack = "Black";

        /// <summary>Color index 1.</summary>
        public const string ColorRed = "Red";

        /// <summary>Color index 2.</summary>
        public const string ColorGreen = "Green";

        /// <summary>Color index 3.</summary>
        public const string ColorYellow = "Yellow";

        /// <summary>Color index 4.</summary>
        public const string ColorBlue = "Blue";

        /// <summary>Color index 5.</summary>
        public const string ColorPurple = "Purple";

        /// <summary>Color index 6.</summary>
        public const string ColorCyan = "Cyan";

        /// <summary>Color index 7.</summary>
        public const string ColorWhite = "White";

        /// <summary>
        /// The eight MergeLife color names in index order, matching PyQt's
        /// RuleTab.COLOR_NAMES. Index into this with a decoded ColorIndex.
        /// </summary>
        public static readonly IReadOnlyList<string> ColorNames = new[]
        {
            ColorBlack,
            ColorRed,
            ColorGreen,
            ColorYellow,
            ColorBlue,
            ColorPurple,
            ColorCyan,
            ColorWhite,
        };

        /// <summary>Decoder button: run the decoded rule in the simulator.</summary>
        public const string RuleDecoderOpenInSimulator = "Open in Simulator";

        /// <summary>Decoder button: put the decoded rule on the clipboard.</summary>
        public const string RuleDecoderCopyRule = "Copy rule";

        /// <summary>
        /// Documents the one deliberate divergence from the PyQt table: this port
        /// prints the octets the rule string actually carries.
        /// </summary>
        public const string RuleDecoderFootnote =
            "Octets are shown raw; the original HeatonCA re-derived Octet-1 from the promoted limit.";

        #endregion

        #region Evolve

        /// <summary>Evolve screen title.</summary>
        public const string EvolveTitle = "Evolve";

        /// <summary>Evolve button: begin a search with a fresh master seed.</summary>
        public const string EvolveStart = "Start";

        /// <summary>Evolve button: ask the search to stop at the next evaluation boundary.</summary>
        public const string EvolveStop = "Stop";

        /// <summary>Readout label: how many restart-forever runs have begun.</summary>
        public const string EvolveRunNumberLabel = "Run Number:";

        /// <summary>Readout label: how many genome evaluations have completed.</summary>
        public const string EvolveEvalNumberLabel = "Eval Number:";

        /// <summary>Readout label: evaluation throughput, recomputed once a minute.</summary>
        public const string EvolveEvalsPerMinuteLabel = "Evals/min:";

        /// <summary>Readout label: the best rule of the run in progress.</summary>
        public const string EvolveCurrentRuleLabel = "Current Rule:";

        /// <summary>Readout label: that rule's score against the save threshold.</summary>
        public const string EvolveCurrentScoreLabel = "Current Score:";

        /// <summary>Readout label: evaluations since the last improvement, over the patience.</summary>
        public const string EvolveNoImproveLabel = "No improve/Max allowed:";

        /// <summary>Readout label: rules that have ever cleared the threshold.</summary>
        public const string EvolveRulesFoundLabel = "Rules found:";

        /// <summary>Readout label: the status line.</summary>
        public const string EvolveStatusLabel = "Status:";

        /// <summary>
        /// Readout label: how many finds are kept on this device. Replaces PyQt's
        /// "Output Directory:", because the finds live in the app, not a folder.
        /// </summary>
        public const string EvolveSavedFindsLabel = "Saved finds:";

        /// <summary>Evolve button: open the persisted finds gallery.</summary>
        public const string EvolveFindsButton = "Finds";

        /// <summary>Label above the threshold slider (-1 to 5, default 3.5).</summary>
        public const string EvolveThresholdLabel = "Threshold";

        /// <summary>Status while the first population is being generated; {0} of {1}.</summary>
        public const string EvolveStatusSeedingFormat = "Generating new population: {0}/{1}";

        /// <summary>Status while evaluations are in flight.</summary>
        public const string EvolveStatusRunning = "Running...";

        /// <summary>Status when patience ran out and the run restarts; {0} is the patience.</summary>
        public const string EvolveStatusNoImprovementFormat = "No improvement for {0}, stopping run.";

        /// <summary>Status between the Stop press and the next evaluation boundary.</summary>
        public const string EvolveStatusStopping = "Stopping...";

        /// <summary>Status once the search has fully stopped.</summary>
        public const string EvolveStatusStopped = "Stopped";

        /// <summary>Status while the app is backgrounded on a mobile device.</summary>
        public const string EvolveStatusPausedApp = "Paused (app in background)";

        /// <summary>Status while the browser tab has lost focus on WebGL.</summary>
        public const string EvolveStatusPausedBrowser = "Paused (browser tab in background)";

        /// <summary>Status toast after a find was exported; {0} is the rule (the file stem).</summary>
        public const string EvolveSavedPngFormat = "Saved {0}.png";

        /// <summary>Status toast when an export failed; {0} is the reason.</summary>
        public const string EvolveSaveFailedFormat = "Save failed: {0}";

        /// <summary>
        /// Shown on WebGL, where the search shares the single browser thread and
        /// one long-lived rule costs a visibly long frame.
        /// </summary>
        public const string EvolveBrowserNote =
            "Browser mode: single-threaded search; long-lived rules cause pauses.";

        /// <summary>Shown on mobile, where a long search is a real power cost.</summary>
        public const string EvolveBatteryNote =
            "Evolve uses all available CPU cores and will drain the battery.";

        /// <summary>Finds page heading.</summary>
        public const string FindsTitle = "Finds";

        /// <summary>Caption under a find's thumbnail; {0} is the score "N2", {1} the run number "N0".</summary>
        public const string FindsScoreRunFormat = "score {0} - run {1}";

        /// <summary>Find card button: run this rule in the simulator; the search keeps going.</summary>
        public const string FindsOpenInSimulator = "Open in Simulator";

        /// <summary>Find card button: export this find as &lt;rule&gt;.png at 4x.</summary>
        public const string FindsSavePng = "Save PNG";

        /// <summary>Find card button: forget this find (confirmed, and tombstoned so it stays gone).</summary>
        public const string FindsDelete = "Delete";

        /// <summary>Finds page button: forget every find (confirmed).</summary>
        public const string FindsClearAll = "Clear all";

        /// <summary>Shown instead of the card list when nothing has cleared the threshold yet.</summary>
        public const string FindsEmpty =
            "No finds yet. Start a search and every rule scoring above the threshold is saved here.";

        /// <summary>Title of the confirm shown before a single find is deleted.</summary>
        public const string FindsDeleteConfirmTitle = "Delete find?";

        /// <summary>Body of that confirm; {0} is the rule being deleted.</summary>
        public const string FindsDeleteConfirmBodyFormat =
            "Delete {0} from your saved finds? This cannot be undone.";

        /// <summary>Title of the confirm shown before every find is deleted.</summary>
        public const string FindsClearAllConfirmTitle = "Clear all finds?";

        /// <summary>Body of that confirm.</summary>
        public const string FindsClearAllConfirmBody =
            "Delete every saved find? This cannot be undone.";

        #endregion

        #region Settings

        /// <summary>Settings screen title.</summary>
        public const string SettingsTitle = "Settings";

        /// <summary>Settings field: cell size in density-independent units, default 5.</summary>
        public const string SettingsCellSizeLabel = "Cell Size (1-25):";

        /// <summary>
        /// Settings field: generations per second, default 30. PyQt called this
        /// FPS because it stepped once per frame; here the two are decoupled.
        /// </summary>
        public const string SettingsSpeedLabel = "Animation Speed (1-60 gen/s):";

        /// <summary>Settings toggle: the simulator's corner overlay, default on.</summary>
        public const string SettingsOverlayLabel = "Display FPS/Steps";

        /// <summary>Settings button: apply and persist.</summary>
        public const string SettingsSave = "Save";

        /// <summary>Settings button: discard the edits and leave.</summary>
        public const string SettingsCancel = "Cancel";

        /// <summary>Settings button: put every field back to its shipped default.</summary>
        public const string SettingsRestoreDefaults = "Restore Defaults";

        /// <summary>Note under the buttons: Save takes effect without a restart.</summary>
        public const string SettingsNote =
            "Changes apply to the running simulator without restarting it.";

        #endregion

        #region About

        /// <summary>About screen title.</summary>
        public const string AboutTitle = "About";

        /// <summary>Version line; {0} is Application.version, {1} the build number.</summary>
        public const string AboutVersionFormat = "Version {0} (build {1})";

        /// <summary>Build stamp line; {0} is the UTC timestamp baked in by BuildInfo.</summary>
        public const string AboutBuiltFormat = "Built {0}";

        /// <summary>Lead-in above the citation of the 2018 paper.</summary>
        public const string AboutPaperIntro =
            "This program implements the cellular automata described in the paper:";

        /// <summary>About button: the MergeLife tutorial page.</summary>
        public const string AboutTutorial = "Tutorial";

        /// <summary>About button: this app's manual.</summary>
        public const string AboutManual = "Manual";

        /// <summary>About button: the privacy policy.</summary>
        public const string AboutPrivacy = "Privacy policy";

        /// <summary>About button: the public repository.</summary>
        public const string AboutSourceCode = "Source code";

        /// <summary>About button: the paper's DOI.</summary>
        public const string AboutDoi = "DOI";

        /// <summary>Self-check line; {0} is <see cref="AboutSelfCheckPassWord"/> or <see cref="AboutSelfCheckFailWord"/>.</summary>
        public const string AboutSelfCheckFormat = "Determinism self-check: {0}";

        /// <summary>Fills <see cref="AboutSelfCheckFormat"/> when every embedded check reproduced its pin.</summary>
        public const string AboutSelfCheckPassWord = "PASS";

        /// <summary>Fills <see cref="AboutSelfCheckFormat"/> when any embedded check did not.</summary>
        public const string AboutSelfCheckFailWord = "FAIL";

        /// <summary>Header of the collapsible third-party license section.</summary>
        public const string AboutThirdPartyNotices = "Third-party notices";

        #endregion

        #region Common

        /// <summary>Navigation button on every non-Home screen.</summary>
        public const string Back = "Back";

        /// <summary>Confirming button of an alert or a confirm dialog.</summary>
        public const string Ok = "OK";

        /// <summary>Dismissing button of a confirm dialog.</summary>
        public const string Cancel = "Cancel";

        #endregion

        #region Determinism self-check (device gates grep the log for these)

        /// <summary>Log line when every embedded determinism check reproduced its pin.</summary>
        public const string SelfCheckPass = "[HeatonCA] SELF-CHECK PASS";

        /// <summary>Log line when any embedded determinism check failed.</summary>
        public const string SelfCheckFail = "[HeatonCA] SELF-CHECK FAIL";

        #endregion
    }
}
