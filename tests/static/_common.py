"""Shared helpers for the static checks. Standard library only, so they run anywhere Python 3 does."""
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
PROJECT = ROOT / "HostelManagement"
UTILITY = PROJECT / "Utility"


def read(path):
    """Read a source file; the project uses UTF-8 with an optional BOM."""
    return pathlib.Path(path).read_text(encoding="utf-8-sig")


def finish(name, problems):
    """Print the outcome and exit non-zero when anything was found."""
    if problems:
        print("FAIL %s: %d problem(s)" % (name, len(problems)))
        for problem in problems:
            print("  - " + problem)
        sys.exit(1)
    print("PASS %s" % name)
