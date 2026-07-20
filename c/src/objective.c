/*
 * MergeLife objective function.
 *
 * Each term maps one statistic onto a contribution to the score.  Three
 * shapes are available:
 *
 *   legacy  the formula used by the reference implementations and by
 *           Algorithm 3 of the paper.  Kept bit-for-bit so that the `paper`
 *           preset reproduces published scores.
 *   peak    full weight at the midpoint of [min, max], tapering to zero at
 *           both ends.  This is what "legacy" reads as if you assume `ideal`
 *           means the midpoint -- legacy actually centers the peak on half
 *           the *range*, which for a term like steps 300..1000 puts the
 *           reward peak at 350 and penalizes the long runs the paper says it
 *           wants.
 *   rise    zero at min rising to full weight at max, for "more is better"
 *           statistics such as ships, guns and longevity.
 */
#include "mergelife.h"

#include <ctype.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

enum { SHAPE_LEGACY = 0, SHAPE_PEAK, SHAPE_RISE };

static int shape_of(const char *s) {
  if (!strcmp(s, "peak")) return SHAPE_PEAK;
  if (!strcmp(s, "rise")) return SHAPE_RISE;
  return SHAPE_LEGACY;
}

int ml_stats_get(const MlStats *s, const char *name, double *out) {
  static const struct {
    const char *name;
    size_t off;
  } table[] = {
      {"steps", offsetof(MlStats, steps)},
      {"background", offsetof(MlStats, background)},
      {"foreground", offsetof(MlStats, foreground)},
      {"active", offsetof(MlStats, active)},
      {"chaos", offsetof(MlStats, chaos)},
      {"rect", offsetof(MlStats, rect)},
      {"mage", offsetof(MlStats, mage)},
      {"mode", offsetof(MlStats, mode)},
      {"colors", offsetof(MlStats, colors)},
      {"ships", offsetof(MlStats, ships)},
      {"shipdist", offsetof(MlStats, shipdist)},
      {"guns", offsetof(MlStats, guns)},
      {"entvar", offsetof(MlStats, entvar)},
      {"late", offsetof(MlStats, late)},
  };
  for (size_t i = 0; i < sizeof table / sizeof *table; i++)
    if (!strcmp(table[i].name, name)) {
      *out = *(const double *)((const char *)s + table[i].off);
      return 1;
    }
  return 0;
}

double ml_objective_score(const MlObjective *o, const MlStats *s) {
  double score = 0.0;
  for (int i = 0; i < o->nterms; i++) {
    const MlObjTerm *t = &o->term[i];
    double x;
    if (!ml_stats_get(s, t->stat, &x)) continue;

    if (x < t->min) {
      score += t->min_weight;
      continue;
    }
    if (x > t->max) {
      score += t->max_weight;
      continue;
    }

    const double range = t->max - t->min;
    double adjust;
    switch (shape_of(t->shape)) {
      case SHAPE_PEAK: {
        const double mid = t->min + range / 2.0;
        adjust = 1.0 - fabs(x - mid) / (range / 2.0);
        break;
      }
      case SHAPE_RISE:
        adjust = range > 0 ? (x - t->min) / range : 1.0;
        break;
      default: {
        /* The reference formula.  Note `ideal` is half the range, not the
         * midpoint of [min, max]; this is deliberate, see the header note. */
        const double ideal = range / 2.0;
        adjust = ((range / 2.0) - fabs(x - ideal)) / (range / 2.0);
        break;
      }
    }
    score += adjust * t->weight;
  }
  return score;
}

/* ------------------------------------------------------------------ */
/* Presets                                                             */
/* ------------------------------------------------------------------ */

static void term(MlObjective *o, const char *stat, const char *shape, double min,
                 double max, double w, double minw, double maxw) {
  if (o->nterms >= ML_MAX_OBJ) return;
  MlObjTerm *t = &o->term[o->nterms++];
  snprintf(t->stat, sizeof t->stat, "%s", stat);
  snprintf(t->shape, sizeof t->shape, "%s", shape);
  t->min = min;
  t->max = max;
  t->weight = w;
  t->min_weight = minw;
  t->max_weight = maxw;
}

