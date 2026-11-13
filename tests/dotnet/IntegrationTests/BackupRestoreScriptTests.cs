using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    /// <summary>
    /// Database/backup.sql and Database/restore.sql, run on a real server against databases of their own: the shared test
    /// database is never backed up over, restored over or taken offline here.
    /// </summary>
    /// <remarks>
    /// SQL Server writes the backup files itself, into the server's own backup folder, and nothing here can remove them:
    /// each run leaves a few small files there. Use a throw-away server (tests/start-sql-server.sh) as for all of these tests.
    /// <para>
    /// Not covered, because it cannot be arranged on a server that is simply there: a backup file that has been damaged
    /// on disk, and an operating-system failure part-way through restoring over a live database (verified by hand, see
    /// docs/deploying.md). A database left RESTORING, which is what that failure leaves behind, is covered.
    /// </para>
    /// </remarks>
    [Collection(DatabaseCollection.Name)]
    public class BackupRestoreScriptTests : IDisposable
    {
        private const string Live = "HMSDB_IntegrationTest_Live";
        private const string Copy = "HMSDB_IntegrationTest_Copy";

        private const string RoomInBackup = "Room in the backup";
        private const string RoomAddedLater = "Room added after the backup";

        private const int NoSuchDatabase = 50010;
        private const int ExpressCannotCompress = 50011;
        private const int SystemDatabaseRefused = 50020;
        private const int NameAlreadyTaken = 50021;
        private const int CannotOpenBackupDevice = 3201;
        private const int DatabaseIsOffline = 942;
        private const int NoFilesReadyToRollForward = 3117;

        private const string SafetyCopyLine = "Safety copy of the current database: ";

        private readonly DatabaseFixture _database;
        private readonly TestData _server;
        private readonly TestData _live;
        private readonly TestData _copy;

        /// <summary>What the script that ran last printed, in order.</summary>
        private readonly List<string> _messages = new List<string>();

        public BackupRestoreScriptTests(DatabaseFixture database)
        {
            _database = database;
            _server = new TestData(database.ServerConnectionString);
            _live = new TestData(ConnectionStringFor(Live));
            _copy = new TestData(ConnectionStringFor(Copy));

            DropScratchDatabases();   // an interrupted earlier run may have left them behind
            SqlScript.Run(database.ServerConnectionString, SqlScript.Retarget(DatabaseFixture.ReadDatabaseScript("schema.sql"), Live));
            _live.AddRoom(RoomInBackup);
        }

        public void Dispose()
        {
            DropScratchDatabases();
        }

        // ---- backup.sql ----

        [Fact]
        public void A_backup_is_one_checked_file_named_after_the_database_and_the_time()
        {
            string file = BackUp();

            Assert.Matches(@"[\\/]" + Live + @"_\d{8}_\d{6}_\d{3}\.bak$", file);
            Assert.Contains(_messages, message => message.StartsWith("Backup written and verified: " + file));
            DataRow header = HeaderOf(file);
            Assert.Equal(Live, (string)header["DatabaseName"]);
            Assert.True(Flag(header, "HasBackupChecksums"), "page checksums are what lets a damaged backup be found");
            Assert.False(Flag(header, "Compressed"));
            Assert.False(Flag(header, "IsCopyOnly"), "an ordinary backup, so that differential backups can build on it");
            Verify(file);   // checked here as well, independently of the script's own check
        }

        [Fact]
        public void Every_run_writes_a_new_file_and_leaves_the_earlier_ones_alone()
        {
            string first = BackUp();
            string second = BackUp();   // straight away: the names carry the time to the millisecond

            Assert.NotEqual(first, second);
            Verify(first);
            Verify(second);
        }

        [Fact]
        public void A_database_that_does_not_exist_is_refused()
        {
            SqlException error = Assert.Throws<SqlException>(() => RunBackup(database: "HMSDB_IntegrationTest_NoSuchDatabase"));

            Assert.Equal(NoSuchDatabase, error.Number);
        }

        [Fact]
        public void A_compressed_backup_is_made_where_the_server_can_make_one_and_refused_where_it_cannot()
        {
            const int expressEdition = 4;
            if (_server.Scalar<int>("SELECT CONVERT(INT, SERVERPROPERTY('EngineEdition'));") == expressEdition)
            {
                Assert.Equal(ExpressCannotCompress, Assert.Throws<SqlException>(() => RunBackup(compress: true)).Number);
                return;
            }

            string file = FileReportedBy(RunBackup(compress: true));

            Assert.True(Flag(HeaderOf(file), "Compressed"));
            Verify(file);
        }

        [Fact]
        public void A_backup_that_cannot_be_written_stops_with_the_cause_and_never_reports_success()
        {
            // A file where the folder should be: nothing can be written inside it, on any operating system. (A folder that
            // merely does not exist would not do: SQL Server on Linux creates it.)
            string aFileNotAFolder = BackUp();

            // Run like SSMS runs it. SQL Server only ends the failed statement, so unless the script stops itself it carries
            // on, and finishes by announcing "Backup written and verified" and returning a file name that does not exist.
            ScriptOutput output = SqlScript.RunReportingErrors(_database.ServerConnectionString, BackupScript(folder: aFileNotAFolder));

            Assert.Contains(output.Errors, e => e.Number == CannotOpenBackupDevice);
            Assert.DoesNotContain(output.Messages, message => message.Contains("Backup written and verified"));
            Assert.Empty(output.ResultSets);
        }

        [Fact]
        public void A_database_in_the_full_recovery_model_is_told_about_its_log_and_a_simple_one_is_not()
        {
            Assert.Equal("SIMPLE", Scalar("SELECT recovery_model_desc FROM sys.databases WHERE name = @Name;", Live));   // schema.sql made it
            BackUp();
            Assert.DoesNotContain(_messages, message => message.Contains("FULL recovery model"));

            _server.Execute("ALTER DATABASE [" + Live + "] SET RECOVERY FULL;");
            BackUp();

            Assert.Contains(_messages, message => message.Contains("FULL recovery model"));
        }

        // ---- restore.sql ----

        [Fact]
        public void A_practice_restore_makes_a_working_copy_beside_the_live_database_and_leaves_that_alone()
        {
            string backup = BackUp();
            _live.AddRoom(RoomAddedLater);

            List<DataTable> result = RunRestore(backup, Copy);

            Assert.Equal("ONLINE", (string)result.Last().Rows[0]["State"]);
            Assert.True(HasRoom(_copy, RoomInBackup));
            Assert.False(HasRoom(_copy, RoomAddedLater), "the copy holds the data as it was at the backup");
            Assert.True(HasRoom(_live, RoomAddedLater), "the live database is untouched");
            Assert.Empty(FilesOf(Copy).Intersect(FilesOf(Live)));
            Assert.All(FilesOf(Copy), file => Assert.Contains(Copy, file));
        }

        [Fact]
        public void A_name_already_in_use_is_refused_unless_replacing_was_asked_for()
        {
            string backup = BackUp();
            RunRestore(backup, Copy);
            _copy.AddRoom("Only in the copy");

            SqlException error = Assert.Throws<SqlException>(() => RunRestore(backup, Copy));

            Assert.Equal(NameAlreadyTaken, error.Number);
            Assert.True(HasRoom(_copy, "Only in the copy"));
        }

        [Fact]
        public void A_system_database_is_never_restored_over()
        {
            string backup = BackUp();

            foreach (string name in new[] { "master", "model", "msdb", "tempdb" })
                Assert.Equal(SystemDatabaseRefused, Assert.Throws<SqlException>(() => RunRestore(backup, name, replace: true)).Number);
        }

        [Fact]
        public void Replacing_the_live_database_brings_the_backup_back_and_keeps_a_safety_copy_of_what_was_there()
        {
            string backup = BackUp();
            _live.AddRoom(RoomAddedLater);

            List<DataTable> result = RunRestore(backup, Live, replace: true);
            string safetyCopy = SafetyCopyFile();

            Assert.Equal("ONLINE", (string)result.Last().Rows[0]["State"]);
            Assert.Equal("MULTI_USER", (string)result.Last().Rows[0]["Access"]);
            Assert.True(HasRoom(_live, RoomInBackup));
            Assert.False(HasRoom(_live, RoomAddedLater), "everything since the backup is gone, as the script's header says");

            Assert.Matches(@"[\\/]" + Live + @"_before_restore_\d{8}_\d{6}_\d{3}\.bak$", safetyCopy);
            Assert.Equal(FolderOf(backup), FolderOf(safetyCopy));
            Assert.True(Flag(HeaderOf(safetyCopy), "IsCopyOnly"), "taking it must not disturb the backup routine's own chain");
            RunRestore(safetyCopy, Copy);
            Assert.True(HasRoom(_copy, RoomAddedLater), "the safety copy holds what the live database held before the restore");
        }

        [Fact]
        public void Replacing_the_live_database_throws_out_whoever_is_connected_and_their_unfinished_work()
        {
            string backup = BackUp();

            using (var user = new SqlConnection(ConnectionStringFor(Live)))
            {
                user.Open();
                using (var work = new SqlCommand(
                    "BEGIN TRAN; INSERT INTO dbo.tbl_Room (RoomName, Rent, Availability, CreatedOn, ModifiedOn) VALUES (N'Unfinished', 1, 1, GETDATE(), GETDATE());", user))
                    work.ExecuteNonQuery();

                RunRestore(backup, Live, replace: true);   // would fail with "database in use" if the user were not sent away

                Assert.ThrowsAny<Exception>(() =>
                {
                    using (var next = new SqlCommand("SELECT 1;", user))
                        next.ExecuteScalar();
                });
            }

            Assert.False(HasRoom(_live, "Unfinished"));
            Assert.True(HasRoom(_live, RoomInBackup));
        }

        [Fact]
        public void A_backup_can_be_restored_over_an_empty_database_made_by_schema_sql()
        {
            string backup = BackUp();
            // The usual recovery path: the database was created again from the schema, and the backup goes over it. SQL
            // Server only allows that with REPLACE, since the empty database is not the one the backup came from.
            SqlScript.Run(_database.ServerConnectionString, SqlScript.Retarget(DatabaseFixture.ReadDatabaseScript("schema.sql"), Copy));
            Assert.False(HasRoom(_copy, RoomInBackup));

            RunRestore(backup, Copy, replace: true);

            Assert.True(HasRoom(_copy, RoomInBackup));
            Assert.Equal("ONLINE", StateOf(Copy));
        }

        [Fact]
        public void A_database_in_the_full_recovery_model_is_replaced_although_the_end_of_its_log_was_never_backed_up()
        {
            _server.Execute("ALTER DATABASE [" + Live + "] SET RECOVERY FULL;");
            string backup = BackUp();
            _live.AddRoom(RoomAddedLater);   // in the log, in no backup: SQL Server insists on REPLACE before it throws that away

            RunRestore(backup, Live, replace: true);

            Assert.Equal("ONLINE", StateOf(Live));
            Assert.False(HasRoom(_live, RoomAddedLater));
        }

        [Fact]
        public void A_restored_database_is_open_to_everyone_even_if_it_was_backed_up_in_single_user_mode()
        {
            _server.Execute("ALTER DATABASE [" + Live + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;");
            string backup = BackUp();   // the backup brings the mode back with it
            _server.Execute("ALTER DATABASE [" + Live + "] SET MULTI_USER;");

            RunRestore(backup, Copy);
            RunRestore(backup, Live, replace: true);

            Assert.Equal("MULTI_USER", AccessOf(Copy));
            Assert.Equal("MULTI_USER", AccessOf(Live));
        }

        [Fact]
        public void Replacing_without_a_safety_copy_is_possible_when_asked_for()
        {
            string backup = BackUp();
            _live.AddRoom(RoomAddedLater);

            RunRestore(backup, Live, replace: true, safetyBackup: false);

            Assert.False(HasRoom(_live, RoomAddedLater));
            Assert.DoesNotContain(_messages, message => message.StartsWith(SafetyCopyLine));
        }

        [Fact]
        public void A_database_that_cannot_be_copied_is_left_alone_until_the_safety_copy_is_waived()
        {
            string backup = BackUp();
            _server.Execute("ALTER DATABASE [" + Live + "] SET OFFLINE WITH ROLLBACK IMMEDIATE;");

            SqlException refused = Assert.Throws<SqlException>(() => RunRestore(backup, Live, replace: true));

            AssertRaised(refused, DatabaseIsOffline);   // the reason is not hidden
            Assert.Equal("OFFLINE", StateOf(Live));   // and nothing was replaced, although the script went on to nothing else

            RunRestore(backup, Live, replace: true, safetyBackup: false);

            Assert.Equal("ONLINE", StateOf(Live));
            Assert.True(HasRoom(_live, RoomInBackup));
        }

        [Fact]
        public void A_database_left_restoring_by_a_failed_attempt_is_completed_by_running_the_script_again()
        {
            string backup = BackUp();
            _live.AddRoom(RoomAddedLater);
            // What a restore that fails part-way leaves behind: the old contents gone, the new ones not finished.
            _server.Execute(
                "ALTER DATABASE [" + Live + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                "RESTORE DATABASE [" + Live + "] FROM DISK = @File WITH REPLACE, NORECOVERY;",
                new SqlParameter("@File", backup));
            Assert.Equal("RESTORING", StateOf(Live));

            RunRestore(backup, Live, replace: true);

            Assert.Equal("ONLINE", StateOf(Live));
            Assert.Equal("MULTI_USER", AccessOf(Live));
            Assert.True(HasRoom(_live, RoomInBackup));
            Assert.False(HasRoom(_live, RoomAddedLater));
            Assert.DoesNotContain(_messages, message => message.StartsWith(SafetyCopyLine));   // it cannot be read yet, so there is nothing to copy
        }

        [Fact]
        public void A_restore_that_fails_before_anything_is_replaced_leaves_the_database_open_to_everyone()
        {
            BackUp();   // a differential backup needs a full one under it
            _live.AddRoom(RoomAddedLater);
            // A differential backup passes every check made up front but cannot be restored without its full backup, so
            // SQL Server refuses only after the script has sent everyone away: the case its error handling is there for.
            string differential = InFolder(BackupFolder(), "HMSDB_IntegrationTest_differential.bak");
            _server.Execute("BACKUP DATABASE [" + Live + "] TO DISK = @File WITH DIFFERENTIAL, CHECKSUM, INIT;", new SqlParameter("@File", differential));

            SqlException error = Assert.Throws<SqlException>(() => RunRestore(differential, Live, replace: true));

            AssertRaised(error, NoFilesReadyToRollForward);
            Assert.Equal("ONLINE", StateOf(Live));
            Assert.Equal("MULTI_USER", AccessOf(Live));
            Assert.True(HasRoom(_live, RoomAddedLater), "nothing was replaced");
        }

        [Fact]
        public void A_missing_backup_file_stops_the_script_before_anything_is_changed()
        {
            _live.AddRoom(RoomAddedLater);
            string missing = InFolder(BackupFolder(), "no-such-backup.bak");

            SqlException error = Assert.Throws<SqlException>(() => RunRestore(missing, Live, replace: true));

            AssertRaised(error, CannotOpenBackupDevice);
            Assert.Equal("ONLINE", StateOf(Live));
            Assert.True(HasRoom(_live, RoomAddedLater));
        }

        /// <summary>Asserts that the failure includes the given error; when it does not, the message says which errors did arrive.</summary>
        private static void AssertRaised(SqlException failure, int number)
        {
            Assert.True(failure.Errors.Cast<SqlError>().Any(e => e.Number == number),
                "Expected error " + number + " but received: " + string.Join("; ", failure.Errors.Cast<SqlError>().Select(e => e.Number + " " + e.Message)));
        }

        // ---- running the scripts the way a person edits and runs them ----

        private List<DataTable> RunScript(string script)
        {
            _messages.Clear();
            return SqlScript.Run(_database.ServerConnectionString, script, _messages.Add);
        }

        private string BackupScript(string database = Live, bool compress = false, string folder = null)
        {
            string script = DatabaseFixture.ReadDatabaseScript("backup.sql");
            script = SqlScript.WithDeclaredValue(script, "DatabaseName", database);
            script = SqlScript.WithDeclaredValue(script, "BackupFolder", folder ?? BackupFolder());
            return SqlScript.WithDeclaredFlag(script, "Compress", compress);
        }

        private List<DataTable> RunBackup(string database = Live, bool compress = false, string folder = null)
        {
            return RunScript(BackupScript(database, compress, folder));
        }

        /// <summary>Backs up the live database and returns the file the script reports.</summary>
        private string BackUp()
        {
            return FileReportedBy(RunBackup());
        }

        private List<DataTable> RunRestore(string backupFile, string restoreAs, bool replace = false, bool safetyBackup = true)
        {
            string script = DatabaseFixture.ReadDatabaseScript("restore.sql");
            script = SqlScript.WithDeclaredValue(script, "BackupFile", backupFile);
            script = SqlScript.WithDeclaredValue(script, "RestoreAs", restoreAs);
            script = SqlScript.WithDeclaredFlag(script, "ReplaceExisting", replace);
            script = SqlScript.WithDeclaredFlag(script, "SafetyBackup", safetyBackup);
            return RunScript(script);
        }

        private static string FileReportedBy(List<DataTable> backupResults)
        {
            return (string)backupResults.Last().Rows[0]["BackupFile"];
        }

        /// <summary>The path the restore script said it saved the safety copy to.</summary>
        private string SafetyCopyFile()
        {
            return _messages.Single(message => message.StartsWith(SafetyCopyLine)).Substring(SafetyCopyLine.Length).Trim();
        }

        // ---- looking at the server ----

        /// <summary>
        /// A folder the server's own account can write to, as the server sees it (it may be a container, or another machine
        /// than the tests run on, so nothing here may assume the tests can reach the files).
        /// </summary>
        private string BackupFolder()
        {
            return _server.Scalar<string>(
                "SELECT COALESCE(CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(260)), " +
                "CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(260)));");
        }

        /// <summary>A path inside a server folder. The server may run on another operating system than the tests, so its own paths say which separator it uses.</summary>
        private static string InFolder(string serverFolder, string name)
        {
            char separator = serverFolder.Contains("\\") ? '\\' : '/';
            return serverFolder.TrimEnd('\\', '/') + separator + name;
        }

        private static string FolderOf(string serverPath)
        {
            return serverPath.Substring(0, serverPath.LastIndexOfAny(new[] { '\\', '/' }));
        }

        private DataRow HeaderOf(string backupFile)
        {
            return SqlScript.Run(_database.ServerConnectionString, "RESTORE HEADERONLY FROM DISK = N'" + backupFile.Replace("'", "''") + "';")[0].Rows[0];
        }

        /// <summary>RESTORE HEADERONLY reports its yes/no columns as tinyint on some versions and bit on others.</summary>
        private static bool Flag(DataRow header, string column)
        {
            return Convert.ToBoolean(header[column]);
        }

        private void Verify(string backupFile)
        {
            SqlScript.Run(_database.ServerConnectionString, "RESTORE VERIFYONLY FROM DISK = N'" + backupFile.Replace("'", "''") + "' WITH CHECKSUM;");
        }

        private List<string> FilesOf(string database)
        {
            DataTable files = SqlScript.Run(_database.ServerConnectionString,
                "SELECT physical_name FROM sys.master_files WHERE database_id = DB_ID(N'" + database + "') ORDER BY file_id;")[0];
            return files.Rows.Cast<DataRow>().Select(row => (string)row[0]).ToList();
        }

        private string StateOf(string database)
        {
            return Scalar("SELECT state_desc FROM sys.databases WHERE name = @Name;", database);
        }

        private string AccessOf(string database)
        {
            return Scalar("SELECT user_access_desc FROM sys.databases WHERE name = @Name;", database);
        }

        private string Scalar(string sql, string database)
        {
            return _server.Scalar<string>(sql, new SqlParameter("@Name", database));
        }

        private static bool HasRoom(TestData database, string name)
        {
            return database.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_Room WHERE RoomName = @Name;", new SqlParameter("@Name", name)) == 1;
        }

        private string ConnectionStringFor(string database)
        {
            // No pooling: restoring over a database throws its sessions out, and a pooled one would be reused broken.
            return new SqlConnectionStringBuilder(_database.ServerConnectionString) { InitialCatalog = database, Pooling = false }.ConnectionString;
        }

        private void DropScratchDatabases()
        {
            SqlConnection.ClearAllPools();
            foreach (string name in new[] { Live, Copy })
            {
                // An offline database is brought back first: dropping it offline would leave its files behind. (EXEC takes
                // only literals and variables, hence the quoted name is worked out beforehand.)
                _server.Execute(
                    "IF DB_ID(@Name) IS NOT NULL BEGIN " +
                    "DECLARE @Quoted NVARCHAR(300) = QUOTENAME(@Name); " +
                    "DECLARE @State NVARCHAR(60) = (SELECT state_desc FROM sys.databases WHERE name = @Name); " +
                    "IF @State = N'OFFLINE' EXEC (N'ALTER DATABASE ' + @Quoted + N' SET ONLINE'); " +
                    "IF @State IN (N'ONLINE', N'OFFLINE') EXEC (N'ALTER DATABASE ' + @Quoted + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE'); " +
                    "EXEC (N'DROP DATABASE ' + @Quoted); END",
                    new SqlParameter("@Name", name));
            }
        }
    }
}
