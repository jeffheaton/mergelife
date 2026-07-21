/*
 * MergeLife CA engine.
 *
 * This reproduces the reference Java/JavaScript implementations bit for bit,
 * so any rule found here animates identically in the on-line viewer.  The
 * speed comes from restructuring the arithmetic, never from approximating it:
 *
 *   - Every key color component is 0 or 255, so the update
 *     g' = g + floor((key - g) * pct) has only two possible transfer curves
 *     per sub-rule.  Both are precomputed into byte->byte tables, which
 *     removes all floating point from the inner loop.
 *   - Selecting the sub-rule is a search for the first alpha above the
 *     neighbor count.  That is precomputed into a 2041-entry table, which
 *     removes the inner search.
 *   - The neighbor count is a 3x3 box sum minus the center, so it is computed
 *     with a separable pass (~5 operations per cell instead of 8 loads with
 *     bounds checks).  A one cell halo holding the background color removes
 *     the edge tests entirely.
 */
#include "mergelife.h"

#include <math.h>
#include <stdlib.h>
#include <string.h>

const uint8_t ML_KEY_COLOR[8][3] = {
    {0, 0, 0},       /* 0 black  */
    {255, 0, 0},     /* 1 red    */
    {0, 255, 0},     /* 2 green  */
    {255, 255, 0},   /* 3 yellow */
    {0, 0, 255},     /* 4 blue   */
    {255, 0, 255},   /* 5 purple */
    {0, 255, 255},   /* 6 cyan   */
    {255, 255, 255}, /* 7 white  */
};

double ml_rand_gauss(uint64_t *s) {
  /* Box-Muller; the second deviate is discarded, which is fine here. */
  double u1 = ml_rand_d(s);
  double u2 = ml_rand_d(s);
  if (u1 < 1e-300) u1 = 1e-300;
  return sqrt(-2.0 * log(u1)) * cos(6.283185307179586 * u2);
}

/* ------------------------------------------------------------------ */
/* Rule parsing and compilation                                        */
/* ------------------------------------------------------------------ */

static int hexval(int c) {
  if (c >= '0' && c <= '9') return c - '0';
  if (c >= 'a' && c <= 'f') return c - 'a' + 10;
  if (c >= 'A' && c <= 'F') return c - 'A' + 10;
  return -1;
}

int ml_genome_parse(const char *text, MlGenome *out) {
  int n = 0;
  for (const char *p = text; *p && n < ML_GENOME_BYTES * 2; p++) {
    int v = hexval((unsigned char)*p);
    if (v < 0) continue; /* hyphens and other separators are ignored */
    if (n % 2 == 0)
      out->b[n / 2] = (uint8_t)(v << 4);
    else
      out->b[n / 2] |= (uint8_t)v;
    n++;
  }
  return n == ML_GENOME_BYTES * 2 ? 0 : -1;
}

void ml_genome_format(const MlGenome *g, char *out) {
  static const char hex[] = "0123456789abcdef";
  int o = 0;
  for (int i = 0; i < ML_SUBRULES; i++) {
    if (i) out[o++] = '-';
    for (int j = 0; j < 2; j++) {
      uint8_t b = g->b[i * 2 + j];
      out[o++] = hex[b >> 4];
      out[o++] = hex[b & 0xf];
    }
  }
  out[o] = '\0';
}

