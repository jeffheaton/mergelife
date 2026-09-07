# HeatonCA User Manual

HeatonCA runs **MergeLife** — a continuous cellular automaton where every rule
is a 32-hex-digit code and a few of those codes grow into something that looks
alive. The app lets you watch the featured rules, decode what a rule actually
says, type or randomize your own, and set a genetic algorithm loose to breed
new ones.

One app covers iPhone, iPad, Mac, Windows, Android, and the browser. The
screens are the same everywhere; only the layout changes.

Everything runs on your device and nothing is uploaded — see the
[privacy policy](privacy.md).

**Contents**

- [The idea in one minute](#the-idea-in-one-minute)
- [Home](#home)
- [Simulator](#simulator)
- [Gallery](#gallery)
- [Rule decoder](#rule-decoder)
- [Evolve](#evolve)
- [Finds](#finds)
- [Settings](#settings)
- [About](#about)
- [Phones versus tablets and desktops](#phones-versus-tablets-and-desktops)
- [Saving a PNG](#saving-a-png)
- [The web build's query parameters](#the-web-builds-query-parameters)
- [Tips and troubleshooting](#tips-and-troubleshooting)

## The idea in one minute

A MergeLife world is a grid of colored cells. Each generation, every cell looks
at the eight cells around it, adds up a number from their colors, and finds
which of the rule's eight sub-rules that number falls into. That sub-rule says
"merge this cell some percentage of the way toward this color." Every cell does
it at once, and the pattern that comes back out is nobody's design.

A **rule** is those eight sub-rules written as eight groups of four hex digits,
for example:

```
e542-5f79-9341-f31e-6c6b-7f08-8773-7068
```

That is the app's default rule. Change one digit and you get a different
universe — usually a boring one. The interesting ones are rare, which is what
the Gallery and the Evolve screen are for.

## Home

The first screen shows the HeatonCA title, the app version in the top-right
corner, and five buttons:

| Button | Goes to |
| --- | --- |
| **Gallery** | the 30 curated rules |
| **Simulator** | the live world |
| **Evolve** | the rule search |
| **Settings** | cell size, speed, overlay |
| **About** | version, the paper, links |

If a search is running, an **Evolving...** chip sits in the top-left corner of
Home. That is deliberate: leaving the Evolve screen does not stop the search,
and the chip is the reminder that your CPU is still busy.

Every other screen has a **Back** button in its top-left corner. On a desktop
the **Esc** key does the same thing; on Android so does the hardware back
gesture. At Home, Back does nothing (there is nowhere further back to go).

## Simulator

The Simulator is the main screen: a live world filling the canvas, a toolbar,
and a rule box.

### The toolbar

| Button | What it does |
| --- | --- |
| **Start** | begins stepping. Start and Step gray out; Stop becomes available |
| **Stop** | pauses. Start and Step come back |
| **Step** | advances exactly one generation. Only works while stopped |
| **Reset** | re-seeds the world with a fresh random lattice and zeroes the step count |
| **Rule** | opens the [rule decoder](#rule-decoder) for the rule now running |
| **Random** | replaces the rule with a randomly generated one |
| **Save PNG** | exports the world as an image (see [Saving a PNG](#saving-a-png)) |
| **Copy rule** | puts the current rule code on the clipboard |

On a phone the toolbar wraps onto two rows. Nothing is hidden or moved into a
menu.

### The rule box and the preset picker

Type or paste a rule into the field and press Enter (or leave the field) to
apply it. The parser is forgiving: it accepts upper or lower case, with or
without the dashes, with stray whitespace, and with a trailing `;comment` — it
canonicalizes whatever you give it to the lowercase dashed form. What it will
not accept is the wrong number of digits or a non-hex character, and then a
message appears under the field for four seconds:

> Invalid rule: need 32 hex digits (8 groups of 4).

Your text stays put so you can fix the typo instead of retyping the whole
thing.

Beside the field is a **preset picker**: the three rules the original HeatonCA
shipped with, followed by the 13 gallery rules that have names, each listed as
`Name - hex`. Picking one applies it immediately.

Applying a new rule — by typing it, by picking a preset, by pressing Random, or
by arriving from the Gallery — re-seeds the world, exactly as the original app
did. A rule change is a new universe, not a change of physics mid-run.

### The overlay

When **Display FPS/Steps** is on in Settings, a small black box in the
top-right corner reads:

```
Steps: 1,234, FPS: 60
```

*Steps* is the generation count since the last seed; *FPS* is the measured
frame rate, which is not the same thing as the simulation speed (see below). A
second line appears when the screen is so large and the cell size so small that
the world had to be coarsened:

```
cell size raised to 3 to fit this screen
```

### Cell size and speed

**Cell size** is a *density-independent* number, not a raw pixel count. On a
phone or tablet, one unit is 1/160 inch, so cell size 5 is the same physical
size on a 460-dpi phone as on a 264-dpi tablet. On a desktop or in a browser,
one unit is a 96-dpi logical pixel (`cell pixels = cell size × max(1, dpi/96)`,
and exactly one pixel when the system reports no dpi) — so on a Mac, cell size
5 looks like the 5-pixel cells of the original PyQt app.

The lattice always fills the canvas at whole cells and is never stretched. Two
limits apply: a world is never smaller than 8×8, and never larger than 262,144
cells. On a very large, very dense display, the second limit raises the
effective cell size and the overlay says so.

**Animation speed** is 1 to 60 generations per second and is *decoupled from
the frame rate*. The original app stepped once per frame, so its "FPS" setting
was really its speed; here a slow device draws fewer frames but still advances
the world at the speed you asked for, up to a per-frame budget. If the device
cannot keep up, backlog is dropped rather than accumulated, so the world never
"catches up" in a jerky burst.

### Resizing and rotating

Resize the window, or rotate a phone or tablet, and the lattice is re-cut about
a third of a second later. **The cells that overlap the old and new shapes are
kept**; only the newly exposed area is seeded fresh. The original app wiped the
world on every resize. All four orientations are supported, and iPad
multitasking works.

While the world is playing on a phone or tablet, the screen is kept awake.

## Gallery

Thirty rules in the order the original app listed them, each with a preview
image, the rule code as a caption, and — for the 13 that have one — a name.
Tap or click a tile and the Simulator opens on that rule, **already running**.

Column counts: three across on a desktop or in a browser (matching the
original), and on a phone or tablet the count comes from the physical width of
your screen, between one and four columns, so a tile stays about the same size
in your hand whichever device you hold.

## Rule decoder

The decoder shows what the eight sub-rules of a rule actually say. The heading
is `Rule: <the rule>` and the table has one row per sub-rule and seven columns:

| Column | Meaning |
| --- | --- |
| **High (α)** | the upper limit of this sub-rule's neighborhood total, after promotion |
| **Range** | the span of totals this sub-rule owns, written `low-high` |
| **Key Color** | a swatch of the color merged toward, labeled with its name |
| **Percent (β)** | how far toward that color a cell moves, as a whole-number percent |
| **Index (γ)** | the raw color index and the color's name |
| **Octet-1** | the raw range byte, in hex and decimal |
| **Octet-2** | the raw percent byte, in hex and decimal |

Two things in that table look like bugs and are not. A degenerate range prints
as `0--1` (a limit of zero) and a tie prints as `128-127`; both are the honest
output of the decoder, and clamping them would hide the tie that produced them.
The color name is drawn in white on the Black swatch and in black on every
other one.

At the bottom:

> Octets are shown raw; the original HeatonCA re-derived Octet-1 from the
> promoted limit.

That is a deliberate difference from the original app, not a regression. The
original re-computed Octet-1 from the promoted limit (so a promoted row printed
`0x100`, which is not a byte) and printed the absolute value of Octet-2's hex.
This app prints the octets the rule string actually carries, which is what the
engine reads and what the conformance vectors pin. The decoded meaning —
limits, ranges, colors, percentages — is identical either way.

**Open in Simulator** runs the decoded rule; **Copy rule** puts it on the
clipboard.

On a phone the table becomes one card per sub-rule in a scrolling list, since
seven columns of numbers do not fit a phone honestly. The values are the same.

## Evolve

Evolve is a genetic algorithm hunting for rules that score well against the
objective function from the 2018 paper. Press **Start** and it runs forever,
restarting whenever a run stops improving, until you press **Stop**.

### The readouts

| Row | Meaning |
| --- | --- |
| **Run Number** | how many runs have begun since you pressed Start. A run ends when it stops improving, and the next one starts from a fresh population |
| **Eval Number** | how many candidate rules have been scored in this run |
| **Evals/min** | throughput, recomputed once a minute — it reads 0 for the first minute of a session, which is normal |
| **Current Rule** | the best rule of the run in progress |
| **Current Score** | that rule's score, against the threshold: `2.87/3.50` |
| **No improve/Max allowed** | evaluations since the last improvement, over the patience (250). When the left number reaches the right one, the run ends and a new one starts |
| **Rules found** | how many rules have ever cleared the threshold |
| **Saved finds** | how many are kept on this device (the cap is 60) |
| **Status** | what the search is doing right now |

The status line says `Generating new population: 12/100` while a run is being
seeded, `Running...` while evaluations are in flight, `No improvement for 250,
stopping run.` when patience runs out, `Stopping...` between your Stop press
and the next safe stopping point, and `Stopped` when it has finished. On a
phone or tablet it reads `Paused (app in background)` when you switch away, and
in a browser `Paused (browser tab in background)` — in both cases the search
picks up where it left off when you come back.

A small live preview shows the current best rule running.

### What a score means

Each candidate is run five times from different random starts, on a 50×50
world, until it converges or hits 1000 generations, and the **best** of those
five runs is its score. Five statistics are measured and each is scored against
the band the paper defines:

| Statistic | The objective wants | Weight |
| --- | --- | --- |
| **steps** — generations before it converged | 300–1000; going the full distance scores best | 1 |
| **foreground** — fraction of cells stably not background | 0.001–0.1: some structure, not a flood | 1 |
| **active** — fraction that recently stopped being background | 0.001–0.1: still changing somewhere | 1 |
| **rect** — the largest solid background rectangle, as a fraction of the grid | 0.02–0.25: room to breathe, but not an empty world | 2 |
| **mage** — generations the background color has held | at least 5; a background that keeps flipping is penalized hard | 0 (−5 below) |

Add them up and the ceiling is **5.00**. A dead world or an explosion lands
near or below zero. Roughly:

- below **1** — dead, frozen, or a screen of noise;
- **1 to 3** — something is happening, but it does not last;
- **3.5** — the default bar, and a rule worth looking at;
- **above 4** — rare; these are the gallery-quality rules.

The signature of a good rule is that it never converges at all: it is still
changing when the 1000-generation cap stops it.

**Threshold** is the only tunable, a slider from −1 to 5, default 3.5. Every
rule that scores above it is saved to your [Finds](#finds). Lower it to collect
more (and worse) rules; raise it to keep only the exceptional ones. Everything
else about the search — population 100, crossover rate 0.75, tournament size 5,
five evaluation cycles, 1000-step cap, patience 250 — is the configuration the
original app used and is not adjustable.

Each press of Start rolls a fresh master seed, so two sessions do not retrace
the same search.

### Notes you will see

On a phone or tablet:

> Evolve uses all available CPU cores and will drain the battery.

In a browser:

> Browser mode: single-threaded search; long-lived rules cause pauses.

Both are literally true; see [Tips and troubleshooting](#tips-and-troubleshooting).

**Back does not stop the search.** It asks the search to stop at the next safe
boundary and returns you to Home, where the Evolving chip keeps you honest.
Use **Stop** when you mean stop.

## Finds

Every rule that clears the threshold is kept here, newest first, up to 60. Each
card shows a thumbnail, the rule code, and `score 3.72 - run 4`.

The thumbnails are *derived*, not stored: each is that rule run on a 100×100
world from a fixed seed for 250 generations, computed when the card is drawn.
This keeps the saved list to a few kilobytes of text, which matters in the
browser build where the entire store must fit in about a megabyte.

Per card:

- **Open in Simulator** — runs the rule. The search keeps going.
- **Save PNG** — writes `<rule>.png`, the thumbnail at 4× (400×400 pixels).
- **Delete** — removes the find after a confirmation. A deleted find stays
  deleted even if the same rule is discovered again.

**Clear all** empties the list, also after a confirmation. There is no undo for
either.

When the list is empty:

> No finds yet. Start a search and every rule scoring above the threshold is
> saved here.

## Settings

| Setting | Range | Default |
| --- | --- | --- |
| **Cell Size (1-25)** | 1 to 25 | 5 |
| **Animation Speed (1-60 gen/s)** | 1 to 60 | 30 |
| **Display FPS/Steps** | on or off | on |

**Save** applies the changes immediately — a running simulator re-cuts its
lattice to the new cell size, changes speed, and shows or hides the overlay
without restarting. **Cancel** discards your edits. **Restore Defaults** puts
all three back to the values in the table.

## About

The About screen carries the version and build number, when the build was made,
the copyright line, and the citation of the paper this app implements:

> Heaton, J. (2018). Evolving continuous cellular automata for aesthetic
> objectives. *Genetic Programming and Evolvable Machines.*

Buttons open the **Tutorial** page, this **Manual**, the **Privacy policy**,
the **Source code** repository, and the paper's **DOI** in your browser. A
**Determinism self-check** line reads PASS or FAIL: the app replays five pinned
computations at every launch to prove the engine produces identical numbers on
this device, this operating system, and this compiler as everywhere else. It
should always read PASS; if it ever does not, please report it with the version
and build number. **Third-party notices** expands to the licenses of the
components used.

## Phones versus tablets and desktops

| | Phone | Tablet, desktop, browser |
| --- | --- | --- |
| Home | one column of buttons | wider, centered |
| Simulator toolbar | two rows | one row |
| Gallery columns | 1–4, from the physical width of the screen | 3 |
| Rule decoder | one card per sub-rule, scrolling | a seven-column table |
| Evolve readouts | stacked, larger touch targets | side by side |
| Cell size unit | 1/160 inch | a 96-dpi logical pixel |

Layouts re-flow on rotation and on window resize; the running world keeps the
cells that still fit.

## Saving a PNG

**Save PNG** in the Simulator writes the world exactly as drawn, at one pixel
per cell, to a file named after the rule's first group and the UTC time —
`heatonca-e542-20260902-140301-123.png`. **Save PNG** on a Finds card writes
`<rule>.png` at 400×400. Where the file goes depends on the platform:

| Platform | Where it goes |
| --- | --- |
| **iPhone, iPad** | your photo library, alongside your other recent photos, plus a copy inside the app that the Files app can reach under *On My iPhone* → *HeatonCA* → *Snapshots*. The first save asks permission to add to Photos |
| **Android** | your gallery, plus a copy in the app's own storage |
| **Mac, Windows** | a `Snapshots` folder in the app's data folder, and the app opens Finder or Explorer with the new file selected. On the Mac App Store version that folder is inside the app's sandbox container, so use the reveal rather than hunting for it |
| **Browser** | your browser's downloads |

A short confirmation appears at the bottom of the screen each time: `saved
<file>`, `saved to Photos`, or `download started`.

## The web build's query parameters

The browser build honors the three parameters the old MergeLife web viewer
used, so existing embed links keep working:

| Parameter | Effect |
| --- | --- |
| `?rule=<hex>` | opens the Simulator on that rule, already running |
| `?size=<n>` | overrides the cell size for this session (1–25) |
| `?controls=off` | hides the controls for a kiosk-style embed. `on` is the default |

They combine, and an unrecognized or out-of-range value is simply ignored:

```
https://example.com/heatonca/?rule=e542-5f79-9341-f31e-6c6b-7f08-8773-7068&size=4&controls=off
```

Anything you change in a browser session — settings, finds — is kept in that
browser's site data for that address only. It does not follow you to another
browser or another device, and clearing site data clears it.

## Tips and troubleshooting

**A search will heat your device and drain the battery.** Evolve is not a
screensaver: it uses every core it is allowed and runs until you stop it. On a
phone or tablet it uses at most half the cores (up to four) to leave the device
usable, keeps the screen awake only while you are watching, and pauses
completely when you switch apps — but a long search still costs real battery
and the device will get warm. Plug in for long runs, and use Stop rather than
just leaving the screen.

**In a browser, evolve is much slower and stutters.** The browser gives the app
one thread, which the search shares with drawing. The app budgets about 8
milliseconds per frame for the search, but a single evaluation cannot be
interrupted partway, and a "treasure" rule — one that survives all 1000
generations, five times over, on 2500 cells — is one long unbreakable piece of
work. That is a visible pause, and it is the good outcome. Run serious searches
in the desktop or mobile app.

**The world looks too coarse on a big screen.** Lower the cell size in
Settings. If the overlay says `cell size raised to N`, the display is dense
enough that the 262,144-cell limit set the size instead of you; there is no way
around it beyond a smaller window.

**The world runs but nothing seems to change.** Many rules converge to a static
or frozen state within a few hundred generations. Press Reset for a different
random start, or try a Gallery rule.

**"Invalid rule."** The rule must be exactly 32 hex digits (0-9, a-f). Dashes,
case, spaces, and a trailing `;comment` are all fine; anything else is not.
Check for a letter beyond `f` — an `o` for a `0`, or an `l` for a `1`.

**Nothing happens when I press Step.** Step only works while the simulation is
stopped; press Stop first.

**Speed is set to 60 but it looks slower.** On a very large world the app caps
how much work one frame may do, so a huge lattice advances more slowly than a
small one at the same setting. Lower the cell size to shrink the world, or
lower the speed and enjoy it.

**The finds list stopped growing.** It holds 60. Delete some, or raise the
threshold so only better rules are kept.

**The determinism self-check on About says FAIL.** That should never happen.
Note the version and build number from the About screen and report it through
the Source code link.
