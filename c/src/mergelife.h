/*
 * MergeLife -- high performance trainer.
 * Copyright 2018 by Jeff Heaton, http://www.heatonresearch.com/mergelife/
 * MIT License
 *
 * Core types shared by the CA engine, the objective statistics, and the search.
 */
#ifndef MERGELIFE_H
#define MERGELIFE_H

#include <stddef.h>
#include <stdint.h>

/* ------------------------------------------------------------------ */
/* Rules                                                               */
/* ------------------------------------------------------------------ */

#define ML_SUBRULES 8
#define ML_GENOME_BYTES 16 /* 8 sub-rules x (octet-1, octet-2) */
#define ML_MAX_NC 2040     /* 8 neighbors x 255 */
#define ML_NOOP 8          /* sub-rule slot meaning "no rule applies" */

/* The 8 MergeLife key colors, every component is either 0 or 255. */
extern const uint8_t ML_KEY_COLOR[8][3];

/*
 * A genome is the raw 16 bytes behind the hex string.  Byte 2i is octet-1 of
 * sub-rule i (unsigned) and byte 2i+1 is octet-2 (two's complement signed).
 */
typedef struct {
  uint8_t b[ML_GENOME_BYTES];
} MlGenome;

/*
 * A rule compiled into the lookup tables the inner loop actually uses.
 *
 * The update is  g' = g + floor((key - g) * pct).  Because every key color
 * component is 0 or 255 there are only two possible transfer curves per
 * sub-rule, so the whole update collapses into a byte->byte table per channel
 * and the inner loop needs no floating point at all.
 */
typedef struct {
  uint8_t nc2slot[ML_MAX_NC + 1]; /* neighbor count -> sub-rule slot (or ML_NOOP) */
  uint8_t chan[ML_NOOP + 1][3][256]; /* [slot][channel][old value] -> new value */
  int alpha[ML_SUBRULES];            /* sorted sub-rule ranges, for display */
  double beta[ML_SUBRULES];
  int gamma[ML_SUBRULES];
  int has_noop; /* nonzero if some neighbor count matches no sub-rule */
} MlRule;

/* Parse "cb97-6a74-..." (separators optional).  Returns 0 on success. */
int ml_genome_parse(const char *text, MlGenome *out);
/* Format as the canonical lowercase hyphenated hex string. `out` needs 40 bytes. */
void ml_genome_format(const MlGenome *g, char *out);
/* Compile a genome into lookup tables. */
void ml_rule_compile(const MlGenome *g, MlRule *out);

/* ------------------------------------------------------------------ */
/* Grid                                                                */
/* ------------------------------------------------------------------ */

typedef struct {
  int rows, cols, size;
  int mstride; /* cols + 2, the padded merge grid row stride */
  uint8_t *rgb[2];
  int cur;
  uint8_t *merge; /* padded (rows+2) x (cols+2), halo holds the mode */
  uint16_t *hsum; /* (rows+2) x cols horizontal 3-sums, scratch */
  int mode;       /* current background: mode of the merged grid */
  int step;
  uint32_t slot_hist[ML_NOOP + 1]; /* sub-rule usage for the last step */
} MlGrid;

int ml_grid_init(MlGrid *g, int rows, int cols);
void ml_grid_free(MlGrid *g);
void ml_grid_randomize(MlGrid *g, uint64_t *rng);
void ml_grid_step(MlGrid *g, const MlRule *r);
/* Read-only view of the merged grid cell (row, col). */
#define ML_MERGE(g, row, col) ((g)->merge[((row) + 1) * (g)->mstride + (col) + 1])

/* ------------------------------------------------------------------ */
/* Objective statistics                                                */
/* ------------------------------------------------------------------ */

/*
 * Statistics collected while a rule runs.  The first block reproduces the
 * measures from the paper; the second block is new and is what lets the
 * search aim specifically at spaceships and spawners.
 */
typedef struct {
  /* --- from "Evolving continuous cellular automata for aesthetic objectives" */
  double steps;      /* CA generations before the grid stabilized */
  double background; /* fraction of cells that have been background > 50 gens */
  double foreground; /* fraction holding a stable non-background color ("still life") */
  double active;     /* fraction background 5-25 gens ago but not since */
  double chaos;      /* everything else */
  double rect;       /* largest all-background rectangle, as a fraction of the grid */
  double mage;       /* generations since the background color last changed */
  double mode;       /* current background color */
  double colors;     /* distinct merged colors present, as a fraction of 256 */

  /* --- new measures */
  double ships;     /* confirmed translating structures per 10k cells */
  double shipdist;  /* mean distance those structures travelled, in cells */
  double guns;      /* sites that emitted >= 3 spaceships over the run */
  double entvar;    /* std-dev of sub-rule usage entropy: a Wolfram class 4 proxy */
  double late;      /* activity in the final quarter of the run vs. the first */
} MlStats;

