using System;
using System.Collections.Generic;
using System.Reflection;
using HeatonCAApp;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// Guards the one file every screen reads its text from. These tests are
    /// deliberately mechanical: they hold the whole table to the house rules
    /// (American English, Latin-1 except the three Greek decoder headers, no
    /// empty entries) so a later screen WP cannot quietly introduce a string
    /// that renders as a box on a device or trips the repository spelling check.
    /// </summary>
    public class AppStringsTests
    {
        /// <summary>
        /// British spellings the repository CLAUDE.md forbids. Matched
        /// case-insensitively as substrings, so "Colour" and "colours" are both
        /// caught. Kept in sync with tools/spelling-check.sh.
        /// </summary>
        private static readonly string[] BritishSpellings =
        {
            "colour",
            "grey",
            "centre",
            "behaviour",
            "neighbour",
            "favourite",
            "cancelled",
            "analyse",
            "normalise",
            "initialise",
            "licence",
            "defence",
            "modelling",
            "labelled",
        };

        /// <summary>
        /// The only characters above Latin-1 that may appear anywhere in
        /// AppStrings, and the constant each one is allowed to appear in.
        /// </summary>
        private static readonly Dictionary<char, string> AllowedNonLatin1 = new Dictionary<char, string>
        {
            { 'α', "DecoderHeaderHigh" },
            { 'β', "DecoderHeaderPercent" },
            { 'γ', "DecoderHeaderIndex" },
        };

        /// <summary>Every public string constant on AppStrings, in declaration-independent order.</summary>
        private static List<FieldInfo> Constants()
        {
            List<FieldInfo> found = new List<FieldInfo>();
            FieldInfo[] fields = typeof(AppStrings).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (FieldInfo field in fields)
            {
                if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                {
                    found.Add(field);
                }
            }

            return found;
        }

        /// <summary>Reads a constant's value, failing the test if it is somehow absent.</summary>
        private static string Value(string name)
        {
            FieldInfo field = typeof(AppStrings).GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field, "AppStrings." + name + " is missing");
            return (string)field.GetRawConstantValue();
        }

        [Test]
        public void TheTableIsNotEmpty()
        {
            // A reflection guard that never finds anything would pass every other
            // test in this file, so pin a floor well under the real count.
            Assert.GreaterOrEqual(Constants().Count, 80, "AppStrings lost most of its constants");
        }

        [Test]
        public void EveryConstantIsNonEmpty()
        {
            foreach (FieldInfo field in Constants())
            {
                string value = (string)field.GetRawConstantValue();
                Assert.IsNotNull(value, "AppStrings." + field.Name + " is null");
                Assert.AreNotEqual(string.Empty, value, "AppStrings." + field.Name + " is empty");
                Assert.AreNotEqual(
                    string.Empty, value.Trim(), "AppStrings." + field.Name + " is only whitespace");
            }
        }

        [Test]
        public void NoConstantUsesABritishSpelling()
        {
            foreach (FieldInfo field in Constants())
            {
                string value = (string)field.GetRawConstantValue();
                string haystack = (field.Name + " " + value).ToLowerInvariant();
                foreach (string british in BritishSpellings)
                {
                    Assert.IsFalse(
                        haystack.Contains(british),
                        "AppStrings." + field.Name + " uses the British spelling \"" + british
                            + "\": \"" + value + "\"");
                }
            }
        }

        [Test]
        public void EveryCharacterIsLatin1ExceptTheGreekHeaders()
        {
            foreach (FieldInfo field in Constants())
            {
                string value = (string)field.GetRawConstantValue();
                foreach (char c in value)
                {
                    if ((int)c <= 0xFF)
                    {
                        continue;
                    }

                    string owner;
                    Assert.IsTrue(
                        AllowedNonLatin1.TryGetValue(c, out owner),
                        "AppStrings." + field.Name + " contains U+" + ((int)c).ToString("X4")
                            + ", which LegacyRuntime.ttf is not guaranteed to render");
                    Assert.AreEqual(
                        owner,
                        field.Name,
                        "U+" + ((int)c).ToString("X4") + " belongs only in AppStrings." + owner);
                }
            }
        }

        [Test]
        public void TheThreeGreekHeadersCarryTheirLetter()
        {
            Assert.AreEqual("High (α)", AppStrings.DecoderHeaderHigh);
            Assert.AreEqual("Percent (β)", AppStrings.DecoderHeaderPercent);
            Assert.AreEqual("Index (γ)", AppStrings.DecoderHeaderIndex);
        }

        [Test]
        public void TheGreekHeadersHaveAsciiFallbacks()
        {
            Assert.AreEqual("High (alpha)", AppStrings.DecoderHeaderHighAscii);
            Assert.AreEqual("Percent (beta)", AppStrings.DecoderHeaderPercentAscii);
            Assert.AreEqual("Index (gamma)", AppStrings.DecoderHeaderIndexAscii);

            string[] fallbacks =
            {
                AppStrings.DecoderHeaderHighAscii,
                AppStrings.DecoderHeaderPercentAscii,
                AppStrings.DecoderHeaderIndexAscii,
            };
            foreach (string fallback in fallbacks)
            {
                foreach (char c in fallback)
                {
                    Assert.Less((int)c, 128, "the fallback \"" + fallback + "\" must be pure ASCII");
                }
            }
        }

        [Test]
        public void PhaseOneIdentifiersKeepTheirValues()
        {
            // AppController and the PlayMode smoke test read these by name.
            Assert.AreEqual("HeatonCA", AppStrings.AppName);
            Assert.AreEqual("Welcome to HeatonCA", AppStrings.HomeTitle);
            Assert.AreEqual("v", AppStrings.VersionBadgePrefix);
            Assert.AreEqual("Gallery", AppStrings.HomeGallery);
            Assert.AreEqual("Simulator", AppStrings.HomeSimulator);
            Assert.AreEqual("Evolve", AppStrings.HomeEvolve);
            Assert.AreEqual("Settings", AppStrings.HomeSettings);
            Assert.AreEqual("About", AppStrings.HomeAbout);
            // The device gates grep player logs for these exact lines.
            Assert.AreEqual("[HeatonCA] SELF-CHECK PASS", AppStrings.SelfCheckPass);
            Assert.AreEqual("[HeatonCA] SELF-CHECK FAIL", AppStrings.SelfCheckFail);
        }

        [Test]
        public void PyQtLabelsAreReproducedExactly()
        {
            // The parity matrix promises these read the same as the PyQt app.
            Assert.AreEqual("Run Number:", AppStrings.EvolveRunNumberLabel);
            Assert.AreEqual("Eval Number:", AppStrings.EvolveEvalNumberLabel);
            Assert.AreEqual("Evals/min:", AppStrings.EvolveEvalsPerMinuteLabel);
            Assert.AreEqual("Current Rule:", AppStrings.EvolveCurrentRuleLabel);
            Assert.AreEqual("Current Score:", AppStrings.EvolveCurrentScoreLabel);
            Assert.AreEqual("No improve/Max allowed:", AppStrings.EvolveNoImproveLabel);
            Assert.AreEqual("Rules found:", AppStrings.EvolveRulesFoundLabel);
            Assert.AreEqual("Status:", AppStrings.EvolveStatusLabel);
            Assert.AreEqual("Generating new population: {0}/{1}", AppStrings.EvolveStatusSeedingFormat);
            Assert.AreEqual("Running...", AppStrings.EvolveStatusRunning);
            Assert.AreEqual(
                "No improvement for {0}, stopping run.", AppStrings.EvolveStatusNoImprovementFormat);
            Assert.AreEqual("Stopping...", AppStrings.EvolveStatusStopping);
            Assert.AreEqual("Stopped", AppStrings.EvolveStatusStopped);
            Assert.AreEqual("Cell Size (1-25):", AppStrings.SettingsCellSizeLabel);
            Assert.AreEqual("Display FPS/Steps", AppStrings.SettingsOverlayLabel);
            Assert.AreEqual("Steps: {0}, FPS: {1}", AppStrings.SimOverlayFormat);
        }

        [Test]
        public void EveryFormatConstantCarriesAPlaceholder()
        {
            foreach (FieldInfo field in Constants())
            {
                if (!field.Name.EndsWith("Format", StringComparison.Ordinal))
                {
                    continue;
                }

                string value = (string)field.GetRawConstantValue();
                Assert.IsTrue(
                    value.Contains("{0}"),
                    "AppStrings." + field.Name + " is named a format but has no {0}: \"" + value + "\"");
            }
        }

        [Test]
        public void NonFormatConstantsHaveNoStrayPlaceholder()
        {
            foreach (FieldInfo field in Constants())
            {
                if (field.Name.EndsWith("Format", StringComparison.Ordinal))
                {
                    continue;
                }

                string value = (string)field.GetRawConstantValue();
                Assert.IsFalse(
                    value.Contains("{0}"),
                    "AppStrings." + field.Name + " has a placeholder but is not named a format: \""
                        + value + "\"");
            }
        }

        [Test]
        public void ColorNamesAreTheEightMergeLifeColorsInIndexOrder()
        {
            string[] expected =
            {
                "Black", "Red", "Green", "Yellow", "Blue", "Purple", "Cyan", "White",
            };
            Assert.AreEqual(expected.Length, AppStrings.ColorNames.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], AppStrings.ColorNames[i], "color index " + i);
            }
        }

        [Test]
        public void ConstantValuesAreTrimmed()
        {
            // Leading or trailing space in a label misaligns every uGUI row it
            // lands in, and is invisible in review.
            foreach (FieldInfo field in Constants())
            {
                string value = (string)field.GetRawConstantValue();
                Assert.AreEqual(
                    value, value.Trim(), "AppStrings." + field.Name + " has surrounding whitespace");
            }
        }

        [Test]
        public void ScreenTitlesMatchTheirHomeButtons()
        {
            // Home must not promise a screen by one name and title it another.
            Assert.AreEqual(AppStrings.HomeGallery, AppStrings.GalleryTitle);
            Assert.AreEqual(AppStrings.HomeSimulator, AppStrings.SimulatorTitle);
            Assert.AreEqual(AppStrings.HomeEvolve, AppStrings.EvolveTitle);
            Assert.AreEqual(AppStrings.HomeSettings, AppStrings.SettingsTitle);
            Assert.AreEqual(AppStrings.HomeAbout, AppStrings.AboutTitle);
            Assert.AreEqual("Rule", Value("RuleDecoderTitle"));
        }
    }
}
