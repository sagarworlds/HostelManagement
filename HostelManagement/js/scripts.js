
window.addEventListener('DOMContentLoaded', event => {

    // Toggle the side navigation
    const sidebarToggle = document.body.querySelector('#sidebarToggle');
    if (sidebarToggle) {
        // Uncomment Below to persist sidebar toggle between refreshes
        // if (localStorage.getItem('sb|sidebar-toggle') === 'true') {
        //     document.body.classList.toggle('sb-sidenav-toggled');
        // }
        sidebarToggle.addEventListener('click', event => {
            event.preventDefault();
            document.body.classList.toggle('sb-sidenav-toggled');
            localStorage.setItem('sb|sidebar-toggle', document.body.classList.contains('sb-sidenav-toggled'));
        });
    }

    // Run onOk whenever the alert is dismissed (OK, close button, Esc or backdrop click). onOk is looked
    // up at that moment (not captured here) so showModalmsg/overrideOnOk can replace it later.
    const alertModal = document.getElementById("modal-alert");
    if (alertModal) {
        alertModal.addEventListener("hidden.bs.modal", () => onOk());
    }

});

// Shows the shared alert dialog. When redirectUrl is given, closing the dialog navigates there
// (e.g. to the login page after a successful registration); otherwise the user stays on the page.
function showModalmsg(msg, redirectUrl) {
    document.getElementById('alert-message').innerText = msg;
    onOk = redirectUrl ? () => { window.location.href = redirectUrl; } : () => { };
    var modalAlert = new bootstrap.Modal(document.getElementById('modal-alert'), {})
    modalAlert.show();
}

let onOk = function () {
    //alert("Default OK button clicked! You can customize this function.");
    // Add your custom logic here
};

// Function to allow dynamic overriding of onOk
function overrideOnOk(newFunction) {
    onOk = newFunction;
}