/* Per-run statistic tracker.  Reused across runs via ml_track_reset. */
typedef struct MlTracker MlTracker;

MlTracker *ml_track_new(int rows, int cols, int max_steps, int detect_ships);
void ml_track_free(MlTracker *t);
void ml_track_reset(MlTracker *t);
/* Called once per CA generation, after ml_grid_step. */
void ml_track_step(MlTracker *t, const MlGrid *g);
/* True once the grid is considered converged. */
int ml_track_stable(MlTracker *t, const MlGrid *g, int max_steps);
/* Finalize: computes the expensive statistics (rect, ships, guns). */
void ml_track_finish(MlTracker *t, const MlGrid *g, MlStats *out);
/* True if the run is already hopeless and can be abandoned early. */
int ml_track_hopeless(const MlTracker *t, const MlGrid *g);

/* Largest axis-aligned rectangle of `value` in the merged grid, in cells. */
int ml_largest_rect(const MlGrid *g, int value);

/* ------------------------------------------------------------------ */
/* Objective function                                                  */
/* ------------------------------------------------------------------ */

#define ML_MAX_OBJ 16

typedef struct {
  char stat[16];
  char shape[8]; /* "legacy" (reference formula), "peak", or "rise" */
  double min, max, weight, min_weight, max_weight;
} MlObjTerm;

typedef struct {
  MlObjTerm term[ML_MAX_OBJ];
  int nterms;
  int rows, cols;      /* full fidelity grid */
  int screen_rows;     /* cheap screening grid */
  int screen_cols;
  int cycles;          /* evaluation cycles at full fidelity */
  int max_steps;       /* hard cap on CA generations */
  int screen_steps;    /* hard cap while screening */
  double screen_keep;  /* screen score needed to earn a full evaluation */
  double threshold;    /* report rules scoring at least this */
  int detect_ships;    /* run the spaceship tracker at full fidelity */
  int early_abort;     /* abandon runs that are already dead or chaotic */
  int aggregate_mean;  /* combine cycles by mean (Java) instead of max (paper text) */
} MlObjective;

/* Named presets: "spaceships", "guns", "paper". Returns 0 on success. */
int ml_objective_preset(const char *name, MlObjective *out);
/* Load the objective from a paperObjective.json style file. Returns 0 on success. */
int ml_objective_load(const char *path, MlObjective *out, char *err, size_t errlen);
/* Score one already-collected set of statistics. */
double ml_objective_score(const MlObjective *o, const MlStats *s);
/* Look a statistic up by name; returns 0 if the name is unknown. */
int ml_stats_get(const MlStats *s, const char *name, double *out);

/* ------------------------------------------------------------------ */
/* Evaluation                                                          */
/* ------------------------------------------------------------------ */

/* Everything one worker thread needs to evaluate rules, allocated once. */
typedef struct {
  MlGrid full, screen;
  MlTracker *track_full, *track_screen;
  MlObjective screen_obj; /* the objective minus terms the screen cannot measure */
  uint64_t rng;
  uint64_t evals;    /* full-fidelity cycles run */
  uint64_t screened; /* rules rejected by the screen */
  uint64_t steps;    /* CA generations computed */
} MlEvaluator;

int ml_eval_init(MlEvaluator *e, const MlObjective *o, uint64_t seed);
void ml_eval_free(MlEvaluator *e);
/* One cycle at full fidelity. */
double ml_eval_cycle(MlEvaluator *e, const MlRule *r, const MlObjective *o, MlStats *out);
/* One cheap cycle on the small grid, used to reject hopeless rules fast. */
double ml_eval_screen(MlEvaluator *e, const MlRule *r);
/*
 * Full evaluation: the max score over up to `cycles` cycles.  Returns 1 if the
 * rule was fully evaluated and 0 if the cheap screen rejected it, in which
 * case *score holds the screen score and *best is not meaningful.
 */
int ml_eval_rule(MlEvaluator *e, const MlGenome *gen, const MlObjective *o,
                 int cycles, MlStats *best, double *score);

/* ------------------------------------------------------------------ */
/* Random numbers (xoshiro-style splitmix64, one state word per thread) */
/* ------------------------------------------------------------------ */

static inline uint64_t ml_rand(uint64_t *s) {
  uint64_t z = (*s += 0x9E3779B97F4A7C15ULL);
  z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9ULL;
  z = (z ^ (z >> 27)) * 0x94D049BB133111EBULL;
  return z ^ (z >> 31);
}
static inline uint32_t ml_rand_n(uint64_t *s, uint32_t n) {
  return (uint32_t)((ml_rand(s) >> 32) * (uint64_t)n >> 32);
}
static inline double ml_rand_d(uint64_t *s) {
  return (double)(ml_rand(s) >> 11) * (1.0 / 9007199254740992.0);
}
double ml_rand_gauss(uint64_t *s);

#endif /* MERGELIFE_H */
