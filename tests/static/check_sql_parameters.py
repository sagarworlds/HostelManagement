"""SQL in the repositories: every @parameter is supplied, none is left over, and every column exists in schema.sql.

These are the mistakes that only show up when a query runs against SQL Server, so they are checked statically."""
import re

from _common import ROOT, UTILITY, finish, read

REPOSITORIES = ["UserDB.cs", "RoomDB.cs", "RequestRoomDB.cs", "DashboardDB.cs"]
DERIVED_COLUMNS = {"ActiveRequestId", "OccupantName", "UserName", "PreferredRoomName",
                   "TotalRooms", "AvailableRooms", "OccupiedRooms", "PendingRequests", "Students", "Admins"}


def schema_columns():
    schema = read(ROOT / "Database" / "schema.sql")
    return set(re.findall(r"^\s+\[?(\w+)\]?\s+(?:INT|NVARCHAR|BIT|DATETIME)", schema, flags=re.M))


def main():
    problems = []
    columns = schema_columns()
    checked = 0

    for filename in REPOSITORIES:
        source = read(UTILITY / filename)
        for method in re.split(r"\n        public ", source)[1:]:
            header = re.match(r"[\w<>\[\]]+ (\w+)\(", method)
            if not header:
                continue  # constructor
            name = header.group(1)
            supplied = set(re.findall(r'Param\("(@\w+)"', method))
            without_param_names = re.sub(r'Param\("@\w+"', 'Param(""', method)
            literals = re.findall(r'"((?:[^"\\]|\\.)*)"', without_param_names)
            sql = " ".join(lit for lit in literals if "@" in lit)
            if not sql and not supplied:
                continue
            checked += 1
            used = set(re.findall(r"@\w+", sql))
            for parameter in sorted(used - supplied):
                problems.append("%s.%s: SQL uses %s but no value is supplied" % (filename, name, parameter))
            for parameter in sorted(supplied - used):
                problems.append("%s.%s: %s is supplied but never used in the SQL" % (filename, name, parameter))

        for literal in re.findall(r'"((?:[^"\\]|\\.)*)"', source):
            for column in re.findall(r"\b(?:R|RR|U|PR)\.\[?(\w+)\]?", literal):
                if column not in columns and column not in DERIVED_COLUMNS:
                    problems.append("%s: column '%s' is not defined in Database/schema.sql" % (filename, column))
            for column in re.findall(r"\b(MustChangePassword|PreferredRoomId|Availability|ApprovedRoomId)\b", literal):
                if column not in columns:
                    problems.append("%s: column '%s' is not defined in Database/schema.sql" % (filename, column))

    if checked < 20:
        problems.append("only %d SQL methods were found; the checker's pattern probably no longer matches the code" % checked)
    finish("SQL parameters and columns (%d methods)" % checked, problems)


main()
