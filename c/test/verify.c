/*
 * Equivalence test for the optimized CA engine.
 *
 * naive_step below is a direct transcription of MergeLifeGrid.step() from the
 * reference Java implementation: per-cell neighbor loop with bounds checks,
 * per-cell linear search through the sorted sub-rules, and floating point
 * arithmetic for the color delta.  It is deliberately slow and obvious.
 *
 * The optimized engine replaces all three of those with lookup tables and a
 * separable box filter.  This test fuzzes the two against each other over
 * random rules and random grids and requires the full RGB buffers to match
 * byte for byte at every generation.
 */
#include "../src/mergelife.h"

#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct {
  int rows, cols;
  uint8_t *rgb[2];
  int cur;
  uint8_t *merge;
  int mode;
} NaiveGrid;

/* MergeLifeGrid.calculateModeGrid */
static void naive_mode(NaiveGrid *g) {
  int hist[256] = {0};
  const uint8_t *px = g->rgb[g->cur];
  for (int r = 0; r < g->rows; r++)
    for (int c = 0; c < g->cols; c++) {
      const uint8_t *p = px + ((size_t)r * g->cols + c) * 3;
      int a = (p[0] + p[1] + p[2]) / 3;
      g->merge[(size_t)r * g->cols + c] = (uint8_t)a;
      hist[a]++;
    }
  int maxIndex = -1;
  for (int i = 0; i < 256; i++)
    if (maxIndex == -1 || hist[i] > hist[maxIndex]) maxIndex = i;
  g->mode = maxIndex;
}

/* MergeLifeGrid.countNeighbors */
static int naive_neighbors(const NaiveGrid *g, int row, int col) {
  static const int xHat[] = {0, 0, -1, 1, -1, 1, 1, -1};
  static const int yHat[] = {-1, 1, 0, 0, -1, 1, -1, 1};
  int sum = 0;
  for (int i = 0; i < 8; i++) {
    int nr = yHat[i] + row, nc = xHat[i] + col;
    if (nr >= 0 && nc >= 0 && nr < g->rows && nc < g->cols)
      sum += g->merge[(size_t)nr * g->cols + nc];
    else
      sum += g->mode;
  }
  return sum;
}

/* MergeLifeGrid.step */
static void naive_step(NaiveGrid *g, const MlRule *rule) {
  int target = g->cur == 0 ? 1 : 0;
  naive_mode(g);

  for (int row = 0; row < g->rows; row++) {
    for (int col = 0; col < g->cols; col++) {
      int c = naive_neighbors(g, row, col);
      const uint8_t *line = g->rgb[g->cur] + ((size_t)row * g->cols + col) * 3;
      uint8_t *linePrime = g->rgb[target] + ((size_t)row * g->cols + col) * 3;

      for (int d = 0; d < ML_SUBRULES; d++) {
        if (c < rule->alpha[d]) {
          int dPrime = rule->gamma[d] + 1;
          if (rule->beta[d] < 0) dPrime = (dPrime % 8) + 1;
          for (int j = 0; j < 3; j++) {
            int delta = ML_KEY_COLOR[dPrime - 1][j] - line[j];
            delta = (int)floor(delta * fabs(rule->beta[d]));
            linePrime[j] = (uint8_t)(line[j] + delta);
          }
          break;
        }
      }
    }
  }
  g->cur = target;
}

int main(int argc, char **argv) {
  const int rows = 37, cols = 53; /* deliberately not square or a multiple of 8 */
  const int trials = argc > 1 ? atoi(argv[1]) : 300;
  const int steps = 40;

  MlGrid fast;
  if (ml_grid_init(&fast, rows, cols)) return 1;

  NaiveGrid slow = {rows, cols, {NULL, NULL}, 0, NULL, 0};
  slow.rgb[0] = calloc((size_t)rows * cols * 3, 1);
  slow.rgb[1] = calloc((size_t)rows * cols * 3, 1);
  slow.merge = calloc((size_t)rows * cols, 1);
  if (!slow.rgb[0] || !slow.rgb[1] || !slow.merge) return 1;

  uint64_t rng = 0xC0FFEE123456789ULL;
  int noop_rules = 0, tie_rules = 0;

  for (int t = 0; t < trials; t++) {
    MlGenome gen;
    for (int i = 0; i < ML_GENOME_BYTES; i++) gen.b[i] = (uint8_t)ml_rand(&rng);

    /* Bias a few trials towards rules that leave cells untouched and rules
     * with tied ranges, since those are the tricky cases. */
    if (t % 5 == 0) gen.b[(t / 5) % 8 * 2] = 255;
    if (t % 7 == 0) {
      gen.b[0] = gen.b[2];
      gen.b[4] = gen.b[2];
    }

    MlRule rule;
    ml_rule_compile(&gen, &rule);
    if (rule.has_noop) noop_rules++;
    for (int i = 1; i < ML_SUBRULES; i++)
      if (rule.alpha[i] == rule.alpha[i - 1]) {
        tie_rules++;
        break;
      }

    /* Identical starting conditions: buffer 0 random, buffer 1 zeroed. */
    ml_grid_randomize(&fast, &rng);
    memcpy(slow.rgb[0], fast.rgb[0], (size_t)rows * cols * 3);
    memset(slow.rgb[1], 0, (size_t)rows * cols * 3);
    slow.cur = 0;

    for (int s = 0; s < steps; s++) {
      ml_grid_step(&fast, &rule);
      naive_step(&slow, &rule);

      if (memcmp(fast.rgb[fast.cur], slow.rgb[slow.cur], (size_t)rows * cols * 3)) {
        char hex[40];
        ml_genome_format(&gen, hex);
        printf("MISMATCH trial %d rule %s at generation %d\n", t, hex, s + 1);
        for (size_t i = 0; i < (size_t)rows * cols * 3; i++)
          if (fast.rgb[fast.cur][i] != slow.rgb[slow.cur][i]) {
            printf("  first difference at byte %zu: fast=%d naive=%d\n", i,
                   fast.rgb[fast.cur][i], slow.rgb[slow.cur][i]);
            break;
          }
        return 1;
      }
      if (fast.mode != slow.mode) {
        printf("MISMATCH trial %d: background %d vs %d at generation %d\n", t, fast.mode,
               slow.mode, s + 1);
        return 1;
      }
    }
  }

  printf("PASS  %d rules x %d generations on a %dx%d grid, byte identical\n", trials, steps,
         rows, cols);
  printf("      (%d rules left some cells untouched, %d had tied ranges)\n", noop_rules,
         tie_rules);

  ml_grid_free(&fast);
  free(slow.rgb[0]);
  free(slow.rgb[1]);
  free(slow.merge);
  return 0;
}
