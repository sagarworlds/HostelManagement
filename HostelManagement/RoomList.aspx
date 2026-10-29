<%@ Page Title="" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="RoomList.aspx.cs" Inherits="HostelManagement.RoomList" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4">Room List</h1>
           <%-- <ol class="breadcrumb mb-4">
                <li class="breadcrumb-item active">Room List</li>
            </ol>--%>
        </div>
        <div class="col-md-10 d-flex justify-content-end">
            <a class="btn btn-primary" href="AddEditRoom.aspx">Add New</a>
        </div>
        <div class="row justify-content-center mt-3 ms-2 col-md-10">
            <asp:GridView ID="gvRooms" DataKeyNames="Id" runat="server" AutoGenerateColumns="false"
                CssClass="table table-bordered " ShowFooter="false" HeaderStyle-Font-Bold="true" OnRowDeleting="gvRooms_RowDeleting" OnRowCommand="gvRooms_RowCommand" EmptyDataText="No rooms have been added yet.">
                <Columns>

                    <asp:TemplateField HeaderText="Room No">
                        <ItemTemplate>
                            <asp:Label ID="lblFirstName" runat="server" Text='<%# HttpUtility.HtmlEncode(Convert.ToString(Eval("RoomName"))) %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Room Rent">
                        <ItemTemplate>
                            <asp:Label ID="lblLastName" runat="server" Text='<%#Eval("Rent") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Description">
                        <ItemTemplate>
                            <asp:Label ID="lblPofessorName" runat="server" Text='<%# HttpUtility.HtmlEncode(Convert.ToString(Eval("Description"))) %>' CssClass="ellipsis" />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Status">
                        <ItemTemplate>
                            <asp:Label ID="lblStatus" runat="server" Text='<%# HttpUtility.HtmlEncode(Convert.ToString(Eval("OccupancyStatus"))) %>' />
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="Action">
                        <ItemTemplate>
                            <asp:HyperLink NavigateUrl='<%# "AddEditRoom.aspx?Id=" + Eval("Id") %>' runat="server" Style="display:inline-block">
                                <span class="material-symbols-outlined">edit_square</span>
                            </asp:HyperLink>

                            <asp:LinkButton runat="server" CommandName="Vacate" CommandArgument='<%# Eval("ActiveRequestId") %>' OnClientClick="return confirm('Vacate this room? The student will no longer hold it.');" ToolTip="Vacate room" Visible='<%# (bool)Eval("IsOccupied") %>'>
                                <span class="material-symbols-outlined">logout</span>
                            </asp:LinkButton>
                            <asp:LinkButton runat="server" CommandName="Delete" Text="Delete" CommandArgument='<%# Container.DataItemIndex %>' OnClientClick="return confirm('Are you sure you want to delete this');" ToolTip="Delete" Visible='<%# !(bool)Eval("IsOccupied") %>'>
                                <span class="material-symbols-outlined">delete</span>
                            </asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </main>
</asp:Content>
