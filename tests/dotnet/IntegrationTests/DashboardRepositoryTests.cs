using HostelManagement.Utility;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    [Collection(DatabaseCollection.Name)]
    public class DashboardRepositoryTests : RepositoryTestBase
    {
        private readonly DashboardDB _dashboard;

        public DashboardRepositoryTests(DatabaseFixture database) : base(database)
        {
            _dashboard = new DashboardDB(Log);
        }

        [Fact]
        public void An_empty_hostel_reports_zero_everywhere()
        {
            DashboardSummary summary = _dashboard.GetSummary();

            Assert.Equal(0, summary.TotalRooms);
            Assert.Equal(0, summary.AvailableRooms);
            Assert.Equal(0, summary.OccupiedRooms);
            Assert.Equal(0, summary.PendingRequests);
            Assert.Equal(0, summary.Students);
            Assert.Equal(0, summary.Admins);
        }

        [Fact]
        public void The_counts_follow_rooms_requests_and_accounts()
        {
            Data.AddAdmin();
            int ann = Data.AddStudent();
            int bob = Data.AddStudent();
            int cy = Data.AddStudent();
            Data.AddStudent();

            int taken = Data.AddRoom("Taken");
            Data.AddRoom("Free 1");
            Data.AddRoom("Free 2");
            Data.AllocateRoom(taken, ann);
            Data.AddRequest(bob, RequestStatus.Vacated, taken);   // history does not count as occupied
            Data.AddRequest(bob, RequestStatus.Rejected);
            Data.AddRequest(bob, RequestStatus.Pending);
            Data.AddRequest(cy, RequestStatus.Pending);

            DashboardSummary summary = _dashboard.GetSummary();

            Assert.Equal(3, summary.TotalRooms);
            Assert.Equal(2, summary.AvailableRooms);
            Assert.Equal(1, summary.OccupiedRooms);
            Assert.Equal(2, summary.PendingRequests);
            Assert.Equal(4, summary.Students);
            Assert.Equal(1, summary.Admins);
        }
    }
}
