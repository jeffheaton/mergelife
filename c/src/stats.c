/*
 * MergeLife objective statistics.
 *
 * The measures named in the paper (background, foreground, active, chaos,
 * rect, mage, steps) are reproduced exactly as the reference implementations
 * compute them, so scores stay comparable with the published rule table.
 *
 * Four new measures are added.  The paper's `active` statistic counts cells
 * that recently stopped being background, which fires for a spaceship but
 * fires just as happily for a blinking oscillator or a noisy edge.  The new
 * measures look at *structure over time* instead:
 *
 *   ships    small shapes that survive many generations while translating,
 *            found by block-matching the foreground mask between frames
 *   guns     locations that emit three or more such shapes, spread out in
 *            time -- a spawner
 *   entvar   the spread of the sub-rule usage entropy over the run.  Wuensche
 *            showed that input-entropy variance separates Wolfram class 4
 *            (ordered *and* varied) from class 3 (varied but stationary) and
 *            class 1/2 (neither)
 *   late     whether activity is still going near the end of the run rather
 *            than being a brief opening transient
 */
#include "mergelife.h"

#include <math.h>
#include <stdlib.h>
#include <string.h>

#define ML_MAX_TRACK 16  /* concurrent block-matching trackers */
#define ML_PATCH 5       /* patch is ML_PATCH x ML_PATCH cells */
#define ML_PATCH_R 2     /* patch radius */
#define ML_SEARCH 2      /* block match search radius, in cells */
#define ML_SEED_EVERY 5  /* generations between attempts to seed a tracker */
#define ML_WARMUP 30     /* generations to settle before tracking starts */
#define ML_SHIP_AGE 10   /* generations a tracker must survive to be a ship */
#define ML_SHIP_DIST 4.0 /* cells it must travel to be a ship */
#ifndef ML_GUN_BUCKET
#define ML_GUN_BUCKET 8 /* emission sites are pooled into buckets this many cells wide */
#endif
#ifndef ML_GUN_GAP
#define ML_GUN_GAP 10 /* generations that must separate two emissions */
#endif
#ifndef ML_GUN_HITS
#define ML_GUN_HITS 2 /* emissions from one site that make it a spawner */
#endif
#ifndef ML_WAKE_MAX
#define ML_WAKE_MAX 4 /* foreground allowed behind a ship, out of 50 cells */
#endif

typedef struct {
  int active;
  int row, col;         /* current position */
  int origin_row, origin_col;
  int age;
  int counted;          /* already confirmed as a ship */
  double path;          /* summed per-step displacement */
  double net_r, net_c;  /* net displacement */
} MlShipTrack;

struct MlTracker {
  int rows, cols, size, max_steps;

  /* Per-cell state, mirroring the reference implementations. */
  uint16_t *mode_count;
  int32_t *last_mode_step;
  uint8_t *last_color;
  uint16_t *last_color_cnt;

  /* Current values of the paper statistics. */
  int mc, fg_cnt, act_cnt, inst_bg;
  int have_stats;
  int mode, mage, have_mode;
  int last_mode_count, mode_count_same;

  /* Early abandonment. */
  int dead_streak;

  /* Sub-rule usage entropy, accumulated with Welford's method. */
  double ent_mean, ent_m2;
  int ent_n;

  /* Per-generation active-cell ratio, used for `late`. */
  float *act_hist;
  int act_n;

  /* Spaceship and spawner detection. */
  int detect_ships;
  uint8_t *fg_now;  /* foreground mask this generation */
  uint8_t *fg_prev; /* foreground mask last generation */
  MlShipTrack track[ML_MAX_TRACK];
  int ship_count;
  double ship_dist_sum;
  int gun_rows, gun_cols;
  uint8_t *gun_hits;
  int32_t *gun_last;
  int gun_count;
};

