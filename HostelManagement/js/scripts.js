/*!
    * Start Bootstrap - SB Admin v7.0.7 (https://startbootstrap.com/template/sb-admin)
    * Copyright 2013-2023 Start Bootstrap
    * Licensed under MIT (https://github.com/StartBootstrap/startbootstrap-sb-admin/blob/master/LICENSE)
    */
// 
// Scripts
// 

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

    // Attach the onOk function to the "OK" button click event
    document.getElementById("okButton").addEventListener("click", onOk);

});

function showModalmsg(msg) {
    document.getElementById('alert-message').innerText = msg;
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


