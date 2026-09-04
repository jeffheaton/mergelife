using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HeatonCA.PlayTests
{
    /// <summary>
    /// The Evolve screen and its finds page, driven the way a user drives them.
    ///
    /// The screen is built standalone here -- its own canvas, its own
    /// <see cref="EvolveHost"/>, its own file store in a throwaway folder -- rather
    /// than through <c>AppController</c>: these tests need a canvas of a chosen
    /// size (the phone layout check) and a search they can hold still one chunk at a
    /// time (the vector replay), neither of which a live controller offers. The
    /// controller's own wiring of this screen is covered by
    /// <c>AppSmokeTests.BootsAndVisitsEveryScreen</c>. <see cref="TestBoot.Boot"/>
    /// still runs first, so the app the PlayMode bootstrap creates has its
    /// preferences reset and its storage redirected away from the developer's real
    /// finds.
    /// </summary>
    public class EvolveViewTests
    {
        // The vendored evolve/mini-run-24 vector, replayed through the screen.
        private const int MiniEdge = 24;
        private const int MiniPopulation = 8;
        private const int MiniCycles = 1;
        private const int MiniMaxSteps = 120;
        private const ulong MiniSeed = 123;
        private const int MiniPatience = 1000;
        private const int MiniTournament = 3;
        private const int MiniChunkEvals = 4;
        private const int MiniEvals = 20;
        private const string MiniBestGenome = "75f6-9a96-f442-05ee-f65c-cbec-23ee-de46";

        /// <summary>Seconds a pumped search may take before the test calls it stalled.</summary>
        private const float PumpTimeoutSeconds = 90f;

        private const float DesktopWidth = 1600f;
        private const float DesktopHeight = 900f;
        private const float PhoneWidth = 420f;
        private const float PhoneHeight = 900f;

        private Harness _harness;

        [UnityTearDown]
        public IEnumerator TearDownHarness()
        {
            yield return DisposeHarness();
        }

        /// <summary>
        /// The whole screen replays the mini-run vector: after its twenty
        /// evaluations the Eval Number row reads 20 and the Current Rule row carries
        /// the vector's champion. The GA is pinned in EditMode; this proves the
        /// readouts are wired to the same run and not to a prettier summary of it.
        /// </summary>
        [UnityTest]
        public IEnumerator MiniRunReplaysThroughTheScreen()
        {
            yield return TestBoot.Boot();
            _harness = NewHarness(NewRoot(), DesktopWidth, DesktopHeight, false, false);
            _harness.Host.Runner = new ThreadedChunkRunner();
            StartMiniRun(_harness.View);

            yield return PumpTo(_harness.View, _harness.Host, MiniEvals);

            Assert.AreEqual("20", _harness.View.EvalNumberText, "Eval Number");
            Assert.AreEqual(MiniBestGenome, _harness.View.CurrentRuleText, "Current Rule");
            Assert.AreEqual("1", _harness.View.RunNumberText, "Run Number");
            Assert.AreEqual(MiniBestGenome, _harness.View.PreviewRule, "the preview follows the best");
            Assert.IsTrue(_harness.View.PreviewImage.enabled, "the preview is on screen");
        }

        /// <summary>
        /// PyQt's number formats, which are locale-free f-strings: the score against
        /// the threshold with two decimals, the stall count over the patience, and
        /// grouped thousands on the counters.
        /// </summary>
        [UnityTest]
        public IEnumerator EvolveReadoutsUsePyQtFormats()
        {
            yield return TestBoot.Boot();
            _harness = NewHarness(NewRoot(), DesktopWidth, DesktopHeight, false, false);

            _harness.View.RenderReadouts(new EvolveHost.Snapshot
            {
                Evals = 1234,
                EvalsPerMinute = 0,
                Run = 7,
                RunLow = 7,
                RunHigh = 7,
                TotalRunsStarted = 7,
                NoImprovement = 12,
                Patience = 250,
                BestScore = 2.87,
                BestGenome = GalleryCatalog.Presets[0],
                Population = new List<(string Genome, double Score)>(),
                SeedingProgress = (8, 8),
                TotalFound = 1234,
                Status = EvolveHost.EvolveStatus.Running,
            });

            Assert.AreEqual("2.87/3.50", _harness.View.CurrentScoreText, "Current Score");
            Assert.AreEqual("12/250", _harness.View.NoImproveText, "No improve/Max allowed");
            Assert.AreEqual("1,234", _harness.View.RulesFoundText, "Rules found");
            Assert.AreEqual("1,234", _harness.View.EvalNumberText, "Eval Number");
            Assert.AreEqual("0.00", _harness.View.EvalsPerMinuteText, "Evals/min");
            Assert.AreEqual("7", _harness.View.RunNumberText, "Run Number");
            Assert.AreEqual(GalleryCatalog.Presets[0], _harness.View.CurrentRuleText, "Current Rule");
            Assert.AreEqual(AppStrings.EvolveStatusRunning, _harness.View.StatusText, "Status");
        }

        /// <summary>
        /// PyQt's three-state transport: Stop is dead until a search runs, both
        /// buttons are dead between the Stop press and the last chunk finishing
        /// ("Stopping..."), and Start comes back with "Stopped" in the status field.
        /// </summary>
        [UnityTest]
        public IEnumerator EvolveStopDisablesBothButtonsThenReenablesStart()
        {
            yield return TestBoot.Boot();
            _harness = NewHarness(NewRoot(), DesktopWidth, DesktopHeight, false, false);
            _harness.Host.Runner = new ThreadedChunkRunner();

            Assert.IsTrue(_harness.View.StartButton.interactable, "Start before any run");
            Assert.IsFalse(_harness.View.StopButton.interactable, "Stop before any run");

            StartMiniRun(_harness.View);
            Assert.IsFalse(_harness.View.StartButton.interactable, "Start while running");
            Assert.IsTrue(_harness.View.StopButton.interactable, "Stop while running");

            _harness.View.StopButton.onClick.Invoke();
            Assert.IsFalse(_harness.View.StartButton.interactable, "Start while stopping");
            Assert.IsFalse(_harness.View.StopButton.interactable, "Stop while stopping");
            Assert.AreEqual(AppStrings.EvolveStatusStopping, _harness.View.StatusText, "Status");

            yield return PumpToStopped(_harness.View, _harness.Host);

            Assert.IsTrue(_harness.View.StartButton.interactable, "Start after the search stopped");
            Assert.IsFalse(_harness.View.StopButton.interactable, "Stop after the search stopped");
            Assert.AreEqual(AppStrings.EvolveStatusStopped, _harness.View.StatusText, "Status");
        }

        /// <summary>
        /// The finds gallery is the user's collection, not one session's output: a
        /// log written by an earlier run of the app comes back, card for card, when
        /// the screen is rebuilt over the same storage folder.
        /// </summary>
        [UnityTest]
        public IEnumerator FindsPersistAcrossRestart()
        {
            yield return TestBoot.Boot();
            string root = NewRoot();
            string best = GalleryCatalog.Presets[0];
            string second = GalleryCatalog.Presets[2];
            WriteFindsLog(root, 2, new[] { (best, 4.10), (second, 3.75) });

            _harness = NewHarness(root, DesktopWidth, DesktopHeight, false, false);
            _harness.View.ShowFinds();
            Assert.IsTrue(_harness.View.FindsVisible, "the finds page is up");
            Assert.AreEqual(2, _harness.View.Finds.CardCount, "cards before the restart");
            Assert.AreEqual(best, _harness.View.Finds.Cards[0].Rule, "best score first");
            Assert.IsFalse(_harness.View.Finds.EmptyVisible, "the empty state is not shown");
            Assert.AreEqual("2", _harness.View.SavedFindsText, "Saved finds");

            yield return DisposeHarness();

            _harness = NewHarness(root, DesktopWidth, DesktopHeight, false, false);
            _harness.View.ShowFinds();
            Assert.AreEqual(2, _harness.View.Finds.CardCount, "cards after the restart");
            CollectionAssert.AreEquivalent(
                new[] { best, second }, RulesOf(_harness.View.Finds), "the rules that came back");
        }

        /// <summary>
        /// Delete is final: the card goes, and the rule is tombstoned so neither the
        /// live population nor a later app launch can bring it back.
        /// </summary>
        [UnityTest]
        public IEnumerator FindDeleteStaysDeleted()
        {
            yield return TestBoot.Boot();
            string root = NewRoot();
            string kept = GalleryCatalog.Presets[0];
            string doomed = GalleryCatalog.Presets[2];
            WriteFindsLog(root, 2, new[] { (kept, 4.10), (doomed, 3.75) });

            _harness = NewHarness(root, DesktopWidth, DesktopHeight, false, false);
            _harness.View.ShowFinds();
            Assert.AreEqual(doomed, _harness.View.Finds.Cards[1].Rule, "the card being deleted");

            _harness.View.Finds.Cards[1].DeleteButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, _harness.Nav.Confirms, "Delete asks first");
            Assert.AreEqual(1, _harness.View.Finds.CardCount, "cards after the delete");
            Assert.AreEqual(kept, _harness.View.Finds.Cards[0].Rule, "the surviving card");

            yield return DisposeHarness();

            _harness = NewHarness(root, DesktopWidth, DesktopHeight, false, false);
            _harness.View.ShowFinds();
            Assert.AreEqual(1, _harness.View.Finds.CardCount, "cards after the restart");
            CollectionAssert.DoesNotContain(
                RulesOf(_harness.View.Finds), doomed, "a deleted find must stay deleted");
        }

        /// <summary>
        /// Opening a find hands the rule to the simulator auto-started, and leaves
        /// the search breeding: browsing the gallery must not cost the run that
        /// bred it (Home carries the "Evolving..." chip while it keeps going).
        /// </summary>
        [UnityTest]
        public IEnumerator FindOpensSimulatorAutoStartedWithoutStoppingTheSearch()
        {
            yield return TestBoot.Boot();
            string root = NewRoot();
            string find = GalleryCatalog.Presets[0];
            WriteFindsLog(root, 1, new[] { (find, 4.10) });

            _harness = NewHarness(root, DesktopWidth, DesktopHeight, false, false);
            _harness.Host.Runner = new ThreadedChunkRunner();
            // Nothing this run breeds may reach the log, so the page holds exactly
            // the one card the test seeded.
            _harness.View.ThresholdSlider.value = (float)EvolveHost.MaxThreshold;
            StartMiniRun(_harness.View);
            Assert.IsTrue(_harness.Host.Running, "the search is running");

            _harness.View.ShowFinds();
            Assert.AreEqual(1, _harness.View.Finds.CardCount, "the seeded card");

            _harness.View.Finds.Cards[0].OpenButton.onClick.Invoke();

            Assert.AreEqual(1, _harness.Nav.SimulatorCalls, "Open in Simulator navigates once");
            Assert.AreEqual(find, _harness.Nav.SimulatorRule, "the rule handed over");
            Assert.IsTrue(_harness.Nav.SimulatorAutoStart, "the simulator auto-starts");
            Assert.IsTrue(_harness.Host.Running, "opening a find must not stop the search");

            yield return null;
            Assert.IsTrue(_harness.Host.Running, "the search is still running a frame later");
        }

        /// <summary>
        /// The readouts stay out from under the controls in both layouts: the wide
        /// two-column form and the phone's stacked one, where a 39-character rule
        /// and a 24-character label have to share about 400 units.
        /// </summary>
        [UnityTest]
        public IEnumerator EvolveScreenTextStaysClearOfItsControls()
        {
            yield return TestBoot.Boot();

            _harness = NewHarness(NewRoot(), DesktopWidth, DesktopHeight, false, false);
            yield return Settle(_harness.View);
            AssertTextIsClearOfControls(_harness.View, "desktop");

            yield return DisposeHarness();

            _harness = NewHarness(NewRoot(), PhoneWidth, PhoneHeight, true, true);
            yield return Settle(_harness.View);
            AssertTextIsClearOfControls(_harness.View, "phone");
        }

        // ---- helpers -------------------------------------------------------------

        private static void StartMiniRun(EvolveView view) =>
            view.StartRun(
                MiniEdge,
                MiniEdge,
                MiniPopulation,
                MiniCycles,
                MiniMaxSteps,
                MiniSeed,
                MiniPatience,
                lanes: 1,
                chunkEvals: MiniChunkEvals,
                tournamentRounds: MiniTournament);

        /// <summary>
        /// Pump the screen until the search has settled on a chunk boundary at or
        /// past <paramref name="targetEvals"/>. The host is held suspended between
        /// chunks and released one chunk at a time (the EditMode suite's technique),
        /// so the readouts are read at a real boundary rather than mid-chunk, where
        /// a worker's per-evaluation publish runs one entry short.
        /// </summary>
        private static IEnumerator PumpTo(EvolveView view, EvolveHost host, int targetEvals)
        {
            float deadline = Time.realtimeSinceStartup + PumpTimeoutSeconds;
            host.Suspend();
            while (true)
            {
                view.Tick(Time.unscaledDeltaTime); // held: reaps and publishes, launches nothing
                int evals = host.Latest != null ? host.Latest.Evals : 0;
                if (!host.Busy)
                {
                    if (evals >= targetEvals)
                    {
                        view.Tick(0f); // settle the rows on the final snapshot
                        yield break;
                    }
                    host.Resume();
                    view.Tick(Time.unscaledDeltaTime);
                    host.Suspend();
                }
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail(
                        "the search stalled at " + evals + " of " + targetEvals + " evaluations");
                }
                yield return null;
            }
        }

        private static IEnumerator PumpToStopped(EvolveView view, EvolveHost host)
        {
            float deadline = Time.realtimeSinceStartup + PumpTimeoutSeconds;
            while (host.Running)
            {
                view.Tick(Time.unscaledDeltaTime);
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("the search never came to a stop");
                }
                yield return null;
            }
            view.Tick(0f);
        }

        /// <summary>Fill the rows with representative text and let the layout settle.</summary>
        private static IEnumerator Settle(EvolveView view)
        {
            view.RenderReadouts(new EvolveHost.Snapshot
            {
                Evals = 123456,
                EvalsPerMinute = 987,
                Run = 12,
                RunLow = 12,
                RunHigh = 12,
                TotalRunsStarted = 12,
                NoImprovement = 249,
                Patience = 250,
                BestScore = 3.98,
                BestGenome = GalleryCatalog.Presets[0],
                Population = new List<(string Genome, double Score)>(),
                SeedingProgress = (100, 100),
                TotalFound = 4321,
                Status = EvolveHost.EvolveStatus.Running,
            });
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.ReadoutContent);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertTextIsClearOfControls(EvolveView view, string mode)
        {
            Rect root = WorldRect((RectTransform)view.Root.transform);
            Rect viewport = WorldRect((RectTransform)view.ReadoutScroll.transform);
            Rect start = WorldRect((RectTransform)view.StartButton.transform);
            Rect stop = WorldRect((RectTransform)view.StopButton.transform);
            Rect finds = WorldRect((RectTransform)view.FindsButton.transform);
            Rect slider = WorldRect((RectTransform)view.ThresholdSlider.transform);
            Rect preview = WorldRect(view.PreviewArea);

            Assert.IsFalse(start.Overlaps(stop), mode + ": Start overlaps Stop");
            Assert.IsFalse(
                viewport.Overlaps(WorldRect(view.BottomBar)),
                mode + ": the readouts run under the transport bar");
            Assert.IsFalse(
                viewport.Overlaps(WorldRect(view.TopBar)),
                mode + ": the readouts run under the title bar");
            Assert.IsFalse(
                viewport.Overlaps(preview), mode + ": the readouts run under the preview");

            foreach (EvolveView.ReadoutRow row in view.Readouts)
            {
                AssertLabelIsClear(row.Caption, row.Label + " label", mode, root, viewport, finds, start, stop, preview);
                AssertLabelIsClear(row.Value, row.Label + " value", mode, root, viewport, finds, start, stop, preview);
            }

            Rect caption = WorldRect((RectTransform)view.ThresholdCaption.transform);
            Rect value = WorldRect((RectTransform)view.ThresholdValue.transform);
            Assert.IsFalse(caption.Overlaps(slider), mode + ": the threshold label sits on its slider");
            Assert.IsFalse(value.Overlaps(slider), mode + ": the threshold value sits on its slider");
            Assert.IsFalse(caption.Overlaps(value), mode + ": the threshold label and value collide");
        }

        private static void AssertLabelIsClear(
            Text text,
            string what,
            string mode,
            Rect root,
            Rect viewport,
            Rect finds,
            Rect start,
            Rect stop,
            Rect preview)
        {
            Rect rect = WorldRect((RectTransform)text.transform);
            Assert.Greater(rect.width, 0f, mode + ": " + what + " has no width");
            Assert.GreaterOrEqual(
                rect.xMin, root.xMin - 0.5f, mode + ": " + what + " runs off the left edge");
            Assert.LessOrEqual(
                rect.xMax, root.xMax + 0.5f, mode + ": " + what + " runs off the right edge");
            Assert.IsFalse(rect.Overlaps(finds), mode + ": " + what + " runs under the Finds button");
            if (!rect.Overlaps(viewport))
            {
                return; // scrolled out of sight; the mask, not the layout, governs
            }
            Assert.IsFalse(rect.Overlaps(start), mode + ": " + what + " runs under Start");
            Assert.IsFalse(rect.Overlaps(stop), mode + ": " + what + " runs under Stop");
            Assert.IsFalse(rect.Overlaps(preview), mode + ": " + what + " runs under the preview");
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static List<string> RulesOf(FindsView page)
        {
            var rules = new List<string>();
            foreach (FindsView.FindCard card in page.Cards)
            {
                rules.Add(card.Rule);
            }
            return rules;
        }

        private static string NewRoot() => Path.Combine(
            Application.temporaryCachePath, "heatonca-evolveview-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Write a finds log straight into a store, the way an earlier session would
        /// have left one. The schema is <see cref="EvolveHost"/>'s.
        /// </summary>
        private static void WriteFindsLog(string root, int totalFound, (string Rule, double Score)[] finds)
        {
            var entries = new List<object>();
            foreach ((string rule, double score) in finds)
            {
                entries.Add(new Dictionary<string, object>
                {
                    ["rule"] = rule,
                    ["score"] = score,
                    ["foundAtEval"] = 1,
                    ["run"] = 1,
                });
            }
            var store = new FileStore(root);
            store.WriteText(EvolveHost.StoreKey, AppJson.Write(new Dictionary<string, object>
            {
                ["schema"] = EvolveHost.Schema,
                ["totalFound"] = totalFound,
                ["finds"] = entries,
                ["deleted"] = new List<object>(),
            }));
            store.Flush();
        }

        private Harness NewHarness(
            string storeRoot, float width, float height, bool portrait, bool phone)
        {
            var canvasGo = new GameObject(
                "EvolveTestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform parent = UIBuilder.CreatePanel(canvasGo.transform, "Parent");
            parent.anchorMin = new Vector2(0.5f, 0.5f);
            parent.anchorMax = new Vector2(0.5f, 0.5f);
            parent.pivot = new Vector2(0.5f, 0.5f);
            parent.anchoredPosition = Vector2.zero;
            parent.sizeDelta = new Vector2(width, height);

            var store = new FileStore(storeRoot);
            var host = new EvolveHost { Workers = 1 };
            host.SetStore(store);
            var services = new AppServices
            {
                Host = new SimulationHost(),
                Blitter = new FrameBlitter(),
                Evolve = host,
                Store = store,
                Thumbnails = new FindsThumbnailer(),
                SelfCheckReport = () => string.Empty,
                SelfCheckPassed = () => true,
            };
            var nav = new RecordingNavigator();
            var view = new EvolveView(parent, nav, services) { PumpsSearch = true };
            view.ApplyLayout(portrait, phone);
            view.Show();
            return new Harness
            {
                CanvasGo = canvasGo,
                Host = host,
                Services = services,
                Nav = nav,
                View = view,
            };
        }

        private IEnumerator DisposeHarness()
        {
            Harness harness = _harness;
            _harness = null;
            if (harness == null)
            {
                yield break;
            }
            if (harness.Host != null && harness.Host.Running)
            {
                harness.Host.RequestStop();
                float deadline = Time.realtimeSinceStartup + PumpTimeoutSeconds;
                while (harness.Host.Running && Time.realtimeSinceStartup < deadline)
                {
                    harness.Host.Tick();
                    yield return null;
                }
            }
            if (harness.CanvasGo != null)
            {
                UnityEngine.Object.Destroy(harness.CanvasGo);
            }
            yield return null;
            harness.Services?.Thumbnails?.Dispose();
            harness.Services?.Blitter?.Dispose();
            harness.Services?.Host?.Unload();
        }

        private sealed class Harness
        {
            public GameObject CanvasGo;
            public EvolveHost Host;
            public AppServices Services;
            public RecordingNavigator Nav;
            public EvolveView View;
        }

        /// <summary>
        /// A navigator that records instead of navigating, and answers every
        /// confirmation the same way, so a destructive action can be driven from a
        /// test without a dialog on screen.
        /// </summary>
        private sealed class RecordingNavigator : INavigator
        {
            public bool ConfirmAnswer = true;
            public int SimulatorCalls;
            public string SimulatorRule;
            public bool SimulatorAutoStart;
            public int BackCalls;
            public int Confirms;
            public string LastStatus;

            public void ShowHome()
            {
            }

            public void ShowGallery()
            {
            }

            public void ShowSimulator(string rule = null, bool autoStart = false)
            {
                SimulatorCalls++;
                SimulatorRule = rule;
                SimulatorAutoStart = autoStart;
            }

            public void ShowRuleDecoder(string rule)
            {
            }

            public void ShowEvolve()
            {
            }

            public void ShowSettings()
            {
            }

            public void ShowAbout()
            {
            }

            public void Back() => BackCalls++;

            public Task ShowAlertAsync(string title, string message) => Task.CompletedTask;

            public Task<bool> ShowConfirmAsync(
                string title,
                string message,
                string ok = AppStrings.Ok,
                string cancel = AppStrings.Cancel)
            {
                Confirms++;
                return Task.FromResult(ConfirmAnswer);
            }

            public void Status(string text) => LastStatus = text;

            public void OpenUrl(string url)
            {
            }
        }
    }
}
