<%@ Page Title="" Language="C#" MasterPageFile="~/UserMaster.Master" AutoEventWireup="true" CodeBehind="UserDashboard.aspx.cs" Inherits="HostelManagement.UserDashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4">Request List</h1>
           <%-- <ol class="breadcrumb mb-4">
                <li class="breadcrumb-item active">Room List</li>
            </ol>--%>
        </div>
        <div class="col-md-10 d-flex justify-content-end">
            <asp:Button Text="Add new Request" runat="server" ID="btnAddNewRequest" CssClass="btn btn-primary"/>
        </div>
        <div class="row justify-content-center mt-3 ms-2 col-md-10">
            <asp:GridView ID="gvRoomsRequest" DataKeyNames="Id" runat="server" AutoGenerateColumns="false"
                CssClass="table table-bordered " ShowFooter="false" HeaderStyle-Font-Bold="true" >
                <Columns>

                    <asp:TemplateField HeaderText="Request ID">
                        <ItemTemplate>
                            <asp:Label ID="lblFirstName" runat="server" Text='<%#Eval("Id") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Request Status">
                        <ItemTemplate>
                            <asp:Label ID="lblLastName" runat="server" Text='<%#Eval("Status") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Alloted Room">
                        <ItemTemplate>
                            <asp:Label ID="lblPofessorName" runat="server" Text='<%#Eval("RoomName") %>' CssClass="ellipsis" />
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
