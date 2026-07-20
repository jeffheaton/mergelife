/*
 * mergelife-trainer -- search for interesting MergeLife rules.
 *
 * Usage examples:
 *   mergelife-trainer                                  search with the default preset
 *   mergelife-trainer --preset guns --time 600         search for spawners for 10 minutes
 *   mergelife-trainer --config paperObjective.json     use a published objective file
 *   mergelife-trainer --eval e542-5f79-...             print every statistic for one rule
 *   mergelife-trainer --render e542-5f79-... -o r.png  render a filmstrip of one rule
 */
#include "mergelife.h"
#include "search.h"

#include <ctype.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <zlib.h>

#ifdef __APPLE__
#include <sys/sysctl.h>
#endif

static int cpu_count(void) {
  long n = sysconf(_SC_NPROCESSORS_ONLN);
  return n > 0 ? (int)n : 4;
}

/* ------------------------------------------------------------------ */
/* PNG output                                                          */
/* ------------------------------------------------------------------ */

static void png_chunk(FILE *f, const char *tag, const unsigned char *data, size_t len) {
  unsigned char hdr[4] = {(unsigned char)(len >> 24), (unsigned char)(len >> 16),
                          (unsigned char)(len >> 8), (unsigned char)len};
  fwrite(hdr, 1, 4, f);
  fwrite(tag, 1, 4, f);
  if (len) fwrite(data, 1, len, f);
  uLong crc = crc32(0, (const Bytef *)tag, 4);
  if (len) crc = crc32(crc, data, (uInt)len);
  unsigned char c[4] = {(unsigned char)(crc >> 24), (unsigned char)(crc >> 16),
                        (unsigned char)(crc >> 8), (unsigned char)crc};
  fwrite(c, 1, 4, f);
}

/* Write an RGB buffer as a PNG.  `rgb` is height x width x 3. */
static int png_write(const char *path, const unsigned char *rgb, int width, int height) {
  FILE *f = fopen(path, "wb");
  if (!f) return -1;
  fwrite("\x89PNG\r\n\x1a\n", 1, 8, f);

  unsigned char ihdr[13] = {(unsigned char)(width >> 24),  (unsigned char)(width >> 16),
                            (unsigned char)(width >> 8),   (unsigned char)width,
                            (unsigned char)(height >> 24), (unsigned char)(height >> 16),
                            (unsigned char)(height >> 8),  (unsigned char)height,
                            8, 2, 0, 0, 0}; /* 8 bit, truecolor */
  png_chunk(f, "IHDR", ihdr, sizeof ihdr);

  /* Each scanline is prefixed with a filter byte (0 = none). */
  size_t raw_len = (size_t)height * ((size_t)width * 3 + 1);
  unsigned char *raw = malloc(raw_len);
  if (!raw) {
    fclose(f);
    return -1;
  }
  for (int y = 0; y < height; y++) {
    unsigned char *dst = raw + (size_t)y * ((size_t)width * 3 + 1);
    *dst++ = 0;
    memcpy(dst, rgb + (size_t)y * width * 3, (size_t)width * 3);
  }

  uLongf zlen = compressBound(raw_len);
  unsigned char *z = malloc(zlen);
  if (!z || compress2(z, &zlen, raw, raw_len, 6) != Z_OK) {
    free(raw);
    free(z);
    fclose(f);
    return -1;
  }
  png_chunk(f, "IDAT", z, zlen);
  png_chunk(f, "IEND", NULL, 0);
  free(raw);
  free(z);
  fclose(f);
  return 0;
}

/* ------------------------------------------------------------------ */
/* Modes                                                               */
/* ------------------------------------------------------------------ */

static void print_stats(const char *hex, double score, const MlStats *s) {
  printf("rule        %s\n", hex);
  printf("score       %.4f\n\n", score);
  printf("  steps       %8.0f    generations before the grid settled\n", s->steps);
  printf("  background  %8.4f    cells background for over 50 generations\n", s->background);
  printf("  foreground  %8.4f    cells holding a stable non-background color\n", s->foreground);
  printf("  active      %8.4f    cells recently vacated by the background\n", s->active);
  printf("  chaos       %8.4f    everything else\n", s->chaos);
  printf("  rect        %8.4f    largest clear rectangle\n", s->rect);
  printf("  mage        %8.0f    generations since the background changed\n", s->mage);
  printf("  colors      %8.4f    distinct merged colors present\n", s->colors);
  printf("  ships       %8.2f    translating structures per 10k cells\n", s->ships);
  printf("  shipdist    %8.2f    mean distance they travelled\n", s->shipdist);
  printf("  guns        %8.2f    repeat emission sites per 10k cells\n", s->guns);
  printf("  entvar      %8.4f    sub-rule entropy spread (class 4 marker)\n", s->entvar);
  printf("  late        %8.4f    late activity vs mid-run activity\n", s->late);
}

