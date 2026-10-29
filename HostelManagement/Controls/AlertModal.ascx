<%@ Control Language="C#" AutoEventWireup="true" %>
<%-- Shared alert dialog. Pages show text in it through HostelManagement.ClientMessage (see js/scripts.js: showModalmsg). --%>
<script runat="server">
    protected void Page_Load(System.Object sender, System.EventArgs e)
    {
        // Show a message that was stored with ClientMessage.Flash before a redirect.
        HostelManagement.ClientMessage.ShowPendingFlash(Page);
    }
</script>
<div class="modal fade" id="modal-alert" tabindex="-1" aria-labelledby="alert-message" aria-hidden="true">
    <div class="modal-dialog">
        <div class="modal-content">
            <div class="modal-header">
                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
            </div>
            <div class="modal-body">
                <p id="alert-message"></p>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-primary" data-bs-dismiss="modal" id="okButton">OK</button>
            </div>
        </div>
    </div>
</div>
