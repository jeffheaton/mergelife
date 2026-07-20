/*
 * The search.
 *
 * The original trainer is a steady-state GA over a single population, with a
 * restart whenever the population stops improving.  That design spends most
 * of its time re-converging on the same basin, and it throws away everything
 * it learned at each restart -- the paper reports 18,796 convergences to
 * collect 1000 rules, a 5.3% yield.
 *
 * This is MAP-Elites instead.  The archive is a grid of behavior cells, keyed
 * on three things you can see when you watch a rule run: how much open space
 * there is, how much of the grid is moving, and how much static structure has
 * settled out.  Each cell keeps the best rule found with that behavior.  Two
 * consequences matter here:
 *
 *   - There is no premature convergence to escape, so no restarts.  A rule
 *     that is mediocre overall but occupies an empty cell is kept, and it
 *     becomes the stepping stone to a good rule in a neighboring cell.
 *   - The output is a diverse set of rules by construction, which is what you
 *     actually want from a search for interesting automata.  The paper notes
 *     it wanted "many interesting rules rather than one global maximum";
 *     that is exactly what this algorithm optimizes for.
 *
 * Variation is done with operators aligned to the genome's real structure --
 * whole sub-rules, and byte-level creep on the (range, percent) pair -- rather
 * than on hex digits, which cut bytes in half and turn a small semantic step
 * into an arbitrary one.
 */
#include "mergelife.h"
#include "search.h"

#include <math.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

/* ------------------------------------------------------------------ */
/* Behavior descriptors                                                */
/* ------------------------------------------------------------------ */

static int bin_of(double v, double lo, double hi, int nbins) {
  if (!(v > lo)) return 0;
  double t = (v - lo) / (hi - lo);
  if (t > 1.0) t = 1.0;
  t = sqrt(t); /* these statistics cluster near zero, so spread them out */
  int b = (int)(t * nbins);
  return b >= nbins ? nbins - 1 : b;
}

static int cell_index(const MlArchive *a, const MlStats *s) {
  int r = bin_of(s->rect, 0.0, 1.0, ML_BINS_RECT);
  int c = bin_of(s->active, 0.0, 0.25, ML_BINS_ACTIVE);
  int f = bin_of(s->foreground, 0.0, 0.30, ML_BINS_FG);
  (void)a;
  return (r * ML_BINS_ACTIVE + c) * ML_BINS_FG + f;
}

/* ------------------------------------------------------------------ */
/* Variation operators                                                 */
/* ------------------------------------------------------------------ */

static void random_genome(MlGenome *g, uint64_t *rng) {
  for (int i = 0; i < ML_GENOME_BYTES; i += 8) {
    uint64_t r = ml_rand(rng);
    memcpy(g->b + i, &r, 8);
  }
}

static uint8_t clamp_o1(double v) {
  if (v < 0) v = 0;
  if (v > 255) v = 255;
  return (uint8_t)lrint(v);
}

static uint8_t clamp_o2(double v) {
  if (v < -128) v = -128;
  if (v > 127) v = 127;
  return (uint8_t)(int8_t)lrint(v);
}

/* Gaussian creep on the (range, percent) pair of a few sub-rules.  This is
 * the operator that exploits the smoothness the paper measured: small moves
 * in a sub-rule's threshold produce small moves in behavior. */
static void mutate_creep(MlGenome *g, uint64_t *rng) {
  int n = 1 + (int)ml_rand_n(rng, 3);
  for (int k = 0; k < n; k++) {
    int i = (int)ml_rand_n(rng, ML_SUBRULES);
    if (ml_rand_d(rng) < 0.5)
      g->b[i * 2] = clamp_o1(g->b[i * 2] + ml_rand_gauss(rng) * 14.0);
    else
      g->b[i * 2 + 1] = clamp_o2((int8_t)g->b[i * 2 + 1] + ml_rand_gauss(rng) * 22.0);
  }
}

/* The original operator: flip one hex digit.  Kept because it makes jumps
 * that byte-level creep is unlikely to make. */
static void mutate_digit(MlGenome *g, uint64_t *rng) {
  int i = (int)ml_rand_n(rng, ML_GENOME_BYTES);
  int hi = (int)ml_rand_n(rng, 2);
  int v = (int)ml_rand_n(rng, 16);
  if (hi)
    g->b[i] = (uint8_t)((g->b[i] & 0x0f) | (v << 4));
  else
    g->b[i] = (uint8_t)((g->b[i] & 0xf0) | v);
}

