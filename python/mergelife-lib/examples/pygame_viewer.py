"""Interactive MergeLife viewer built on pygame.

A desktop take on the web viewer at
https://www.heatonresearch.com/mergelife/ml-viewer.html — type or paste a rule
hex code, load one of the paper's named rules, and watch the CA run.

Controls:
    click the rule box   edit the rule: the text starts selected, so typing or
                         pasting replaces it. Cmd/Ctrl+A selects all, arrows /
                         Home / End move the cursor, held keys repeat,
                         Cmd/Ctrl+V pastes, Enter applies, Esc cancels.
    P                    paste a rule from the clipboard and apply it
    Cmd/Ctrl+C           copy the current rule to the clipboard
    Space                pause / resume
    S                    single step while paused
    R                    reset the lattice (same rule, new random grid)
    N                    random rule
    1-6                  load a preset rule
    + / -                double / halve the generations-per-second rate
    Q                    quit

Run:
    pip install pygame
    python pygame_viewer.py [--rule HEX] [--rows N] [--cols N] [--zoom N] [--gps N]
"""

import argparse
import os
import subprocess
import sys
import time

import numpy as np

try:
    import mergelife
except ImportError:  # allow running straight from a source checkout
    sys.path.insert(
        0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src")
    )
    import mergelife

import pygame

# The named rules offered by the web viewer's dropdown (paper Sec. 6).
PRESETS = [
    ("Red world", "e542-5f79-9341-f31e-6c6b-7f08-8773-7068"),
    ("Conway's Game of Life", "a07f-c000-0000-0000-0000-0000-ff80-807f"),
    ("Yellow world", "7b58-f7b4-c5b4-fd87-22fa-eb10-6de8-107c"),
    ("Shrinking cells w/ spaceships", "8503-5eb6-084c-04df-7657-a5b3-6044-3524"),
    ("Still life & oscillators", "6769-5dd6-7d03-564e-a5ec-cae2-54c4-810c"),
    ("Sustained cellular", "df1d-bba1-8e06-aa66-48ff-7414-6a2f-6237"),
]

COLOR_NAMES = ["Black", "Red", "Green", "Yellow", "Blue", "Purple", "Cyan", "White"]

BAR_HEIGHT = 76
MIN_WIDTH = 620

BG = (24, 24, 28)
BOX_BG = (40, 40, 48)
BOX_BORDER = (90, 90, 110)
BOX_FOCUS = (120, 170, 255)
SELECT = (60, 90, 150)
TEXT = (225, 225, 230)
DIM = (150, 150, 160)
ERROR = (255, 110, 110)


def normalize_rule(text):
    """Return the cleaned rule string, or None if it is not a valid rule."""
    hexpart = text.strip().lower().replace("-", "")
    if len(hexpart) != 32 or any(c not in "0123456789abcdef" for c in hexpart):
        return None
    return "-".join(hexpart[i:i + 4] for i in range(0, 32, 4))


def print_decoded(rule):
    """Print the sub-rule table for a rule, like the web viewer's decoder."""
    print(f"\nRule {rule} decoded (sorted by alpha):")
    print(f"  {'alpha':>6}  {'percent':>8}  key-color")
    for limit, pct, idx in mergelife.parse_update_rule(rule):
        key = idx
        if pct < 0:
            key = 0 if idx + 1 >= len(COLOR_NAMES) else idx + 1
        print(f"  {limit:>6}  {pct:>8.3f}  {COLOR_NAMES[key]}")


