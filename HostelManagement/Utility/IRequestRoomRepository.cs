using System.Collections.Generic;

namespace HostelManagement.Utility
{
    /// <summary>Room requests and the allocations they create.</summary>
    /// <remarks>
    /// Lifecycle (see <see cref="RequestStatus"/>): Pending -> Approved | Rejected | Cancelled; Approved -> Vacated.
    /// Every state change is a conditional update, so a request can only move out of the state it is expected
    /// to be in; a second administrator acting on the same request, or on the same room, gets a failure result
    /// instead of silently overwriting the first. Reads throw <see cref="DataAccessException"/> when the
    /// database fails; writes return an <see cref="OperationResult"/> with a user-safe message.
    /// </remarks>
    public interface IRequestRoomRepository
    {
        /// <summary>
        /// Creates a Pending request. A student may hold one pending request or one allocated room at a time;
        /// otherwise the result is a failure.
        /// </summary>
        OperationResult AddRoomRequest(RequestRoom request);

        /// <summary>Cancels the student's own Pending request.</summary>
        OperationResult CancelRequest(int requestId, int userId);

        /// <summary>
        /// Approves a Pending request and allocates the room to it, atomically. Fails, changing nothing, when the
        /// request is no longer Pending or the room is no longer available.
        /// </summary>
        OperationResult ApproveRequest(int requestId, int roomId, int adminId);

        /// <summary>Rejects a Pending request.</summary>
        OperationResult RejectRequest(int requestId, int adminId);

        /// <summary>
        /// Ends an Approved allocation (the student checks out): the request becomes Vacated and the room is
        /// available again, atomically.
        /// </summary>
        /// <param name="requestId">The Approved request that holds the room.</param>
        OperationResult VacateRoom(int requestId, int adminId);

        /// <summary>A student's requests, newest first.</summary>
        /// <exception cref="DataAccessException">The database failed.</exception>
        List<RequestRoom> GetRoomRequestListByUserId(int userId);

        /// <summary>All requests, Pending ones first, then newest first.</summary>
        /// <exception cref="DataAccessException">The database failed.</exception>
        List<RequestRoom> GetRoomRequestList();

        /// <returns>The request including the student's name, or null when it does not exist.</returns>
        /// <exception cref="DataAccessException">The database failed.</exception>
        RequestRoom GetRoomRequestById(int id);
    }
}
