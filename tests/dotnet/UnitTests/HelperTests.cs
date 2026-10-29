using HostelManagement;
using HostelManagement.Utility;
using Xunit;

namespace HostelManagement.UnitTests
{
    public class UserTypesTests
    {
        [Theory]
        [InlineData("Student", "Student")]
        [InlineData("student", "Student")]
        [InlineData("STUDENT", "Student")]
        [InlineData("Admin", "Admin")]
        [InlineData("admin", "Admin")]
        // Anything unknown, including hostile input from a query string, falls back to Admin.
        [InlineData(null, "Admin")]
        [InlineData("", "Admin")]
        [InlineData("Student' OR '1'='1", "Admin")]
        [InlineData("Teacher", "Admin")]
        public void Normalize_maps_untrusted_input_to_a_known_type(string input, string expected)
        {
            Assert.Equal(expected, UserTypes.Normalize(input));
        }
    }

    public class RequestStatusTests
    {
        [Fact]
        public void Lists_the_five_statuses_in_lifecycle_order()
        {
            Assert.Equal(new[] { "Pending", "Approved", "Rejected", "Cancelled", "Vacated" }, RequestStatus.All);
        }

        [Theory]
        [InlineData("Pending", true)]
        [InlineData("Vacated", true)]
        [InlineData("pending", false)]   // statuses are stored with this exact spelling
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("Pending' OR 1=1 --", false)]
        public void IsKnown_accepts_only_stored_values(string status, bool expected)
        {
            Assert.Equal(expected, RequestStatus.IsKnown(status));
        }
    }

    public class OperationResultTests
    {
        [Fact]
        public void Ok_succeeds_with_an_empty_message()
        {
            OperationResult result = OperationResult.Ok();

            Assert.True(result.Success);
            Assert.Equal(string.Empty, result.Message);
        }

        [Fact]
        public void Fail_carries_the_message()
        {
            OperationResult result = OperationResult.Fail("Nope");

            Assert.False(result.Success);
            Assert.Equal("Nope", result.Message);
        }

        [Fact]
        public void Fail_with_null_never_yields_a_null_message()
        {
            Assert.Equal(string.Empty, OperationResult.Fail(null).Message);
        }
    }

    public class RoomStatusTests
    {
        [Fact]
        public void An_allocated_room_names_its_occupant_even_if_the_flag_says_available()
        {
            var room = new Room { Availability = true, ActiveRequestId = 4, OccupantName = "Asha Rao" };

            Assert.True(room.IsOccupied);
            Assert.Equal("Occupied by Asha Rao", room.OccupancyStatus);
        }

        [Fact]
        public void A_free_room_is_available()
        {
            Assert.Equal("Available", new Room { Availability = true }.OccupancyStatus);
        }

        [Fact]
        public void A_room_that_is_neither_free_nor_allocated_is_unavailable()
        {
            var room = new Room { Availability = false };

            Assert.False(room.IsOccupied);
            Assert.Equal("Unavailable", room.OccupancyStatus);
        }
    }
}