MlTracker *ml_track_new(int rows, int cols, int max_steps, int detect_ships) {
  MlTracker *t = calloc(1, sizeof *t);
  if (!t) return NULL;
  t->rows = rows;
  t->cols = cols;
  t->size = rows * cols;
  t->max_steps = max_steps;
  t->detect_ships = detect_ships;
  t->mode_count = calloc((size_t)t->size, sizeof *t->mode_count);
  t->last_mode_step = calloc((size_t)t->size, sizeof *t->last_mode_step);
  t->last_color = calloc((size_t)t->size, sizeof *t->last_color);
  t->last_color_cnt = calloc((size_t)t->size, sizeof *t->last_color_cnt);
  t->act_hist = calloc((size_t)max_steps + 2, sizeof *t->act_hist);
  if (detect_ships) {
    t->fg_now = calloc((size_t)t->size, 1);
    t->fg_prev = calloc((size_t)t->size, 1);
    t->gun_rows = (rows + ML_GUN_BUCKET - 1) / ML_GUN_BUCKET;
    t->gun_cols = (cols + ML_GUN_BUCKET - 1) / ML_GUN_BUCKET;
    t->gun_hits = calloc((size_t)t->gun_rows * t->gun_cols, 1);
    t->gun_last = calloc((size_t)t->gun_rows * t->gun_cols, sizeof *t->gun_last);
  }
  if (!t->mode_count || !t->last_mode_step || !t->last_color ||
      !t->last_color_cnt || !t->act_hist ||
      (detect_ships && (!t->fg_now || !t->fg_prev || !t->gun_hits || !t->gun_last))) {
    ml_track_free(t);
    return NULL;
  }
  return t;
}

void ml_track_free(MlTracker *t) {
  if (!t) return;
  free(t->mode_count);
  free(t->last_mode_step);
  free(t->last_color);
  free(t->last_color_cnt);
  free(t->act_hist);
  free(t->fg_now);
  free(t->fg_prev);
  free(t->gun_hits);
  free(t->gun_last);
  free(t);
}

void ml_track_reset(MlTracker *t) {
  memset(t->mode_count, 0, (size_t)t->size * sizeof *t->mode_count);
  memset(t->last_mode_step, 0, (size_t)t->size * sizeof *t->last_mode_step);
  memset(t->last_color, 0, (size_t)t->size);
  memset(t->last_color_cnt, 0, (size_t)t->size * sizeof *t->last_color_cnt);
  t->mc = t->fg_cnt = t->act_cnt = t->inst_bg = 0;
  t->have_stats = 0;
  t->mode = 0;
  t->mage = 0;
  t->have_mode = 0;
  t->last_mode_count = 0;
  t->mode_count_same = 0;
  t->dead_streak = 0;
  t->ent_mean = t->ent_m2 = 0.0;
  t->ent_n = 0;
  t->act_n = 0;
  if (t->detect_ships) {
    memset(t->fg_now, 0, (size_t)t->size);
    memset(t->fg_prev, 0, (size_t)t->size);
    memset(t->track, 0, sizeof t->track);
    memset(t->gun_hits, 0, (size_t)t->gun_rows * t->gun_cols);
    memset(t->gun_last, 0, (size_t)t->gun_rows * t->gun_cols * sizeof *t->gun_last);
    t->ship_count = 0;
    t->ship_dist_sum = 0.0;
    t->gun_count = 0;
  }
}

/* ------------------------------------------------------------------ */
/* Spaceship tracking                                                  */
/* ------------------------------------------------------------------ */

/* Foreground cells inside the patch centered on (r, c). */
static int patch_weight(const uint8_t *fg, int rows, int cols, int r, int c) {
  int n = 0;
  for (int dr = -ML_PATCH_R; dr <= ML_PATCH_R; dr++) {
    int rr = r + dr;
    if (rr < 0 || rr >= rows) continue;
    for (int dc = -ML_PATCH_R; dc <= ML_PATCH_R; dc++) {
      int cc = c + dc;
      if (cc < 0 || cc >= cols) continue;
      n += fg[rr * cols + cc];
    }
  }
  return n;
}

/*
 * Foreground cells in the ring just outside the patch.
 *
 * A spaceship is a small shape with clear space around it.  Chaotic noise
 * also throws up 5x5 blocks that happen to match a shifted block in the next
 * generation, but in a chaotic grid there is no clear space, so requiring the
 * surrounding ring to be mostly background separates a real travelling
 * structure from a lucky correlation in the noise.
 */