static int mode_eval(const char *rule, MlObjective *o, int cycles, uint64_t seed) {
  MlGenome g;
  if (ml_genome_parse(rule, &g)) {
    fprintf(stderr, "cannot parse rule: %s\n", rule);
    return 1;
  }
  o->screen_keep = -1e9; /* never screen an explicit request */

  MlEvaluator ev;
  if (ml_eval_init(&ev, o, seed)) {
    fprintf(stderr, "out of memory\n");
    return 1;
  }

  MlStats best;
  double score;
  ml_eval_rule(&ev, &g, o, cycles, &best, &score);

  char hex[40];
  ml_genome_format(&g, hex);
  print_stats(hex, score, &best);
  printf("\n(best of %d cycles on a %dx%d grid)\n", cycles, o->rows, o->cols);
  ml_eval_free(&ev);
  return 0;
}

/*
 * Render a filmstrip: the same run sampled at several generations, left to
 * right.  A single frame cannot show whether anything is moving, which is the
 * whole point of these rules.
 */
static int mode_render(const char *rule, MlObjective *o, const char *path, int frames,
                       int steps, int zoom, uint64_t seed, int start, int every) {
  MlGenome g;
  if (ml_genome_parse(rule, &g)) {
    fprintf(stderr, "cannot parse rule: %s\n", rule);
    return 1;
  }
  MlRule r;
  ml_rule_compile(&g, &r);

  MlGrid grid;
  if (ml_grid_init(&grid, o->rows, o->cols)) {
    fprintf(stderr, "out of memory\n");
    return 1;
  }
  uint64_t rng = seed ? seed : 12345;
  ml_grid_randomize(&grid, &rng);

  const int gap = 4;
  const int fw = o->cols * zoom, fh = o->rows * zoom;
  const int W = frames * fw + (frames - 1) * gap, H = fh;
  unsigned char *img = calloc((size_t)W * H * 3, 1);
  if (!img) {
    ml_grid_free(&grid);
    return 1;
  }
  memset(img, 0x20, (size_t)W * H * 3); /* gap color */

  /* Frames land at `start`, then every `every` generations.  Close spacing is
   * what makes a spaceship visible: it shifts a few cells between frames,
   * while still life and oscillators stay put. */
  if (every <= 0) every = steps / frames;
  if (start <= 0) start = every;
  for (int fidx = 0; fidx < frames; fidx++) {
    int target = start + fidx * every;
    while (grid.step < target) ml_grid_step(&grid, &r);

    const unsigned char *src = grid.rgb[grid.cur];
    const int x0 = fidx * (fw + gap);
    for (int row = 0; row < o->rows; row++) {
      for (int col = 0; col < o->cols; col++) {
        const unsigned char *px = src + ((size_t)row * o->cols + col) * 3;
        for (int dy = 0; dy < zoom; dy++) {
          unsigned char *dst = img + ((size_t)(row * zoom + dy) * W + x0 + col * zoom) * 3;
          for (int dx = 0; dx < zoom; dx++, dst += 3) {
            dst[0] = px[0];
            dst[1] = px[1];
            dst[2] = px[2];
          }
        }
      }
    }
  }

  int rc = png_write(path, img, W, H);
  if (rc)
    fprintf(stderr, "cannot write %s\n", path);
  else
    printf("wrote %s  (%d frames, generations %d..%d step %d, %dx%d)\n", path, frames,
           start, start + (frames - 1) * every, every, W, H);

  free(img);
  ml_grid_free(&grid);
  return rc ? 1 : 0;
}

