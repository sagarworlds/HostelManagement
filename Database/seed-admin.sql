/*
    HostelHub - create the first administrator account.

    Registration only creates students, so a new installation needs one administrator before anyone
    can manage rooms. No default password is shipped on purpose: generate the hash yourself.

    1. Generate a password hash with PowerShell (same algorithm and format as PasswordUtility.HashPassword;
       needs .NET Framework 4.7.2 or later, which Windows PowerShell has on any machine that can run the application):

           $iterations = 600000
           $salt = New-Object byte[] 16
           [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
           $kdf  = New-Object Security.Cryptography.Rfc2898DeriveBytes('YOUR-PASSWORD-HERE', $salt, $iterations, [Security.Cryptography.HashAlgorithmName]::SHA256)
           'v2$' + $iterations + '$' + [Convert]::ToBase64String($salt) + '$' + [Convert]::ToBase64String($kdf.GetBytes(32))

    2. Paste the output into @PasswordHash below, adjust the other values and run the script.
*/

USE HMSDB;
GO

DECLARE @Email        NVARCHAR(256) = N'admin@example.com';
DECLARE @FirstName    NVARCHAR(100) = N'System';
DECLARE @LastName     NVARCHAR(100) = N'Administrator';
DECLARE @PasswordHash NVARCHAR(128) = N'PASTE_HASH_HERE';

IF @PasswordHash = N'PASTE_HASH_HERE'
    THROW 50000, 'Replace @PasswordHash with a hash generated as described at the top of this script.', 1;

IF EXISTS (SELECT 1 FROM dbo.tbl_User WHERE Email = @Email)
    THROW 50001, 'A user with this e-mail address already exists.', 1;

INSERT INTO dbo.tbl_User (Email, [Password], UserType, FirstName, LastName, MobileNo, CreatedOn, ModifiedOn)
VALUES (@Email, @PasswordHash, N'Admin', @FirstName, @LastName, N'', GETDATE(), GETDATE());

SELECT Id, Email, UserType FROM dbo.tbl_User WHERE Email = @Email;
GO