static int ring_weight(const uint8_t *fg, int rows, int cols, int r, int c) {
  const int R = ML_PATCH_R + 1;
  int n = 0;
  for (int dr = -R; dr <= R; dr++) {
    for (int dc = -R; dc <= R; dc++) {
      if (dr > -R && dr < R && dc > -R && dc < R) continue; /* interior */
      int rr = r + dr, cc = c + dc;
      if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) continue;
      n += fg[rr * cols + cc];
    }
  }
  return n;
}

/*
 * Foreground left behind along a track's direction of travel.
 *
 * This is what separates a spaceship from a growing line.  Both have a tip
 * that translates, and a block matcher happily follows either one, but a
 * spaceship leaves background in its wake while a line leaves the line.
 * Sampling twice, at four and eight cells behind, catches both a solid trail
 * and a periodic one.
 */
static int wake_weight(const uint8_t *fg, int rows, int cols, int r, int c, double dr,
                       double dc) {
  double len = sqrt(dr * dr + dc * dc);
  if (len < 1e-6) return 0;
  dr /= len;
  dc /= len;
  int n = 0;
  for (int k = 4; k <= 8; k += 4) {
    int rr = r - (int)lrint(dr * k);
    int cc = c - (int)lrint(dc * k);
    n += patch_weight(fg, rows, cols, rr, cc);
  }
  return n;
}

/*
 * Hamming distance between the patch around (r0, c0) in `a` and the patch
 * around (r1, c1) in `b`.  Gives up as soon as it exceeds `cutoff`, which is
 * what makes the offset scan cheap.
 */
static int patch_diff(const uint8_t *a, const uint8_t *b, int rows, int cols,
                      int r0, int c0, int r1, int c1, int cutoff) {
  int diff = 0;
  for (int dr = -ML_PATCH_R; dr <= ML_PATCH_R; dr++) {
    int ra = r0 + dr, rb = r1 + dr;
    for (int dc = -ML_PATCH_R; dc <= ML_PATCH_R; dc++) {
      int ca = c0 + dc, cb = c1 + dc;
      int va = (ra < 0 || ra >= rows || ca < 0 || ca >= cols) ? 0 : a[ra * cols + ca];
      int vb = (rb < 0 || rb >= rows || cb < 0 || cb >= cols) ? 0 : b[rb * cols + cb];
      diff += (va != vb);
    }
    if (diff > cutoff) return diff;
  }
  return diff;
}

/*
 * Record a confirmed spaceship and check whether its origin is a spawner.
 *
 * "Spawner" here means a location that launched at least ML_GUN_HITS
 * spaceships separated by at least ML_GUN_GAP generations -- evidence of a
 * structure that emits repeatedly, not a proof of a Gosper-style gun.  The
 * thresholds were set by checking them against the rules the paper describes:
 * they fire on the rule the paper calls a gun and on Red World's "gun-like"
 * exhaust trails, and stay at zero for its still-life, chaos and dead rules.
 */
static void note_ship(MlTracker *t, MlShipTrack *s, int step) {
  t->ship_count++;
  t->ship_dist_sum += sqrt(s->net_r * s->net_r + s->net_c * s->net_c);

  int gr = s->origin_row / ML_GUN_BUCKET;
  int gc = s->origin_col / ML_GUN_BUCKET;
  if (gr < 0 || gr >= t->gun_rows || gc < 0 || gc >= t->gun_cols) return;
  int gi = gr * t->gun_cols + gc;
  /* Only count emissions that are separated in time; a single structure
   * breaking apart should not read as a gun. */
  if (step - t->gun_last[gi] < ML_GUN_GAP) return;
  t->gun_last[gi] = step;
  if (t->gun_hits[gi] < 255) t->gun_hits[gi]++;
  if (t->gun_hits[gi] == ML_GUN_HITS) t->gun_count++;
}

