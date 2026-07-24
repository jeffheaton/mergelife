"""MergeLife: evolving continuous cellular automata for aesthetic objectives.

The engine lives in :mod:`mergelife.mergelife`, the evolutionary trainer in
:mod:`mergelife.ml_evolve`, and the largest-rectangle helper in
:mod:`mergelife.dp`. The engine API is re-exported here so the historical
flat-module style (``import mergelife; mergelife.update_step(ml)``) keeps
working alongside the submodule style (``import mergelife.mergelife``).
"""

from . import dp
from .mergelife import (
    COLOR_TABLE,
    calc_objective_function,
    calc_objective_stats,
    calc_stat_largest_rect,
    count_discrete,
    fromHex,
    is_lattice_stable,
    new_ml_instance,
    objective_function,
    parse_update_rule,
    random_update_rule,
    randomize_lattice,
    save_image,
    toHex,
    update_step,
)
from . import ml_evolve

__version__ = "1.0.0"

__all__ = [
    "COLOR_TABLE",
    "calc_objective_function",
    "calc_objective_stats",
    "calc_stat_largest_rect",
    "count_discrete",
    "dp",
    "fromHex",
    "is_lattice_stable",
    "ml_evolve",
    "new_ml_instance",
    "objective_function",
    "parse_update_rule",
    "random_update_rule",
    "randomize_lattice",
    "save_image",
    "toHex",
    "update_step",
]
