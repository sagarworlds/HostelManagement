using System;
using System.Data.SqlClient;
using System.Linq;
using HostelManagement.Utility;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    [Collection(DatabaseCollection.Name)]
    public class RequestRoomRepositoryTests : RepositoryTestBase
    {
        private readonly RequestRoomDB _requests;

        public RequestRoomRepositoryTests(DatabaseFixture database) : base(database)
        {
            _requests = new RequestRoomDB(Log);
        }

        private int RequestOf(int userId)
        {
            return Data.Scalar<int>("SELECT MAX(Id) FROM dbo.tbl_RequestRoom WHERE UserId = @Id;", new SqlParameter("@Id", userId));
        }

        // ---- asking for a room ----

        [Fact]
        public void AddRoomRequest_creates_a_pending_request()
        {
            int student = Data.AddStudent();

            OperationResult result = _requests.AddRoomRequest(new RequestRoom { UserId = student });

            Assert.True(result.Success, result.Message);
            var mine = _requests.GetRoomRequestListByUserId(student);
            Assert.Single(mine);
            Assert.Equal(RequestStatus.Pending, mine[0].Status);
            Assert.Equal(string.Empty, mine[0].PreferredRoomName);
        }

        [Fact]
        public void AddRoomRequest_records_the_preferred_room()
        {
            int student = Data.AddStudent();
            int room = Data.AddRoom("Sea view");

            Assert.True(_requests.AddRoomRequest(new RequestRoom { UserId = student, PreferredRoomId = room }).Success);

            var mine = _requests.GetRoomRequestListByUserId(student);
            Assert.Equal("Sea view", mine[0].PreferredRoomName);
            Assert.Equal(room, _requests.GetRoomRequestById(mine[0].Id).PreferredRoomId);
        }

        [Fact]
        public void AddRoomRequest_refuses_a_second_active_request()
        {
            int student = Data.AddStudent();
            Assert.True(_requests.AddRoomRequest(new RequestRoom { UserId = student }).Success);

            OperationResult second = _requests.AddRoomRequest(new RequestRoom { UserId = student });

            Assert.False(second.Success);
            Assert.Contains("already have", second.Message);
            Assert.Equal(1, Data.Count("dbo.tbl_RequestRoom"));
        }

        [Fact]
        public void AddRoomRequest_refuses_a_student_who_already_holds_a_room()
        {
            int student = Data.AddStudent();
            Data.AllocateRoom(Data.AddRoom(), student);

            Assert.False(_requests.AddRoomRequest(new RequestRoom { UserId = student }).Success);
        }

        [Theory]
        [InlineData(RequestStatus.Rejected)]
        [InlineData(RequestStatus.Cancelled)]
        [InlineData(RequestStatus.Vacated)]
        public void AddRoomRequest_allows_a_new_request_once_the_earlier_one_is_over(string finishedStatus)
        {
            int student = Data.AddStudent();
            Data.AddRequest(student, finishedStatus);

            Assert.True(_requests.AddRoomRequest(new RequestRoom { UserId = student }).Success);
            Assert.Equal(2, Data.Count("dbo.tbl_RequestRoom"));
        }

        [Fact]
        public void AddRoomRequest_reports_a_preferred_room_that_does_not_exist_without_saving_anything()
        {
            int student = Data.AddStudent();

            OperationResult result = _requests.AddRoomRequest(new RequestRoom { UserId = student, PreferredRoomId = 987654 });

            Assert.False(result.Success);
            Assert.Contains("no longer exists", result.Message);
            Assert.Equal(0, Data.Count("dbo.tbl_RequestRoom"));
        }

        [Fact]
        public void AddRoomRequest_reports_an_account_that_does_not_exist()
        {
            OperationResult result = _requests.AddRoomRequest(new RequestRoom { UserId = 987654 });

            Assert.False(result.Success);
            Assert.Equal(0, Data.Count("dbo.tbl_RequestRoom"));
        }

        [Fact]
        public void A_burst_of_identical_requests_from_one_student_creates_exactly_one()
        {
            // One burst rarely overlaps closely enough to show a missing lock, so repeat it; the connection pool is
            // warm after the first round, which makes the threads reach the INSERT at nearly the same moment.
            for (int round = 0; round < 40; round++)
            {
                Database.ClearAllData();
                int student = Data.AddStudent();
                Func<OperationResult>[] clicks = Enumerable.Range(0, 12)
                    .Select(i => (Func<OperationResult>)(() => new RequestRoomDB(Log).AddRoomRequest(new RequestRoom { UserId = student })))
                    .ToArray();

                var results = Concurrently.Run(clicks);

                Assert.Equal(1, Data.Count("dbo.tbl_RequestRoom"));
                Assert.Equal(1, results.Count(r => r.Success));
                AssertNothingLogged();
            }
        }

        // ---- cancelling ----

        [Fact]
        public void CancelRequest_lets_a_student_cancel_their_own_pending_request()
        {
            int student = Data.AddStudent();
            int request = Data.AddRequest(student);

            Assert.True(_requests.CancelRequest(request, student).Success);
            Assert.Equal(RequestStatus.Cancelled, Data.StatusOf(request));
        }

        [Fact]
        public void CancelRequest_refuses_someone_elses_request_and_requests_that_are_not_pending()
        {
            int owner = Data.AddStudent();
            int stranger = Data.AddStudent();
            int pending = Data.AddRequest(owner);
            int rejected = Data.AddRequest(Data.AddStudent(), RequestStatus.Rejected);

            Assert.False(_requests.CancelRequest(pending, stranger).Success);
            Assert.Equal(RequestStatus.Pending, Data.StatusOf(pending));
            Assert.False(_requests.CancelRequest(rejected, Data.Scalar<int>("SELECT UserId FROM dbo.tbl_RequestRoom WHERE Id = @Id;", new SqlParameter("@Id", rejected))).Success);
            Assert.False(_requests.CancelRequest(987654, owner).Success);
        }

        // ---- approving ----

        [Fact]
        public void ApproveRequest_allocates_the_room_and_records_who_and_when()
        {
            int student = Data.AddStudent(null, "Sam", "Student");
            int admin = Data.AddAdmin();
            int room = Data.AddRoom("A-1");
            int request = Data.AddRequest(student);

            OperationResult result = _requests.ApproveRequest(request, room, admin);

            Assert.True(result.Success, result.Message);
            Assert.Equal(RequestStatus.Approved, Data.StatusOf(request));
            Assert.False(Data.IsAvailable(room));
            RequestRoom stored = _requests.GetRoomRequestById(request);
            Assert.Equal("A-1", stored.RoomName);
            Assert.Equal("Sam Student", stored.UserName);
            Assert.Equal(admin, Data.Scalar<int>("SELECT ModifiedBy FROM dbo.tbl_RequestRoom WHERE Id = @Id;", new SqlParameter("@Id", request)));
            Assert.Equal(1, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE Id = @Id AND ResponseDate IS NOT NULL;", new SqlParameter("@Id", request)));
        }

        [Fact]
        public void ApproveRequest_twice_fails_the_second_time_without_changing_anything()
        {
            int request = Data.AddRequest(Data.AddStudent());
            int first = Data.AddRoom("First");
            int second = Data.AddRoom("Second");
            Assert.True(_requests.ApproveRequest(request, first, 1).Success);

            OperationResult again = _requests.ApproveRequest(request, second, 1);

            Assert.False(again.Success);
            Assert.Contains("already been processed", again.Message);
            Assert.True(Data.IsAvailable(second));
            Assert.Equal(first, Data.Scalar<int>("SELECT ApprovedRoomId FROM dbo.tbl_RequestRoom WHERE Id = @Id;", new SqlParameter("@Id", request)));
        }

        [Fact]
        public void ApproveRequest_into_a_taken_room_fails_and_leaves_the_request_pending()
        {
            int holder = Data.AddStudent();
            int waiting = Data.AddStudent();
            int room = Data.AddRoom("Taken");
            Data.AllocateRoom(room, holder);
            int request = Data.AddRequest(waiting);

            OperationResult result = _requests.ApproveRequest(request, room, 1);

            Assert.False(result.Success);
            Assert.Contains("no longer available", result.Message);
            Assert.Equal(RequestStatus.Pending, Data.StatusOf(request)); // the first update was rolled back
        }

        [Fact]
        public void ApproveRequest_for_a_room_that_does_not_exist_fails_and_leaves_the_request_pending()
        {
            int request = Data.AddRequest(Data.AddStudent());

            OperationResult result = _requests.ApproveRequest(request, 987654, 1);

            Assert.False(result.Success);
            Assert.Contains("does not exist", result.Message);
            Assert.Equal(RequestStatus.Pending, Data.StatusOf(request));
        }

        [Fact]
        public void ApproveRequest_refuses_requests_that_are_no_longer_pending()
        {
            int room = Data.AddRoom();
            int rejected = Data.AddRequest(Data.AddStudent(), RequestStatus.Rejected);
            int cancelled = Data.AddRequest(Data.AddStudent(), RequestStatus.Cancelled);

            Assert.False(_requests.ApproveRequest(rejected, room, 1).Success);
            Assert.False(_requests.ApproveRequest(cancelled, room, 1).Success);
            Assert.True(Data.IsAvailable(room));
        }

        // ---- rejecting ----

        [Fact]
        public void RejectRequest_rejects_a_pending_request_once()
        {
            int request = Data.AddRequest(Data.AddStudent());

            Assert.True(_requests.RejectRequest(request, 1).Success);
            Assert.Equal(RequestStatus.Rejected, Data.StatusOf(request));
            Assert.False(_requests.RejectRequest(request, 1).Success);
            Assert.False(_requests.RejectRequest(987654, 1).Success);
        }

        // ---- vacating ----

        [Fact]
        public void VacateRoom_ends_the_allocation_and_frees_the_room_but_keeps_the_history()
        {
            int student = Data.AddStudent();
            int room = Data.AddRoom("Leaving");
            int request = Data.AllocateRoom(room, student);

            OperationResult result = _requests.VacateRoom(request, 1);

            Assert.True(result.Success, result.Message);
            Assert.Equal(RequestStatus.Vacated, Data.StatusOf(request));
            Assert.True(Data.IsAvailable(room));
            Assert.Equal(room, Data.Scalar<int>("SELECT ApprovedRoomId FROM dbo.tbl_RequestRoom WHERE Id = @Id;", new SqlParameter("@Id", request)));
        }

        [Fact]
        public void VacateRoom_refuses_requests_that_hold_no_room()
        {
            int pending = Data.AddRequest(Data.AddStudent());
            int room = Data.AddRoom("Still free");
            int vacated = Data.AddRequest(Data.AddStudent(), RequestStatus.Vacated, room);

            Assert.False(_requests.VacateRoom(pending, 1).Success);
            Assert.False(_requests.VacateRoom(vacated, 1).Success);
            Assert.False(_requests.VacateRoom(987654, 1).Success);
        }

        [Fact]
        public void A_full_life_cycle_request_approve_vacate_then_the_room_goes_to_someone_else()
        {
            int ann = Data.AddStudent();
            int bob = Data.AddStudent();
            int admin = Data.AddAdmin();
            int room = Data.AddRoom("Only room");

            Assert.True(_requests.AddRoomRequest(new RequestRoom { UserId = ann }).Success);
            Assert.True(_requests.ApproveRequest(RequestOf(ann), room, admin).Success);
            Assert.True(_requests.AddRoomRequest(new RequestRoom { UserId = bob }).Success);
            Assert.False(_requests.ApproveRequest(RequestOf(bob), room, admin).Success); // taken
            Assert.True(_requests.VacateRoom(RequestOf(ann), admin).Success);
            Assert.True(_requests.ApproveRequest(RequestOf(bob), room, admin).Success);  // free again
            Assert.True(_requests.AddRoomRequest(new RequestRoom { UserId = ann }).Success); // Ann may ask again

            Assert.Equal(new[] { RequestStatus.Pending, RequestStatus.Vacated },
                _requests.GetRoomRequestListByUserId(ann).Select(r => r.Status).ToArray()); // newest first
        }

        // ---- reading ----

        [Fact]
        public void GetRoomRequestListByUserId_returns_only_that_students_requests_newest_first()
        {
            int ann = Data.AddStudent();
            int bob = Data.AddStudent();
            int oldest = Data.AddRequest(ann, RequestStatus.Rejected);
            int newest = Data.AddRequest(ann, RequestStatus.Pending);
            Data.AddRequest(bob);

            var mine = _requests.GetRoomRequestListByUserId(ann);

            Assert.Equal(new[] { newest, oldest }, mine.Select(r => r.Id).ToArray());
        }

        [Fact]
        public void GetRoomRequestList_puts_pending_first_then_newest_and_can_filter_by_status()
        {
            int a = Data.AddStudent(null, "Ann", "A");
            int b = Data.AddStudent(null, "Bob", "B");
            int c = Data.AddStudent(null, "Cy", "C");
            int rejected = Data.AddRequest(a, RequestStatus.Rejected);
            int pendingOld = Data.AddRequest(b, RequestStatus.Pending);
            int vacated = Data.AddRequest(a, RequestStatus.Vacated);
            int pendingNew = Data.AddRequest(c, RequestStatus.Pending);

            Assert.Equal(new[] { pendingNew, pendingOld, vacated, rejected },
                _requests.GetRoomRequestList(null).Select(r => r.Id).ToArray());
            Assert.Equal(new[] { pendingNew, pendingOld },
                _requests.GetRoomRequestList(RequestStatus.Pending).Select(r => r.Id).ToArray());
            Assert.Equal(new[] { rejected }, _requests.GetRoomRequestList(RequestStatus.Rejected).Select(r => r.Id).ToArray());
            Assert.Equal(4, _requests.GetRoomRequestList(string.Empty).Count);
            Assert.Equal("Cy C", _requests.GetRoomRequestList(null)[0].UserName);
        }

        [Fact]
        public void GetRoomRequestById_returns_null_for_an_unknown_request()
        {
            Assert.Null(_requests.GetRoomRequestById(987654));
        }

        // ---- two administrators at once ----

        [Fact]
        public void Two_administrators_approving_different_requests_into_one_room_allocate_it_once()
        {
            for (int round = 0; round < 15; round++)
            {
                Database.ClearAllData();
                int room = Data.AddRoom("Contested");
                int first = Data.AddRequest(Data.AddStudent());
                int second = Data.AddRequest(Data.AddStudent());

                var results = Concurrently.Run(
                    () => new RequestRoomDB(Log).ApproveRequest(first, room, 1),
                    () => new RequestRoomDB(Log).ApproveRequest(second, room, 2));

                Assert.Equal(1, results.Count(r => r.Success));
                Assert.Equal(1, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE [Status] = N'Approved';"));
                Assert.Equal(1, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE [Status] = N'Pending';"));
                Assert.False(Data.IsAvailable(room));
                AssertNothingLogged();
            }
        }

        [Fact]
        public void Two_administrators_approving_one_request_into_different_rooms_take_only_one_room()
        {
            for (int round = 0; round < 15; round++)
            {
                Database.ClearAllData();
                int roomA = Data.AddRoom("A");
                int roomB = Data.AddRoom("B");
                int request = Data.AddRequest(Data.AddStudent());

                var results = Concurrently.Run(
                    () => new RequestRoomDB(Log).ApproveRequest(request, roomA, 1),
                    () => new RequestRoomDB(Log).ApproveRequest(request, roomB, 2));

                Assert.Equal(1, results.Count(r => r.Success));
                Assert.Equal(1, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_Room WHERE Availability = 0;"));
                AssertNothingLogged();
            }
        }

        [Fact]
        public void An_approval_racing_a_rejection_of_the_same_request_ends_in_one_consistent_state()
        {
            for (int round = 0; round < 15; round++)
            {
                Database.ClearAllData();
                int room = Data.AddRoom("Contested");
                int request = Data.AddRequest(Data.AddStudent());

                var results = Concurrently.Run(
                    () => new RequestRoomDB(Log).ApproveRequest(request, room, 1),
                    () => new RequestRoomDB(Log).RejectRequest(request, 2));

                Assert.Equal(1, results.Count(r => r.Success));
                string status = Data.StatusOf(request);
                Assert.Equal(status == RequestStatus.Approved, !Data.IsAvailable(room)); // room is held exactly when approved
                AssertNothingLogged();
            }
        }

        [Fact]
        public void Two_administrators_vacating_the_same_room_free_it_once()
        {
            for (int round = 0; round < 10; round++)
            {
                Database.ClearAllData();
                int room = Data.AddRoom("Leaving");
                int request = Data.AllocateRoom(room, Data.AddStudent());

                var results = Concurrently.Run(
                    () => new RequestRoomDB(Log).VacateRoom(request, 1),
                    () => new RequestRoomDB(Log).VacateRoom(request, 2));

                Assert.Equal(1, results.Count(r => r.Success));
                Assert.True(Data.IsAvailable(room));
                AssertNothingLogged();
            }
        }

        [Fact]
        public void Deleting_a_room_while_it_is_being_approved_never_leaves_an_approval_without_a_room()
        {
            for (int round = 0; round < 15; round++)
            {
                Database.ClearAllData();
                int room = Data.AddRoom("Contested");
                int request = Data.AddRequest(Data.AddStudent());

                Concurrently.Run(
                    () => new RequestRoomDB(Log).ApproveRequest(request, room, 1).Success,
                    () => new RoomDB(Log).DeleteRoom(room).Success);

                int approvedWithoutRoom = Data.Scalar<int>(
                    "SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE [Status] = N'Approved' AND ApprovedRoomId IS NULL;");
                Assert.Equal(0, approvedWithoutRoom);
                AssertNothingLogged();
            }
        }

        [Fact]
        public void Asking_for_a_room_while_it_is_being_deleted_ends_consistently()
        {
            // The window is narrow (a request landing between the delete's two statements), so this one gets more rounds.
            for (int round = 0; round < 30; round++)
            {
                Database.ClearAllData();
                int room = Data.AddRoom("Going");
                int student = Data.AddStudent();

                Concurrently.Run(
                    () => new RequestRoomDB(Log).AddRoomRequest(new RequestRoom { UserId = student, PreferredRoomId = room }).Success,
                    () => new RoomDB(Log).DeleteRoom(room).Success);

                Assert.Equal(0, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_Room;"));
                Assert.Equal(0, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE PreferredRoomId IS NOT NULL;"));
                AssertNothingLogged();
            }
        }

        [Fact]
        public void Vacating_a_room_while_it_is_being_deleted_always_completes_the_vacate()
        {
            for (int round = 0; round < 15; round++)
            {
                Database.ClearAllData();
                int room = Data.AddRoom("Leaving");
                int request = Data.AllocateRoom(room, Data.AddStudent());

                Concurrently.Run(
                    () => new RequestRoomDB(Log).VacateRoom(request, 1).Success,
                    () => new RoomDB(Log).DeleteRoom(room).Success);

                Assert.Equal(RequestStatus.Vacated, Data.StatusOf(request));
                AssertNothingLogged();
            }
        }

        [Fact]
        public void Deleting_a_student_while_their_request_is_being_approved_leaves_no_trace_and_a_free_room()
        {
            for (int round = 0; round < 15; round++)
            {
                Database.ClearAllData();
                int room = Data.AddRoom("Contested");
                int student = Data.AddStudent();
                int request = Data.AddRequest(student);

                Concurrently.Run(
                    () => new RequestRoomDB(Log).ApproveRequest(request, room, 1).Success,
                    () => new UserDB(Log).DeleteStudent(student).Success);

                Assert.Equal(0, Data.Count("dbo.tbl_User"));
                Assert.Equal(0, Data.Count("dbo.tbl_RequestRoom"));
                Assert.True(Data.IsAvailable(room));
                AssertNothingLogged();
            }
        }
    }
}