static void ships_step(MlTracker *t, const MlGrid *g) {
  const int rows = t->rows, cols = t->cols;
  const int step = g->step;

  /* Foreground mask for this generation. */
  for (int r = 0; r < rows; r++) {
    const uint8_t *m = g->merge + (size_t)(r + 1) * g->mstride + 1;
    uint8_t *f = t->fg_now + (size_t)r * cols;
    for (int c = 0; c < cols; c++) f[c] = (uint8_t)(m[c] != g->mode);
  }

  if (step > ML_WARMUP) {
    /* Advance every live tracker by block matching against the new frame. */
    for (int i = 0; i < ML_MAX_TRACK; i++) {
      MlShipTrack *s = &t->track[i];
      if (!s->active) continue;

      int w = patch_weight(t->fg_prev, rows, cols, s->row, s->col);
      if (w < 3 || w > 16) { /* dissolved, or absorbed into a large mass */
        s->active = 0;
        continue;
      }

      int best = ML_PATCH * ML_PATCH + 1, br = 0, bc = 0, ties = 0;
      for (int dr = -ML_SEARCH; dr <= ML_SEARCH; dr++) {
        for (int dc = -ML_SEARCH; dc <= ML_SEARCH; dc++) {
          int d = patch_diff(t->fg_prev, t->fg_now, rows, cols, s->row, s->col,
                             s->row + dr, s->col + dc, best);
          if (d < best) {
            best = d;
            br = dr;
            bc = dc;
            ties = 1;
          } else if (d == best) {
            ties++;
          }
        }
      }

      /* A shape that has changed too much, or that matches everywhere
       * equally well, is not something we can call a spaceship. */
      if (best > 4 || ties > 6) {
        s->active = 0;
        continue;
      }

      int nr = s->row + br, nc = s->col + bc;
      if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) {
        s->active = 0; /* left the grid */
        continue;
      }
      s->row = nr;
      s->col = nc;
      s->age++;
      s->path += sqrt((double)(br * br + bc * bc));
      s->net_r += br;
      s->net_c += bc;

      if (!s->counted && s->age >= ML_SHIP_AGE) {
        /* Still travelling through open space at the moment we confirm it. */
        if (ring_weight(t->fg_now, rows, cols, s->row, s->col) > 8) {
          s->active = 0;
          continue;
        }
        double net = sqrt(s->net_r * s->net_r + s->net_c * s->net_c);
        /* Require real translation, require it to be directed (a shape
         * jittering back and forth covers path length without going far), and
         * require it to leave clear space behind it rather than a trail. */
        if (net >= ML_SHIP_DIST && net >= 0.6 * s->path &&
            wake_weight(t->fg_now, rows, cols, s->row, s->col, s->net_r, s->net_c) <=
                ML_WAKE_MAX) {
          s->counted = 1;
          note_ship(t, s, step);
        } else if (s->age > ML_SHIP_AGE * 3) {
          s->active = 0; /* stationary oscillator, free the slot */
        }
      }
    }

    /* Seed new trackers on small isolated shapes. */
    if (step % ML_SEED_EVERY == 0) {
      for (int i = 0; i < ML_MAX_TRACK; i++) {
        MlShipTrack *s = &t->track[i];
        if (s->active) continue;
        /* Deterministic scan from a step-dependent offset, so different parts
         * of the grid get sampled over the run without needing an RNG. */
        int span = t->size;
        int start = (step * 7919 + i * 104729) % span;
        for (int k = 0; k < span; k += 13) {
          int idx = (start + k) % span;
          int r = idx / cols, c = idx % cols;
          if (r < ML_PATCH_R || r >= rows - ML_PATCH_R) continue;
          if (c < ML_PATCH_R || c >= cols - ML_PATCH_R) continue;
          if (!t->fg_now[idx]) continue;
          int w = patch_weight(t->fg_now, rows, cols, r, c);
          if (w < 3 || w > 14) continue;
          /* Must be a small shape sitting in open space, not a fragment of a
           * larger mass and not a knot in a chaotic grid. */
          if (ring_weight(t->fg_now, rows, cols, r, c) > 6) continue;
          /* Skip anything an existing tracker is already following. */
          int taken = 0;
          for (int j = 0; j < ML_MAX_TRACK; j++) {
            MlShipTrack *o = &t->track[j];
            if (o->active && abs(o->row - r) <= ML_PATCH && abs(o->col - c) <= ML_PATCH) {
              taken = 1;
              break;
            }
          }
          if (taken) continue;
          s->active = 1;
          s->row = s->origin_row = r;
          s->col = s->origin_col = c;
          s->age = 0;
          s->counted = 0;
          s->path = 0.0;
          s->net_r = s->net_c = 0.0;
          break;
        }
      }
    }
  }

  uint8_t *tmp = t->fg_prev;
  t->fg_prev = t->fg_now;
  t->fg_now = tmp;
}

