using System;
using System.Collections;
using System.IO;
using HeatonCAApp;
using UnityEngine;

namespace HeatonCA.PlayTests
{
    /// <summary>
    /// Boots the app headlessly for a PlayMode test (the heaton-life-unity BootApp
    /// pattern): any App the scene or the runtime bootstrap already created is torn
    /// down, preferences are reset to defaults, a fresh controller is created, and
    /// its persistent storage is redirected to a throwaway folder so a test run
    /// never touches the user's real finds or snapshots.
    ///
    /// Yield <see cref="Boot"/> from a <c>[UnityTest]</c> and read
    /// <c>AppController.Instance</c> afterwards. Every PlayMode test in the suite
    /// starts this way, so the order below is the whole suite's contract.
    /// </summary>
    public static class TestBoot
    {
        /// <summary>Boot a fresh <see cref="AppController"/>; yield this from a [UnityTest].</summary>
        public static IEnumerator Boot()
        {
            bool destroyedAny = false;
            foreach (AppController existing in UnityEngine.Object.FindObjectsByType<AppController>(
                         FindObjectsInactive.Include))
            {
                UnityEngine.Object.Destroy(existing.gameObject);
                destroyedAny = true;
            }
            if (destroyedAny)
            {
                // Destroy is deferred to the end of the frame, and the new controller
                // reuses a live Camera.main / EventSystem: let the old one's objects
                // actually go away before the new one looks.
                yield return null;
            }

            ResetSettings();

            AppController app = new GameObject("App").AddComponent<AppController>();
            yield return null;
            app.UseStorageRoot(Path.Combine(
                Application.temporaryCachePath, "heatonca-tests-" + DateTime.Now.Ticks));
            // One more frame with the redirect in place: the resize watch has run, so
            // every screen is laid out and the store the views read is the test's.
            yield return null;
        }

        /// <summary>
        /// Put the three preference keys back to their defaults before the screens
        /// are built. AppSettings is PlayerPrefs-backed and therefore machine-global:
        /// without this the suite would inherit whatever cell size, speed, and
        /// overlay state the developer last saved in the editor.
        ///
        /// <c>Changed</c> is a static event, so a screen from an earlier test that
        /// subscribed and was then destroyed is still on the invocation list and will
        /// be called here with its Unity objects already gone. That is a leak in the
        /// screen, not a failure of the test that happens to boot next, so the raise
        /// is isolated: the keys are deleted before the event fires, and this boot
        /// creates its screens afterwards.
        /// </summary>
        private static void ResetSettings()
        {
            try
            {
                AppSettings.Reset();
            }
            catch (Exception exception)
            {
                Debug.Log(
                    "[TestBoot] ignoring a stale AppSettings.Changed subscriber from an "
                    + "earlier test: " + exception.GetType().Name);
            }
        }
    }
}