class MergeLifeViewer:
    def __init__(self, rule, rows=100, cols=100, zoom=5, gps=20):
        self.rows = rows
        self.cols = cols
        self.zoom = zoom
        self.gps = gps
        self.rule = rule
        self.ml = mergelife.new_ml_instance(rows, cols, rule)
        self.playing = True
        self.editing = False
        self.edit_text = rule
        self.cursor = len(rule)
        self.select_all = False
        self.error_until = 0.0

        self.grid_w = cols * zoom
        self.grid_h = rows * zoom
        self.win_w = max(self.grid_w, MIN_WIDTH)
        self.win_h = BAR_HEIGHT + self.grid_h
        self.grid_x = (self.win_w - self.grid_w) // 2

        pygame.init()
        self.screen = pygame.display.set_mode((self.win_w, self.win_h))
        pygame.display.set_caption("MergeLife Viewer")
        self.font = pygame.font.SysFont("menlo,monaco,consolas,monospace", 14)
        self.small = pygame.font.SysFont("menlo,monaco,consolas,monospace", 12)
        self.input_rect = pygame.Rect(8, 8, self.win_w - 16, 24)

    # ------------------------------------------------------------- simulation

    def apply_rule(self, text):
        rule = normalize_rule(text)
        if rule is None:
            self.error_until = time.time() + 2.0
            return False
        self.rule = rule
        self.ml = mergelife.new_ml_instance(self.rows, self.cols, rule)
        self.edit_text = rule
        print_decoded(rule)
        return True

    def reset_lattice(self):
        mergelife.randomize_lattice(self.ml)

    def step(self):
        mergelife.update_step(self.ml)

    # -------------------------------------------------------------------- ui

    def _paste_text(self):
        # pygame-ce exposes SDL's native clipboard as scrap.get_text; classic
        # pygame only ships the older experimental scrap API, which does not
        # work on macOS -- there, use the system's own pbpaste instead.
        try:
            if hasattr(pygame.scrap, "get_text"):
                text = pygame.scrap.get_text()
                if text:
                    return text.strip()
        except Exception:
            pass
        if sys.platform == "darwin":
            try:
                out = subprocess.run(["pbpaste"], capture_output=True,
                                     text=True, timeout=2)
                if out.stdout:
                    return out.stdout.strip()
            except Exception:
                pass
        try:
            if not pygame.scrap.get_init():
                pygame.scrap.init()
            data = pygame.scrap.get(pygame.SCRAP_TEXT)
            if data:
                return data.decode("utf-8", "ignore").replace("\x00", "").strip()
        except Exception:
            pass
        return ""

    def _copy_text(self, text):
        try:
            if hasattr(pygame.scrap, "put_text"):
                pygame.scrap.put_text(text)
                return
        except Exception:
            pass
        if sys.platform == "darwin":
            try:
                subprocess.run(["pbcopy"], input=text, text=True, timeout=2)
            except Exception:
                pass

    def _set_editing(self, flag, select=False):
        self.editing = flag
        self.select_all = flag and select
        if flag:
            self.cursor = len(self.edit_text)
            # Held keys (backspace, arrows, characters) repeat while editing.
            pygame.key.set_repeat(350, 35)
        else:
            pygame.key.set_repeat()

    def _replace_edit_text(self, text):
        self.edit_text = text[:64]
        self.cursor = len(self.edit_text)
        self.select_all = False

    def _insert_edit_text(self, text):
        if self.select_all:
            self._replace_edit_text(text)
            return
        t = self.edit_text
        self.edit_text = (t[:self.cursor] + text + t[self.cursor:])[:64]
        self.cursor = min(self.cursor + len(text), len(self.edit_text))

    def handle_event(self, event):
        """Process one pygame event. Returns False when the app should quit."""
        if event.type == pygame.QUIT:
            return False

        if event.type == pygame.MOUSEBUTTONDOWN:
            inside = self.input_rect.collidepoint(event.pos)
            if inside and not self.editing:
                self.edit_text = self.rule
                self._set_editing(True, select=True)
            elif not inside and self.editing:
                self.edit_text = self.rule
                self._set_editing(False)
            return True

        if event.type != pygame.KEYDOWN:
            return True

        if self.editing:
            mod = event.mod & (pygame.KMOD_CTRL | pygame.KMOD_META)
            if event.key == pygame.K_RETURN:
                if self.apply_rule(self.edit_text):
                    self._set_editing(False)
            elif event.key == pygame.K_ESCAPE:
                self.edit_text = self.rule
                self._set_editing(False)
            elif event.key == pygame.K_a and mod:
                self.select_all = True
            elif event.key == pygame.K_c and mod:
                self._copy_text(self.edit_text)
            elif event.key == pygame.K_v and mod:
                pasted = self._paste_text()
                norm = normalize_rule(pasted)
                if norm is not None:
                    self._replace_edit_text(norm)
                elif pasted:
                    self._insert_edit_text(pasted)
            elif event.key == pygame.K_BACKSPACE:
                if self.select_all:
                    self._replace_edit_text("")
                elif self.cursor > 0:
                    t = self.edit_text
                    self.edit_text = t[:self.cursor - 1] + t[self.cursor:]
                    self.cursor -= 1
            elif event.key == pygame.K_DELETE:
                if self.select_all:
                    self._replace_edit_text("")
                elif self.cursor < len(self.edit_text):
                    t = self.edit_text
                    self.edit_text = t[:self.cursor] + t[self.cursor + 1:]
            elif event.key in (pygame.K_LEFT, pygame.K_HOME):
                if event.key == pygame.K_HOME or mod or self.select_all:
                    self.cursor = 0
                else:
                    self.cursor = max(0, self.cursor - 1)
                self.select_all = False
            elif event.key in (pygame.K_RIGHT, pygame.K_END):
                if event.key == pygame.K_END or mod or self.select_all:
                    self.cursor = len(self.edit_text)
                else:
                    self.cursor = min(len(self.edit_text), self.cursor + 1)
                self.select_all = False
            elif event.unicode and event.unicode.isprintable() and not mod:
                self._insert_edit_text(event.unicode)
            return True

        if event.key == pygame.K_SPACE:
            self.playing = not self.playing
        elif event.key == pygame.K_s:
            self.step()
        elif event.key == pygame.K_r:
            self.reset_lattice()
        elif event.key == pygame.K_n:
            self.apply_rule(mergelife.random_update_rule())
        elif event.key == pygame.K_p:
            self.apply_rule(self._paste_text())
        elif event.key == pygame.K_c and event.mod & (pygame.KMOD_CTRL | pygame.KMOD_META):
            self._copy_text(self.rule)
        elif event.key in (pygame.K_PLUS, pygame.K_EQUALS, pygame.K_KP_PLUS):
            self.gps = min(240, self.gps * 2)
        elif event.key in (pygame.K_MINUS, pygame.K_KP_MINUS):
            self.gps = max(1, self.gps // 2)
        elif event.key == pygame.K_q:
            return False
        elif pygame.K_1 <= event.key <= pygame.K_6:
            self.apply_rule(PRESETS[event.key - pygame.K_1][1])
        return True

    def draw(self):
        self.screen.fill(BG)

        # Rule input box.
        border = BOX_FOCUS if self.editing else BOX_BORDER
        if time.time() < self.error_until:
            border = ERROR
        pygame.draw.rect(self.screen, BOX_BG, self.input_rect)
        pygame.draw.rect(self.screen, border, self.input_rect, 1)
        shown = self.edit_text if self.editing else self.rule
        tx, ty = self.input_rect.x + 6, self.input_rect.y + 4
        if self.editing and self.select_all and shown:
            sel_w = self.font.size(shown)[0]
            pygame.draw.rect(self.screen, SELECT,
                             pygame.Rect(tx - 1, ty - 1, sel_w + 2, 18))
        self.screen.blit(self.font.render(shown, True, TEXT), (tx, ty))
        if self.editing and not self.select_all and int(time.time() * 2) % 2 == 0:
            cx = tx + self.font.size(shown[:self.cursor])[0]
            pygame.draw.line(self.screen, TEXT, (cx, ty), (cx, ty + 15))

        # Status and help lines.
        if time.time() < self.error_until:
            status = "Invalid rule — need 32 hex digits"
            color = ERROR
        else:
            state = "running" if self.playing else "paused"
            status = f"gen {self.ml['time_step']} | {self.gps} gen/s | {state}"
            color = TEXT
        self.screen.blit(self.small.render(status, True, color), (8, 38))
        help_line = ("Space pause | S step | R reset | N random | P paste rule | "
                     "1-6 presets | +/- speed | Q quit")
        self.screen.blit(self.small.render(help_line, True, DIM), (8, 56))

        # The CA grid.
        data = self.ml["lattice"][0]["data"]
        surface = pygame.surfarray.make_surface(np.transpose(data, (1, 0, 2)))
        surface = pygame.transform.scale(surface, (self.grid_w, self.grid_h))
        self.screen.blit(surface, (self.grid_x, BAR_HEIGHT))
        pygame.display.flip()

    # ------------------------------------------------------------------ loop

    def run(self):
        clock = pygame.time.Clock()
        acc = 0.0
        running = True
        while running:
            for event in pygame.event.get():
                if not self.handle_event(event):
                    running = False
            acc += clock.tick(60) / 1000.0
            interval = 1.0 / self.gps
            steps = 0
            while self.playing and acc >= interval and steps < 32:
                self.step()
                acc -= interval
                steps += 1
            if not self.playing:
                acc = 0.0
            self.draw()
        pygame.quit()


def main(argv=None):
    parser = argparse.ArgumentParser(description="Interactive MergeLife viewer")
    parser.add_argument("--rule", default=PRESETS[0][1],
                        help="rule hex code (default: Red world)")
    parser.add_argument("--rows", type=int, default=100)
    parser.add_argument("--cols", type=int, default=100)
    parser.add_argument("--zoom", type=int, default=5,
                        help="screen pixels per cell")
    parser.add_argument("--gps", type=int, default=20,
                        help="CA generations per second")
    args = parser.parse_args(argv)

    rule = normalize_rule(args.rule)
    if rule is None:
        parser.error(f"invalid rule: {args.rule!r} (need 32 hex digits)")

    viewer = MergeLifeViewer(rule, args.rows, args.cols, args.zoom, args.gps)
    print_decoded(rule)
    viewer.run()


if __name__ == "__main__":
    main()
