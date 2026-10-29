// Who may open which page. e2e/access.test.js checks every entry, and fails if a page exists that is not listed here,
// so a new page cannot be added without deciding who may see it.
const fs = require('fs');
const path = require('path');
const { repoRoot } = require('./config');

const ANONYMOUS = ['Login.aspx', 'Register.aspx', 'ForgotPassword.aspx', 'ResetPassword.aspx', 'Error.aspx'];
const ADMIN_ONLY = ['adminDashboard.aspx', 'RoomList.aspx', 'AddEditRoom.aspx', 'AdminList.aspx', 'AddEditAdmin.aspx', 'Announcements.aspx', 'ApproveRoomRequest.aspx'];
const STUDENT_ONLY = ['UserDashboard.aspx', 'AvailableRooms.aspx'];
const ANY_SIGNED_IN = ['MyProfile.aspx'];

function pagesOnDisk() {
  return fs.readdirSync(path.join(repoRoot, 'HostelManagement')).filter(f => f.endsWith('.aspx')).sort();
}

module.exports = { ANONYMOUS, ADMIN_ONLY, STUDENT_ONLY, ANY_SIGNED_IN, pagesOnDisk };
