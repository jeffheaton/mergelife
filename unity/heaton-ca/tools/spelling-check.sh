#!/usr/bin/env bash
# spelling-check.sh -- house-style lint for the HeatonCA Unity project.
#
# Two rules, both of which exist because they have bitten this family of
# repositories before:
#
#   1. American English only. Both CLAUDE.md files (the repository's and the
#      user's) require American spellings in identifiers, comments, log and UI
#      strings, and documentation. A British spelling that reaches a shipped
#      string is a store-visible defect, and one that reaches an identifier is
#      a rename nobody wants to do later. This check greps the whole project
#      for the fourteen forms the house rules name.
#
#   2. ASCII source. Assets/Scripts and Assets/Tests should be plain ASCII
#      apart from the three Greek letters the rule decoder needs (U+03B1
#      alpha, U+03B2 beta, U+03B3 gamma, the column headers in AppStrings.cs
#      and the table in AppStringsTests.cs). Anything else is a character that
#      may have no glyph in the runtime font atlas, may not survive a
#      round-trip through a tool that guesses the encoding, and -- in a string
#      literal -- may reach the user as a box.
#
# Rule 1 fails the run. Rule 2 reports by default and fails only with
# --strict, because the tree still carries prose punctuation (em dashes) in
# doc comments; see "Rule 2 severity" below. Every occurrence is counted and
# summarized on every run, so nothing is hidden either way.
#
# This is the shell twin of Assets/Tests/EditMode/AppStringsTests.cs, which
# holds the same two rules against the AppStrings table from inside the
# EditMode suite. That file spells all fourteen British forms out in a C#
# array, so it is excluded here, exactly as this script excludes itself.
#
# Rule 2 severity
#   Non-ASCII in code (outside a // comment) is the hazard the rule is about:
#   it can ship. Non-ASCII in a comment is cosmetic. Both are listed; --strict
#   turns either into a failure. Turn --strict on in CI once Assets/Scripts
#   and Assets/Tests are ASCII-clean.
#
# Exclusions, and why each one is not a loophole
#   Library/, Temp/, Logs/, build/, obj/   generated or downloaded; not ours.
#   Assets/Plugins/                        vendored native plug-ins.
#   Assets/Engine/                         a verbatim copy of HeatonLife.Core.
#                                          tools/engine-sync-check.sh forbids
#                                          hand edits there, so a hit would be
#                                          unfixable in this repository; fix
#                                          it upstream instead.
#   Assets/Tests/Vectors~/                 conformance vectors, copied
#                                          verbatim and never edited here.
#   THIRD_PARTY_NOTICES.md                 upstream license texts, quoted
#                                          verbatim (they say "licence").
#   AppStringsTests.cs, spelling-check.sh  the two files that must name the
#                                          forbidden forms to check for them.
#   Unity type names ending in Behaviour   MonoBehaviour, StateMachineBehaviour
#                                          and friends: the external-API
#                                          exception both CLAUDE.md files
#                                          grant by name.
#
# Usage:
#   tools/spelling-check.sh [--strict] [--list] [--help]
#
#   --strict   make rule 2 (non-ASCII) a failure as well
#   --list     print every non-ASCII occurrence, not just the summary
#
# Exit codes: 0 clean; 1 a British spelling (or, with --strict, a non-ASCII
# character); 2 usage error or a missing prerequisite.
#
# Runs anywhere bash, grep, sed and perl exist; it never launches Unity and
# needs no network. It does not assume the Claude Code Bash sandbox -- it only
# reads files under the project, so the sandbox does not need to be disabled.
set -euo pipefail

export LC_ALL=C

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# The fourteen forms named by CLAUDE.md. Substrings, matched
# case-insensitively, so plurals and inflections ("Colours", "analysed",
# "initialising") are caught by the same entry. Keep in sync with the
# BritishSpellings array in Assets/Tests/EditMode/AppStringsTests.cs.
BRITISH='colour|grey|centre|behaviour|neighbour|favourite|cancelled|analyse|normalise|initialise|licence|defence|modelling|labelled'

