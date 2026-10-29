using System.Data.SqlClient;
using System.Linq;
using HostelManagement.Utility;
using Xunit;

namespace HostelManagement.IntegrationTests
{
    [Collection(DatabaseCollection.Name)]
    public class RoomRepositoryTests : RepositoryTestBase
    {
        private readonly RoomDB _rooms;

        public RoomRepositoryTests(DatabaseFixture database) : base(database)
        {
            _rooms = new RoomDB(Log);
        }

        private int IdOf(string roomName)
        {
            return Data.Scalar<int>("SELECT Id FROM dbo.tbl_Room WHERE RoomName = @Name;", new SqlParameter("@Name", roomName));
        }

        [Fact]
        public void AddRoom_creates_a_room_that_is_available()
        {
            OperationResult result = _rooms.AddRoom(new Room { RoomName = "A-101", Rent = 450, Description = "Sea view" });

            Assert.True(result.Success, result.Message);
            Room stored = _rooms.GetRoomById(IdOf("A-101"));
            Assert.Equal(450, stored.Rent);
            Assert.Equal("Sea view", stored.Description);
            Assert.True(stored.Availability);
        }

        [Fact]
        public void AddRoom_accepts_a_missing_description()
        {
            Assert.True(_rooms.AddRoom(new Room { RoomName = "A-102", Rent = 100, Description = null }).Success);
            Assert.Equal(string.Empty, _rooms.GetRoomById(IdOf("A-102")).Description);
        }

        [Fact]
        public void AddRoom_refuses_a_negative_rent_and_logs_why()
        {
            OperationResult result = _rooms.AddRoom(new Room { RoomName = "Bad", Rent = -5 });

            Assert.False(result.Success);
            Assert.DoesNotContain("CK_", result.Message); // no database detail reaches the user
            Assert.Equal(0, Data.Count("dbo.tbl_Room"));
            Assert.Single(Log.Errors);
        }

        [Fact]
        public void UpdateRoom_changes_name_rent_and_description_but_not_availability()
        {
            int id = Data.AddRoom("Old", 100, available: false);

            OperationResult result = _rooms.UpdateRoom(new Room { Id = id, RoomName = "New", Rent = 250, Description = "Repainted", ModifiedBy = 7 });

            Assert.True(result.Success, result.Message);
            Room stored = _rooms.GetRoomById(id);
            Assert.Equal("New", stored.RoomName);
            Assert.Equal(250, stored.Rent);
            Assert.Equal("Repainted", stored.Description);
            Assert.False(stored.Availability);
        }

        [Fact]
        public void UpdateRoom_reports_a_room_that_no_longer_exists()
        {
            Assert.False(_rooms.UpdateRoom(new Room { Id = 987654, RoomName = "x", Rent = 1 }).Success);
        }

        [Fact]
        public void DeleteRoom_removes_a_free_room_and_reports_unknown_ones()
        {
            int id = Data.AddRoom("Free");

            Assert.True(_rooms.DeleteRoom(id).Success);
            Assert.Null(_rooms.GetRoomById(id));

            OperationResult again = _rooms.DeleteRoom(id);
            Assert.False(again.Success);
            Assert.Contains("no longer exists", again.Message);
        }

        [Fact]
        public void DeleteRoom_is_refused_while_a_student_holds_the_room()
        {
            int student = Data.AddStudent();
            int room = Data.AddRoom("Held");
            int request = Data.AllocateRoom(room, student);

            OperationResult result = _rooms.DeleteRoom(room);

            Assert.False(result.Success);
            Assert.Contains("Vacate", result.Message);
            Assert.NotNull(_rooms.GetRoomById(room));
            Assert.Equal(RequestStatus.Approved, Data.StatusOf(request));
        }

        [Fact]
        public void DeleteRoom_clears_the_preference_of_students_who_asked_for_it_but_keeps_their_requests()
        {
            int student = Data.AddStudent();
            int room = Data.AddRoom("Wanted");
            int request = Data.AddRequest(student, RequestStatus.Pending, preferredRoomId: room);

            Assert.True(_rooms.DeleteRoom(room).Success);

            Assert.Equal(RequestStatus.Pending, Data.StatusOf(request));
            Assert.Equal(0, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE PreferredRoomId IS NOT NULL;"));
        }

        [Fact]
        public void A_refused_DeleteRoom_leaves_other_students_preferences_untouched()
        {
            int holder = Data.AddStudent();
            int waiting = Data.AddStudent();
            int room = Data.AddRoom("Held");
            Data.AllocateRoom(room, holder);
            int waitingRequest = Data.AddRequest(waiting, RequestStatus.Pending, preferredRoomId: room);

            Assert.False(_rooms.DeleteRoom(room).Success);

            // The preference was cleared inside the transaction that the refusal rolled back.
            Assert.Equal(room, Data.Scalar<int>("SELECT PreferredRoomId FROM dbo.tbl_RequestRoom WHERE Id = @Id;", new SqlParameter("@Id", waitingRequest)));
        }

        [Fact]
        public void DeleteRoom_keeps_the_history_of_students_who_have_left_it()
        {
            int student = Data.AddStudent();
            int room = Data.AddRoom("Past");
            int request = Data.AddRequest(student, RequestStatus.Vacated, approvedRoomId: room);

            Assert.True(_rooms.DeleteRoom(room).Success);

            Assert.Equal(RequestStatus.Vacated, Data.StatusOf(request));
            Assert.Equal(0, Data.Scalar<int>("SELECT COUNT(1) FROM dbo.tbl_RequestRoom WHERE ApprovedRoomId IS NOT NULL;"));
        }

        [Fact]
        public void GetRoomList_shows_each_room_once_with_its_current_occupant_only()
        {
            int ann = Data.AddStudent(null, "Ann", "Occupant");
            int bob = Data.AddStudent(null, "Bob", "Previous");
            int held = Data.AddRoom("B-2");
            Data.AddRoom("A-1");
            Data.AddRequest(bob, RequestStatus.Vacated, approvedRoomId: held);
            Data.AddRequest(bob, RequestStatus.Vacated, approvedRoomId: held);
            int active = Data.AllocateRoom(held, ann);

            var rooms = _rooms.GetRoomList();

            Assert.Equal(new[] { "A-1", "B-2" }, rooms.Select(r => r.RoomName).ToArray());
            Assert.Equal("Ann Occupant", rooms[1].OccupantName);
            Assert.Equal(active, rooms[1].ActiveRequestId);
            Assert.False(rooms[1].Availability);
            Assert.True(rooms[0].Availability);
            Assert.Equal(0, rooms[0].ActiveRequestId);
        }

        [Fact]
        public void GetAvailableRooms_lists_only_free_rooms_ordered_by_name()
        {
            Data.AddRoom("C-3");
            Data.AddRoom("A-1");
            Data.AddRoom("B-2", available: false);

            Assert.Equal(new[] { "A-1", "C-3" }, _rooms.GetAvailableRooms().Select(r => r.RoomName).ToArray());
        }
    }
}
