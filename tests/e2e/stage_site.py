#!/usr/bin/env python3
"""Builds a runnable copy of the web application for the end-to-end tests, without touching the repository.

The application is an ASP.NET Web Forms site for .NET Framework 4.8 and IIS. There is no .NET Framework on Linux, so
the end-to-end tests run it on Mono's System.Web (hosted by fastcgi-mono-server4 behind nginx). Mono's System.Web is a
re-implementation and lacks a few things .NET Framework 4.8 has. Each difference found so far is listed below as an
explicit adaptation that is applied to the COPY and must match exactly once, so a change to the real files that makes an
adaptation stale fails here instead of silently testing something else.

What this means for the results:
  * Page markup, code-behind, master pages, navigation, validation, the data layer and the real SQL Server are all the
    real thing.
  * The cookie SameSite attribute cannot be tested here (Mono has no such property); the compile check against the
    .NET Framework 4.8 reference assemblies and tests/static/check_webconfig.py cover that it is set.
  * Anything specific to IIS or the .NET Framework runtime is not exercised; scripts/build-windows.ps1 and a real
    deployment remain the authority for those.

Usage: stage_site.py --out DIR --sql-server "Server=...;User Id=...;Password=..."   (needs the .NET SDK to compile)

The site is pointed at the database HMSDB_E2E on that server, never at HMSDB.
"""
import argparse
import pathlib
import re
import shutil
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
WEB = ROOT / "HostelManagement"

# Source files are compiled from a copy with these edits applied: (file under HostelManagement/, regex, replacement).
SOURCE_ADAPTATIONS = [
    # Mono's HttpCookie has no SameSite property or SameSiteMode type, so loading the assembly fails at the first sign-in.
    ("Utility/LoginBinding.cs", r",\s*SameSite = SameSiteMode\.Lax", ""),
]

# Web.config is adapted the same way: (regex, replacement, why).
CONFIG_ADAPTATIONS = [
    (r'\s+sameSite="Lax"', "", "Mono's httpCookies section does not know the sameSite attribute"),
    (r"<system\.codedom>.*?</system\.codedom>", "",
     "selects the Roslyn compiler package of the .NET Framework toolchain; Mono compiles pages with its own compiler"),
]

# The only database the end-to-end tests use; the tests' own support code (tests/js/e2e/support/config.js) says the same.
DATABASE = "HMSDB_E2E"

# Page markup is adapted too: (file glob, regex, replacement, why). `minimum` is how many replacements must be made in
# total, so the adaptation cannot silently stop applying.
MARKUP_ADAPTATIONS = [
    ("*.aspx", r'TextMode="(?:Email|Number|Date)"', 'TextMode="SingleLine"', 4,
     "Mono's TextBox renders neither type nor value for the HTML5 modes, so edit forms would open empty. The staged copy "
     "renders them as plain text boxes: the browser's own email/number/date handling is not exercised here."),
]

# Files that are source or tooling, not part of what a web server serves.
NOT_SERVED_SUFFIXES = (".cs", ".csproj", ".example", ".user")
NOT_SERVED_NAMES = {"packages.config", "Web.Debug.config", "Web.Release.config"}
NOT_SERVED_DIRS = {"obj", "bin", "Properties"}

CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <OutputType>Library</OutputType>
    <AssemblyName>HostelManagement</AssemblyName>
    <RootNamespace>HostelManagement</RootNamespace>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="{src}/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
    <Reference Include="System.Web" />
    <Reference Include="System.Web.Extensions" />
    <Reference Include="System.Configuration" />
    <Reference Include="System.Data" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.Xml.Linq" />
  </ItemGroup>
</Project>
"""


def with_database(connection_string, database):
    """The connection string with its database replaced, whichever of the two spellings it used."""
    kept = [part for part in connection_string.split(";")
            if part.strip() and part.split("=")[0].strip().lower() not in ("initial catalog", "database")]
    return ";".join(kept + ["Initial Catalog=" + database])


def adapt_exactly_once(text, pattern, replacement, what, flags=0):
    """Applies one adaptation and insists it matched once; anything else means the real file has drifted."""
    result, count = re.subn(pattern, replacement, text, flags=flags)
    if count != 1:
        sys.exit("stage_site: adaptation '%s' matched %d times, expected exactly 1. Update tests/e2e/stage_site.py." % (what, count))
    return result


def served_files():
    for path in sorted(WEB.rglob("*")):
        relative = path.relative_to(WEB)
        if not path.is_file() or relative.parts[0] in NOT_SERVED_DIRS:
            continue
        if path.name in NOT_SERVED_NAMES or path.name.endswith(NOT_SERVED_SUFFIXES):
            continue
        yield relative


def stage(out, connection_string):
    site = out / "site"
    src = out / "src"
    for folder in (site, src, out / "build"):
        if folder.exists():
            shutil.rmtree(folder)
        folder.mkdir(parents=True)

    for relative in served_files():
        target = site / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy(WEB / relative, target)

    config = (site / "Web.config").read_text(encoding="utf-8-sig")
    for pattern, replacement, why in CONFIG_ADAPTATIONS:
        config = adapt_exactly_once(config, pattern, replacement, why, flags=re.S)
    (site / "Web.config").write_text(config, encoding="utf-8")

    for pattern_glob, pattern, replacement, minimum, why in MARKUP_ADAPTATIONS:
        replaced = 0
        for page in sorted(site.glob(pattern_glob)):
            text = page.read_text(encoding="utf-8-sig")
            text, count = re.subn(pattern, replacement, text)
            if count:
                page.write_text(text, encoding="utf-8")
            replaced += count
        if replaced < minimum:
            sys.exit("stage_site: markup adaptation '%s' made %d replacements, expected at least %d. Update tests/e2e/stage_site.py." % (why[:40], replaced, minimum))

    escaped = connection_string.replace("&", "&amp;").replace('"', "&quot;")
    (site / "connectionStrings.config").write_text(
        '<connectionStrings><add name="DBConnection" connectionString="%s" providerName="System.Data.SqlClient" /></connectionStrings>' % escaped,
        encoding="utf-8")
    (site / "App_Data" / "Logs").mkdir(parents=True, exist_ok=True)

    for path in sorted(WEB.rglob("*.cs")):
        relative = path.relative_to(WEB)
        if relative.parts[0] in NOT_SERVED_DIRS:
            continue
        target = src / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy(path, target)
    for relative, pattern, replacement in SOURCE_ADAPTATIONS:
        file = src / relative
        file.write_text(adapt_exactly_once(file.read_text(encoding="utf-8-sig"), pattern, replacement, relative), encoding="utf-8")

    (out / "build" / "HostelManagement.csproj").write_text(CSPROJ.format(src=src.as_posix()), encoding="utf-8")
    bin_dir = site / "bin"
    result = subprocess.run(["dotnet", "build", str(out / "build" / "HostelManagement.csproj"), "-nologo", "-v", "quiet", "-o", str(bin_dir)],
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, universal_newlines=True)
    if result.returncode != 0:
        sys.exit("stage_site: the build failed:\n" + result.stdout)
    print("staged %s (%d files served, assembly built)" % (site, sum(1 for _ in served_files())))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", required=True, type=pathlib.Path)
    parser.add_argument("--sql-server", required=True, help="connection string of the SQL Server to use")
    args = parser.parse_args()
    stage(args.out.resolve(), with_database(args.sql_server, DATABASE))


if __name__ == "__main__":
    main()