/* ------------------------------------------------------------------ */
/* Per-generation tracking                                             */
/* ------------------------------------------------------------------ */

void ml_track_step(MlTracker *t, const MlGrid *g) {
  const int rows = t->rows, cols = t->cols;
  const int step = g->step;
  const int mode = g->mode;

  if (t->have_mode && t->mode == mode) {
    t->mage++;
  } else {
    t->mode = mode;
    t->mage = 0;
    t->have_mode = 1;
  }

  int mc = 0, fg = 0, act = 0, bg = 0;
  for (int r = 0; r < rows; r++) {
    const uint8_t *m = g->merge + (size_t)(r + 1) * g->mstride + 1;
    const size_t base = (size_t)r * cols;
    for (int c = 0; c < cols; c++) {
      const size_t i = base + c;
      const int v = m[c];

      if (v == mode) {
        t->mode_count[i]++;
        t->last_mode_step[i] = step;
        if (t->mode_count[i] > 50) mc++;
        bg++;
      } else {
        t->mode_count[i] = 0;
      }

      const int since = step - t->last_mode_step[i];
      if (step > 25 && since > 5 && since < 25) act++;

      if (t->last_color[i] != v || v == mode) {
        t->last_color[i] = (uint8_t)v;
        t->last_color_cnt[i] = 0;
      } else {
        t->last_color_cnt[i]++;
        if (t->last_color_cnt[i] > 5) fg++;
      }
    }
  }
  t->mc = mc;
  t->fg_cnt = fg;
  t->act_cnt = act;
  t->inst_bg = bg;
  t->have_stats = 1;

  /* Sub-rule usage entropy for this generation. */
  double h = 0.0;
  const double inv = 1.0 / (double)t->size;
  for (int i = 0; i <= ML_NOOP; i++) {
    if (!g->slot_hist[i]) continue;
    double p = (double)g->slot_hist[i] * inv;
    h -= p * log2(p);
  }
  if (step > 20) { /* skip the opening transient */
    t->ent_n++;
    double d = h - t->ent_mean;
    t->ent_mean += d / t->ent_n;
    t->ent_m2 += d * (h - t->ent_mean);
  }

  if (t->act_n < t->max_steps + 2) t->act_hist[t->act_n++] = (float)((double)act * inv);

  /* Death watch: a grid that has gone completely uniform will never recover. */
  if ((double)bg * inv > 0.999)
    t->dead_streak++;
  else
    t->dead_streak = 0;

  if (t->detect_ships) ships_step(t, g);
}

/*
 * Convergence, matching CalculateObjectiveStats.hasStabilized().
 *
 * Two details here are load bearing.  The reference calls this *before* each
 * step, so it does the "unchanged background count" bookkeeping once per
 * generation on the previous generation's statistics; and the generation cap
 * is a strict `>`, so a rule that never settles runs to generation 1001 and
 * its `steps` statistic lands *above* the objective's max of 1000.  That is
 * why surviving to the cap earns max_weight rather than the steeply negative
 * in-range value -- reproduce the loop shape or long-running rules score two
 * points lower than the published table.
 */
int ml_track_stable(MlTracker *t, const MlGrid *g, int max_steps) {
  if (!t->have_stats) return 0;

  if (t->mc == t->last_mode_count) {
    if (++t->mode_count_same > 100) return 1;
  } else {
    t->mode_count_same = 0;
    t->last_mode_count = t->mc;
  }
  return g->step > max_steps;
}