/* Swap two whole sub-rules, remapping which key colors the ranges reach. */
static void mutate_swap(MlGenome *g, uint64_t *rng) {
  int i = (int)ml_rand_n(rng, ML_SUBRULES);
  int j = (int)ml_rand_n(rng, ML_SUBRULES);
  if (i == j) return;
  uint8_t t0 = g->b[i * 2], t1 = g->b[i * 2 + 1];
  g->b[i * 2] = g->b[j * 2];
  g->b[i * 2 + 1] = g->b[j * 2 + 1];
  g->b[j * 2] = t0;
  g->b[j * 2 + 1] = t1;
}

/* Uniform crossover at sub-rule granularity: never splits a (range, percent)
 * pair, unlike a cut at an arbitrary hex offset. */
static void crossover_subrule(MlGenome *out, const MlGenome *a, const MlGenome *b,
                              uint64_t *rng) {
  for (int i = 0; i < ML_SUBRULES; i++) {
    const MlGenome *src = (ml_rand_d(rng) < 0.5) ? a : b;
    out->b[i * 2] = src->b[i * 2];
    out->b[i * 2 + 1] = src->b[i * 2 + 1];
  }
}

/*
 * "Iso+LineDD": step isotropically from one elite, plus a step along the line
 * joining it to a second elite.  The line term lets the search follow
 * directions that are already paying off in the archive, and it is the
 * standard reason modern MAP-Elites converges faster than plain mutation.
 */
static void crossover_isoline(MlGenome *out, const MlGenome *a, const MlGenome *b,
                              uint64_t *rng) {
  const double iso = 0.055 * 255.0, line = 0.18;
  double l = ml_rand_gauss(rng) * line;
  for (int i = 0; i < ML_SUBRULES; i++) {
    double d1 = (double)a->b[i * 2] + ml_rand_gauss(rng) * iso +
                l * ((double)b->b[i * 2] - (double)a->b[i * 2]);
    out->b[i * 2] = clamp_o1(d1);
    double x = (int8_t)a->b[i * 2 + 1], y = (int8_t)b->b[i * 2 + 1];
    double d2 = x + ml_rand_gauss(rng) * iso + l * (y - x);
    out->b[i * 2 + 1] = clamp_o2(d2);
  }
}

/* ------------------------------------------------------------------ */
/* Archive                                                             */
/* ------------------------------------------------------------------ */

int ml_archive_init(MlArchive *a) {
  memset(a, 0, sizeof *a);
  a->ncells = ML_BINS_RECT * ML_BINS_ACTIVE * ML_BINS_FG;
  a->cell = calloc((size_t)a->ncells, sizeof *a->cell);
  a->best = -1e30;
  if (!a->cell) return -1;
  if (pthread_mutex_init(&a->lock, NULL)) return -1;
  return 0;
}

void ml_archive_free(MlArchive *a) {
  free(a->cell);
  pthread_mutex_destroy(&a->lock);
  memset(a, 0, sizeof *a);
}

/* Copy out a random occupied cell.  Returns 0 if the archive is still empty. */
static int archive_sample(MlArchive *a, MlGenome *out, uint64_t *rng) {
  if (a->filled == 0) return 0;
  for (int tries = 0; tries < 64; tries++) {
    int i = (int)ml_rand_n(rng, (uint32_t)a->ncells);
    if (a->cell[i].filled) {
      *out = a->cell[i].genome;
      return 1;
    }
  }
  for (int i = 0; i < a->ncells; i++)
    if (a->cell[i].filled) {
      *out = a->cell[i].genome;
      return 1;
    }
  return 0;
}

/* ------------------------------------------------------------------ */
/* Reporting                                                           */
/* ------------------------------------------------------------------ */

static int byte_distance(const MlGenome *a, const MlGenome *b) {
  int n = 0;
  for (int i = 0; i < ML_GENOME_BYTES; i++)
    if (a->b[i] != b->b[i]) n++;
  return n;
}

/* True if this rule is far enough from everything already reported. */
static int is_novel(const MlSearch *s, const MlGenome *g) {
  for (int i = 0; i < s->nfound; i++)
    if (byte_distance(g, &s->found[i]) < ML_MIN_DISTANCE) return 0;
  return 1;
}

static void report_found(MlSearch *s, const MlGenome *g, double score, const MlStats *st) {
  char hex[40];
  ml_genome_format(g, hex);

  double elapsed = ml_now() - s->start_time;
  printf("\n"
         "  FOUND  %s\n"
         "         score %.3f   after %.0fs\n"
         "         ships %.1f  guns %.1f  travel %.1f   rect %.3f  active %.4f  "
         "foreground %.4f\n"
         "         generations %.0f  late %.2f  entropy-var %.3f\n",
         hex, score, elapsed, st->ships, st->guns, st->shipdist, st->rect, st->active,
         st->foreground, st->steps, st->late, st->entvar);
  fflush(stdout);

  if (s->out) {
    fprintf(s->out,
            "%s\tscore=%.4f\tships=%.2f\tguns=%.2f\tshipdist=%.2f\trect=%.4f\t"
            "active=%.5f\tforeground=%.5f\tsteps=%.0f\tlate=%.3f\tentvar=%.4f\n",
            hex, score, st->ships, st->guns, st->shipdist, st->rect, st->active,
            st->foreground, st->steps, st->late, st->entvar);
    fflush(s->out);
  }
}

