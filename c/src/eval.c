/*
 * Rule evaluation.
 *
 * A random MergeLife rule is overwhelmingly likely to be worthless: the paper
 * measured 55% chaos and 37% dead out of 100 random samples.  Spending a full
 * 5 cycles x 1000 generations on a 100x100 grid to establish that is the
 * single biggest waste in the original trainer, so evaluation happens in
 * three stages that get progressively more expensive:
 *
 *   1. a screen on a small grid with a short generation cap, scored with the
 *      subset of the objective that a short run can actually measure;
 *   2. full-fidelity cycles, each of which abandons the run the moment the
 *      grid is provably dead or provably boiling;
 *   3. a race: if two full cycles land far below the reporting threshold, the
 *      remaining cycles cannot rescue the rule, so they are skipped.
 *
 * Together these cut the average cost of rejecting a bad rule by well over an
 * order of magnitude, which is what buys the extra search throughput.
 */
#include "mergelife.h"

#include <math.h>
#include <string.h>

int ml_eval_init(MlEvaluator *e, const MlObjective *o, uint64_t seed) {
  memset(e, 0, sizeof *e);
  e->rng = seed ? seed : 0x9E3779B97F4A7C15ULL;

  if (ml_grid_init(&e->full, o->rows, o->cols)) return -1;
  if (ml_grid_init(&e->screen, o->screen_rows, o->screen_cols)) return -1;
  e->track_full = ml_track_new(o->rows, o->cols, o->max_steps, o->detect_ships);
  e->track_screen = ml_track_new(o->screen_rows, o->screen_cols, o->screen_steps, 0);
  if (!e->track_full || !e->track_screen) return -1;

  /* The screen cannot see spaceships, so drop those terms rather than let
   * them contribute a constant offset that would make screen_keep opaque. */
  e->screen_obj = *o;
  e->screen_obj.nterms = 0;
  for (int i = 0; i < o->nterms; i++) {
    const char *s = o->term[i].stat;
    if (!strcmp(s, "ships") || !strcmp(s, "guns") || !strcmp(s, "shipdist")) continue;
    e->screen_obj.term[e->screen_obj.nterms++] = o->term[i];
  }
  return 0;
}

void ml_eval_free(MlEvaluator *e) {
  ml_grid_free(&e->full);
  ml_grid_free(&e->screen);
  ml_track_free(e->track_full);
  ml_track_free(e->track_screen);
  memset(e, 0, sizeof *e);
}

/* Run one cycle to convergence and collect its statistics. */
static double run_cycle(MlEvaluator *e, MlGrid *g, MlTracker *t, const MlRule *r,
                        const MlObjective *scoring, int max_steps, int early_abort,
                        MlStats *out) {
  ml_grid_randomize(g, &e->rng);
  ml_track_reset(t);

  /* The convergence test runs *before* each step, matching the reference
   * implementations.  See ml_track_stable for why that ordering matters. */
  for (;;) {
    if (ml_track_stable(t, g, max_steps)) break;
    ml_grid_step(g, r);
    ml_track_step(t, g);
    if (early_abort && ml_track_hopeless(t, g)) break;
  }
  e->steps += (uint64_t)g->step;

  ml_track_finish(t, g, out);
  return ml_objective_score(scoring, out);
}

double ml_eval_cycle(MlEvaluator *e, const MlRule *r, const MlObjective *o, MlStats *out) {
  e->evals++;
  return run_cycle(e, &e->full, e->track_full, r, o, o->max_steps, o->early_abort, out);
}

double ml_eval_screen(MlEvaluator *e, const MlRule *r) {
  MlStats s;
  return run_cycle(e, &e->screen, e->track_screen, r, &e->screen_obj,
                   e->screen_obj.screen_steps, 1, &s);
}

int ml_eval_rule(MlEvaluator *e, const MlGenome *gen, const MlObjective *o,
                 int cycles, MlStats *best, double *score) {
  MlRule rule;
  ml_rule_compile(gen, &rule);

  if (o->screen_keep > -1e8) {
    double s = ml_eval_screen(e, &rule);
    if (s < o->screen_keep) {
      e->screened++;
      *score = s;
      return 0;
    }
  }

  /*
   * The starting grid is random, so a rule is evaluated over several cycles.
   * The paper text and the Python implementation take the max ("a rule only
   * needs to be good on one of them"); the Java implementation, which is what
   * actually produced the published rule table, takes the mean.  Both are
   * available; max is the default.
   */
  double top = -1e30, sum = 0.0;
  int n = 0;
  const double race_cut = o->threshold - 3.0;
  for (int i = 0; i < cycles; i++) {
    MlStats s;
    double v = ml_eval_cycle(e, &rule, o, &s);
    sum += v;
    n++;
    if (v > top) {
      top = v;
      *best = s;
    }
    /* Under max semantics a rule two cycles deep and still far below the
     * threshold cannot be rescued by the remaining cycles.  Under mean
     * semantics later cycles can still move the result, so run them all. */
    if (!o->aggregate_mean && i + 1 >= 2 && top < race_cut) break;
  }
  *score = o->aggregate_mean ? sum / n : top;
  return 1;
}