int ml_track_hopeless(const MlTracker *t, const MlGrid *g) {
  const double inv = 1.0 / (double)t->size;
  /* Converged to a single flat color. */
  if (t->dead_streak > 8) return 1;
  /* Boiling noise with no stable background at all: Wolfram class 3. */
  if (g->step > 90 && t->mc == 0 && (double)t->inst_bg * inv < 0.03) return 1;
  return 0;
}

/* ------------------------------------------------------------------ */
/* Largest background rectangle                                        */
/* ------------------------------------------------------------------ */

static int max_area_in_hist(const int *height, int n, int *stack) {
  int top = 0, i = 0, max = 0;
  while (i < n) {
    if (top == 0 || height[stack[top - 1]] <= height[i]) {
      stack[top++] = i++;
    } else {
      int h = height[stack[--top]];
      int width = (top == 0) ? i : i - stack[top - 1] - 1;
      int area = h * width;
      if (area > max) max = area;
    }
  }
  return max;
}

int ml_largest_rect(const MlGrid *g, int value) {
  const int rows = g->rows, cols = g->cols;
  int *height = calloc((size_t)cols + 1, sizeof *height);
  int *stack = calloc((size_t)cols + 1, sizeof *stack);
  if (!height || !stack) {
    free(height);
    free(stack);
    return 0;
  }
  int max = 0;
  for (int r = 0; r < rows; r++) {
    const uint8_t *m = g->merge + (size_t)(r + 1) * g->mstride + 1;
    for (int c = 0; c < cols; c++)
      height[c] = (m[c] != value) ? 0 : (r == 0 ? 1 : height[c] + 1);
    height[cols] = 0; /* sentinel so the stack drains */
    int area = max_area_in_hist(height, cols + 1, stack);
    if (area > max) max = area;
  }
  free(height);
  free(stack);
  return max;
}

/* ------------------------------------------------------------------ */
/* Finalize                                                            */
/* ------------------------------------------------------------------ */

void ml_track_finish(MlTracker *t, const MlGrid *g, MlStats *out) {
  const double inv = 1.0 / (double)t->size;
  memset(out, 0, sizeof *out);

  out->steps = g->step;
  out->background = t->mc * inv;
  out->foreground = t->fg_cnt * inv;
  out->active = t->act_cnt * inv;
  out->chaos = (t->size - (t->mc + t->fg_cnt + t->act_cnt)) * inv;
  out->mage = t->mage;
  out->mode = t->mode;
  out->rect = ml_largest_rect(g, t->mode) * inv;

  /* Distinct merged colors currently on the grid. */
  uint8_t seen[256] = {0};
  int distinct = 0;
  for (int r = 0; r < t->rows; r++) {
    const uint8_t *m = g->merge + (size_t)(r + 1) * g->mstride + 1;
    for (int c = 0; c < t->cols; c++)
      if (!seen[m[c]]) {
        seen[m[c]] = 1;
        distinct++;
      }
  }
  out->colors = distinct / 256.0;

  out->entvar = t->ent_n > 1 ? sqrt(t->ent_m2 / (t->ent_n - 1)) : 0.0;

  /* Activity late in the run relative to the middle of the run.  A rule whose
   * patterns are still moving at the end scores near or above 1. */
  if (t->act_n > 40) {
    int q0 = t->act_n / 4, q1 = t->act_n / 2, q3 = (t->act_n * 3) / 4;
    double mid = 0.0, late = 0.0;
    for (int i = q0; i < q1; i++) mid += t->act_hist[i];
    for (int i = q3; i < t->act_n; i++) late += t->act_hist[i];
    mid /= (q1 - q0);
    late /= (t->act_n - q3);
    out->late = mid > 1e-6 ? late / mid : (late > 1e-6 ? 2.0 : 0.0);
    if (out->late > 4.0) out->late = 4.0;
  }

  if (t->detect_ships) {
    /* Normalized per 10k cells so the measure does not depend on grid size. */
    out->ships = t->ship_count * 10000.0 * inv;
    out->shipdist = t->ship_count ? t->ship_dist_sum / t->ship_count : 0.0;
    out->guns = t->gun_count * 10000.0 * inv;
  }
}
