"""Page rules that running the real pages showed are easy to break, checked on the markup so they also cover pages the
end-to-end tests cannot render (for example the forgotten-password form, which only appears once a mail server is set up):

* every page that uses a master page has a non-empty Title (otherwise the browser tab shows nothing useful)
* every <label for="..."> points at something: either <%= Control.ClientID %> for a server control declared in the same
  file, or the plain id of an ordinary HTML element. A server control's id is rewritten by ASP.NET
  (ctl00_ContentPlaceHolder1_...), so a hard-coded for="inputEmail" silently labels nothing.
* when a page has validators that are not in a ValidationGroup, buttons inside grid/repeater templates must set
  CausesValidation="false" (or have a ValidationGroup). Otherwise clicking Delete in a list is blocked by the "Required"
  messages of the empty form on the same page.
"""
import re

from _common import PROJECT, finish, read

TEMPLATE = re.compile(r"<(ItemTemplate|AlternatingItemTemplate|EditItemTemplate|FooterTemplate|HeaderTemplate)\b.*?</\1>", re.S)
BUTTON = re.compile(r"<asp:(?:LinkButton|Button|ImageButton)\b[^>]*>", re.S)
VALIDATOR = re.compile(r"<asp:\w*Validator\b[^>]*>", re.S)
PAGE_DIRECTIVE = re.compile(r"<%@\s*Page\b(.*?)%>", re.S)


def without_comments(markup):
    return re.sub(r"<%--.*?--%>", lambda m: re.sub(r"[^\n]", " ", m.group(0)), markup, flags=re.S)


def line_of(markup, index):
    return markup.count("\n", 0, index) + 1


def title_problems(name, markup):
    directive = PAGE_DIRECTIVE.search(markup)
    if not directive or "MasterPageFile=" not in directive.group(1):
        return []
    title = re.search(r'\bTitle="([^"]*)"', directive.group(1))
    if title and title.group(1).strip():
        return []
    return ["%s:%d: the page has no Title" % (name, line_of(markup, directive.start()))]


def label_problems(name, markup):
    problems = []
    server_ids = set(re.findall(r'\bID="(\w+)"', markup))     # ASP.NET controls: ID, rewritten when rendered
    html_ids = set(re.findall(r'\bid="(\w+)"', markup))       # ordinary elements: id, rendered as written
    for match in re.finditer(r'<label\b[^>]*\bfor="([^"]*)"', markup):
        target, line = match.group(1), line_of(markup, match.start())
        client_id = re.fullmatch(r"<%=\s*(\w+)\.ClientID\s*%>", target)
        if client_id:
            if client_id.group(1) not in server_ids:
                problems.append("%s:%d: label is for <%%= %s.ClientID %%>, but no control has that ID" % (name, line, client_id.group(1)))
        elif "<%" in target:
            problems.append("%s:%d: label for=\"%s\" must be written <%%= Control.ClientID %%>" % (name, line, target))
        elif target in server_ids and target not in html_ids:
            problems.append("%s:%d: label for=\"%s\" names a server control; its rendered id is different, use <%%= %s.ClientID %%>" % (name, line, target, target))
        elif target not in html_ids:
            problems.append("%s:%d: label for=\"%s\" points at nothing" % (name, line, target))
    return problems


def validation_problems(name, markup):
    ungrouped = [v for v in VALIDATOR.findall(markup) if "ValidationGroup=" not in v]
    if not ungrouped:
        return []
    problems = []
    for template in TEMPLATE.finditer(markup):
        for button in BUTTON.finditer(template.group(0)):
            tag = button.group(0)
            if 'CausesValidation="false"' not in tag and "ValidationGroup=" not in tag:
                line = line_of(markup, template.start() + button.start())
                problems.append("%s:%d: a button in a grid/repeater template would run this page's validators; add CausesValidation=\"false\"" % (name, line))
    return problems


def check_markup(name, markup):
    markup = without_comments(markup)
    return title_problems(name, markup) + label_problems(name, markup) + validation_problems(name, markup)


def self_test():
    """Each rule must fire on a known-bad example, or the check proves nothing."""
    bad_title = '<%@ Page Title="" MasterPageFile="~/Site.Master" %>'
    assert title_problems("x", bad_title) and not title_problems("x", bad_title.replace('Title=""', 'Title="Ok"'))
    assert not title_problems("x", '<%@ Page Language="C#" %>'), "a page without a master page is not required to have a title"

    bad_label = '<label for="inputEmail">E</label><asp:TextBox ID="inputEmail" runat="server" />'
    good_label = '<label for="<%= inputEmail.ClientID %>">E</label><asp:TextBox ID="inputEmail" runat="server" />'
    assert label_problems("x", bad_label) and not label_problems("x", good_label)
    assert label_problems("x", '<label for="nothing">E</label>')
    assert not label_problems("x", '<label for="plain">E</label><input id="plain" />')
    assert label_problems("x", '<label for="<%= missing.ClientID %>">E</label>')

    grid = '<asp:RequiredFieldValidator ControlToValidate="a" runat="server" /><ItemTemplate><asp:LinkButton runat="server" CommandName="Delete" /></ItemTemplate>'
    assert validation_problems("x", grid)
    assert not validation_problems("x", grid.replace('CommandName="Delete"', 'CommandName="Delete" CausesValidation="false"'))
    assert not validation_problems("x", grid.replace("RequiredFieldValidator ", 'RequiredFieldValidator ValidationGroup="g" '))


def main():
    self_test()
    files = sorted(PROJECT.glob("*.aspx")) + sorted(PROJECT.glob("*.Master"))
    problems = []
    for path in files:
        problems += check_markup(path.name, read(path))
    finish("page rules: titles, label targets, validation in grids (%d files)" % len(files), problems)


main()
