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
            <div class="d-flex align-items-center mb-3">
                <asp:Label runat="server" AssociatedControlID="ddlStatusFilter" Text="Show:" CssClass="me-2" />
                <asp:DropDownList runat="server" ID="ddlStatusFilter" CssClass="form-select w-auto" AutoPostBack="true"
                    OnSelectedIndexChanged="ddlStatusFilter_SelectedIndexChanged" />
            </div>

        <asp:GridView ID="gvRoomsRequest" DataKeyNames="Id" runat="server" AutoGenerateColumns="false"
            CssClass="table table-bordered " ShowFooter="false" HeaderStyle-Font-Bold="true" EmptyDataText="There are no room requests.">
            <Columns>
                <asp:TemplateField HeaderText="Request ID">
                    <ItemTemplate>
                        <asp:Label ID="lblFirstName" runat="server" Text='<%#Eval("Id") %>' />
                    </ItemTemplate>
                </asp:TemplateField>
                <asp:TemplateField HeaderText="Student">
                    <ItemTemplate>
                        <asp:Label ID="lblStudent" runat="server" Text='<%# HttpUtility.HtmlEncode(Convert.ToString(Eval("UserName"))) %>' />
                    </ItemTemplate>
                </asp:TemplateField>
                <asp:TemplateField HeaderText="Requested On">
                    <ItemTemplate>
                        <asp:Label ID="lblRequestDate" runat="server" Text='<%# Eval("RequestDate", "{0:dd MMM yyyy}") %>' />
                    </ItemTemplate>
                </asp:TemplateField>
                <asp:TemplateField HeaderText="Request Status">
                    <ItemTemplate>
                        <asp:Label ID="lblPofessorName" runat="server" Text='<%#Eval("Status") %>' CssClass="ellipsis" />

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
                       <asp:HyperLink Text="Take Action" NavigateUrl='<%# "ApproveRoomRequest.aspx?Id=" + Eval("Id") %>' runat="server" Style="display:inline-block"
                            Visible='<%# string.Equals(Convert.ToString(Eval("Status")), HostelManagement.Utility.RequestStatus.Pending) %>'>
                            </asp:HyperLink>
                    </ItemTemplate>
                </asp:TemplateField>
            </Columns>
        </asp:GridView>

        </div>
    </main>
</asp:Content>
