namespace HeatonCAApp
{
    /// <summary>
    /// Every URL and attribution line the app shows or opens, in one place so the
    /// Gate 4 link check (each URL resolves) has a single file to read and the
    /// About page never hard-codes an address. Phase 2 (WP2.5) may extend this;
    /// the values below are final.
    /// </summary>
    public static class AppLinks
    {
        /// <summary>The MergeLife tutorial page (About &gt; Tutorial).</summary>
        public const string TutorialUrl = "https://www.heatonresearch.com/mergelife/";

        /// <summary>DOI of the 2018 paper the app implements (About &gt; DOI).</summary>
        public const string PaperDoiUrl = "https://doi.org/10.1007/s10710-018-9336-1";

        /// <summary>The public source repository (About &gt; Source code).</summary>
        public const string RepoUrl = "https://github.com/jeffheaton/mergelife";

        /// <summary>Privacy policy, served by GitHub from this repository.</summary>
        public const string PrivacyUrl =
            "https://github.com/jeffheaton/mergelife/blob/master/unity/heaton-ca/docs/privacy.md";

        /// <summary>User manual, served by GitHub from this repository.</summary>
        public const string ManualUrl =
            "https://github.com/jeffheaton/mergelife/blob/master/unity/heaton-ca/docs/manual.md";

        /// <summary>Copyright line on the About page.</summary>
        public const string CopyrightLine = "Copyright 2018-2026 Jeff Heaton, MIT License";

        /// <summary>Citation of the paper, shown under the "implements the paper" note.</summary>
        public const string Citation =
            "Heaton, J. (2018). Evolving continuous cellular automata for aesthetic objectives. "
            + "Genetic Programming and Evolvable Machines.";
    }
}
