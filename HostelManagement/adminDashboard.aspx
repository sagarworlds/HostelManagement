<%@ Page Title="" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="adminDashboard.aspx.cs" Inherits="HostelManagement.adminDashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4">Request List</h1>
            <%-- <ol class="breadcrumb mb-4">
                <li class="breadcrumb-item active">Room List</li>
            </ol>--%>

        <asp:GridView ID="gvRoomsRequest" DataKeyNames="Id" runat="server" AutoGenerateColumns="false"
            CssClass="table table-bordered " ShowFooter="false" HeaderStyle-Font-Bold="true" OnRowCommand="gvRoomsRequest_RowCommand">
            <Columns>
                <asp:TemplateField HeaderText="Request ID">
                    <ItemTemplate>
                        <asp:Label ID="lblFirstName" runat="server" Text='<%#Eval("Id") %>' />
                    </ItemTemplate>
                </asp:TemplateField>
                <asp:TemplateField HeaderText="Request Status">
                    <ItemTemplate>
                        <asp:Label ID="lblPofessorName" runat="server" Text='<%#Eval("Status") %>' CssClass="ellipsis" />

                    </ItemTemplate>
                </asp:TemplateField>
                <asp:TemplateField HeaderText="Alloted Room">
                    <ItemTemplate>
                        <asp:Label ID="lblPofessorName" runat="server" Text='<%#Eval("RoomName") %>' CssClass="ellipsis" />
                    </ItemTemplate>
                </asp:TemplateField>

                <asp:TemplateField HeaderText="Action">
                    <ItemTemplate>
                       <asp:HyperLink Text="Take Action" NavigateUrl='<%# "ApproveRoomRequest.aspx?Id=" + Eval("Id") %>' runat="server" Style="display:inline-block">
                            </asp:HyperLink>
                    </ItemTemplate>
                </asp:TemplateField>
            </Columns>
        </asp:GridView>

        </div>
    </main>
</asp:Content>