/* Throughput measurement, for comparing against the other implementations. */
static int mode_bench(MlObjective *o, int seconds) {
  MlGenome g;
  ml_genome_parse("e542-5f79-9341-f31e-6c6b-7f08-8773-7068", &g);
  MlRule r;
  ml_rule_compile(&g, &r);

  MlGrid grid;
  if (ml_grid_init(&grid, o->rows, o->cols)) return 1;
  uint64_t rng = 99;
  ml_grid_randomize(&grid, &rng);

  double t0 = ml_now(), t1;
  long steps = 0;
  do {
    for (int i = 0; i < 200; i++) ml_grid_step(&grid, &r);
    steps += 200;
    t1 = ml_now();
  } while (t1 - t0 < seconds);

  double dt = t1 - t0;
  double cells = (double)steps * grid.size;
  printf("CA engine: %ld generations of %dx%d in %.2fs\n", steps, o->rows, o->cols, dt);
  printf("           %.0f generations/sec, %.1f million cell updates/sec\n", steps / dt,
         cells / dt / 1e6);
  ml_grid_free(&grid);
  return 0;
}

/*
 * Equivalence check against the reference implementations: run a rule from a
 * grid loaded from disk and print a checksum of the result.
 */
static int mode_hash(const char *rule, const char *gridfile, int rows, int cols, int steps) {
  MlGenome g;
  if (ml_genome_parse(rule, &g)) {
    fprintf(stderr, "cannot parse rule: %s\n", rule);
    return 1;
  }
  MlRule r;
  ml_rule_compile(&g, &r);

  MlGrid grid;
  if (ml_grid_init(&grid, rows, cols)) return 1;

  FILE *f = fopen(gridfile, "rb");
  if (!f) {
    fprintf(stderr, "cannot open %s\n", gridfile);
    ml_grid_free(&grid);
    return 1;
  }
  size_t want = (size_t)rows * cols * 3;
  if (fread(grid.rgb[0], 1, want, f) != want) {
    fprintf(stderr, "%s is not %zu bytes\n", gridfile, want);
    fclose(f);
    ml_grid_free(&grid);
    return 1;
  }
  fclose(f);
  memset(grid.rgb[1], 0, want);
  grid.cur = 0;
  grid.step = 0;

  for (int i = 0; i < steps; i++) ml_grid_step(&grid, &r);

  /* FNV-1a over the final grid. */
  uint64_t h = 1469598103934665603ULL;
  const unsigned char *p = grid.rgb[grid.cur];
  for (size_t i = 0; i < want; i++) {
    h ^= p[i];
    h *= 1099511628211ULL;
  }
  printf("steps=%d mode=%d hash=%016llx\n", steps, grid.mode, (unsigned long long)h);
  ml_grid_free(&grid);
  return 0;
}

/* ------------------------------------------------------------------ */
/* Training                                                            */
/* ------------------------------------------------------------------ */

static int cmp_elite(const void *a, const void *b) {
  double sa = (*(const MlElite *const *)a)->score;
  double sb = (*(const MlElite *const *)b)->score;
  return sa < sb ? 1 : (sa > sb ? -1 : 0);
}

static void print_archive(MlSearch *s, int top) {
  MlElite **v = calloc((size_t)s->arch.ncells, sizeof *v);
  if (!v) return;
  int n = 0;
  for (int i = 0; i < s->arch.ncells; i++)
    if (s->arch.cell[i].filled) v[n++] = &s->arch.cell[i];
  qsort(v, (size_t)n, sizeof *v, cmp_elite);

  printf("\nBest %d of %d archived rules\n", n < top ? n : top, n);
  printf("%-42s %8s %7s %6s %7s %8s %7s\n", "rule", "score", "ships", "guns", "rect",
         "active", "steps");
  for (int i = 0; i < n && i < top; i++) {
    char hex[40];
    ml_genome_format(&v[i]->genome, hex);
    printf("%-42s %8.3f %7.1f %6.1f %7.3f %8.4f %7.0f\n", hex, v[i]->score,
           v[i]->stats.ships, v[i]->stats.guns, v[i]->stats.rect, v[i]->stats.active,
           v[i]->stats.steps);
  }
  free(v);
}

