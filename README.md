# HostelHub

A centralized hostel & dormitory management system for administrators and staff. Built with **ASP.NET Web Forms** and **C#**.

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![ASP.NET](https://img.shields.io/badge/ASP.NET-512BD4?style=for-the-badge&logo=dot-net&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white)
![HTML5](https://img.shields.io/badge/HTML5-E34C26?style=for-the-badge&logo=html5&logoColor=white)
![CSS3](https://img.shields.io/badge/CSS3-1572B6?style=for-the-badge&logo=css3&logoColor=white)
![JavaScript](https://img.shields.io/badge/JavaScript-F7DF1E?style=for-the-badge&logo=javascript&logoColor=black)
![Bootstrap](https://img.shields.io/badge/Bootstrap-7952B3?style=for-the-badge&logo=bootstrap&logoColor=white)

A comprehensive web-based Hostel Management System built with **ASP.NET Web Forms**, **C#**, and **Microsoft SQL Server**. It simplifies the administrative processes of managing a hostel by providing room allocation, student accounts, announcements and password management. Fee tracking and maintenance requests are not built yet (see the roadmap below).

## 🚀 Features

The system offers role-based access control with distinct features for Admins and Users:

### 👨‍💼 Admin Portal
* **Dashboard:** Headline statistics (pending requests, total/available/occupied rooms, students, administrators) and the room requests, filterable by status.
* **Room Management:** Add, edit, and delete rooms, see each room's status and current occupant, and vacate a room when a student leaves.
* **Request Management:** Review, approve, or reject room allocation requests from users.
* **Admin Management:** Add and manage other administrator accounts.
* **Announcements:** Post notices that appear on every student's dashboard until they end or are removed.
* **Student Management:** View and edit student accounts, and issue a temporary password when a student is locked out (they must choose a new one at their next sign-in).

### 🎓 User Portal
* **User Authentication:** Secure registration and login functionalities.
* **Dashboard:** Current announcements, and the status of your room requests.
* **Room Booking:** Browse available rooms and ask for one (the administrator chooses the room), or submit a request with no preference.
* **Account Settings:** Edit name and mobile number, and change the password (at least 8 characters, and not a commonly guessed one).
* **Forgot password:** Emailed single-use link that expires after an hour. Switched off until a mail server is configured (see setup step 3); until then an administrator can issue a temporary password.

### 🔄 Room Request Lifecycle
`Pending` → `Approved` / `Rejected` / `Cancelled`, and `Approved` → `Vacated`.
* A student can hold **one** pending request or one allocated room at a time, and can cancel a request while it is pending.
* Approving a request allocates a free room in a single transaction; if another administrator took the room (or decided the request) first, nothing changes and the admin is told why.
* Vacating a room ends the allocation and returns the room to the available pool.

## 🛠️ Technology Stack

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![ASP.NET](https://img.shields.io/badge/ASP.NET-512BD4?style=for-the-badge&logo=dot-net&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white)
![HTML5](https://img.shields.io/badge/HTML5-E34C26?style=for-the-badge&logo=html5&logoColor=white)
![CSS3](https://img.shields.io/badge/CSS3-1572B6?style=for-the-badge&logo=css3&logoColor=white)
![JavaScript](https://img.shields.io/badge/JavaScript-F7DF1E?style=for-the-badge&logo=javascript&logoColor=black)
![Bootstrap](https://img.shields.io/badge/Bootstrap-7952B3?style=for-the-badge&logo=bootstrap&logoColor=white)

* **Backend:** C# (.NET Framework 4.8)
* **Framework:** ASP.NET Web Forms
* **Database:** Microsoft SQL Server (ADO.NET)
* **Frontend:** HTML5, CSS3, JavaScript
* **UI Theme:** Bootstrap 5 (SB Admin Theme)
* **IDE:** Microsoft Visual Studio

## ⚙️ Architecture & Security
* **N-Tier Architecture:** Separation of concerns using Model classes (`User.cs`, `Room.cs`) and Data Access Layer classes (`DBAccess.cs`, `UserDB.cs`, `RoomDB.cs`).
* **Security:**
  * Passwords are hashed with PBKDF2-HMAC-SHA256 (600,000 iterations, per-password salt, constant-time comparison). Hashes written by older versions still work and are upgraded the next time that user signs in.
  * Failed sign-ins are rate limited per account+address, per address and per account, without revealing which accounts exist.
  * A signed-in session is only trusted together with a second HttpOnly cookie issued at sign-in (defeats session fixation); signed-in pages are never cached; view state is tied to the session.
  * Cookies are HttpOnly and SameSite=Lax; Release builds also require HTTPS cookies and turn off debug and detailed errors. Security headers (`nosniff`, frame options, referrer policy) are set.
  * Everything users type is HTML-encoded where it is displayed; reset-link tokens are stored only as hashes.
  * Not covered: a Content-Security-Policy and HSTS (both depend on how you deploy), and the rate limits are in memory, so they reset when the application restarts and are per server. A reset link's token is part of its URL, so your web server's access log will contain it while it is valid (single use, one hour); keep those logs private.
* **Validation:** Robust client-side (JavaScript/Bootstrap) and server-side (ASP.NET Validators) input validation.

## 💻 Prerequisites

To run this project locally, you will need:
* **Microsoft Visual Studio** (2019 or later recommended)
* **Microsoft SQL Server** (Express or standard edition)
* **.NET Framework 4.8** SDK

## 🛠️ Installation & Setup

1. **Clone the Repository**
   ```bash
   git clone https://github.com/sagarworlds/HostelManagement.git
   cd HostelManagement
   ```

2. **Database Setup**
   * Open Microsoft SQL Server Management Studio (SSMS).
   * Run `Database\schema.sql` — it creates the `HMSDB` database and its tables.
   * Registration only creates students, so create the first administrator: follow the steps at the top of `Database\seed-admin.sql` (it explains how to generate the password hash) and run it.
   * Already have an `HMSDB` database from an earlier version? Don't run `schema.sql`; run `Database\upgrade-existing-db.sql` instead, which is safe to repeat and ends with diagnostics for data that needs a manual look. **Run it before deploying a newer version of the application:** newer versions read columns (`MustChangePassword`, `PreferredRoomId`) that older databases don't have, and sign-in fails — with a log entry saying the schema looks out of date — until they exist.

3. **Configure Connection String**
   * Copy `HostelManagement\connectionStrings.config.example` to `HostelManagement\connectionStrings.config`.
   * Edit the new file and set the `Data Source` and credentials for your SQL Server instance:
     ```xml
     <add name="DBConnection" connectionString="Data Source=YOUR_SERVER_NAME;Initial Catalog=HMSDB;User ID=YOUR_USER;Password=YOUR_PASSWORD;" providerName="System.Data.SqlClient" />
     ```
   * `connectionStrings.config` is git-ignored, so your credentials are never committed. `Web.config` points to it via `configSource`.
   * *Optional:* to switch on emailed password reset, copy `HostelManagement\appSettings.local.config.example` to `appSettings.local.config` and fill in your mail server and the public `App.BaseUrl` (https). It is git-ignored too. Without it, nothing is ever sent and the "Forgot password?" link is hidden.

4. **Run the Application**
   * Open `HostelManagement.sln` in Visual Studio.
   * Restore any NuGet packages if prompted.
   * Press `F5` or click **Start** to run the application in your browser.
   * Errors and sign-ins are logged to `HostelManagement\App_Data\Logs\` (one file per day, never served by IIS). The app pool identity needs write access to `App_Data`; if it has none, log entries fall back to `System.Diagnostics.Trace`.

## ✅ Testing & CI

Checks are split by what they need, so Windows-only work stays on a developer machine and only the cheap Linux checks run on GitHub.

| Where | What | How |
|---|---|---|
| Any OS with Python, .NET 8 SDK, Node | Static checks (csproj lists every file, designer files match markup, SQL parameters and columns match, markup is well-formed and handlers exist), a compile of all C# against the .NET Framework 4.8 reference assemblies, unit tests, and a headless-browser test of the grid search script | `tests/run-local.sh` (add `--require-all` to fail instead of skip when a tool is missing) |
| GitHub Actions, Linux (`.github/workflows/ci.yml`) | The same `tests/run-local.sh --require-all` on every push and pull request | automatic |
| Windows with Visual Studio / Build Tools | MSBuild of the real web project and the ASPX precompile, which catches broken markup before run time | `powershell -ExecutionPolicy Bypass -File scripts\build-windows.ps1` |
| GitHub Actions, Windows (`.github/workflows/windows-build.yml`) | The same Windows script, only when started by hand from the Actions tab | manual |

## 📸 Screenshots
*(You can add screenshots of your Dashboard, Login Page, and Room Management here to make the repository more appealing)*

## 🗺️ Roadmap
Not built yet, because each needs decisions only the people running a hostel can make:
* **Fee / rent tracking** — what is billed and when, payments, receipts, arrears.
* **Maintenance requests** — who can raise them, categories, assignment, status.

## 🤝 Contributing
Contributions, issues, and feature requests are welcome! Feel free to check the issues page if you want to contribute.

---
*Built with ❤️ by [sagarworlds](https://github.com/sagarworlds)*