static void defaults(MlObjective *o) {
  memset(o, 0, sizeof *o);
  o->rows = o->cols = 100;
  o->screen_rows = o->screen_cols = 48;
  o->cycles = 5;
  o->max_steps = 1000;
  o->screen_steps = 260;
  o->screen_keep = 0.0;
  o->detect_ships = 1;
  o->early_abort = 1;
}

#define ML_NO_SCREEN (-1e9) /* screen_keep value that disables screening */

int ml_objective_preset(const char *name, MlObjective *o) {
  defaults(o);

  if (!strcmp(name, "paper")) {
    /* Table 3 of the paper, reproduced exactly.  Screening and early
     * abandonment are off so that scores match the published ones. */
    o->detect_ships = 0;
    o->early_abort = 0;
    o->threshold = 3.5;
    o->screen_keep = ML_NO_SCREEN;
    term(o, "steps", "legacy", 300, 1000, 1, -1, 1);
    term(o, "foreground", "legacy", 0.001, 0.1, 1, -0.1, -1);
    term(o, "active", "legacy", 0.001, 0.1, 1, -1, -1);
    term(o, "rect", "legacy", 0.02, 0.25, 2, -2, 2);
    term(o, "mage", "legacy", 5, 10, 0, -5, 0);
    return 0;
  }

  if (!strcmp(name, "spaceships")) {
    /* Directly rewards structures that translate, over open space, for a
     * long time -- rather than inferring all of that from `active`.
     *
     * The threshold is calibrated against the paper's own named rules: Red
     * World, the rule its author calls his favourite, scores about 10.5 here,
     * and the reporting bar is set just below it so that anything reported is
     * in that class.  For reference, still-life-and-oscillators scores 3.8,
     * Game of Life 2.0, chaos -2.6 and a dead rule -6.7. */
    o->threshold = 9.0;
    o->screen_keep = 0.5;
    term(o, "ships", "rise", 0.5, 20, 3.0, -1.0, 3.0);
    term(o, "guns", "rise", 0.0, 3.0, 1.5, 0.0, 1.5);
    term(o, "shipdist", "rise", 4.0, 25.0, 1.0, 0.0, 1.0);
    term(o, "rect", "peak", 0.05, 0.45, 2.0, -2.0, -1.0);
    term(o, "active", "peak", 0.002, 0.08, 1.0, -1.0, -1.0);
    term(o, "foreground", "peak", 0.001, 0.12, 1.0, -0.2, -1.0);
    term(o, "steps", "rise", 200, 1000, 1.5, -2.0, 1.5);
    term(o, "late", "rise", 0.2, 1.0, 1.5, -1.0, 1.5);
    term(o, "entvar", "rise", 0.02, 0.35, 1.0, -0.5, 1.0);
    term(o, "mage", "legacy", 5, 10, 0, -5, 0);
    term(o, "chaos", "legacy", 0.0, 0.5, 0, 0, -2.0);
    return 0;
  }

  if (!strcmp(name, "guns")) {
    /* Same idea, weighted towards recurring emission sites. */
    o->threshold = 9.0;
    o->screen_keep = 0.5;
    term(o, "guns", "rise", 0.0, 4.0, 4.0, 0.0, 4.0);
    term(o, "ships", "rise", 1.0, 25, 2.0, -1.0, 2.0);
    term(o, "shipdist", "rise", 4.0, 25.0, 1.0, 0.0, 1.0);
    term(o, "rect", "peak", 0.08, 0.55, 2.0, -2.0, -1.0);
    term(o, "foreground", "peak", 0.002, 0.15, 1.0, -0.2, -0.5);
    term(o, "steps", "rise", 300, 1000, 2.0, -2.0, 2.0);
    term(o, "late", "rise", 0.3, 1.1, 2.0, -1.0, 2.0);
    term(o, "entvar", "rise", 0.02, 0.35, 1.0, -0.5, 1.0);
    term(o, "mage", "legacy", 5, 10, 0, -5, 0);
    return 0;
  }

  return -1;
}

/* ------------------------------------------------------------------ */
/* JSON                                                                */
/* ------------------------------------------------------------------ */

/*
 * A small reader for the objective file format used by the Python, Java and
 * JavaScript versions.  It handles the subset that format needs: objects,
 * arrays, strings, and numbers.
 */