static void usage(void) {
  printf(
      "mergelife-trainer -- search for interesting MergeLife rules\n"
      "\n"
      "  --preset NAME     spaceships (default), guns, or paper\n"
      "  --config FILE     load an objective in the published JSON format\n"
      "  --threads N       worker threads (default: one per core)\n"
      "  --time SECONDS    stop after this long (default: run until interrupted)\n"
      "  --report SECONDS  status line interval (default 15)\n"
      "  --threshold X     report rules scoring at least X\n"
      "  --cycles N        evaluation cycles per rule (default 5)\n"
      "  --rows N --cols N grid size (default 100x100)\n"
      "  --no-screen       evaluate every rule at full fidelity\n"
      "  --aggregate WHICH how to combine cycles: max (default) or mean (Java)\n"
      "  --seed N          random seed\n"
      "  -o, --out FILE    append found rules to a tab separated file\n"
      "  --top N           how many archived rules to list at the end (default 25)\n"
      "\n"
      "  --eval RULE       print every statistic for one rule and exit\n"
      "  --render RULE     render a filmstrip to --out (PNG) and exit\n"
      "    --frames N        frames in the strip (default 4)\n"
      "    --steps N         final generation (default 1000)\n"
      "    --start N         generation of the first frame\n"
      "    --every N         generations between frames\n"
      "    --zoom N          pixels per cell (default 3)\n"
      "  --bench [SECS]    measure CA throughput and exit\n"
      "  --hash RULE       run --grid FILE for --steps and print a checksum\n");
}

