<%@ Page Title="Announcements" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="Announcements.aspx.cs" Inherits="HostelManagement.Announcements" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4">Announcements</h1>
            <p class="text-muted">Notices posted here appear on every student's dashboard until they end or are removed.</p>
            <div class="card mb-4 col-lg-8">
                <div class="card-header">Post an announcement</div>
                <div class="card-body">
                    <div class="mb-3">
                        <asp:Label runat="server" AssociatedControlID="txtTitle" CssClass="form-label" Text="Title" />
                        <asp:TextBox runat="server" ID="txtTitle" CssClass="form-control" MaxLength="150" />
                        <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="txtTitle" runat="server" CssClass="text-danger" Display="Dynamic" />
                    </div>
                    <div class="mb-3">
                        <asp:Label runat="server" AssociatedControlID="txtBody" CssClass="form-label" Text="Message" />
                        <asp:TextBox runat="server" ID="txtBody" TextMode="MultiLine" Rows="4" CssClass="form-control" />
                        <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="txtBody" runat="server" CssClass="text-danger" Display="Dynamic" />
                    </div>
                    <div class="mb-3">
                        <asp:Label runat="server" AssociatedControlID="txtLastDay" CssClass="form-label" Text="Show until (optional)" />
                        <asp:TextBox runat="server" ID="txtLastDay" TextMode="Date" CssClass="form-control w-auto" />
                    </div>
                    <asp:Button Text="Post announcement" runat="server" ID="btnPost" CssClass="btn btn-primary" />
                </div>
            </div>
        </div>
        <div class="row justify-content-center mt-3 ms-2 col-md-10">
            <asp:GridView ID="gvAnnouncements" DataKeyNames="Id" runat="server" AutoGenerateColumns="false"
                CssClass="table table-bordered js-datatable" ShowFooter="false" HeaderStyle-Font-Bold="true"
                OnRowDeleting="gvAnnouncements_RowDeleting" EmptyDataText="No announcements have been posted yet.">
                <Columns>
                    <asp:TemplateField HeaderText="Title">
                        <ItemTemplate>
                            <asp:Label ID="lblTitle" runat="server" Text='<%# HttpUtility.HtmlEncode(Convert.ToString(Eval("Title"))) %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Posted">
                        <ItemTemplate>
                            <asp:Label ID="lblPosted" runat="server" Text='<%# Eval("CreatedOn", "{0:dd MMM yyyy}") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Status">
                        <ItemTemplate>
                            <asp:Label ID="lblStatus" runat="server" Text='<%# HttpUtility.HtmlEncode(Convert.ToString(Eval("StatusText"))) %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Action">
                        <ItemTemplate>
                            <asp:LinkButton runat="server" CommandName="Delete" Text="Delete" ToolTip="Delete"
                                OnClientClick="return confirm('Remove this announcement?');">
                                <span class="material-symbols-outlined">delete</span>
                            </asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </main>
</asp:Content>
