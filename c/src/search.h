/*
 * MAP-Elites archive and search driver.
 */
#ifndef ML_SEARCH_H
#define ML_SEARCH_H

#include "mergelife.h"

#include <pthread.h>
#include <stdatomic.h>
#include <stdio.h>

/* Archive shape: open space x motion x static structure. */
#define ML_BINS_RECT 8
#define ML_BINS_ACTIVE 8
#define ML_BINS_FG 6

#define ML_SEED_CELLS 24    /* random sampling until the archive has this many */
#define ML_MIN_DISTANCE 4   /* bytes that must differ for a rule to count as new */
#define ML_MAX_FOUND 4096

typedef struct {
  MlGenome genome;
  double score;
  MlStats stats;
  int filled;
} MlElite;

typedef struct {
  MlElite *cell;
  int ncells;
  int filled;
  double best;
  uint64_t inserts;
  pthread_mutex_t lock;
} MlArchive;

typedef struct {
  MlObjective obj;
  MlArchive arch;
  volatile int stop;
  double start_time;
  FILE *out;

  MlGenome found[ML_MAX_FOUND];
  int nfound;
  pthread_mutex_t found_lock;

  _Atomic uint64_t total_candidates; /* rules considered, screened or not */
  _Atomic uint64_t total_evals;      /* full-fidelity cycles run */
  _Atomic uint64_t total_screened;   /* rules the cheap screen rejected */
  _Atomic uint64_t total_steps;      /* CA generations computed */
} MlSearch;

int ml_archive_init(MlArchive *a);
void ml_archive_free(MlArchive *a);

/*
 * Run until `seconds` elapse (or forever if <= 0), printing a status line
 * every `report_every` seconds.  The archive is initialized here and is left
 * intact on return so the caller can summarize it; free it with
 * ml_archive_free.
 */
int ml_search_run(MlSearch *s, int threads, double seconds, double report_every);

double ml_now(void);

#endif /* ML_SEARCH_H */
