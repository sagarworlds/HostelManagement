<%@ Page Title="" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="AddEditRoom.aspx.cs" Inherits="HostelManagement.AddEditRoom" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4">Add/Edit Room</h1>
            <ol class="breadcrumb mb-4">
                <li class="breadcrumb-item active">Add/Edit Room</li>
            </ol>
        </div>
        <div class="row justify-content-center">
            <div class="col-lg-5">
                <div class="card border-0 rounded-lg mt-5">
                    <div class="card-body">
                        <div class="form-floating mb-3">
                            <asp:TextBox runat="server" ID="txtRoomName" placeholder="Room Name" CssClass="form-control" autocomplete="off" />
                            <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="txtRoomName" runat="server" CssClass="text-danger" />
                            <label for="inputEmail">Roon Name</label>
                        </div>
                        <div class="form-floating mb-3">
                            <asp:TextBox runat="server" ID="txtDescription" TextMode="MultiLine" placeholder="Password" CssClass="form-control" autocomplete="off" />
                            <label for="inputPassword">Description</label>
                        </div>
                         <div class="form-floating mb-3">
                            <asp:TextBox runat="server" ID="txtRent" TextMode="Number" placeholder="Rent" CssClass="form-control" autocomplete="off" />
                            <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="txtRent" runat="server" CssClass="text-danger" />
                            <label for="inputPassword">Rent</label>
                        </div>

                        <div class="d-flex align-items-center justify-content-between mt-4 mb-0">
                            <asp:Button Text="Create Room" runat="server" ID="btnCreateRoom" CssClass="btn btn-primary" />
                        </div>
                    </div>

                </div>
            </div>
        </div>
    </main>
</asp:Content>
