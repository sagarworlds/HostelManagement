"""Configuration guard rails: the security settings added in the hardening work stay in place, the Release transforms
still apply them, and no real credential is committed in a config file."""
import re
import xml.etree.ElementTree as ET

from _common import PROJECT, ROOT, finish

XDT = "{http://schemas.microsoft.com/XML-Document-Transform}Transform"


def main():
    problems = []

    web = ET.parse(PROJECT / "Web.config").getroot()
    release = ET.parse(PROJECT / "Web.Release.config").getroot()

    def attr(root, path, name):
        node = root.find(path)
        return None if node is None else node.get(name)

    expectations = [
        ("system.web/httpCookies", "httpOnlyCookies", "true"),
        ("system.web/httpCookies", "sameSite", "Lax"),
        ("system.web/httpRuntime", "enableVersionHeader", "false"),
        ("system.web/sessionState", "cookieless", "UseCookies"),
        ("system.web/customErrors", "mode", "RemoteOnly"),
    ]
    for path, name, expected in expectations:
        actual = attr(web, path, name)
        if actual != expected:
            problems.append("Web.config: %s@%s is %r, expected %r" % (path, name, actual, expected))

    target = attr(web, "system.web/customErrors", "defaultRedirect")
    if not target or not (PROJECT / target.replace("~/", "")).exists():
        problems.append("Web.config: customErrors defaultRedirect %r does not point at a file in the project" % target)

    headers = {node.get("name") for node in web.findall("system.webServer/httpProtocol/customHeaders/add")}
    for required in ("X-Content-Type-Options", "X-Frame-Options", "Referrer-Policy"):
        if required not in headers:
            problems.append("Web.config: response header %s is not set" % required)

    if web.find("connectionStrings") is None or web.find("connectionStrings").get("configSource") != "connectionStrings.config":
        problems.append("Web.config: connection strings must come from the git-ignored connectionStrings.config (configSource)")

    compilation = release.find("system.web/compilation")
    if compilation is None or "RemoveAttributes(debug)" not in (compilation.get(XDT) or ""):
        problems.append("Web.Release.config: Release builds must remove compilation debug")
    cookies = release.find("system.web/httpCookies")
    if cookies is None or cookies.get("requireSSL") != "true":
        problems.append("Web.Release.config: Release builds must set httpCookies requireSSL=true")
    errors = release.find("system.web/customErrors")
    if errors is None or errors.get("mode") != "On":
        problems.append("Web.Release.config: Release builds must set customErrors mode=On")

    # Secrets: any password in a committed config must be an obvious placeholder.
    for path in list(PROJECT.glob("*.config")) + list(PROJECT.glob("*.config.example")):
        if path.name == "connectionStrings.config":
            continue  # the local, git-ignored file; never committed
        text = path.read_text(encoding="utf-8-sig")
        for match in re.finditer(r"(?i)\b(?:password|pwd)\s*=\s*([^;\"'<]+)", text):
            if not re.fullmatch(r"YOUR_[A-Z_]+|\s*", match.group(1)):
                problems.append("%s: contains what looks like a real password (%r)" % (path.name, match.group(1)))

    # The example settings file may only carry placeholders for the mail credentials.
    example = (PROJECT / "appSettings.local.config.example").read_text(encoding="utf-8-sig")
    for match in re.finditer(r'key="Smtp\.(?:Password|UserName)"\s+value="([^"]*)"', example):
        if match.group(1) and not match.group(1).startswith("YOUR_"):
            problems.append("appSettings.local.config.example: a mail credential is not a placeholder (%r)" % match.group(1))
    if web.find("appSettings") is None or web.find("appSettings").get("file") != "appSettings.local.config":
        problems.append("Web.config: appSettings must read the git-ignored appSettings.local.config (file=)")

    ignore = (ROOT / ".gitignore").read_text(encoding="utf-8")
    for needed in ("HostelManagement/connectionStrings.config", "HostelManagement/appSettings.local.config",
                   "HostelManagement/App_Data/Logs/"):
        if needed not in ignore:
            problems.append(".gitignore does not ignore %s" % needed)

    finish("Web.config hardening, Release transforms and committed secrets", problems)


main()
