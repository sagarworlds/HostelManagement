<%@ Page Title="Dashboard" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="adminDashboard.aspx.cs" Inherits="HostelManagement.adminDashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4">Dashboard</h1>
            <div class="row mt-3">
                <asp:Repeater runat="server" ID="rptStats">
                    <ItemTemplate>
                        <div class="col-xl-2 col-md-4 col-sm-6 mb-4">
                            <div class='<%# "card h-100 " + Eval("CssClass") %>'>
                                <div class="card-body">
                                    <div class="fs-2 fw-bold"><%# Eval("Value") %></div>
                                    <div><%# HttpUtility.HtmlEncode(Convert.ToString(Eval("Title"))) %></div>
                                </div>
                                <asp:PlaceHolder runat="server" Visible='<%# !string.IsNullOrEmpty(Convert.ToString(Eval("Url"))) %>'>
                                    <div class="card-footer small">
                                        <a class="text-reset stretched-link" href='<%# Eval("Url") %>'>View details</a>
                                    </div>
                                </asp:PlaceHolder>
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
            </div>
            <h2 class="h4">Room requests</h2>
            <%-- <ol class="breadcrumb mb-4">
                <li class="breadcrumb-item active">Room List</li>
            </ol>--%>
            <div class="d-flex align-items-center mb-3">
                <asp:Label runat="server" AssociatedControlID="ddlStatusFilter" Text="Show:" CssClass="me-2" />
                <asp:DropDownList runat="server" ID="ddlStatusFilter" CssClass="form-select w-auto" AutoPostBack="true"
                    OnSelectedIndexChanged="ddlStatusFilter_SelectedIndexChanged" />
            </div>

        <asp:GridView ID="gvRoomsRequest" DataKeyNames="Id" runat="server" AutoGenerateColumns="false"
            CssClass="table table-bordered js-datatable" ShowFooter="false" HeaderStyle-Font-Bold="true" EmptyDataText="There are no room requests.">
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
