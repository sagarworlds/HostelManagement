# Hostel Management System 🏨

A comprehensive web-based Hostel Management System built with **ASP.NET Web Forms**, **C#**, and **Microsoft SQL Server**. It simplifies the administrative processes of managing a hostel by providing an intuitive interface for administrators and users.

## 🚀 Features

The system offers role-based access control with distinct features for Admins and Users:

### 👨‍💼 Admin Portal
* **Dashboard:** Overview of system statistics and pending room requests.
* **Room Management:** Add, edit, and manage hostel rooms.
* **Request Management:** Review, approve, or reject room allocation requests from users.
* **Admin Management:** Add and manage other administrator accounts.

### 🎓 User Portal
* **User Authentication:** Secure registration and login functionalities.
* **Dashboard:** View current room status and announcements.
* **Room Booking:** Browse available rooms and submit requests for room allocation.
* **Account Settings:** Secure password reset and profile management.

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
* **Security:** Passwords are mathematically secured using the built-in `PasswordUtility` before saving to the database.
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
   * Create a new database named `HMSDB`.
   * *(Note: If there are SQL script files provided in the repository, execute them in SSMS to create the necessary tables).*
   
3. **Configure Connection String**
   * Open `HostelManagement\Web.config`.
   * Locate the `<connectionStrings>` block.
   * Update the `Data Source` and credentials to match your local SQL Server instance:
     ```xml
     <add name="DBConnection" connectionString="Data Source=YOUR_SERVER_NAME;Initial Catalog=HMSDB;User ID=YOUR_USER;Password=YOUR_PASSWORD;" providerName="System.Data.SqlClient;"/>
     ```

4. **Run the Application**
   * Open `HostelManagement.sln` in Visual Studio.
   * Restore any NuGet packages if prompted.
   * Press `F5` or click **Start** to run the application in your browser.

## 📸 Screenshots
*(You can add screenshots of your Dashboard, Login Page, and Room Management here to make the repository more appealing)*

## 🤝 Contributing
Contributions, issues, and feature requests are welcome! Feel free to check the issues page if you want to contribute.

---
*Built with ❤️ by [sagarworlds](https://github.com/sagarworlds)*
