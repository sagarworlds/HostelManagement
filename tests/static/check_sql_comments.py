"""Database scripts: block comments are balanced the way SQL Server reads them.

Unlike C, T-SQL block comments nest, so a stray "/*" inside a comment (for example a file pattern such as
"Utility/*DB.cs") opens a second comment and the script fails with "Missing end comment mark". That mistake once
shipped in schema.sql, so it is checked on every run. Single-quoted strings and "--" line comments are skipped,
since either can legitimately contain the characters."""
from _common import ROOT, finish, read


def find_unbalanced(text):
    """Return (line, message) for each place a block comment opens or closes without a partner."""
    problems = []
    open_lines = []  # one entry per comment currently open: the line it was opened on
    line = 1
    i = 0
    in_string = False
    in_line_comment = False
    while i < len(text):
        two = text[i:i + 2]
        char = text[i]
        if char == "\n":
            line += 1
            in_line_comment = False
        elif in_line_comment:
            pass
        elif open_lines:
            if two == "/*":
                open_lines.append(line)
                problems.append((line, "'/*' inside a comment opened on line %d opens a nested comment" % open_lines[-2]))
                i += 1
            elif two == "*/":
                open_lines.pop()
                i += 1
        elif in_string:
            if char == "'":
                in_string = False
        elif char == "'":
            in_string = True
        elif two == "--":
            in_line_comment = True
        elif two == "/*":
            open_lines.append(line)
            i += 1
        elif two == "*/":
            problems.append((line, "'*/' with no comment open"))
            i += 1
        i += 1
    for opened_on in open_lines:
        problems.append((opened_on, "comment opened here is never closed"))
    return problems


def self_test():
    """The checker must catch the known-bad shape, or it proves nothing."""
    assert not find_unbalanced("/* fine */ SELECT 1 -- /* ignored\n")
    assert not find_unbalanced("SELECT '/*' AS pattern")
    assert find_unbalanced("/* see Utility/*DB.cs */ SELECT 1"), "a nested opener must be reported"
    assert find_unbalanced("/* never closed"), "an unclosed comment must be reported"
    assert find_unbalanced("SELECT 1 */"), "a stray closer must be reported"


def main():
    self_test()
    problems = []
    scripts = sorted((ROOT / "Database").glob("*.sql"))
    if not scripts:
        problems.append("no scripts found under Database/; the checker's path probably no longer matches")
    for script in scripts:
        found = find_unbalanced(read(script))  # in the order met, so the cause comes before its consequences
        if found:
            # Everything after the first mistake is a consequence of it (one stray opener makes every later
            # comment nest), so only the first is reported.
            line, message = found[0]
            problems.append("%s line %d: %s" % (script.name, line, message))
    finish("SQL block comments (%d scripts)" % len(scripts), problems)


main()