# Unity type names the house rules exempt as an external API. Removed from a
# candidate line before it is judged, so "MonoBehaviour" is invisible to the
# grep above while a real "behaviour" on the same line still fails. The
# leading [A-Za-z0-9_]+ and the capital B are both required, so bare
# "Behaviour" and lowercase "behaviour" are still hits.
EXEMPT_TOKENS='[A-Za-z0-9_]+Behaviours?'

INCLUDES=(--include='*.cs' --include='*.md' --include='*.sh' --include='*.py' --include='*.html')
EXCLUDE_DIRS=(
    --exclude-dir=Library
    --exclude-dir=Temp
    --exclude-dir=Logs
    --exclude-dir=build
    --exclude-dir=obj
    --exclude-dir=Plugins
    --exclude-dir=Engine
    --exclude-dir='Vectors~'
)
EXCLUDE_FILES=(
    --exclude=THIRD_PARTY_NOTICES.md
    --exclude=AppStringsTests.cs
    --exclude=spelling-check.sh
)

# Rule 2 scope: the two source trees this project actually writes.
ASCII_ROOTS=(Assets/Scripts Assets/Tests)

# The only characters allowed above U+007F, as UTF-8 byte pairs.
#   U+03B1 alpha = CE B1, U+03B2 beta = CE B2, U+03B3 gamma = CE B3
ALLOWED_UTF8_RE='\xCE[\xB1-\xB3]'

STRICT=0
LIST=0

usage()
{
    cat <<__USAGE__
usage: tools/spelling-check.sh [--strict] [--list]
       American-English and ASCII-source lint for unity/heaton-ca
       --strict  also fail on non-ASCII characters in Assets/Scripts and Assets/Tests
       --list    print every non-ASCII occurrence instead of a summary
exit:  0 clean, 1 violation, 2 usage error or missing prerequisite
__USAGE__
}

while [ $# -gt 0 ]; do
    case "$1" in
        --strict) STRICT=1 ;;
        --list) LIST=1 ;;
        -h|--help) usage; exit 0 ;;
        *)
            echo "spelling-check: unknown argument: $1" >&2
            usage >&2
            exit 2 ;;
    esac
    shift
done

command -v perl >/dev/null 2>&1 || {
    echo "spelling-check: perl is required (rule 2 reads files as raw bytes)" >&2
    exit 2
}

cd "$PROJECT_ROOT"

# Under GitHub Actions the findings are also emitted as annotations, which want
# repository-relative paths; everything below is relative to the project root.
# `git rev-parse --show-prefix` gives "unity/heaton-ca/" in a checkout and
# nothing at all outside one, which is the right answer in both cases.
GIT_PREFIX="$(git -C "$PROJECT_ROOT" rev-parse --show-prefix 2>/dev/null || true)"
ANNOTATE=0
[ "${GITHUB_ACTIONS:-}" = "true" ] && ANNOTATE=1

TMPDIR_RUN="$(mktemp -d "${TMPDIR:-/tmp}/spelling-check.XXXXXX")"
trap 'rm -rf "$TMPDIR_RUN"' EXIT

FAILED=0

# --- rule 1: American English ----------------------------------------------

RAW="$TMPDIR_RUN/british.raw"
HITS="$TMPDIR_RUN/british.hits"
: > "$HITS"

grep -rniE "$BRITISH" . "${INCLUDES[@]}" "${EXCLUDE_DIRS[@]}" "${EXCLUDE_FILES[@]}" > "$RAW" 2>/dev/null || true

# Re-judge each candidate with the exempt Unity type names deleted, so a line
# whose only match was MonoBehaviour drops out and a line with a real hit
# beside it does not.
while IFS= read -r hit; do
    [ -n "$hit" ] || continue
    if printf '%s\n' "$hit" | sed -E "s/$EXEMPT_TOKENS//g" | grep -qiE "$BRITISH"; then
        printf '%s\n' "$hit" >> "$HITS"
    fi
done < "$RAW"

