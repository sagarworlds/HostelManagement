<%@ Page Title="Available Rooms" Language="C#" MasterPageFile="~/UserMaster.Master" AutoEventWireup="true" CodeBehind="AvailableRooms.aspx.cs" Inherits="HostelManagement.AvailableRooms" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4">Available Rooms</h1>
            <p class="text-muted">
                Choose the room you would like. An administrator reviews every request and decides which room to allocate,
                so asking for a room does not reserve it. You can have one pending request or one allocated room at a time.
            </p>
        </div>
        <div class="row justify-content-center mt-3 ms-2 col-md-10">
            <asp:GridView ID="gvRooms" DataKeyNames="Id" runat="server" AutoGenerateColumns="false"
                CssClass="table table-bordered js-datatable" ShowFooter="false" HeaderStyle-Font-Bold="true"
                OnRowCommand="gvRooms_RowCommand" EmptyDataText="No rooms are available right now. Please check again later.">
                <Columns>
                    <asp:TemplateField HeaderText="Room">
                        <ItemTemplate>
                            <asp:Label ID="lblRoomName" runat="server" Text='<%# HttpUtility.HtmlEncode(Convert.ToString(Eval("RoomName"))) %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Rent">
                        <ItemTemplate>
                            <asp:Label ID="lblRent" runat="server" Text='<%# Eval("Rent", "{0:N0}") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Description">
                        <ItemTemplate>
                            <asp:Label ID="lblDescription" runat="server" Text='<%# HttpUtility.HtmlEncode(Convert.ToString(Eval("Description"))) %>' CssClass="ellipsis" />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Action">
                        <ItemTemplate>
                            <asp:LinkButton runat="server" CommandName="RequestRoom" CommandArgument='<%# Eval("Id") %>' Text="Request this room"
                                CssClass="btn btn-sm btn-primary"
                                OnClientClick="return confirm('Send a request for this room?');" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </main>
</asp:Content>
