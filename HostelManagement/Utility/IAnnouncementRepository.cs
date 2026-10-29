using System;
using System.Collections.Generic;

namespace HostelManagement
{
    /// <summary>Persistence of announcements. Reads throw <see cref="DataAccessException"/>; writes return an <see cref="OperationResult"/>.</summary>
    public interface IAnnouncementRepository
    {
        OperationResult Add(Announcement announcement);

        OperationResult Delete(int id);

        /// <summary>The newest announcements that have not expired yet, newest first.</summary>
        /// <param name="maximum">How many to return at most.</param>
        /// <exception cref="DataAccessException">The database failed.</exception>
        List<Announcement> GetActive(int maximum, DateTime now);

        /// <summary>Every announcement, newest first, including ended ones (for the administrator's list).</summary>
        /// <exception cref="DataAccessException">The database failed.</exception>
        List<Announcement> GetAll();
    }
}