void ml_rule_compile(const MlGenome *g, MlRule *out) {
  int alpha[ML_SUBRULES], gamma[ML_SUBRULES];
  double beta[ML_SUBRULES];

  for (int i = 0; i < ML_SUBRULES; i++) {
    int o1 = g->b[i * 2];
    int o2 = (int8_t)g->b[i * 2 + 1]; /* octet-2 is two's complement */
    int a = o1 * 8;
    if (a == 2040) a = 2048; /* the top sub-rule covers the whole range */
    alpha[i] = a;
    beta[i] = o2 > 0 ? o2 / 127.0 : o2 / 128.0;
    gamma[i] = i;
  }

  /* Sort by alpha.  This must be a *stable* sort so that ties keep their
   * original hex-string order, matching Collections.sort and Array.sort in
   * the reference implementations. */
  for (int i = 1; i < ML_SUBRULES; i++) {
    int a = alpha[i], c = gamma[i];
    double b = beta[i];
    int j = i - 1;
    while (j >= 0 && alpha[j] > a) {
      alpha[j + 1] = alpha[j];
      beta[j + 1] = beta[j];
      gamma[j + 1] = gamma[j];
      j--;
    }
    alpha[j + 1] = a;
    beta[j + 1] = b;
    gamma[j + 1] = c;
  }

  memcpy(out->alpha, alpha, sizeof alpha);
  memcpy(out->beta, beta, sizeof beta);
  memcpy(out->gamma, gamma, sizeof gamma);

  /* Neighbor count -> sub-rule slot.  The applicable sub-rule is the first,
   * in sorted order, whose alpha exceeds the neighbor count. */
  out->has_noop = 0;
  for (int nc = 0; nc <= ML_MAX_NC; nc++) {
    int slot = ML_NOOP;
    for (int i = 0; i < ML_SUBRULES; i++) {
      if (nc < alpha[i]) {
        slot = i;
        break;
      }
    }
    if (slot == ML_NOOP) out->has_noop = 1;
    out->nc2slot[nc] = (uint8_t)slot;
  }

  /* Per-slot, per-channel transfer tables. */
  for (int i = 0; i < ML_SUBRULES; i++) {
    int key = gamma[i];
    double pct = beta[i];
    if (pct < 0) {
      /* A negative percent targets the *next* key color, wrapping round. */
      pct = -pct;
      key = (key + 1) & 7;
    }
    for (int ch = 0; ch < 3; ch++) {
      int target = ML_KEY_COLOR[key][ch];
      for (int v = 0; v < 256; v++) {
        int delta = (int)floor((double)(target - v) * pct);
        out->chan[i][ch][v] = (uint8_t)(v + delta);
      }
    }
  }
  for (int ch = 0; ch < 3; ch++)
    for (int v = 0; v < 256; v++) out->chan[ML_NOOP][ch][v] = (uint8_t)v;
}

/* ------------------------------------------------------------------ */
/* Grid                                                                */
/* ------------------------------------------------------------------ */

int ml_grid_init(MlGrid *g, int rows, int cols) {
  memset(g, 0, sizeof *g);
  g->rows = rows;
  g->cols = cols;
  g->size = rows * cols;
  g->mstride = cols + 2;
  g->rgb[0] = calloc((size_t)g->size * 3, 1);
  g->rgb[1] = calloc((size_t)g->size * 3, 1);
  g->merge = calloc((size_t)(rows + 2) * g->mstride, 1);
  g->hsum = calloc((size_t)(rows + 2) * cols, sizeof(uint16_t));
  if (!g->rgb[0] || !g->rgb[1] || !g->merge || !g->hsum) {
    ml_grid_free(g);
    return -1;
  }
  return 0;
}

void ml_grid_free(MlGrid *g) {
  free(g->rgb[0]);
  free(g->rgb[1]);
  free(g->merge);
  free(g->hsum);
  memset(g, 0, sizeof *g);
}

void ml_grid_randomize(MlGrid *g, uint64_t *rng) {
  uint8_t *p = g->rgb[0];
  size_t n = (size_t)g->size * 3;
  size_t i = 0;
  /* Eight bytes of randomness at a time. */
  for (; i + 8 <= n; i += 8) {
    uint64_t r = ml_rand(rng);
    memcpy(p + i, &r, 8);
  }
  for (; i < n; i++) p[i] = (uint8_t)ml_rand(rng);
  /* The reference implementations start the second buffer at zero, which is
   * observable for rules that leave some cells untouched (see ml_grid_step). */
  memset(g->rgb[1], 0, n);
  g->cur = 0;
  g->step = 0;
  g->mode = 0;
}

