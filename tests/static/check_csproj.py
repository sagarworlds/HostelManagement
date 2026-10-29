"""Every source file must be listed in HostelManagement.csproj and every listed file must exist.

The project is an old-style csproj with explicit file lists: a new file that was never added compiles on
nobody's machine but the author's, and an entry for a deleted file breaks the build."""
import pathlib
import re
import xml.etree.ElementTree as ET

from _common import PROJECT, finish

NS = {"m": "http://schemas.microsoft.com/developer/msbuild/2003"}
SOURCE_SUFFIXES = {".cs", ".aspx", ".ascx", ".master", ".asax", ".js", ".css"}
IGNORED_DIRS = {"bin", "obj", "Properties", ".vs", "packages"}


def listed_files(tree):
    items = {}
    for kind in ("Compile", "Content", "None"):
        for node in tree.getroot().iter("{%s}%s" % (NS["m"], kind)):
            include = node.get("Include")
            if include:
                items[include.replace("\\", "/").lower()] = (include, node.get("Condition"))
    return items


def main():
    tree = ET.parse(PROJECT / "HostelManagement.csproj")
    listed = listed_files(tree)
    problems = []

    for path in PROJECT.rglob("*"):
        relative = path.relative_to(PROJECT)
        if not path.is_file() or set(relative.parts[:-1]) & IGNORED_DIRS:
            continue
        if path.suffix.lower() in SOURCE_SUFFIXES and relative.as_posix().lower() not in listed:
            problems.append("%s exists but is not in HostelManagement.csproj" % relative.as_posix())

    for key, (include, condition) in sorted(listed.items()):
        if condition:  # e.g. connectionStrings.config only when the local file exists
            continue
        if not (PROJECT / include.replace("\\", "/")).exists():
            problems.append("%s is listed in HostelManagement.csproj but missing on disk" % include)

    for node in tree.getroot().iter("{%s}DependentUpon" % NS["m"]):
        parent_include = None
        for parent in tree.getroot().iter():
            if node in list(parent):
                parent_include = parent.get("Include")
        target = (node.text or "").strip()
        if parent_include and target:
            folder = pathlib.PurePosixPath(parent_include.replace("\\", "/")).parent
            if not (PROJECT / folder / target).exists():
                problems.append("%s depends on %s, which does not exist" % (parent_include, target))

    finish("csproj lists exactly the source files on disk", problems)


main()
