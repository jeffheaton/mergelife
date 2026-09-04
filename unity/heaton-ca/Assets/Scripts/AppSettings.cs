using System;
using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// App-wide user preferences, PlayerPrefs-backed: the three values the PyQt
    /// Settings tab persisted (cell size, animation speed, FPS/steps overlay),
    /// with the same defaults, so a HeatonCA 1.x user recognizes the screen.
    ///
    /// Deliberately small. Everything else the user changes (the current rule,
    /// the evolve threshold while a run lasts) is view state, not a preference,
    /// and belongs to the screen that owns it. Getters clamp what they read so a
    /// hand-edited or stale value can never put a control outside its own range;
    /// setters clamp what they store for the same reason. Every successful set
    /// raises <see cref="Changed"/>, which is how the simulator re-cuts its
    /// lattice and retimes playback when the Settings page saves.
    /// </summary>
    public static class AppSettings
    {
        private const string KeyCellSize = "heatonca.cellSize";
        private const string KeyStepsPerSecond = "heatonca.stepsPerSecond";
        private const string KeyShowOverlay = "heatonca.showOverlay";

        /// <summary>Smallest cell edge, in density-independent units.</summary>
        public const int MinCellSize = 1;

        /// <summary>Largest cell edge, in density-independent units (PyQt's spin box maximum).</summary>
        public const int MaxCellSize = 25;

        /// <summary>PyQt's default cell size.</summary>
        public const int DefaultCellSize = 5;

        /// <summary>Slowest playback, generations per second.</summary>
        public const int MinStepsPerSecond = 1;

        /// <summary>Fastest playback, generations per second (PyQt coupled 30 FPS to frames; the port decouples).</summary>
        public const int MaxStepsPerSecond = 60;

        /// <summary>PyQt's default animation speed.</summary>
        public const int DefaultStepsPerSecond = 30;

        /// <summary>PyQt showed the FPS/steps overlay by default.</summary>
        public const bool DefaultShowOverlay = true;

        /// <summary>
        /// Raised after any setter stores a value and after <see cref="Reset"/>.
        /// Subscribers read the properties again; the event carries no payload.
        /// </summary>
        public static event Action Changed;

        /// <summary>
        /// Cell edge in density-independent units (1..25, default 5): 1/160 inch
        /// on mobile, a 96-dpi logical pixel on desktop and WebGL.
        /// </summary>
        public static int CellSize
        {
            get => Mathf.Clamp(
                PlayerPrefs.GetInt(KeyCellSize, DefaultCellSize), MinCellSize, MaxCellSize);
            set
            {
                PlayerPrefs.SetInt(KeyCellSize, Mathf.Clamp(value, MinCellSize, MaxCellSize));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Playback rate in generations per second (1..60, default 30).</summary>
        public static int StepsPerSecond
        {
            get => Mathf.Clamp(
                PlayerPrefs.GetInt(KeyStepsPerSecond, DefaultStepsPerSecond),
                MinStepsPerSecond, MaxStepsPerSecond);
            set
            {
                PlayerPrefs.SetInt(
                    KeyStepsPerSecond, Mathf.Clamp(value, MinStepsPerSecond, MaxStepsPerSecond));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Whether the simulator draws its "Steps / FPS" overlay (default on).</summary>
        public static bool ShowOverlay
        {
            get => PlayerPrefs.GetInt(KeyShowOverlay, DefaultShowOverlay ? 1 : 0) != 0;
            set
            {
                PlayerPrefs.SetInt(KeyShowOverlay, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Restore defaults by deleting the three keys (the Settings page's
        /// Restore Defaults, and the test hook every PlayMode boot calls so a
        /// developer's own preferences never leak into a test run). Raises
        /// <see cref="Changed"/>.
        /// </summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(KeyCellSize);
            PlayerPrefs.DeleteKey(KeyStepsPerSecond);
            PlayerPrefs.DeleteKey(KeyShowOverlay);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
