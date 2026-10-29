<%@ Page Title="My Requests" Language="C#" MasterPageFile="~/UserMaster.Master" AutoEventWireup="true" CodeBehind="UserDashboard.aspx.cs" Inherits="HostelManagement.UserDashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <main>
        <asp:PlaceHolder runat="server" ID="phAnnouncements">
            <div class="container-fluid px-4 mt-4">
                <h2 class="h4">Announcements</h2>
                <asp:Repeater runat="server" ID="rptAnnouncements">
                    <ItemTemplate>
                        <div class="alert alert-info mb-2" role="alert">
                            <strong><%# HttpUtility.HtmlEncode(Convert.ToString(Eval("Title"))) %></strong>
                            <small class="text-muted ms-2"><%# Eval("CreatedOn", "{0:dd MMM yyyy}") %></small>
                            <p class="mb-0"><%# HostelManagement.HtmlText.FromPlain(Convert.ToString(Eval("Body"))) %></p>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
            </div>
        </asp:PlaceHolder>
        <div class="container-fluid px-4">
            <h1 class="mt-4">Request List</h1>
           <%-- <ol class="breadcrumb mb-4">
                <li class="breadcrumb-item active">Room List</li>
            </ol>--%>
        </div>
        <div class="col-md-10 d-flex justify-content-end">
            <a class="btn btn-outline-primary me-2" href="AvailableRooms.aspx">Browse available rooms</a>
            <asp:Button Text="Add new Request" runat="server" ID="btnAddNewRequest" CssClass="btn btn-primary"/>
        </div>
        <div class="row justify-content-center mt-3 ms-2 col-md-10">
            <asp:GridView ID="gvRoomsRequest" DataKeyNames="Id" runat="server" AutoGenerateColumns="false"
                CssClass="table table-bordered " ShowFooter="false" HeaderStyle-Font-Bold="true" OnRowCommand="gvRoomsRequest_RowCommand" EmptyDataText="You have not made any room requests yet.">
                <Columns>

                    <asp:TemplateField HeaderText="Request ID">
                        <ItemTemplate>
                            <asp:Label ID="lblFirstName" runat="server" Text='<%#Eval("Id") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Requested On">
                        <ItemTemplate>
                            <asp:Label ID="lblRequestDate" runat="server" Text='<%# Eval("RequestDate", "{0:dd MMM yyyy}") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Request Status">
                        <ItemTemplate>
                            <asp:Label ID="lblLastName" runat="server" Text='<%#Eval("Status") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Preferred Room">
                        <ItemTemplate>
                            <asp:Label ID="lblPreferredRoom" runat="server" Text='<%# HttpUtility.HtmlEncode(Convert.ToString(Eval("PreferredRoomName"))) %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Alloted Room">
                        <ItemTemplate>
                            <asp:Label ID="lblPofessorName" runat="server" Text='<%# HttpUtility.HtmlEncode(Convert.ToString(Eval("RoomName"))) %>' CssClass="ellipsis" />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Action">
                        <ItemTemplate>
                            <asp:LinkButton runat="server" CommandName="CancelRequest" CommandArgument='<%# Eval("Id") %>' Text="Cancel request"
                                OnClientClick="return confirm('Cancel this request?');"
                                Visible='<%# string.Equals(Convert.ToString(Eval("Status")), HostelManagement.Utility.RequestStatus.Pending) %>' />
                        </ItemTemplate>
                    </asp:TemplateField>

                    <%--<asp:TemplateField HeaderText="Action">
                        <ItemTemplate>
                            <asp:LinkButton runat="server" CommandName="Delete" Text="Delete" CommandArgument='<%# Container.DataItemIndex %>' OnClientClick="return confirm('Are you sure you want to delete this');" ToolTip="Delete">
                                <span class="material-symbols-outlined">delete</span>
                            </asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateField>--%>
                </Columns>
            </asp:GridView>
        </div>
    </main>
</asp:Content>
