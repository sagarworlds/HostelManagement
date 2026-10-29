using System;
using HostelManagement;
using Xunit;

namespace HostelManagement.UnitTests
{
    public class AnnouncementTests
    {
        [Fact]
        public void No_last_day_means_it_never_expires()
        {
            Assert.Null(Announcement.ExpiryAfterLastDay(null));
            Assert.True(new Announcement { ExpiresOn = null }.IsActive(DateTime.MaxValue.AddDays(-1)));
        }

        [Fact]
        public void The_last_day_is_shown_all_day_so_it_ends_at_the_next_midnight()
        {
            DateTime? expiry = Announcement.ExpiryAfterLastDay(new DateTime(2026, 11, 5, 14, 30, 0));

            Assert.Equal(new DateTime(2026, 11, 6, 0, 0, 0), expiry);
        }

        [Fact]
        public void An_announcement_is_active_up_to_but_not_including_its_expiry_moment()
        {
            var notice = new Announcement { ExpiresOn = new DateTime(2026, 11, 6) };

            Assert.True(notice.IsActive(new DateTime(2026, 11, 5, 23, 59, 59)));
            Assert.False(notice.IsActive(new DateTime(2026, 11, 6)));
            Assert.False(notice.IsActive(new DateTime(2026, 11, 7)));
        }

        [Fact]
        public void LastDay_is_the_day_the_administrator_picked()
        {
            var notice = new Announcement { ExpiresOn = Announcement.ExpiryAfterLastDay(new DateTime(2026, 11, 5)) };

            Assert.Equal(new DateTime(2026, 11, 5), notice.LastDay);
            Assert.Null(new Announcement().LastDay);
        }

        [Fact]
        public void The_status_text_says_when_it_ends_or_that_it_already_has()
        {
            Assert.Equal("Shown until removed", new Announcement().StatusText);
            Assert.StartsWith("Shown until", new Announcement { ExpiresOn = DateTime.Now.AddDays(30) }.StatusText);
            Assert.StartsWith("Ended", new Announcement { ExpiresOn = DateTime.Now.AddDays(-30) }.StatusText);
        }
    }

    public class HtmlTextTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("plain text", "plain text")]
        public void Plain_text_passes_through(string input, string expected)
        {
            Assert.Equal(expected, HtmlText.FromPlain(input));
        }

        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("<img src=x onerror=alert(1)>")]
        [InlineData("\"><svg/onload=alert(1)>")]
        [InlineData("<a href=\"javascript:alert(1)\">click</a>")]
        public void Markup_typed_by_a_user_can_not_become_markup(string hostile)
        {
            string html = HtmlText.FromPlain(hostile);

            Assert.DoesNotContain("<", html);
            Assert.DoesNotContain(">", html);
            Assert.DoesNotContain("\"", html);
        }

        [Fact]
        public void Ampersands_and_quotes_are_encoded()
        {
            Assert.Equal("Fish &amp; chips &quot;today&quot;", HtmlText.FromPlain("Fish & chips \"today\""));
        }

        [Fact]
        public void Line_breaks_of_every_kind_become_br_tags_after_encoding()
        {
            Assert.Equal("one<br />two<br />three<br />four", HtmlText.FromPlain("one\r\ntwo\nthree\rfour"));
        }

        [Fact]
        public void A_line_break_does_not_let_markup_through()
        {
            Assert.Equal("&lt;b&gt;bold&lt;/b&gt;<br />next", HtmlText.FromPlain("<b>bold</b>\nnext"));
        }
    }
}
