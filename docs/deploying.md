# Deploying HostelHub

How to put HostelHub on a Windows server with IIS and SQL Server, keep it backed up, and bring it back when something
goes wrong. It assumes you can use IIS Manager, SQL Server Management Studio (SSMS) and an elevated PowerShell, and
nothing more. The last section says which parts of this guide were run for real and which were not, so you know what to
check on your first deployment.

Steps that change something you cannot easily undo say so. Take a backup first (section 8) whenever a database exists.

* [1. What you need](#1-what-you-need)
* [2. The database](#2-the-database)
* [3. Publishing the site](#3-publishing-the-site)
* [4. IIS](#4-iis)
* [5. The two settings files on the server](#5-the-two-settings-files-on-the-server)
* [6. HTTPS](#6-https)
* [7. First start: how to tell it works](#7-first-start-how-to-tell-it-works)
* [8. Backups, and restoring](#8-backups-and-restoring)
* [9. Mail (optional)](#9-mail-optional)
* [10. Upgrading to a newer version](#10-upgrading-to-a-newer-version)
* [11. Running it: logs, health, restarts](#11-running-it-logs-health-restarts)
* [12. When something is wrong](#12-when-something-is-wrong)
* [13. What has and has not been tested](#13-what-has-and-has-not-been-tested)

---

## 1. What you need

* **A Windows machine for the site**: Windows Server 2016 or later, or Windows 10/11 Pro, with IIS and .NET Framework 4.8.
  One machine only: sign-in sessions and the sign-in rate limits are kept in the memory of the web process, so two web servers
  behind a load balancer would each know only half of them.
* **SQL Server** (Express is enough), on that machine or another that it can reach.
* **A host name and a TLS certificate** for it. The Release build only works over HTTPS (section 6).
* **A machine with Visual Studio or the Build Tools** to build and publish the site. It does not have to be the server.
* *Optional:* an SMTP server, to switch on the e-mailed "forgot password" link (section 9).

## 2. The database

### 2.1 A new installation

1. In SSMS, run `Database\schema.sql`. It creates the database `HMSDB` and its tables, and puts a new database in the
   SIMPLE recovery model (explained in section 8.5).
2. Registration only creates students, so create the first administrator: follow the steps at the top of
   `Database\seed-admin.sql` (it shows how to produce the password hash) and run it.

### 2.2 A database from an earlier version

Do not run `schema.sql`. Take a backup (section 8), then run `Database\upgrade-existing-db.sql`. It is safe to repeat and ends
with a list of data that needs a human decision (a room given to two students, accounts that share an e-mail address, ...).
Fix what it lists and run it again. Run it **before** putting a newer version of the site on the server.

### 2.3 The login the site uses (not `sa`)

The site only reads and writes rows. It does not need to be `sa` or `db_owner`, and a flaw in the site, or a copy of
`connectionStrings.config` that falls into the wrong hands, should not hand over more than that.

1. Open `Database\create-app-login.sql`. Set `@Password` to a long random password (20 characters or more; letters and
   digits are simplest). `@LoginName` can stay `hostelhub_app`.
2. Run it as an administrator, in SSMS. It creates a login that can read and write the rows of `HMSDB` and nothing else:
   it cannot create, change or drop tables, and has no rights at server level.
3. Put the login in the connection string (section 5).

Run the script again to change the password, to take away rights that were granted by hand, or after restoring the database
on another server (see 8.4). SQL Server logins need **mixed-mode authentication**: in SSMS, server Properties, Security,
"SQL Server and Windows Authentication mode", then restart the SQL Server service. Express installs often start in
Windows-only mode, and then the login is refused with "Login failed" whatever the password.

**Without a password at all.** When IIS and SQL Server are on the same machine you can give the application pool's own
Windows identity access instead. In SSMS (replace `HostelHub` with your application pool's name):

```sql
USE master;
CREATE LOGIN [IIS APPPOOL\HostelHub] FROM WINDOWS;
GO
USE HMSDB;
CREATE USER [IIS APPPOOL\HostelHub] FOR LOGIN [IIS APPPOOL\HostelHub];
ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\HostelHub];
ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\HostelHub];
GO
```

and use `Integrated Security=True` in the connection string instead of a user and password. This variant has the same
rights as the script's login but is not covered by the project's automated tests (section 13).

### 2.4 Stop using `sa`, and change its password

If the site, or anything else, ever signed in as `sa`, retire that. Earlier versions of this repository kept a connection
string for `sa` in `Web.config` (see the history: "Move database credentials out of Web.config"), so anyone who has ever
had a copy of the repository has that password. Treat it as public:

```sql
ALTER LOGIN sa WITH PASSWORD = N'a new, long, random password, kept in your password manager';
```

Look for the old password in old copies of `Web.config`, deployment notes and backups of the old site, and change it
wherever it is still used. Once you have another way to administer the server (your own Windows account as a `sysadmin`),
you may also `ALTER LOGIN sa DISABLE;`. Check that other way works first: disabling `sa` while it is your only
administrator locks you out.

## 3. Publishing the site

A plain "Build" does not apply `Web.Release.config`; publishing does. The publish step is what removes `debug`, makes the
cookies HTTPS-only, and shows the error page to everyone. Publish from a **clean checkout** of the repository:

* Visual Studio: open `HostelManagement.sln`, set the configuration to **Release**, right-click the web project, Publish,
  Folder; or
* the command line, from a Developer Command Prompt:

  ```bat
  msbuild HostelManagement\HostelManagement.csproj /p:Configuration=Release /p:DeployOnBuild=true /p:WebPublishMethod=FileSystem /p:PublishUrl=C:\publish\HostelHub /p:ExcludeApp_Data=true
  ```

Then check the output folder before copying it anywhere:

1. `Web.config` has no `debug="true"` on `<compilation>`, has `requireSSL="true"` on `<httpCookies>`, and has
   `<customErrors mode="On" ...>`.
2. There is **no** `connectionStrings.config` and no `appSettings.local.config` in it. The project includes them in a
   publish when they exist on the build machine, which would put your development database password on the server. They are
   git-ignored so that each machine has its own; delete them from the output if they are there.
3. `Health.ashx` and `bin\HostelManagement.dll` are there.

`scripts\build-windows.ps1` builds the solution with MSBuild and precompiles every page, which catches broken markup before
it fails at run time. Run it on the build machine before the first deployment.

## 4. IIS

Run these in an elevated PowerShell. Names and paths are examples; use your own and keep them consistent.

**Install IIS with ASP.NET 4.x**

```powershell
# Windows Server
Install-WindowsFeature Web-Server, Web-Asp-Net45, Web-Mgmt-Console
# Windows 10/11
Enable-WindowsOptionalFeature -Online -All -FeatureName IIS-WebServerRole, IIS-ASPNET45, IIS-ManagementConsole
```

**Copy the site and give the site's identity the rights it needs**

```powershell
Copy-Item C:\publish\HostelHub C:\inetpub\HostelHub -Recurse
New-Item -ItemType Directory C:\inetpub\HostelHub\App_Data -Force | Out-Null
```

The application pool's identity needs to *read* the site (IIS grants that by default) and to *write* to `App_Data`, where
the log goes (the pool is created below):

```powershell
icacls C:\inetpub\HostelHub\App_Data /grant "IIS AppPool\HostelHub:(OI)(CI)M"
```

If it cannot write there, the site still runs, but log entries fall back to the Windows trace and you lose the log.

**Application pool and site**

```powershell
Import-Module WebAdministration
New-WebAppPool -Name HostelHub
Set-ItemProperty IIS:\AppPools\HostelHub -Name managedRuntimeVersion -Value v4.0
# Sessions live in the pool's memory. An idle time-out would sign everyone out whenever the site is quiet.
Set-ItemProperty IIS:\AppPools\HostelHub -Name processModel.idleTimeout -Value "00:00:00"
New-Website -Name HostelHub -PhysicalPath C:\inetpub\HostelHub -ApplicationPool HostelHub -HostHeader hostel.example.com -Port 80
```

Leave the pool on the integrated pipeline and a single worker process. In IIS Manager, Advanced Settings of the pool, set
its regular recycle to a fixed quiet hour (the default is every 29 hours, in the middle of somebody's afternoon). A recycle
signs everybody out and resets the sign-in rate limits, as does editing `Web.config` or anything in `bin`.

**The default document.** Visitors are sent to `Login.aspx`, but the site does not name a default document, so
`https://hostel.example.com/` on its own may answer "403". In IIS Manager, select the site, Default Document, and add
`Login.aspx`.

## 5. The two settings files on the server

Neither is in the repository (so a password never is), and neither is overwritten when you upgrade (section 10). Create
them in the site folder, next to `Web.config`.

**`connectionStrings.config`** (needed): copy `connectionStrings.config.example` and fill it in.

```xml
<connectionStrings>
  <add name="DBConnection" providerName="System.Data.SqlClient"
       connectionString="Data Source=.\SQLEXPRESS;Initial Catalog=HMSDB;User ID=hostelhub_app;Password=THE_PASSWORD;" />
</connectionStrings>
```

`Data Source` is `.\SQLEXPRESS` for Express on the same machine, or `SERVERNAME\INSTANCE`, or `SERVERNAME,1433` for another
machine (which also needs TCP/IP enabled in SQL Server Configuration Manager and port 1433 open in its firewall). With the
Windows-identity variant of 2.3 use `Integrated Security=True` instead of `User ID` and `Password`. Restrict who can read
the file:

```powershell
icacls C:\inetpub\HostelHub\connectionStrings.config /inheritance:r /grant:r "BUILTIN\Administrators:F" /grant:r "IIS AppPool\HostelHub:R"
```

**`appSettings.local.config`** (optional): copy `appSettings.local.config.example`. It holds the mail settings (section 9)
and can hold any other application setting:

| Setting | Meaning | Default |
|---|---|---|
| `LogRetentionDays` | Days of daily log files to keep before they are deleted. `0` keeps every file. | 90 |
| `Logging` | `0` writes errors only; anything else also writes routine entries. | on |
| `LogFileName` | Prefix of the daily log files (`log1-20261113.log`). | `log1` |

## 6. HTTPS

1. Get a certificate for the host name (from your provider, your organisation, or a free one from Let's Encrypt with an ACME
   client) and import it into the machine's "Personal" store.
2. Bind it to the site (the thumbprint is shown in the certificate's details):

   ```powershell
   New-WebBinding -Name HostelHub -Protocol https -Port 443 -HostHeader hostel.example.com -SslFlags 1
   (Get-WebBinding -Name HostelHub -Protocol https).AddSslCertificate("THE_CERTIFICATE_THUMBPRINT", "My")
   ```

3. Send plain-HTTP visitors to HTTPS (the IIS "URL Rewrite" module does this) or remove the port-80 binding.

The Release build marks its cookies HTTPS-only. Over plain `http://` the browser never sends the sign-in cookie back, so a
visitor signs in and is immediately asked to sign in again. That is the Release configuration working, not a fault. Once
HTTPS works, you may add an HSTS header in IIS Manager (HTTP Response Headers, `Strict-Transport-Security`,
`max-age=31536000`); do it only when you are sure the site will stay on HTTPS, because browsers remember it.

## 7. First start: how to tell it works

1. Open `https://hostel.example.com/Login.aspx` and sign in as the administrator from `seed-admin.sql`.
2. Open `https://hostel.example.com/Health.ashx`. It answers `Healthy`. (`Unhealthy`, with status 503, means the site cannot
   reach its database or the database lacks something this version needs; the reason is in the log.)
3. Open today's file in `App_Data\Logs` (`log1-<date>.log`). The application writes what it found when it started. You want:

   ```
   [INFO] Startup: the database has every table and column this version of the application needs.
   ```

   If instead it says `Startup: the database does not match this version of the application` followed by what is missing,
   run `Database\upgrade-existing-db.sql` (section 2.2). `Startup: the database could not be read` means the connection
   string or the SQL Server is wrong; the entry before it has the SQL Server's own message.
4. Add a room, register a student in a private window, and approve the request.

## 8. Backups, and restoring

A backup is the only thing that brings the data back after a failed disk, a mistaken delete or a bad upgrade, and a backup
that has never been restored is a hope. So there are two scripts, `Database\backup.sql` and `Database\restore.sql`, and a
habit: **practise restoring** (8.3) before you need to.

### 8.1 Take a backup

1. Make a folder for the backups, for example `C:\HostelHubBackups`. The account SQL Server runs as must be able to write
   to it (for a default instance that is `NT SERVICE\MSSQLSERVER`, for Express `NT SERVICE\MSSQL$SQLEXPRESS`):

   ```powershell
   icacls C:\HostelHubBackups /grant "NT SERVICE\MSSQL`$SQLEXPRESS:(OI)(CI)M"
   ```

2. Copy `Database\backup.sql` to a folder outside the website (`C:\HostelHubOps`), and set the three values at the top of
   your copy: `@DatabaseName` (`HMSDB`), `@BackupFolder`, and `@Compress` (`1` makes smaller files; Express cannot, and the
   script refuses).
3. Run your copy in SSMS as an administrator. It writes `HMSDB_<date>_<time>.bak` (the time runs to the millisecond, so no
   two runs share a name), then reads the whole file back and checks every page against its checksum. You should see
   `Backup written and verified: <path>`. If anything fails, the script stops at the first error and says why; it never
   announces success after an error.

Take one by hand before every upgrade and before anything you are not sure about.

### 8.2 Take one every day, and keep copies elsewhere

SQL Server Express has no SQL Server Agent, so use Windows Task Scheduler. The task must run as a Windows account that is a
member of SQL Server's `sysadmin` role (the account that installed SQL Server is one), because the check of the finished
file needs rights that an ordinary login does not have. Once, in an elevated command prompt (it asks for the password):

```bat
schtasks /Create /TN "HostelHub backup" /SC DAILY /ST 02:15 /RU "DOMAIN\sqladmin" /RP * /TR "sqlcmd -S .\SQLEXPRESS -E -b -i C:\HostelHubOps\backup.sql -o C:\HostelHubOps\backup.log"
```

`-E` signs in with that account, and `-b` makes `sqlcmd` return an error code when the script fails, which Task Scheduler
then shows in "Last Run Result" (`0x0` is success). Look at it, and at the date of the newest file, once a week. If `sqlcmd`
complains about the server's certificate, add `-C`.

A backup on the same disk as the database does not survive that disk. Copy the folder to another machine or to cloud storage
every day (`robocopy C:\HostelHubBackups \\fileserver\hostelhub-backups *.bak /XO`, or a sync client).

Nothing deletes old backups. Save this as `C:\HostelHubOps\tidy-backups.ps1` and schedule it weekly the same way
(`powershell -NoProfile -File C:\HostelHubOps\tidy-backups.ps1`); it removes only `HMSDB_*.bak` files older than 30 days:

```powershell
Get-ChildItem -Path C:\HostelHubBackups -Filter "HMSDB_*.bak" |
    Where-Object LastWriteTime -lt (Get-Date).AddDays(-30) |
    Remove-Item
```

### 8.3 Practise a restore

Do this once now, and again every few months. It touches nothing live.

1. Copy `Database\restore.sql` to `C:\HostelHubOps`. In your copy set `@BackupFile` to a real backup file and leave the
   rest alone: `@RestoreAs = N'HMSDB_Practice'`, `@ReplaceExisting = 0`.
2. Run it as an administrator. It checks the file first, restores it as `HMSDB_Practice` beside the live database, and then
   checks the copy (`DBCC CHECKDB`). The last lines say what it did.
3. Look at the copy (`SELECT COUNT(*) FROM HMSDB_Practice.dbo.tbl_User;`). When you are satisfied:
   `DROP DATABASE HMSDB_Practice;`.

### 8.4 Recover

Everything entered since the last backup is lost, so how recent your backups are is how much you can lose.

1. Take the site offline: put a file called `app_offline.htm` (any text) in the site folder. IIS then answers every request
   with it.
2. In your copy of `restore.sql` set `@BackupFile` to the backup to go back to, `@RestoreAs = N'HMSDB'` and
   `@ReplaceExisting = 1`. Run it as an administrator.
   * Everyone connected to `HMSDB` is disconnected first.
   * Replacing is one-way: once SQL Server starts, the old contents are gone. So the script first saves a **safety copy** of
     the current database, `HMSDB_before_restore_<time>.bak`, next to the backup file, and stops, showing why, if it
     cannot (folder not writable, disk full, database offline). Fix that and run it again. If the current database is too
     damaged to copy and you accept losing it, set `@SafetyBackup = 0`.
   * If the restore fails part-way, `HMSDB` is left in the `RESTORING` state and cannot be used. Fix the cause named in the
     error messages and **run the script again with the same settings**: it carries on and completes the restore.
   * The file must carry page checksums, as every backup made by `backup.sql` does. A backup made some other way without
     them is refused (error 3187) before anything changes.
3. **On a different server**, the login of section 2.3 does not exist yet: run `Database\create-app-login.sql` in the
   restored `HMSDB` (it also reconnects the database user to the new login) and put the login in
   `connectionStrings.config`. On a server that has no `HMSDB` at all, set `@ReplaceExisting = 0`: there is nothing to
   replace, and `schema.sql` is not needed.
4. Delete `app_offline.htm`, open `Health.ashx`, and read the first lines of the log (section 7).

### 8.5 A note on the recovery model

A database made by `schema.sql` uses the SIMPLE recovery model: the transaction log reuses its space on its own, and a restore
can only go back to the moment of a full backup, not to an exact time. The other model, FULL, lets you restore to an exact time
but the log grows until the log itself is backed up, and a small installation that only takes daily full backups would
eventually fill its disk. If your database was made by an earlier version it may be in FULL: `backup.sql` prints a note when a
FULL database has never had its log backed up. Unless you want log backups, run `ALTER DATABASE HMSDB SET RECOVERY SIMPLE;`.

## 9. Mail (optional)

Without mail settings nothing is ever sent and the "Forgot password?" link is hidden; an administrator can issue a temporary
password from the student list instead. To switch it on, create `appSettings.local.config` (section 5) with the values from
`appSettings.local.config.example`:

* `App.BaseUrl`, the public address of the site, **https** (plain `http` is accepted only for `localhost`). The link in the
  e-mail is built from this, never from the incoming request, so a forged `Host` header cannot point reset links at someone
  else's server.
* `Smtp.Host`, `Smtp.Port`, `Smtp.EnableSsl`, `Smtp.UserName`, `Smtp.Password`, `Smtp.FromAddress`, `Smtp.FromName`.

Changing the file restarts the site. To test: the "Forgot password?" link appears on the sign-in page; request a reset for a
real student address and read the message. Each request is logged: `A password reset link was sent to user <id>.` on
success, `Could not send the password reset e-mail to user <id>.` followed by the mail library's error when it fails (wrong
host, port, user or password, or a server that refuses relaying). Mail sent from a new address is often judged spam: ask the
mail provider how to set up SPF and DKIM for the `Smtp.FromAddress` domain.

## 10. Upgrading to a newer version

1. Take a backup (8.1).
2. If the new version comes with database changes, run `Database\upgrade-existing-db.sql` (2.2). It is safe to repeat, and
   running it before the new files arrive is what keeps the site working in between.
3. Publish the new version (section 3) to a folder, take the site offline (`app_offline.htm` in the site folder), and copy
   the files **without** touching the two settings files or `App_Data`. Check what would change before doing it:

   ```powershell
   $new = "C:\publish\HostelHub"; $site = "C:\inetpub\HostelHub"
   robocopy $new $site /MIR /L /XF connectionStrings.config appSettings.local.config app_offline.htm /XD App_Data
   ```

   then run it again without `/L`. (`/MIR` also deletes files that are not in the new version; the excluded files and folder
   are never deleted.)
4. Delete `app_offline.htm`, open `Health.ashx`, and read the first lines of the log (section 7). Everyone is signed out by
   the restart.

## 11. Running it: logs, health, restarts

* **Logs** are in `App_Data\Logs`, one file a day, and are never served by IIS. They hold e-mail addresses (every sign-in), so
  keep the folder private; files older than `LogRetentionDays` (90 unless you set it) are deleted by the site itself, from the
  first entry of each day. Only files named like its own (`log1-yyyymmdd.log`) are ever deleted; set `LogRetentionDays` to `0`
  if you want to keep everything.
* **IIS's own logs** (`C:\inetpub\logs\LogFiles`) record every address requested, and an e-mailed reset link carries its token
  in the address. The link works once and for an hour, but keep those logs private too.
* **Health.ashx** is for a monitor: any uptime service, or a load balancer, that calls it every minute or so and alerts on
  anything but `200`. It looks at the database each time, so it reports a database that has gone away, which a page that
  only checks the web server would not. It is open to anyone and says only `Healthy` or `Unhealthy`.
* **Restarts and recycles** sign everyone out and forget the sign-in rate limits (section 4). Do deployments and
  settings changes at a quiet hour.

## 12. When something is wrong

| What you see | Usual cause | What to do |
|---|---|---|
| `https://host/` alone gives 403 or 404 | no default document | add `Login.aspx` as the default document (section 4), or use `/Login.aspx` |
| You sign in and are sent straight back to the sign-in page | the Release build only keeps its cookie over HTTPS, and you are on `http://` | use `https://` (section 6) |
| "Login failed for user" in the log, or `Startup: the database could not be read` | wrong password in `connectionStrings.config`; SQL logins not allowed (mixed mode is off); the login is disabled; a firewall or TCP/IP setting if SQL Server is on another machine | check the entry before the `Startup` line for the SQL Server's message; section 2.3 |
| Pages fail, the log says `the database schema looks out of date`, or `Startup: the database does not match ...` | the site was deployed before the database was upgraded | run `Database\upgrade-existing-db.sql` (section 2.2) |
| The startup line says a table is missing "or the application's database login has no rights on it" for **every** table | the login was never given its rights | run `Database\create-app-login.sql` (section 2.3) |
| `Health.ashx` answers 503 | the database is down, unreachable, or lacks something | the log has the reason (an error from the database, or `Health check failed: ...`) |
| There is no log, or it has stopped | the pool's identity cannot write to `App_Data` | grant it Modify (section 4) |
| "Forgot password?" is missing from the sign-in page | mail is not configured, or `Smtp.Host`, `Smtp.FromAddress` or an https `App.BaseUrl` is missing or invalid | section 9 |
| Everyone is signed out at odd times | the pool recycled (idle time-out, the regular recycle, or a change to `Web.config` or `bin`) | section 4 |
| A student typed something and saw "That text cannot be accepted" | the form refuses text that contains HTML tags (for example `<b>`), for safety | ask them to remove the tags; the log has a `Request validation refused input` warning naming the page, not the text |
| The backup task's "Last Run Result" is not `0x0`, or there is no new file | the task's account is not a `sysadmin`, or SQL Server cannot write to the folder | run the same script by hand in SSMS: the first error says which |
| `restore.sql` stops with an error about the safety copy | the folder is not writable for SQL Server, the disk is full, or `HMSDB` is offline or damaged | the error before it says which; fix it, or set `@SafetyBackup = 0` if you accept losing the database as it is |
| `restore.sql` reports error 3187 | the backup file has no page checksums (it was not made by `backup.sql`) | take backups with `backup.sql` |
| `HMSDB` says `RESTORING` | a restore failed part-way | fix the cause and run `restore.sql` again with the same settings |

## 13. What has and has not been tested

So that you know what to look at on the first deployment.

**Run automatically in this project's checks** (Linux, SQL Server 2022 in Docker, and Mono for the pages):

* every script in `Database`: `schema.sql`, `upgrade-existing-db.sql` on a database shaped like the first version's,
  `seed-admin.sql`, `create-app-login.sql`, `backup.sql` and `restore.sql`, including how they fail (a damaged backup, an
  unwritable folder, an offline database, a restore that fails part-way, a backup taken in single-user mode, a database
  using the FULL recovery model);
* the whole test suite signing in as the least-privilege login of section 2.3, so every statement the site issues is known to
  work with exactly those rights;
* `Web.Release.config` applied with the real XDT engine, and what it leaves in `Web.config`;
* the real pages in a real browser: access control, every screen, `Health.ashx` (200, and 503 while a table is hidden), the
  start-up check in the log, log retention, and the refusal of text that contains HTML tags;
* a compile of all the C# against the .NET Framework 4.8 reference assemblies.

**Not run, because the checks have no Windows machine:**

* IIS itself and the .NET Framework runtime (the pages run on Mono's re-implementation of `System.Web`);
* the commands in this guide: `msbuild` publishing, `icacls`, `schtasks`, `WebAdministration`, `New-WebBinding` and the
  Windows features (the old-backup clean-up in 8.2 was run in PowerShell 7 on Linux, not in Windows PowerShell 5.1);
* HTTPS, the HTTP redirect, HSTS, the default document;
* SQL Server Express (so the refusal of compression there is not exercised), and any SQL Server before 2022. The scripts
  avoid newer syntax, but they have only been run on 2022;
* the Windows-identity variant of 2.3, and sending real mail.

Treat the first deployment as the test of those, and read section 7's checks carefully.
