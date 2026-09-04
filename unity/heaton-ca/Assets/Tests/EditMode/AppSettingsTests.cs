using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;

namespace HeatonCA.Tests
{
    /// <summary>
    /// AppSettings: PyQt's defaults after Reset, clamped setters and getters,
    /// and the Changed event. PlayerPrefs is machine-global, so every test saves
    /// the developer's real values first and puts them back afterwards.
    /// </summary>
    public class AppSettingsTests
    {
        private static readonly string[] Keys =
        {
            "heatonca.cellSize",
            "heatonca.stepsPerSecond",
            "heatonca.showOverlay",
        };

        private readonly bool[] _had = new bool[3];
        private readonly int[] _values = new int[3];

        [SetUp]
        public void SavePlayerPrefs()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                _had[i] = PlayerPrefs.HasKey(Keys[i]);
                _values[i] = _had[i] ? PlayerPrefs.GetInt(Keys[i]) : 0;
            }
        }

        [TearDown]
        public void RestorePlayerPrefs()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                if (_had[i])
                {
                    PlayerPrefs.SetInt(Keys[i], _values[i]);
                }
                else
                {
                    PlayerPrefs.DeleteKey(Keys[i]);
                }
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void ResetRestoresPyQtDefaults()
        {
            AppSettings.CellSize = 9;
            AppSettings.StepsPerSecond = 7;
            AppSettings.ShowOverlay = false;

            AppSettings.Reset();

            Assert.AreEqual(5, AppSettings.CellSize);
            Assert.AreEqual(30, AppSettings.StepsPerSecond);
            Assert.IsTrue(AppSettings.ShowOverlay);
            Assert.AreEqual(AppSettings.DefaultCellSize, AppSettings.CellSize);
            Assert.AreEqual(AppSettings.DefaultStepsPerSecond, AppSettings.StepsPerSecond);
            Assert.AreEqual(AppSettings.DefaultShowOverlay, AppSettings.ShowOverlay);
        }

        [Test]
        public void ResetDeletesTheThreeKeys()
        {
            AppSettings.CellSize = 9;
            AppSettings.StepsPerSecond = 7;
            AppSettings.ShowOverlay = false;
            foreach (string key in Keys)
            {
                Assert.IsTrue(PlayerPrefs.HasKey(key), key);
            }

            AppSettings.Reset();

            foreach (string key in Keys)
            {
                Assert.IsFalse(PlayerPrefs.HasKey(key), key);
            }
        }

        [Test]
        public void CellSizeClampsToOneThroughTwentyFive()
        {
            AppSettings.CellSize = 0;
            Assert.AreEqual(1, AppSettings.CellSize);
            AppSettings.CellSize = -40;
            Assert.AreEqual(1, AppSettings.CellSize);
            AppSettings.CellSize = 26;
            Assert.AreEqual(25, AppSettings.CellSize);
            AppSettings.CellSize = 1000;
            Assert.AreEqual(25, AppSettings.CellSize);
            AppSettings.CellSize = 12;
            Assert.AreEqual(12, AppSettings.CellSize);
            Assert.AreEqual(1, AppSettings.MinCellSize);
            Assert.AreEqual(25, AppSettings.MaxCellSize);
        }

        [Test]
        public void StepsPerSecondClampsToOneThroughSixty()
        {
            AppSettings.StepsPerSecond = 0;
            Assert.AreEqual(1, AppSettings.StepsPerSecond);
            AppSettings.StepsPerSecond = 61;
            Assert.AreEqual(60, AppSettings.StepsPerSecond);
            AppSettings.StepsPerSecond = 120;
            Assert.AreEqual(60, AppSettings.StepsPerSecond);
            AppSettings.StepsPerSecond = 45;
            Assert.AreEqual(45, AppSettings.StepsPerSecond);
            Assert.AreEqual(1, AppSettings.MinStepsPerSecond);
            Assert.AreEqual(60, AppSettings.MaxStepsPerSecond);
        }

        [Test]
        public void GettersClampStaleStoredValues()
        {
            // A value written by hand, or by an older build with wider bounds,
            // must never put a control outside its own range.
            PlayerPrefs.SetInt("heatonca.cellSize", 200);
            PlayerPrefs.SetInt("heatonca.stepsPerSecond", -5);
            Assert.AreEqual(25, AppSettings.CellSize);
            Assert.AreEqual(1, AppSettings.StepsPerSecond);
        }

        [Test]
        public void ShowOverlayRoundTrips()
        {
            AppSettings.ShowOverlay = false;
            Assert.IsFalse(AppSettings.ShowOverlay);
            Assert.AreEqual(0, PlayerPrefs.GetInt("heatonca.showOverlay", -1));
            AppSettings.ShowOverlay = true;
            Assert.IsTrue(AppSettings.ShowOverlay);
            Assert.AreEqual(1, PlayerPrefs.GetInt("heatonca.showOverlay", -1));
        }

        [Test]
        public void ChangedFiresOnEverySetAndOnReset()
        {
            int fired = 0;
            void Count() => fired++;
            AppSettings.Changed += Count;
            try
            {
                AppSettings.CellSize = 8;
                Assert.AreEqual(1, fired);
                AppSettings.StepsPerSecond = 15;
                Assert.AreEqual(2, fired);
                AppSettings.ShowOverlay = false;
                Assert.AreEqual(3, fired);
                AppSettings.Reset();
                Assert.AreEqual(4, fired);
            }
            finally
            {
                AppSettings.Changed -= Count;
            }

            // Unsubscribed: further changes are silent.
            AppSettings.CellSize = 3;
            Assert.AreEqual(4, fired);
        }
    }
}
