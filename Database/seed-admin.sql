/*
    HostelHub - create the first administrator account.

    Registration only creates students, so a new installation needs one administrator before anyone
    can manage rooms. No default password is shipped on purpose: generate the hash yourself.

    1. Generate a password hash with PowerShell (same algorithm as PasswordUtility.HashPassword):

           $salt = New-Object byte[] 16
           [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
           $kdf  = New-Object Security.Cryptography.Rfc2898DeriveBytes('YOUR-PASSWORD-HERE', $salt, 10000)
           [Convert]::ToBase64String([byte[]]($salt + $kdf.GetBytes(20)))

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