/* Merge the RGB channels, find the background color, and fill the halo. */
static void merge_and_mode(MlGrid *g) {
  uint32_t hist[256] = {0};
  const uint8_t *src = g->rgb[g->cur];
  const int rows = g->rows, cols = g->cols, ms = g->mstride;

  for (int r = 0; r < rows; r++) {
    uint8_t *m = g->merge + (r + 1) * ms + 1;
    const uint8_t *s = src + (size_t)r * cols * 3;
    for (int c = 0; c < cols; c++, s += 3) {
      uint8_t v = (uint8_t)((s[0] + s[1] + s[2]) / 3); /* floor, as in the reference */
      m[c] = v;
      hist[v]++;
    }
  }

  /* First index holding the maximum, matching indexOf(max(...)) / the Java loop. */
  int best = 0;
  for (int i = 1; i < 256; i++)
    if (hist[i] > hist[best]) best = i;
  g->mode = best;

  uint8_t b = (uint8_t)best;
  memset(g->merge, b, (size_t)ms);                          /* top halo    */
  memset(g->merge + (size_t)(rows + 1) * ms, b, (size_t)ms); /* bottom halo */
  for (int r = 1; r <= rows; r++) {
    g->merge[r * ms] = b;            /* left halo  */
    g->merge[r * ms + cols + 1] = b; /* right halo */
  }
}

void ml_grid_step(MlGrid *g, const MlRule *rule) {
  const int rows = g->rows, cols = g->cols, ms = g->mstride;

  merge_and_mode(g);

  /* Horizontal 3-sums for every padded row, including the two halo rows. */
  for (int pr = 0; pr < rows + 2; pr++) {
    const uint8_t *m = g->merge + (size_t)pr * ms;
    uint16_t *h = g->hsum + (size_t)pr * cols;
    for (int c = 0; c < cols; c++) h[c] = (uint16_t)(m[c] + m[c + 1] + m[c + 2]);
  }

  const uint8_t *src = g->rgb[g->cur];
  uint8_t *dst = g->rgb[g->cur ^ 1];
  memset(g->slot_hist, 0, sizeof g->slot_hist);

  for (int r = 0; r < rows; r++) {
    const int pr = r + 1;
    const uint16_t *h0 = g->hsum + (size_t)(pr - 1) * cols;
    const uint16_t *h1 = g->hsum + (size_t)pr * cols;
    const uint16_t *h2 = g->hsum + (size_t)(pr + 1) * cols;
    const uint8_t *m = g->merge + (size_t)pr * ms + 1;
    const uint8_t *s = src + (size_t)r * cols * 3;
    uint8_t *d = dst + (size_t)r * cols * 3;

    if (!rule->has_noop) {
      /* Common case: every neighbor count selects a sub-rule, so the inner
       * loop is three table lookups with no branches. */
      for (int c = 0; c < cols; c++, s += 3, d += 3) {
        int nc = h0[c] + h1[c] + h2[c] - m[c];
        unsigned slot = rule->nc2slot[nc];
        g->slot_hist[slot]++;
        d[0] = rule->chan[slot][0][s[0]];
        d[1] = rule->chan[slot][1][s[1]];
        d[2] = rule->chan[slot][2][s[2]];
      }
    } else {
      for (int c = 0; c < cols; c++, s += 3, d += 3) {
        int nc = h0[c] + h1[c] + h2[c] - m[c];
        unsigned slot = rule->nc2slot[nc];
        g->slot_hist[slot]++;
        /* Paper: a cell matched by no sub-rule keeps its current value.
         * chan[ML_NOOP] is the identity map, so fall through and copy s -> d. */
        d[0] = rule->chan[slot][0][s[0]];
        d[1] = rule->chan[slot][1][s[1]];
        d[2] = rule->chan[slot][2][s[2]];
      }
    }
  }

  g->cur ^= 1;
  g->step++;
}
