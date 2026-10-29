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
            <div class="row mt-5">
                <div class="col-md-6">
                    <p>
                        Request No:
                    <asp:Label runat="server" ID="lblRequestNo" />
                    </p>
                    <p>
                        Request Status:
                    <asp:Label runat="server" ID="lblStatus" />
                    </p>
                    <p>
                        Requested User:
                    <asp:Label runat="server" ID="lblUserName" />
                    </p>
                    <p>
                        Preferred Room:
                    <asp:Label runat="server" ID="lblPreferredRoom" />
                    </p>
                    <p>
                        Available Room:
                    <asp:DropDownList runat="server" ID="ddlAvailableRooms" DataValueField="Id" DataTextField="RoomName" />
                    <asp:RequiredFieldValidator runat="server" ControlToValidate="ddlAvailableRooms" InitialValue="0"
                        ErrorMessage="Select a room." Display="Dynamic" CssClass="text-danger" ValidationGroup="approve" />
                    </p>
                    <asp:Button Text="Approve" runat="server" CssClass="btn btn-primary" ID="btnApprove" ValidationGroup="approve" />
                    <asp:Button Text="Reject" runat="server" CssClass="btn btn-outline-danger" ID="btnReject" CausesValidation="false"
                        OnClientClick="return confirm('Reject this request?');" />

                </div>


            </div>
        </div>

    </main>
</asp:Content>
