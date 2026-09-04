using System.Text;
using HeatonCA.Engine;

namespace HeatonCAApp
{
    /// <summary>
    /// Accepts a MergeLife rule the way a person types or pastes one and hands
    /// back the canonical dashed lowercase form.
    ///
    /// Rules travel through email, papers, and this repository's own text files,
    /// so the tolerated forms are the ones seen in the wild: uppercase, no
    /// dashes at all, dashes in odd places, spaces or tabs between groups, and a
    /// trailing <c>;name</c> comment (the format the MergeLife command line
    /// writes its found rules in). Everything else is rejected with the one
    /// error message the Simulator shows.
    ///
    /// Rejection is never an exception: the Simulator keeps the typed text on
    /// screen so the user can fix a digit, so a bad rule is an ordinary result,
    /// not an error path.
    /// </summary>
    public static class RuleParser
    {
        /// <summary>
        /// Try to read <paramref name="input"/> as a rule.
        ///
        /// On success <paramref name="canonical"/> is the
        /// <see cref="MergeLife.CanonicalRule"/> form (eight lowercase hex groups
        /// of four, dash-separated) and <paramref name="error"/> is null; on
        /// failure <paramref name="canonical"/> is null and
        /// <paramref name="error"/> is <see cref="AppStrings.SimInvalidRule"/>.
        /// </summary>
        public static bool TryParse(string input, out string canonical, out string error)
        {
            canonical = null;
            error = null;
            if (string.IsNullOrEmpty(input))
            {
                error = AppStrings.SimInvalidRule;
                return false;
            }
            string digits = Clean(input);
            if (digits == null)
            {
                error = AppStrings.SimInvalidRule;
                return false;
            }
            canonical = MergeLife.CanonicalRule(digits);
            return true;
        }

        /// <summary>
        /// The 32 lowercase hex digits behind a typed rule, or null when the
        /// text is not one: the trailing <c>;comment</c> is dropped, dashes and
        /// whitespace are removed, letters are lowercased, and what remains must
        /// be exactly 32 hex digits.
        /// </summary>
        private static string Clean(string input)
        {
            int comment = input.IndexOf(';');
            string body = comment >= 0 ? input.Substring(0, comment) : input;
            var digits = new StringBuilder(32);
            foreach (char c in body)
            {
                if (c == '-' || char.IsWhiteSpace(c))
                {
                    continue;
                }
                char lower = char.ToLowerInvariant(c);
                bool isHex = (lower >= '0' && lower <= '9') || (lower >= 'a' && lower <= 'f');
                if (!isHex || digits.Length == 32)
                {
                    // Not hex, or a 33rd digit: either way this is not a rule.
                    return null;
                }
                digits.Append(lower);
            }
            return digits.Length == 32 ? digits.ToString() : null;
        }
    }
}
