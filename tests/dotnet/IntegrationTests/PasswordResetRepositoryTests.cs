using System;
using System.Data.SqlClient;
using System.Linq;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    [Collection(DatabaseCollection.Name)]
    public class PasswordResetRepositoryTests : RepositoryTestBase
    {
        private static readonly DateTime Noon = new DateTime(2030, 1, 15, 12, 0, 0);

        private readonly PasswordResetDB _resets;

        public PasswordResetRepositoryTests(DatabaseFixture database) : base(database)
        {
            _resets = new PasswordResetDB(Log);
        }

        private static string HashOf(string token)
        {
            return SecureToken.Sha256Hex(token);
        }

        private string StoredPassword(int userId)
        {
            return Data.Scalar<string>("SELECT [Password] FROM dbo.tbl_User WHERE Id = @Id;", new SqlParameter("@Id", userId));
        }

        private OperationResult CreateLink(int userId, string token, DateTime? createdOn = null, TimeSpan? lifetime = null)
        {
            DateTime created = createdOn ?? Noon;
            return _resets.CreateToken(userId, HashOf(token), created, created + (lifetime ?? TimeSpan.FromHours(1)));
        }

        [Fact]
        public void A_new_link_is_valid_until_it_expires()
        {
            int user = Data.AddStudent();

            Assert.True(CreateLink(user, "t1").Success);

            Assert.True(_resets.IsTokenValid(HashOf("t1"), Noon.AddMinutes(30)));
            Assert.False(_resets.IsTokenValid(HashOf("t1"), Noon.AddHours(1)));   // expiry moment itself is already too late
            Assert.False(_resets.IsTokenValid(HashOf("t1"), Noon.AddHours(2)));
            Assert.False(_resets.IsTokenValid(HashOf("never issued"), Noon.AddMinutes(30)));
        }

        [Fact]
        public void Only_the_hash_of_the_token_is_stored()
        {
            int user = Data.AddStudent();

            CreateLink(user, "the-secret-token");

            Assert.Equal(HashOf("the-secret-token"), Data.Scalar<string>("SELECT TokenHash FROM dbo.tbl_PasswordReset;").Trim());
        }

        [Fact]
        public void Asking_for_a_new_link_retires_the_earlier_one()
        {
            int user = Data.AddStudent();
            int other = Data.AddStudent();
            CreateLink(user, "old");
            CreateLink(other, "someone else's");

            CreateLink(user, "new", Noon.AddMinutes(5));

            Assert.False(_resets.IsTokenValid(HashOf("old"), Noon.AddMinutes(10)));
            Assert.True(_resets.IsTokenValid(HashOf("new"), Noon.AddMinutes(10)));
            Assert.True(_resets.IsTokenValid(HashOf("someone else's"), Noon.AddMinutes(10)));
        }

        [Fact]
        public void CreateToken_for_a_missing_account_fails_without_a_database_message_and_is_logged()
        {
            OperationResult result = CreateLink(987654, "orphan");

            Assert.False(result.Success);
            Assert.Equal(0, Data.Count("dbo.tbl_PasswordReset"));
            Assert.Single(Log.Errors);
        }

        [Fact]
        public void The_same_token_cannot_be_issued_twice()
        {
            int user = Data.AddStudent();
            int other = Data.AddStudent();
            Assert.True(CreateLink(user, "same").Success);

            Assert.False(CreateLink(other, "same").Success);
            Assert.Equal(1, Data.Count("dbo.tbl_PasswordReset"));
        }

        [Fact]
        public void CompleteReset_sets_the_password_clears_the_flag_and_spends_the_link()
        {
            int user = Data.AddUser(UserTypes.Student, mustChangePassword: true);
            CreateLink(user, "t1");

            OperationResult result = _resets.CompleteReset(HashOf("t1"), "v2$1$hash-one", Noon.AddMinutes(10));

            Assert.True(result.Success, result.Message);
            Assert.Equal("v2$1$hash-one", StoredPassword(user));
            Assert.False(Data.Scalar<bool>("SELECT MustChangePassword FROM dbo.tbl_User WHERE Id = @Id;", new SqlParameter("@Id", user)));
            Assert.False(_resets.IsTokenValid(HashOf("t1"), Noon.AddMinutes(11)));
        }

        [Fact]
        public void A_link_works_only_once()
        {
            int user = Data.AddStudent();
            CreateLink(user, "t1");
            Assert.True(_resets.CompleteReset(HashOf("t1"), "hash-one", Noon.AddMinutes(1)).Success);

            OperationResult second = _resets.CompleteReset(HashOf("t1"), "hash-two", Noon.AddMinutes(2));

            Assert.False(second.Success);
            Assert.Equal(PasswordResetService.InvalidLinkMessage, second.Message);
            Assert.Equal("hash-one", StoredPassword(user));
        }

        [Fact]
        public void An_expired_or_unknown_link_changes_nothing()
        {
            int user = Data.AddStudent();
            string before = StoredPassword(user);
            CreateLink(user, "t1");

            Assert.False(_resets.CompleteReset(HashOf("t1"), "hash-late", Noon.AddHours(2)).Success);
            Assert.False(_resets.CompleteReset(HashOf("unknown"), "hash-unknown", Noon.AddMinutes(1)).Success);

            Assert.Equal(before, StoredPassword(user));
        }

        [Fact]
        public void Completing_a_reset_spends_every_other_unused_link_of_that_user()
        {
            int user = Data.AddStudent();
            CreateLink(user, "first");
            // A second unused link can only exist through direct access, e.g. a restored backup.
            Data.Execute("INSERT INTO dbo.tbl_PasswordReset (UserId, TokenHash, CreatedOn, ExpiresOn) VALUES (@Id, @Hash, @Created, @Expires);",
                new SqlParameter("@Id", user), new SqlParameter("@Hash", HashOf("second")),
                new SqlParameter("@Created", Noon), new SqlParameter("@Expires", Noon.AddHours(1)));

            Assert.True(_resets.CompleteReset(HashOf("first"), "hash-one", Noon.AddMinutes(1)).Success);

            Assert.False(_resets.IsTokenValid(HashOf("second"), Noon.AddMinutes(2)));
        }

        [Fact]
        public void Several_link_requests_for_one_account_at_once_all_succeed_and_none_is_lost()
        {
            for (int round = 0; round < 20; round++)
            {
                Database.ClearAllData();
                int user = Data.AddStudent();

                Func<OperationResult>[] requests = Enumerable.Range(0, 6)
                    .Select(i => (Func<OperationResult>)(() => new PasswordResetDB(Log).CreateToken(user, HashOf("burst-" + i), Noon, Noon.AddHours(1))))
                    .ToArray();
                var results = Concurrently.Run(requests);

                Assert.All(results, r => Assert.True(r.Success, r.Message));
                Assert.Equal(6, Data.Count("dbo.tbl_PasswordReset"));
                AssertNothingLogged();
            }
        }

        [Fact]
        public void A_link_used_by_several_people_at_once_resets_the_password_exactly_once()
        {
            for (int round = 0; round < 10; round++)
            {
                Database.ClearAllData();
                int user = Data.AddStudent();
                CreateLink(user, "race");

                Func<OperationResult>[] attempts = Enumerable.Range(0, 8)
                    .Select(i => (Func<OperationResult>)(() => new PasswordResetDB(Log).CompleteReset(HashOf("race"), "hash-" + i, Noon.AddMinutes(1))))
                    .ToArray();
                var results = Concurrently.Run(attempts);

                Assert.Equal(1, results.Count(r => r.Success));
                int winner = results.IndexOf(results.Single(r => r.Success));
                Assert.Equal("hash-" + winner, StoredPassword(user));
                AssertNothingLogged();
            }
        }
    }
}