typedef struct {
  const char *p;
  const char *end;
  char err[128];
} Json;

static void jskip(Json *j) {
  while (j->p < j->end && isspace((unsigned char)*j->p)) j->p++;
}

static int jfail(Json *j, const char *msg) {
  if (!j->err[0]) snprintf(j->err, sizeof j->err, "%s at offset %ld", msg, (long)(j->p - j->end));
  return -1;
}

static int jexpect(Json *j, char c) {
  jskip(j);
  if (j->p >= j->end || *j->p != c) return jfail(j, "unexpected character");
  j->p++;
  return 0;
}

static int jstring(Json *j, char *out, size_t outlen) {
  jskip(j);
  if (jexpect(j, '"')) return -1;
  size_t n = 0;
  while (j->p < j->end && *j->p != '"') {
    if (*j->p == '\\' && j->p + 1 < j->end) j->p++;
    if (n + 1 < outlen) out[n++] = *j->p;
    j->p++;
  }
  out[n] = '\0';
  return jexpect(j, '"');
}

static int jnumber(Json *j, double *out) {
  jskip(j);
  char *endp = NULL;
  double v = strtod(j->p, &endp);
  if (endp == j->p) return jfail(j, "expected a number");
  j->p = endp;
  *out = v;
  return 0;
}

static int jvalue_skip(Json *j);

static int jcontainer_skip(Json *j, char open, char close) {
  if (jexpect(j, open)) return -1;
  jskip(j);
  if (j->p < j->end && *j->p == close) {
    j->p++;
    return 0;
  }
  for (;;) {
    if (open == '{') {
      char key[64];
      if (jstring(j, key, sizeof key)) return -1;
      if (jexpect(j, ':')) return -1;
    }
    if (jvalue_skip(j)) return -1;
    jskip(j);
    if (j->p < j->end && *j->p == ',') {
      j->p++;
      continue;
    }
    return jexpect(j, close);
  }
}

static int jvalue_skip(Json *j) {
  jskip(j);
  if (j->p >= j->end) return jfail(j, "unexpected end of input");
  switch (*j->p) {
    case '{': return jcontainer_skip(j, '{', '}');
    case '[': return jcontainer_skip(j, '[', ']');
    case '"': {
      char tmp[256];
      return jstring(j, tmp, sizeof tmp);
    }
    case 't': j->p += 4; return 0;
    case 'f': j->p += 5; return 0;
    case 'n': j->p += 4; return 0;
    default: {
      double d;
      return jnumber(j, &d);
    }
  }
}

/* Parse one { "stat": ..., "min": ... } object into a term. */
static int jterm(Json *j, MlObjective *o) {
  MlObjTerm t;
  memset(&t, 0, sizeof t);
  snprintf(t.shape, sizeof t.shape, "legacy");

  if (jexpect(j, '{')) return -1;
  for (;;) {
    char key[64];
    if (jstring(j, key, sizeof key)) return -1;
    if (jexpect(j, ':')) return -1;

    if (!strcmp(key, "stat")) {
      if (jstring(j, t.stat, sizeof t.stat)) return -1;
    } else if (!strcmp(key, "shape")) {
      if (jstring(j, t.shape, sizeof t.shape)) return -1;
    } else if (!strcmp(key, "min")) {
      if (jnumber(j, &t.min)) return -1;
    } else if (!strcmp(key, "max")) {
      if (jnumber(j, &t.max)) return -1;
    } else if (!strcmp(key, "weight")) {
      if (jnumber(j, &t.weight)) return -1;
    } else if (!strcmp(key, "min_weight")) {
      if (jnumber(j, &t.min_weight)) return -1;
    } else if (!strcmp(key, "max_weight")) {
      if (jnumber(j, &t.max_weight)) return -1;
    } else if (jvalue_skip(j)) {
      return -1;
    }

    jskip(j);
    if (j->p < j->end && *j->p == ',') {
      j->p++;
      continue;
    }
    if (jexpect(j, '}')) return -1;
    break;
  }

  if (!t.stat[0]) return jfail(j, "objective term is missing \"stat\"");
  if (o->nterms >= ML_MAX_OBJ) return jfail(j, "too many objective terms");
  o->term[o->nterms++] = t;
  return 0;
}