int main(int argc, char **argv) {
  MlObjective obj;
  ml_objective_preset("spaceships", &obj);

  const char *eval_rule = NULL, *render_rule = NULL, *hash_rule = NULL;
  const char *gridfile = NULL, *outpath = NULL, *config = NULL;
  int threads = 0, top = 25, frames = 4, steps = 1000, zoom = 3, bench = 0;
  int start = 0, every = 0;
  double seconds = 0, report_every = 15.0;
  double threshold_override = 0;
  int have_threshold = 0, cycles_override = 0, no_screen = 0, agg_mean = -1;
  uint64_t seed = 0;

  for (int i = 1; i < argc; i++) {
    const char *a = argv[i];
#define NEXT(var)                                                      \
  do {                                                                 \
    if (i + 1 >= argc) {                                               \
      fprintf(stderr, "%s needs a value\n", a);                        \
      return 2;                                                        \
    }                                                                  \
    var = argv[++i];                                                   \
  } while (0)

    if (!strcmp(a, "-h") || !strcmp(a, "--help")) {
      usage();
      return 0;
    } else if (!strcmp(a, "--preset")) {
      const char *v;
      NEXT(v);
      if (ml_objective_preset(v, &obj)) {
        fprintf(stderr, "unknown preset: %s (try spaceships, guns, paper)\n", v);
        return 2;
      }
    } else if (!strcmp(a, "--config")) {
      NEXT(config);
    } else if (!strcmp(a, "--threads")) {
      const char *v;
      NEXT(v);
      threads = atoi(v);
    } else if (!strcmp(a, "--time")) {
      const char *v;
      NEXT(v);
      seconds = atof(v);
    } else if (!strcmp(a, "--report")) {
      const char *v;
      NEXT(v);
      report_every = atof(v);
    } else if (!strcmp(a, "--threshold")) {
      const char *v;
      NEXT(v);
      threshold_override = atof(v);
      have_threshold = 1;
    } else if (!strcmp(a, "--cycles")) {
      const char *v;
      NEXT(v);
      cycles_override = atoi(v);
    } else if (!strcmp(a, "--rows")) {
      const char *v;
      NEXT(v);
      obj.rows = atoi(v);
    } else if (!strcmp(a, "--cols")) {
      const char *v;
      NEXT(v);
      obj.cols = atoi(v);
    } else if (!strcmp(a, "--aggregate")) {
      const char *v;
      NEXT(v);
      if (!strcmp(v, "mean")) agg_mean = 1;
      else if (!strcmp(v, "max")) agg_mean = 0;
      else {
        fprintf(stderr, "--aggregate takes max or mean\n");
        return 2;
      }
    } else if (!strcmp(a, "--no-screen")) {
      no_screen = 1;
    } else if (!strcmp(a, "--seed")) {
      const char *v;
      NEXT(v);
      seed = strtoull(v, NULL, 10);
    } else if (!strcmp(a, "-o") || !strcmp(a, "--out")) {
      NEXT(outpath);
    } else if (!strcmp(a, "--top")) {
      const char *v;
      NEXT(v);
      top = atoi(v);
    } else if (!strcmp(a, "--eval")) {
      NEXT(eval_rule);
    } else if (!strcmp(a, "--render")) {
      NEXT(render_rule);
    } else if (!strcmp(a, "--hash")) {
      NEXT(hash_rule);
    } else if (!strcmp(a, "--grid")) {
      NEXT(gridfile);
    } else if (!strcmp(a, "--frames")) {
      const char *v;
      NEXT(v);
      frames = atoi(v);
    } else if (!strcmp(a, "--steps")) {
      const char *v;
      NEXT(v);
      steps = atoi(v);
    } else if (!strcmp(a, "--start")) {
      const char *v;
      NEXT(v);
      start = atoi(v);
    } else if (!strcmp(a, "--every")) {
      const char *v;
      NEXT(v);
      every = atoi(v);
    } else if (!strcmp(a, "--zoom")) {
      const char *v;
      NEXT(v);
      zoom = atoi(v);
    } else if (!strcmp(a, "--bench")) {
      bench = 5;
      if (i + 1 < argc && isdigit((unsigned char)argv[i + 1][0])) bench = atoi(argv[++i]);
    } else {
      fprintf(stderr, "unknown option: %s (try --help)\n", a);
      return 2;
    }
#undef NEXT
  }

  if (config) {
    char err[256];
    if (ml_objective_load(config, &obj, err, sizeof err)) {
      fprintf(stderr, "%s\n", err);
      return 2;
    }
  }
  if (have_threshold) obj.threshold = threshold_override;
  if (cycles_override) obj.cycles = cycles_override;
  if (no_screen) obj.screen_keep = -1e9;
  if (agg_mean >= 0) obj.aggregate_mean = agg_mean;
  if (obj.screen_rows > obj.rows) obj.screen_rows = obj.rows;
  if (obj.screen_cols > obj.cols) obj.screen_cols = obj.cols;

  if (bench) return mode_bench(&obj, bench);
  if (hash_rule) {
    if (!gridfile) {
      fprintf(stderr, "--hash needs --grid FILE\n");
      return 2;
    }
    return mode_hash(hash_rule, gridfile, obj.rows, obj.cols, steps);
  }
  if (eval_rule) return mode_eval(eval_rule, &obj, obj.cycles, seed);
  if (render_rule)
    return mode_render(render_rule, &obj, outpath ? outpath : "mergelife.png", frames,
                       steps, zoom, seed, start, every);

  /* --- training --- */
  if (threads <= 0) threads = cpu_count();

  MlSearch *s = calloc(1, sizeof *s);
  if (!s) {
    fprintf(stderr, "out of memory\n");
    return 1;
  }
  s->obj = obj;
  if (outpath) {
    s->out = fopen(outpath, "a");
    if (!s->out) fprintf(stderr, "warning: cannot append to %s\n", outpath);
  }

  printf("MergeLife trainer\n");
  printf("  objective   %s%s with %d terms, reporting at %.2f\n", config ? config : "preset ",
         config ? "" : (obj.detect_ships ? "" : "paper"), obj.nterms, obj.threshold);
  printf("  grid        %dx%d, up to %d generations, %d cycles per rule\n", obj.rows,
         obj.cols, obj.max_steps, obj.cycles);
  printf("  screening   %s\n",
         obj.screen_keep > -1e8 ? "on (small grid pre-pass)" : "off");
  printf("  threads     %d\n", threads);
  if (seconds > 0) printf("  running for %.0f seconds\n", seconds);
  printf("\n");
  fflush(stdout);

  ml_search_run(s, threads, seconds, report_every);

  uint64_t cand = atomic_load(&s->total_candidates);
  uint64_t screened = atomic_load(&s->total_screened);
  uint64_t cycles = atomic_load(&s->total_evals);
  uint64_t casteps = atomic_load(&s->total_steps);
  double elapsed = ml_now() - s->start_time;

  printf("\n%.0f seconds: %llu rules considered (%.0f/min), %llu screened out, "
         "%llu full cycles, %.1fM CA generations\n",
         elapsed, (unsigned long long)cand, cand / elapsed * 60.0,
         (unsigned long long)screened, (unsigned long long)cycles, casteps / 1e6);

  print_archive(s, top);

  if (s->out) fclose(s->out);
  ml_archive_free(&s->arch);
  free(s);
  return 0;
}