/* ------------------------------------------------------------------ */
/* Worker                                                              */
/* ------------------------------------------------------------------ */

typedef struct {
  MlSearch *s;
  int id;
} WorkerArg;

static void *worker(void *varg) {
  WorkerArg *arg = varg;
  MlSearch *s = arg->s;
  const MlObjective *o = &s->obj;

  MlEvaluator ev;
  if (ml_eval_init(&ev, o, 0x243F6A8885A308D3ULL ^ ((uint64_t)(arg->id + 1) * 0x9E3779B97F4A7C15ULL))) {
    fprintf(stderr, "worker %d: out of memory\n", arg->id);
    return NULL;
  }
  uint64_t published_evals = 0, published_screened = 0, published_steps = 0;
  int since_publish = 0;

  while (!s->stop) {
    MlGenome child, parent, mate;
    int have_parent, have_mate;

    pthread_mutex_lock(&s->arch.lock);
    have_parent = archive_sample(&s->arch, &parent, &ev.rng);
    have_mate = archive_sample(&s->arch, &mate, &ev.rng);
    int filled = s->arch.filled;
    pthread_mutex_unlock(&s->arch.lock);

    /* Seed the archive with random rules until it has something to build on,
     * then keep a trickle of random injection to stay exploratory. */
    double r = ml_rand_d(&ev.rng);
    if (!have_parent || filled < ML_SEED_CELLS || r < 0.06) {
      random_genome(&child, &ev.rng);
    } else {
      child = parent;
      if (r < 0.30 && have_mate) {
        crossover_isoline(&child, &parent, &mate, &ev.rng);
      } else if (r < 0.44 && have_mate) {
        crossover_subrule(&child, &parent, &mate, &ev.rng);
      } else if (r < 0.78) {
        mutate_creep(&child, &ev.rng);
      } else if (r < 0.92) {
        mutate_digit(&child, &ev.rng);
      } else {
        mutate_swap(&child, &ev.rng);
        mutate_creep(&child, &ev.rng);
      }
    }

    atomic_fetch_add_explicit(&s->total_candidates, 1, memory_order_relaxed);
    if (++since_publish >= 32) {
      since_publish = 0;
      atomic_fetch_add_explicit(&s->total_evals, ev.evals - published_evals, memory_order_relaxed);
      atomic_fetch_add_explicit(&s->total_screened, ev.screened - published_screened, memory_order_relaxed);
      atomic_fetch_add_explicit(&s->total_steps, ev.steps - published_steps, memory_order_relaxed);
      published_evals = ev.evals;
      published_screened = ev.screened;
      published_steps = ev.steps;
    }

    MlStats st;
    double score;
    if (!ml_eval_rule(&ev, &child, o, o->cycles, &st, &score)) continue;

    /* Insert into the archive. */
    int idx = cell_index(&s->arch, &st);
    int improved = 0;
    pthread_mutex_lock(&s->arch.lock);
    MlElite *cell = &s->arch.cell[idx];
    if (!cell->filled || score > cell->score) {
      if (!cell->filled) s->arch.filled++;
      cell->filled = 1;
      cell->genome = child;
      cell->score = score;
      cell->stats = st;
      improved = 1;
    }
    if (score > s->arch.best) s->arch.best = score;
    s->arch.inserts++;
    pthread_mutex_unlock(&s->arch.lock);

    /* Anything at or above the threshold gets an independent confirmation
     * run before it is reported.  The objective is noisy -- the paper measures
     * a standard deviation of about 0.5 -- so without this the output fills
     * up with rules that were simply lucky once. */
    if (improved && score >= o->threshold) {
      pthread_mutex_lock(&s->found_lock);
      int novel = is_novel(s, &child) && s->nfound < ML_MAX_FOUND;
      pthread_mutex_unlock(&s->found_lock);

      if (novel) {
        MlStats cst;
        double cscore;
        if (ml_eval_rule(&ev, &child, o, o->cycles, &cst, &cscore) &&
            cscore >= o->threshold) {
          pthread_mutex_lock(&s->found_lock);
          if (is_novel(s, &child) && s->nfound < ML_MAX_FOUND) {
            s->found[s->nfound++] = child;
            pthread_mutex_unlock(&s->found_lock);
            report_found(s, &child, cscore, &cst);
          } else {
            pthread_mutex_unlock(&s->found_lock);
          }
        }
      }
    }

    /* Occasionally re-score an existing elite with fresh starting grids and
     * overwrite its stored score.  Without this, a cell that was once filled
     * by a lucky evaluation keeps that inflated score forever and stops
     * accepting the genuinely better rules that follow. */
    if (ml_rand_d(&ev.rng) < 0.04 && filled > 0) {
      pthread_mutex_lock(&s->arch.lock);
      int i = (int)ml_rand_n(&ev.rng, (uint32_t)s->arch.ncells);
      MlGenome g;
      int ok = s->arch.cell[i].filled;
      if (ok) g = s->arch.cell[i].genome;
      pthread_mutex_unlock(&s->arch.lock);

      if (ok) {
        MlStats rst;
        double rscore;
        if (ml_eval_rule(&ev, &g, o, o->cycles, &rst, &rscore)) {
          pthread_mutex_lock(&s->arch.lock);
          MlElite *c = &s->arch.cell[i];
          if (c->filled && !memcmp(&c->genome, &g, sizeof g)) {
            c->score = rscore;
            c->stats = rst;
          }
          pthread_mutex_unlock(&s->arch.lock);
        }
      }
    }
  }

  atomic_fetch_add_explicit(&s->total_evals, ev.evals - published_evals, memory_order_relaxed);
  atomic_fetch_add_explicit(&s->total_screened, ev.screened - published_screened, memory_order_relaxed);
  atomic_fetch_add_explicit(&s->total_steps, ev.steps - published_steps, memory_order_relaxed);

  ml_eval_free(&ev);
  return NULL;
}

