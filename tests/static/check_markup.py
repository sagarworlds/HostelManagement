"""Markup sanity for .aspx/.ascx/.Master files, standing in for the Windows-only ASPX precompile:

* tags are balanced and properly nested
* every <asp:Content> targets a placeholder that its master page defines
* every On...="Handler" on a server control names a method that exists in the code-behind"""
import re

from _common import PROJECT, finish, read

VOID = {"br", "hr", "img", "input", "link", "meta", "col", "area", "base", "source", "track", "wbr"}
TAG = re.compile(r"<(/?)([A-Za-z][\w:.\-]*)((?:[^>\"']|\"[^\"]*\"|'[^']*')*?)(/?)>")


def blank_out(markup):
    """Replace constructs that are not plain tags by spaces of equal length/newlines so line numbers survive."""
    def blank(match):
        return re.sub(r"[^\n]", " ", match.group(0))
    for pattern in (r"<%--.*?--%>", r"<%@.*?%>", r"<%[#=:]?.*?%>", r"<!--.*?-->", r"<script\b[^>]*>.*?</script>", r"<!DOCTYPE[^>]*>"):
        markup = re.sub(pattern, blank, markup, flags=re.S | re.I)
    return markup


def balance(name, markup):
    problems, stack = [], []
    text = blank_out(markup)
    for match in TAG.finditer(text):
        closing, tag, _attributes, self_closing = match.groups()
        line = text.count("\n", 0, match.start()) + 1
        lowered = tag.lower()
        if self_closing or lowered in VOID:
            continue
        if not closing:
            stack.append((lowered, line))
        elif not stack or stack[-1][0] != lowered:
            expected = stack[-1] if stack else None
            problems.append("%s:%d: </%s> does not match %s" % (
                name, line, tag, "<%s> opened on line %d" % expected if expected else "any open tag"))
            if expected and any(t == lowered for t, _ in stack):
                while stack and stack[-1][0] != lowered:
                    stack.pop()
                stack.pop()
        else:
            stack.pop()
    for tag, line in stack:
        problems.append("%s:%d: <%s> is never closed" % (name, line, tag))
    return problems


def placeholders(master_path):
    return set(re.findall(r'<asp:ContentPlaceHolder\s+ID="(\w+)"', read(master_path)))


def code_methods(markup_path):
    code = markup_path.with_name(markup_path.name + ".cs")
    if markup_path.suffix == ".Master":
        code = markup_path.with_name(markup_path.stem + ".Master.cs")
    return set(re.findall(r"\b(\w+)\s*\(", read(code))) if code.exists() else set()


def main():
    problems = []
    files = sorted(PROJECT.glob("*.aspx")) + sorted(PROJECT.glob("*.Master")) + sorted(PROJECT.glob("Controls/*.ascx"))
    for path in files:
        markup = read(path)
        problems += balance(path.name, markup)

        directive = re.search(r'MasterPageFile="~/([^"]+)"', markup)
        if directive and path.suffix == ".aspx":
            master = PROJECT / directive.group(1)
            available = placeholders(master)
            for target in re.findall(r'<asp:Content\b[^>]*ContentPlaceHolderID="(\w+)"', markup):
                if target not in available:
                    problems.append("%s: <asp:Content> targets '%s', which %s does not define" % (path.name, target, master.name))

        methods = code_methods(path)
        if path.suffix != ".ascx":
            for tag in re.findall(r"<asp:\w+\b[^>]*>", blank_out(markup), flags=re.S):
                for handler in re.findall(r'\bOn(?!ClientClick)[A-Za-z]+="(\w+)"', tag):
                    if handler not in methods:
                        problems.append("%s: handler '%s' is not defined in the code-behind" % (path.name, handler))
    finish("markup balance, placeholders and handlers (%d files)" % len(files), problems)


main()
