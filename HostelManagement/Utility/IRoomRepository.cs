using System.Collections.Generic;

namespace HostelManagement
{
    /// <summary>Persistence of hostel rooms.</summary>
    /// <remarks>
    /// Reads throw <see cref="DataAccessException"/> when the database fails; writes return an
    /// <see cref="OperationResult"/> with a user-safe message.
    /// Allocating and releasing rooms is part of the request workflow (<c>IRequestRoomRepository</c>).
    /// </remarks>
    public interface IRoomRepository
    {
        /// <summary>Creates a room that is immediately available for allocation.</summary>
        OperationResult AddRoom(Room room);

        /// <summary>Updates name, rent and description; availability is changed only by the request workflow.</summary>
        OperationResult UpdateRoom(Room room);

        /// <summary>Deletes a room. Refused while a student holds it.</summary>
        OperationResult DeleteRoom(int id);

        /// <summary>Lists every room with its availability and current occupant, ordered by name.</summary>
        /// <exception cref="DataAccessException">The database failed.</exception>
        List<Room> GetRoomList();

        /// <returns>The room, or null when it does not exist.</returns>
        /// <exception cref="DataAccessException">The database failed.</exception>
        Room GetRoomById(int id);

        /// <summary>Lists the rooms that can be allocated now, ordered by name.</summary>
        /// <exception cref="DataAccessException">The database failed.</exception>
        List<Room> GetAvailableRooms();
    }
}
