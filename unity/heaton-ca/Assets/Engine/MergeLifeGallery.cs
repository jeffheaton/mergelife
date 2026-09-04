// HeatonCA.Engine: copied verbatim from HeatonLife.Core (github.com/jeffheaton/heaton-life, dotnet/src/HeatonLife.Core/MergeLifeGallery.cs, commit 46a117baf33a0f916d39e2c05991604ae8a1b424).
// Copyright Jeff Heaton. Licensed under the Apache License, Version 2.0; see THIRD_PARTY_NOTICES.md.
// Modifications: namespace HeatonLife -> HeatonCA.Engine.
// Do not edit: re-run tools/import-engine.sh to sync.

namespace HeatonCA.Engine
{
    /// <summary>One entry of the featured-rule catalog (spec/mergelife.md "Featured rules").</summary>
    public sealed class FeaturedRule
    {
        internal FeaturedRule(string rule, string name, string description)
        {
            Rule = rule;
            Name = name;
            Description = description;
        }

        /// <summary>The rule, already canonical — <c>CanonicalRule(Rule) == Rule</c>.</summary>
        public string Rule { get; }

        /// <summary>Display name; non-empty and unique across the catalog.</summary>
        public string Name { get; }

        /// <summary>One-line blurb for a gallery card; presentation text, never a wire format.</summary>
        public string Description { get; }
    }

    /// <summary>
    /// The featured MergeLife rules — spec/mergelife.md "Featured rules (the
    /// gallery)". The MergeLife counterpart of <see cref="BuiltinPatterns"/>: a rule
    /// here is 32 raw octets with no mnemonic, so a curated, ordered catalog is the
    /// only practical way into the family, and every implementation ships the same
    /// one. Apps surface it as a rule gallery.
    ///
    /// Provenance: the twelve gallery rules of the upstream HeatonCA gallery
    /// (github.com/jeffheaton/mergelife, python/application/pyqt/tab_gallery.py
    /// GALLERY_RULES), in that order with the 2017 paper's rule first, plus Cobalt
    /// Reef — engineered by permuting Red World's octets, so it sits beside its
    /// parent. Names and descriptions are authored from observed 128x128 soup
    /// behavior (multi-seed runs to generation 640).
    ///
    /// The order is part of the set. Behavior-pinned in the test suite.
    /// </summary>
    public static class MergeLifeGallery
    {
        private static FeaturedRule Featured(string rule, string name, string description) =>
            new FeaturedRule(rule, name, description);

        public static readonly FeaturedRule[] All =
        {
            Featured(
                "e542-5f79-9341-f31e-6c6b-7f08-8773-7068",
                "Red World (paper)",
                "The 2017 paper's rule: blue-green colonies adrift on a churning red sea."),
            Featured(
                "e542-9341-6c6b-f31e-5f79-7f08-8773-7068",
                "Cobalt Reef",
                "Engineered from Red World's parts: green reefs adrift on a deep cobalt sea."),
            Featured(
                "a07f-c000-0000-0000-0000-0000-ff80-807f",
                "Pen and Ink",
                "Monochrome MergeLife: inky rings and specks doodle across a white page."),
            Featured(
                "6eb6-ba3d-70b4-ac6f-baae-2604-8529-8998",
                "Brushfire",
                "Ember-red fire lines crackle and creep across a hot yellow plain."),
            Featured(
                "ea44-55df-9025-bead-5f6e-45ca-6168-275a",
                "Beetle Meadow",
                "Blue-shelled beetles with speckled backs graze a bright green meadow."),
            Featured(
                "7b58-f7b4-c5b4-fd87-22fa-eb10-6de8-107c",
                "High Noon",
                "Dark gnat swarms mill about, then burn away under a blank yellow glare."),
            Featured(
                "8503-5eb6-084c-04df-7657-a5b3-6044-3524",
                "Lagoons",
                "Cyan lagoons ringed by deep-blue shores drain slowly into green lowland."),
            Featured(
                "1c48-9004-8831-41be-2804-8f50-9901-db18",
                "Frost",
                "White discs bloom on green, merge into foam, and freeze the world still."),
            Featured(
                "df1d-bba1-8e06-aa66-48ff-7414-6a2f-6237",
                "Lichen",
                "Green lichen creeps in ragged mats across a yellow wall, never settling."),
            Featured(
                "6769-5dd6-7d03-564e-a5ec-cae2-54c4-810c",
                "Plankton",
                "A magenta tide boils away, stranding tiny rose-ringed cells in still cyan."),
            Featured(
                "cb97-6a74-88c0-28aa-1b6a-834b-4fe8-60ac",
                "Neon Storm",
                "A full boil that never settles: red-pink static with dark drifting squalls."),
            Featured(
                "6007-7d42-05e5-1b9b-2899-e043-1cd4-2f7b",
                "Coral Bloom",
                "White-flecked coral heads grow and fuse into continents in a hot-pink sea."),
            Featured(
                "dfda-67af-bc97-7ef6-be98-42d9-9147-97d3",
                "Emeralds",
                "Cyan lace condenses into blue-rimmed emerald pods scattered across gold."),
            Featured(
                "7e18-62ac-5c42-109e-45a1-9ff2-b7d8-64a1",
                "Diamond Mine",
                "Pin-striped magenta diamonds crystallize around white grit on open green."),
            Featured(
                "2152-9b71-abb7-162a-45ff-dd03-fe15-957e",
                "Mood Ring",
                "The sky drifts white to cyan to violet, then pink confetti settles on gold."),
        };
    }
}