/* Parse the "config" object; every key is optional. */
static int jconfig(Json *j, MlObjective *o) {
  if (jexpect(j, '{')) return -1;
  jskip(j);
  if (j->p < j->end && *j->p == '}') {
    j->p++;
    return 0;
  }
  for (;;) {
    char key[64];
    if (jstring(j, key, sizeof key)) return -1;
    if (jexpect(j, ':')) return -1;

    double v;
    if (!strcmp(key, "rows")) {
      if (jnumber(j, &v)) return -1;
      o->rows = (int)v;
    } else if (!strcmp(key, "cols")) {
      if (jnumber(j, &v)) return -1;
      o->cols = (int)v;
    } else if (!strcmp(key, "evalCycles")) {
      if (jnumber(j, &v)) return -1;
      o->cycles = (int)v;
    } else if (!strcmp(key, "scoreThreshold")) {
      if (jnumber(j, &v)) return -1;
      o->threshold = v;
    } else if (!strcmp(key, "maxSteps")) {
      if (jnumber(j, &v)) return -1;
      o->max_steps = (int)v;
    } else if (!strcmp(key, "detectShips")) {
      if (jnumber(j, &v)) return -1;
      o->detect_ships = (int)v;
    } else if (jvalue_skip(j)) {
      return -1;
    }

    jskip(j);
    if (j->p < j->end && *j->p == ',') {
      j->p++;
      continue;
    }
    return jexpect(j, '}');
  }
}

int ml_objective_load(const char *path, MlObjective *o, char *err, size_t errlen) {
  FILE *f = fopen(path, "rb");
  if (!f) {
    snprintf(err, errlen, "cannot open %s", path);
    return -1;
  }
  fseek(f, 0, SEEK_END);
  long len = ftell(f);
  fseek(f, 0, SEEK_SET);
  if (len < 0 || len > (1 << 20)) {
    fclose(f);
    snprintf(err, errlen, "%s is not a plausible objective file", path);
    return -1;
  }
  char *buf = malloc((size_t)len + 1);
  if (!buf) {
    fclose(f);
    snprintf(err, errlen, "out of memory");
    return -1;
  }
  size_t got = fread(buf, 1, (size_t)len, f);
  fclose(f);
  buf[got] = '\0';

  defaults(o);
  o->threshold = 3.5;
  o->detect_ships = 0; /* a file in the published format has no ship terms */

  Json j = {buf, buf + got, {0}};
  int rc = 0;
  if (jexpect(&j, '{')) {
    rc = -1;
  } else {
    for (;;) {
      char key[64];
      if (jstring(&j, key, sizeof key)) {
        rc = -1;
        break;
      }
      if (jexpect(&j, ':')) {
        rc = -1;
        break;
      }
      if (!strcmp(key, "config")) {
        if (jconfig(&j, o)) {
          rc = -1;
          break;
        }
      } else if (!strcmp(key, "objective")) {
        if (jexpect(&j, '[')) {
          rc = -1;
          break;
        }
        jskip(&j);
        if (j.p < j.end && *j.p == ']') {
          j.p++;
        } else {
          for (;;) {
            if (jterm(&j, o)) {
              rc = -1;
              break;
            }
            jskip(&j);
            if (j.p < j.end && *j.p == ',') {
              j.p++;
              continue;
            }
            if (jexpect(&j, ']')) rc = -1;
            break;
          }
          if (rc) break;
        }
      } else if (jvalue_skip(&j)) {
        rc = -1;
        break;
      }
      jskip(&j);
      if (j.p < j.end && *j.p == ',') {
        j.p++;
        continue;
      }
      if (jexpect(&j, '}')) rc = -1;
      break;
    }
  }

  /* Any term naming a ship statistic implies the tracker must run. */
  for (int i = 0; i < o->nterms; i++) {
    const char *s = o->term[i].stat;
    if (!strcmp(s, "ships") || !strcmp(s, "guns") || !strcmp(s, "shipdist"))
      o->detect_ships = 1;
  }

  if (rc) snprintf(err, errlen, "%s: %s", path, j.err[0] ? j.err : "malformed JSON");
  else if (o->nterms == 0) {
    snprintf(err, errlen, "%s has no objective terms", path);
    rc = -1;
  }
  free(buf);
  return rc;
}
