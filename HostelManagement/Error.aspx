<%@ Page Language="C#" %>
<%-- Standalone on purpose: no master page, no session, no database, so it still renders when those are what failed. --%>
<script runat="server">
    protected void Page_Load(System.Object sender, System.EventArgs e)
    {
        // The page is served in place of the failing one (ResponseRewrite), so say honestly that it failed.
        bool notFound = Request.QueryString["code"] == "404";
        Response.StatusCode = notFound ? 404 : 500;
        Response.TrySkipIisCustomErrors = true;
        Title = notFound ? "Page not found" : "Something went wrong";
        heading.InnerText = Title;
        message.InnerText = notFound
            ? "The page you asked for does not exist."
            : "Sorry, something went wrong on our side. The problem has been recorded. Please try again in a moment.";
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
                            <a class="btn btn-primary" href="<%= ResolveUrl("~/Login.aspx") %>">Back to sign in</a>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </form>
</body>
</html>