BRITISH_COUNT="$(wc -l < "$HITS" | tr -d ' ')"
if [ "$BRITISH_COUNT" -gt 0 ]; then
    echo "spelling-check: FAIL: $BRITISH_COUNT British spelling(s); use the American form" >&2
    sed 's/^\.\///' "$HITS" >&2
    if [ "$ANNOTATE" -eq 1 ]; then
        while IFS= read -r hit; do
            rel="${hit%%:*}"; rel="${rel#./}"
            rest="${hit#*:}"
            printf '::error file=%s%s,line=%s,title=British spelling::%s\n' \
                "$GIT_PREFIX" "$rel" "${rest%%:*}" "${rest#*:}"
        done < "$HITS"
    fi
    FAILED=1
else
    echo "spelling-check: rule 1 OK: no British spellings"
fi

# --- rule 2: ASCII source ---------------------------------------------------

FILES="$TMPDIR_RUN/files"
: > "$FILES"
for root in "${ASCII_ROOTS[@]}"; do
    [ -d "$root" ] || continue
    find "$root" -type f -not -path '*/Vectors~/*' -print0 >> "$FILES"
done

NONASCII="$TMPDIR_RUN/nonascii"
: > "$NONASCII"

# One perl pass over every file, reading raw bytes (no PERL_UNICODE / PERL5OPT
# from the environment, which would decode the input and defeat the byte test).
# Each line has the allowed alpha/beta/gamma sequences deleted first; whatever
# high bytes remain are reported with the UTF-8 character they belong to and
# whether they sit in code or in a // comment.
if [ -s "$FILES" ]; then
    PERL_UNICODE= PERL5OPT= xargs -0 perl -e '
        use strict; use warnings;
        binmode(STDOUT, ":encoding(UTF-8)");   # the report echoes decoded characters
        my $allowed = qr/'"$ALLOWED_UTF8_RE"'/;
        FILE: for my $path (@ARGV) {
            next FILE if -B $path;          # skip binaries; they are not source
            open(my $fh, "<:raw", $path) or next FILE;
            my $lineno = 0;
            while (my $line = <$fh>) {
                $lineno++;
                my $probe = $line;
                $probe =~ s/$allowed//g;
                next unless $probe =~ /[\x80-\xFF]/;
                # Decode for reporting only; report bytes we cannot decode raw.
                my $text = $line;
                my $ok = eval { require Encode; $text = Encode::decode("UTF-8", $line, 1); 1; };
                if ($ok) {
                    my $slashes = index($text, "//");
                    my $col = 0;
                    for my $ch (split //, $text) {
                        $col++;
                        next if ord($ch) < 0x80;
                        next if ord($ch) >= 0x03B1 && ord($ch) <= 0x03B3;
                        my $where = ($slashes >= 0 && $col - 1 > $slashes) ? "comment" : "code";
                        printf("%s:%d:%d:%s:U+%04X:%s\n", $path, $lineno, $col, $where, ord($ch), $ch);
                    }
                } else {
                    printf("%s:%d:0:code:U+FFFD:?\n", $path, $lineno);
                }
            }
            close($fh);
        }
    ' < "$FILES" > "$NONASCII" || true
fi

NONASCII_COUNT="$(wc -l < "$NONASCII" | tr -d ' ')"
# Only code under Assets/Scripts can reach a rendered label; a non-ASCII literal
# in a test is input data (PngExporterTests feeds one deliberately to prove the
# snapshot slug strips it), so it is counted as a comment-class hit.
CODE_COUNT="$(grep ':code:' "$NONASCII" | grep -c '^Assets/Scripts/' || true)"
COMMENT_COUNT="$(grep -c ':comment:' "$NONASCII" || true)"

# A non-ASCII character only reaches a user when it is rendered by the legacy
# uGUI font, and every rendered string lives in AppStrings.cs (checked strictly
# just below). An em dash in a doc comment renders nowhere, so comment-only hits
# are reported at --list volume and do not colour the verdict; code hits still
# do, because that is where a stray glyph could reach a label.
if [ "$NONASCII_COUNT" -gt 0 ] && [ "$CODE_COUNT" -eq 0 ] && [ "$STRICT" -eq 0 ]; then
    echo "spelling-check: rule 2 OK: no non-ASCII in code" \
         "($COMMENT_COUNT in comments, which render nowhere; --list to see them)"
    if [ "$LIST" -eq 1 ]; then
        sed 's/^/    /' "$NONASCII" >&2
    fi
    NONASCII_COUNT=0
    RULE2_REPORTED=1
