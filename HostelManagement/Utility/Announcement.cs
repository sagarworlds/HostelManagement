using System;

namespace HostelManagement
{
    /// <summary>A notice an administrator posts for students to see on their dashboard.</summary>
    public class Announcement
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }

        /// <summary>The first moment the notice is no longer shown; null when it never expires.</summary>
        public DateTime? ExpiresOn { get; set; }

        /// <summary>
        /// Converts "show until this day" (inclusive, as an administrator thinks of it) into the exclusive moment the
        /// notice stops being shown: midnight after that day.
        /// </summary>
        public static DateTime? ExpiryAfterLastDay(DateTime? lastDay)
        {
            return lastDay.HasValue ? lastDay.Value.Date.AddDays(1) : (DateTime?)null;
        }

        /// <summary>The last day the notice is shown; null when it never expires.</summary>
        public DateTime? LastDay
        {
            get { return ExpiresOn.HasValue ? ExpiresOn.Value.AddDays(-1).Date : (DateTime?)null; }
        }

        public bool IsActive(DateTime now)
        {
            return !ExpiresOn.HasValue || ExpiresOn.Value > now;
        }

        /// <summary>Text for the administrator's list: when it ends, or that it already has.</summary>
        public string StatusText
        {
            get
            {
                if (!ExpiresOn.HasValue)
                    return "Shown until removed";

                return IsActive(DateTime.Now) ? "Shown until " + LastDay.Value.ToString("dd MMM yyyy") : "Ended " + LastDay.Value.ToString("dd MMM yyyy");
            }
        }
    }
}
