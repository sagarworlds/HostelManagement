using System;
using System.Data.SqlClient;
using System.Linq;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    [Collection(DatabaseCollection.Name)]
    public class AnnouncementRepositoryTests : RepositoryTestBase
    {
        private static readonly DateTime Noon = new DateTime(2030, 1, 15, 12, 0, 0);

        private readonly AnnouncementDB _announcements;

        public AnnouncementRepositoryTests(DatabaseFixture database) : base(database)
        {
            _announcements = new AnnouncementDB(Log);
        }

        private OperationResult Post(string title, DateTime createdOn, DateTime? expiresOn = null, string body = "Body")
        {
            return _announcements.Add(new Announcement { Title = title, Body = body, CreatedBy = 1, CreatedOn = createdOn, ExpiresOn = expiresOn });
        }

        [Fact]
        public void An_announcement_round_trips_including_a_missing_expiry()
        {
            Assert.True(Post("Open ended", Noon).Success);
            Assert.True(Post("Until Friday", Noon.AddMinutes(1), Noon.AddDays(3)).Success);

            var all = _announcements.GetAll();

            Assert.Equal(new[] { "Until Friday", "Open ended" }, all.Select(a => a.Title).ToArray());
            Assert.Equal(Noon.AddDays(3), all[0].ExpiresOn);
            Assert.Null(all[1].ExpiresOn);
            Assert.Equal(1, all[1].CreatedBy);
            Assert.Equal(Noon, all[1].CreatedOn);
        }

        [Fact]
        public void GetActive_hides_ended_notices_keeps_open_ended_ones_and_lists_newest_first()
        {
            Post("Ended", Noon.AddMinutes(1), Noon.AddHours(-1));
            Post("Ends exactly now", Noon.AddMinutes(2), Noon);
            Post("Open ended", Noon.AddMinutes(3));
            Post("Future", Noon.AddMinutes(4), Noon.AddDays(1));

            var active = _announcements.GetActive(10, Noon);

            Assert.Equal(new[] { "Future", "Open ended" }, active.Select(a => a.Title).ToArray());
        }

        [Fact]
        public void GetActive_returns_no_more_than_the_maximum_keeping_the_newest()
        {
            for (int i = 0; i < 8; i++)
                Post("Notice " + i, Noon.AddMinutes(i));

            var active = _announcements.GetActive(5, Noon);

            Assert.Equal(5, active.Count);
            Assert.Equal("Notice 7", active[0].Title);
            Assert.Equal("Notice 3", active[4].Title);
        }

        [Fact]
        public void Notices_posted_in_the_same_moment_are_listed_newest_posted_first()
        {
            Post("Posted first", Noon);
            Post("Posted second", Noon);

            Assert.Equal(new[] { "Posted second", "Posted first" }, _announcements.GetAll().Select(a => a.Title).ToArray());
        }

        [Fact]
        public void Delete_removes_a_notice_once()
        {
            Post("Gone soon", Noon);
            int id = _announcements.GetAll().Single().Id;

            Assert.True(_announcements.Delete(id).Success);
            Assert.False(_announcements.Delete(id).Success);
            Assert.Empty(_announcements.GetAll());
        }

        [Fact]
        public void The_longest_allowed_title_and_body_fit()
        {
            Assert.True(Post(new string('T', 150), Noon, null, new string('B', 2000)).Success);

            Announcement stored = _announcements.GetAll().Single();
            Assert.Equal(150, stored.Title.Length);
            Assert.Equal(2000, stored.Body.Length);
        }

        [Fact]
        public void Text_that_is_too_long_is_refused_not_silently_cut_short()
        {
            OperationResult tooLongBody = Post("Title", Noon, null, new string('B', 2001));
            OperationResult tooLongTitle = Post(new string('T', 151), Noon);

            Assert.False(tooLongBody.Success);
            Assert.False(tooLongTitle.Success);
            Assert.Equal(0, Data.Count("dbo.tbl_Announcement"));
            Assert.Equal(2, Log.Errors.Count);
        }

        [Fact]
        public void Emoji_and_markup_are_stored_exactly_as_typed()
        {
            const string title = "Fête \U0001F389 <b>tonight</b>";
            const string body = "It's 'quoted'; DROP TABLE dbo.tbl_Announcement;--";

            Assert.True(Post(title, Noon, null, body).Success);

            Announcement stored = _announcements.GetAll().Single();
            Assert.Equal(title, stored.Title);
            Assert.Equal(body, stored.Body);
        }
    }
}
