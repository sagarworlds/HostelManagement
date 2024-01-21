<%@ Page Title="" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="ApproveRoomRequest.aspx.cs" Inherits="HostelManagement.ApproveRoomRequest" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4">Approve Request</h1>
            <%-- <ol class="breadcrumb mb-4">
                <li class="breadcrumb-item active">Room List</li>
            </ol>--%>
        </div>
        <div class="row">
            <asp:Label runat="server" ID="lblRequestNo" />
            <br />
            <asp:Label runat="server" ID="lblStatus" />
            <br />

            <asp:Label runat="server" ID="lblUserName" />
            <br />

            <asp:DropDownList runat="server" ID="ddlAvailableRooms" />

        </div>

        <div class="col-md-10 ">
            <asp:Button Text="Add new Request" runat="server" ID="btnApprove" CssClass="btn btn-primary" />
        </div>
    </main>
</asp:Content>