fi
RULE2_REPORTED="${RULE2_REPORTED:-0}"

if [ "$NONASCII_COUNT" -gt 0 ]; then
    if [ "$STRICT" -eq 1 ]; then
        LABEL="FAIL"
        FAILED=1
    else
        LABEL="WARNING"
    fi
    {
        echo "spelling-check: $LABEL: $NONASCII_COUNT non-ASCII character(s) under ${ASCII_ROOTS[*]}" \
             "($CODE_COUNT in code, $COMMENT_COUNT in comments); only U+03B1/03B2/03B3 are allowed"
        echo "spelling-check: by character:"
        cut -d: -f5,6 "$NONASCII" | sort | uniq -c | sort -rn | sed 's/^/    /'
        echo "spelling-check: by file:"
        cut -d: -f1 "$NONASCII" | sort | uniq -c | sort -rn | sed 's/^/    /'
        if [ "$LIST" -eq 1 ]; then
            echo "spelling-check: occurrences:"
            sed 's/^/    /' "$NONASCII"
        else
            grep ':code:' "$NONASCII" | sed 's/^/    in code: /' || true
            echo "spelling-check: re-run with --list for every occurrence, --strict to make this a failure"
        fi
    } >&2
    # One annotation, not 129: the summary belongs in the run header, the list
    # stays in the log.
    if [ "$ANNOTATE" -eq 1 ]; then
        printf '::%s title=Non-ASCII source::%s\n' \
            "$([ "$STRICT" -eq 1 ] && echo error || echo warning)" \
            "$NONASCII_COUNT non-ASCII character(s) under ${ASCII_ROOTS[*]} ($CODE_COUNT in code, $COMMENT_COUNT in comments). Only U+03B1/03B2/03B3 are allowed; see the job log for the list."
    fi
elif [ "$RULE2_REPORTED" -eq 0 ]; then
    echo "spelling-check: rule 2 OK: ${ASCII_ROOTS[*]} are ASCII apart from U+03B1/03B2/03B3"
fi

# --- rule 3: every rendered string is Latin-1 -------------------------------
# AppStrings.cs is the single home of user-visible text, so it is the one file
# where a non-ASCII character can become a blank box on iOS (LegacyRuntime.ttf's
# fallback is weaker there than on macOS). Only the three Greek rule-decoder
# headers are allowed, and each has an ASCII fallback constant beside it.

STRINGS_FILE="Assets/Scripts/AppStrings.cs"
if [ -f "$STRINGS_FILE" ]; then
    STRAY="$(PERL_UNICODE= PERL5OPT= perl -e '
        open(my $fh, "<:raw", $ARGV[0]) or exit 0;
        my $n = 0;
        while (my $line = <$fh>) {
            $line =~ s/\xCE[\xB1-\xB3]//g;   # UTF-8 for U+03B1..U+03B3
            $n++ while $line =~ /[\x80-\xFF]/g;
        }
        print $n;
    ' "$STRINGS_FILE")"
    if [ "${STRAY:-0}" -gt 0 ]; then
        echo "spelling-check: FAIL: $STRINGS_FILE holds $STRAY non-ASCII byte(s) beyond" \
             "U+03B1/03B2/03B3; a rendered string must stay in ASCII or it can show as a box on iOS" >&2
        FAILED=1
    else
        echo "spelling-check: rule 3 OK: $STRINGS_FILE renders only ASCII plus the three Greek headers"
    fi
fi

# --- verdict ----------------------------------------------------------------

if [ "$FAILED" -ne 0 ]; then
    echo "spelling-check: FAIL" >&2
    exit 1
fi

echo "spelling-check: PASS"
