<%@ Page Language="C#" %>
<%-- Standalone on purpose: no master page, no session, no database, so it still renders when those are what failed. --%>
<script runat="server">
    protected void Page_Load(System.Object sender, System.EventArgs e)
    {
        // The page is served in place of the failing one (ResponseRewrite), so say honestly what happened.
        string code = Request.QueryString["code"];
        bool notFound = code == "404";
        bool rejected = code == "400";   // ASP.NET refused something the visitor typed (Global.asax.cs sends them here)
        Response.StatusCode = notFound ? 404 : rejected ? 400 : 500;
        Response.TrySkipIisCustomErrors = true;
        Title = notFound ? "Page not found" : rejected ? "That text cannot be accepted" : "Something went wrong";
        heading.InnerText = Title;
        message.InnerText = notFound
            ? "The page you asked for does not exist."
            : rejected
                ? "Something you typed cannot be accepted: text that contains HTML tags (for example <b> or <script>) is refused for safety. Please go back, take it out and try again."
                : "Sorry, something went wrong on our side. The problem has been recorded. Please try again in a moment.";
        goBack.Visible = rejected;
    }
</script>
<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title></title>
    <link href="~/css/styles.css" rel="stylesheet" runat="server" />
</head>
<body class="bg-primary">
    <form id="form1" runat="server">
        <div class="container">
            <div class="row justify-content-center">
                <div class="col-lg-6">
                    <div class="card shadow-lg border-0 rounded-lg mt-5">
                        <div class="card-header">
                            <h3 class="text-center font-weight-light my-4" id="heading" runat="server"></h3>
                        </div>
                        <div class="card-body">
                            <p id="message" runat="server"></p>
                            <a id="goBack" runat="server" visible="false" class="btn btn-secondary me-2" href="javascript:history.back()">Go back</a>
                            <a class="btn btn-primary" href="<%= ResolveUrl("~/Login.aspx") %>">Back to sign in</a>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </form>
</body>
</html>
