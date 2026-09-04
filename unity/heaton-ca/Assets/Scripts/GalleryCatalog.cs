using System;
using System.Collections.Generic;
using HeatonCA.Engine;

namespace HeatonCAApp
{
    /// <summary>
    /// The rules the app ships: the 30-rule gallery, the 3 Simulator presets,
    /// and the display names the engine's featured catalog supplies for some of
    /// them.
    ///
    /// The gallery list and its order come from the PyQt app
    /// (python/application/pyqt/tab_gallery.py, GALLERY_RULES) and are
    /// transcribed here verbatim — the pre-rendered previews under
    /// Resources/Gallery are keyed by these exact strings, and
    /// GalleryCatalogTests re-reads that Python file to prove the two never
    /// drift. The presets are that app's tab_simulate.py RULES.
    ///
    /// Names come from <see cref="MergeLifeGallery"/>, the engine's
    /// cross-implementation featured set. Thirteen of the thirty gallery rules
    /// are named there; the rest show their hex alone, as PyQt did for all of
    /// them. Two of the thirty are near-duplicates of named rules (one octet
    /// apart from High Noon and from Mood Ring) and are deliberately kept: they
    /// are what the gallery shipped with, and they show how little a rule has to
    /// change to look like a different world.
    /// </summary>
    public static class GalleryCatalog
    {
        private static readonly string[] GalleryRules =
        {
            "e542-5f79-9341-f31e-6c6b-7f08-8773-7068",
            "a07f-c000-0000-0000-0000-0000-ff80-807f",
            "6eb6-ba3d-70b4-ac6f-baae-2604-8529-8998",
            "ea44-55df-9025-bead-5f6e-45ca-6168-275a",
            "7b58-f7b4-c5b4-fd87-22fa-eb10-6de8-107c",
            "8503-5eb6-084c-04df-7657-a5b3-6044-3524",
            "1c48-9004-8831-41be-2804-8f50-9901-db18",
            "df1d-bba1-8e06-aa66-48ff-7414-6a2f-6237",
            "6769-5dd6-7d03-564e-a5ec-cae2-54c4-810c",
            "cb97-6a74-88c0-28aa-1b6a-834b-4fe8-60ac",
            "6007-7d42-05e5-1b9b-2899-e043-1cd4-2f7b",
            "dfda-67af-bc97-7ef6-be98-42d9-9147-97d3",
            "f81b-38d1-7f60-62ad-850b-2085-ddff-8154",
            "548c-aac9-97d2-b8dd-1425-88c1-599d-78e2",
            "8501-677e-655f-236e-53ba-d52d-8cf1-1e46",
            "5688-0f6c-6619-8605-d7e4-4074-de2e-96c8",
            "c168-7b61-b5cc-4e64-8f7a-df90-5362-8750",
            "5eb3-2d3b-df40-ee83-e472-60c3-3342-48be",
            "5a3d-45de-96fd-de64-ecf9-77c1-8461-9c8c",
            "2085-c66a-84d8-fbe8-b3c0-70e4-0e2e-799c",
            "5a55-983c-daad-60f5-2969-3077-90e7-9188",
            "6da1-0852-5e0f-2ad9-c902-f8a0-78fd-4473",
            "a45d-d552-3331-a34f-890a-bb71-64e2-c4f0",
            "d106-f969-cda8-ceb6-9964-977c-cc43-62b1",
            "2152-9b71-abb7-162a-45ff-dd03-fe15-957e",
            "2152-9b71-bbc7-162a-c5ff-ad03-fd65-957e",
            "4d56-d1e3-4acb-60d6-5e2f-5fbf-33ad-e266",
            "7b58-f7b4-a5b4-fd87-22fa-eb12-6de8-107f",
            "bf51-3628-3bcf-1ee1-5b18-7b95-7898-6a9a",
            "ef12-d680-9430-8853-a368-55f9-7451-7c44",
        };

        private static readonly string[] PresetRules =
        {
            "e542-5f79-9341-f31e-6c6b-7f08-8773-7068",
            "a07f-c000-0000-0000-0000-0000-ff80-807f",
            "ea44-55df-9025-bead-5f6e-45ca-6168-275a",
        };

        private static readonly Dictionary<string, string> NamesByRule = BuildNames();

        private static readonly (string Name, string Rule)[] NamedGalleryRules = BuildNamedRules();

        /// <summary>The 30 gallery rules, canonical, in the PyQt gallery's order.</summary>
        public static IReadOnlyList<string> Rules => GalleryRules;

        /// <summary>The 3 rules the Simulator's preset dropdown lists first.</summary>
        public static IReadOnlyList<string> Presets => PresetRules;

        /// <summary>The rule a fresh Simulator starts on: Red World, the 2017 paper's rule.</summary>
        public static string DefaultRule => MergeLife.DefaultRule;

        /// <summary>
        /// The (name, rule) pairs the preset dropdown appends after
        /// <see cref="Presets"/>, in gallery order: the 13 gallery rules the
        /// engine's featured catalog names.
        /// </summary>
        public static IReadOnlyList<(string Name, string Rule)> NamedRules => NamedGalleryRules;

        /// <summary>
        /// The display name for <paramref name="rule"/>, or null when the
        /// featured catalog does not name it. Any form the rule can be written
        /// in is accepted; a rule that will not parse is simply unnamed.
        /// </summary>
        public static string Name(string rule)
        {
            if (string.IsNullOrEmpty(rule))
            {
                return null;
            }
            if (MergeLife.RuleError(rule) != null)
            {
                return null;
            }
            return NamesByRule.TryGetValue(MergeLife.CanonicalRule(rule), out string name) ? name : null;
        }

        private static Dictionary<string, string> BuildNames()
        {
            var names = new Dictionary<string, string>(MergeLifeGallery.All.Length, StringComparer.Ordinal);
            foreach (FeaturedRule entry in MergeLifeGallery.All)
            {
                names[entry.Rule] = entry.Name;
            }
            return names;
        }

        private static (string Name, string Rule)[] BuildNamedRules()
        {
            var named = new List<(string Name, string Rule)>();
            foreach (string rule in GalleryRules)
            {
                if (NamesByRule.TryGetValue(rule, out string name))
                {
                    named.Add((name, rule));
                }
            }
            return named.ToArray();
        }
    }
}