/* ------------------------------------------------------------------ */
/* Driver                                                              */
/* ------------------------------------------------------------------ */

double ml_now(void) {
  struct timespec ts;
  clock_gettime(CLOCK_MONOTONIC, &ts);
  return (double)ts.tv_sec + (double)ts.tv_nsec * 1e-9;
}

int ml_search_run(MlSearch *s, int threads, double seconds, double report_every) {
  if (ml_archive_init(&s->arch)) return -1;
  if (pthread_mutex_init(&s->found_lock, NULL)) return -1;
  s->start_time = ml_now();
  s->stop = 0;

  pthread_t *tid = calloc((size_t)threads, sizeof *tid);
  WorkerArg *args = calloc((size_t)threads, sizeof *args);
  if (!tid || !args) return -1;

  for (int i = 0; i < threads; i++) {
    args[i].s = s;
    args[i].id = i;
    if (pthread_create(&tid[i], NULL, worker, &args[i])) {
      fprintf(stderr, "cannot start thread %d\n", i);
      s->stop = 1;
      break;
    }
  }

  /* The main thread only reports; workers never block on it. */
  double next_report = s->start_time + report_every;
  uint64_t last_candidates = 0;
  double last_time = s->start_time;

  while (!s->stop) {
    struct timespec nap = {0, 200 * 1000 * 1000};
    nanosleep(&nap, NULL);

    double now = ml_now();
    if (seconds > 0 && now - s->start_time >= seconds) s->stop = 1;
    if (now < next_report && !s->stop) continue;
    next_report = now + report_every;

    pthread_mutex_lock(&s->arch.lock);
    int filled = s->arch.filled;
    double best = s->arch.best;
    pthread_mutex_unlock(&s->arch.lock);

    pthread_mutex_lock(&s->found_lock);
    int nfound = s->nfound;
    pthread_mutex_unlock(&s->found_lock);

    uint64_t cand = atomic_load_explicit(&s->total_candidates, memory_order_relaxed);
    uint64_t screened = atomic_load_explicit(&s->total_screened, memory_order_relaxed);

    double dt = now - last_time;
    double rate = dt > 0 ? (double)(cand - last_candidates) / dt * 60.0 : 0.0;
    last_candidates = cand;
    last_time = now;

    printf("[%6.0fs] rules/min %-8.0f  archive %3d/%-3d (%2.0f%%)  best %7.3f  "
           "screened %2.0f%%  found %d\n",
           now - s->start_time, rate, filled, s->arch.ncells,
           100.0 * filled / s->arch.ncells, best,
           cand ? 100.0 * (double)screened / (double)cand : 0.0, nfound);
    fflush(stdout);
  }

  s->stop = 1;
  for (int i = 0; i < threads; i++) pthread_join(tid[i], NULL);
  free(tid);
  free(args);
  return 0;
}
