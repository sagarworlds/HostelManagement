"""Every server control with an ID in a page's markup needs a field in its .designer.cs, and vice versa.

A missing field is a compile error; a stale one is a field that stays null at run time. Controls inside
templates (GridView/Repeater rows) are not designer fields and are skipped."""
import re

from _common import PROJECT, finish, read

TEMPLATE_BLOCK = re.compile(
    r"<(ItemTemplate|HeaderTemplate|FooterTemplate|AlternatingItemTemplate|EditItemTemplate)>.*?</\1>", re.S)


def control_ids(markup):
    stripped = TEMPLATE_BLOCK.sub("", markup)
    ids = set(re.findall(r'(?<![A-Za-z])ID="(\w+)"', stripped))
    # plain HTML controls: <h1 id="title" runat="server"> (attributes in either order)
    ids |= set(re.findall(r'<[^>]*\bid="(\w+)"[^>]*runat="server"', stripped))
    ids |= set(re.findall(r'<[^>]*runat="server"[^>]*\bid="(\w+)"', stripped))
    return ids - {"Content1", "Content2", "Content3"}


def main():
    problems = []
    for markup_path in sorted(PROJECT.glob("*.aspx")) + sorted(PROJECT.glob("*.ascx")) + sorted(PROJECT.glob("*.Master")):
        designer = markup_path.with_name(markup_path.name + ".designer.cs")
        if markup_path.suffix == ".Master":
            designer = markup_path.with_name(markup_path.stem + ".Master.designer.cs")
        if not designer.exists():
            continue  # controls without code-behind (e.g. Controls/AlertModal.ascx) have no designer
        wanted = control_ids(read(markup_path))
        declared = set(re.findall(r"protected global::[\w.]+ (\w+);", read(designer)))
        for name in sorted(wanted - declared):
            problems.append("%s: control '%s' has no field in %s" % (markup_path.name, name, designer.name))
        for name in sorted(declared - wanted):
            problems.append("%s: field '%s' has no control in the markup" % (designer.name, name))
    finish("designer files match page markup", problems)


main()
