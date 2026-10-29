<%@ Page Title="My Profile" Language="C#" MasterPageFile="~/UserMaster.Master" AutoEventWireup="true" CodeBehind="MyProfile.aspx.cs" Inherits="HostelManagement.MyProfile" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4">My Profile</h1>
            <asp:Panel runat="server" ID="pnlMustChange" Visible="false" CssClass="alert alert-warning mt-3" role="alert">
                An administrator reset your password. Please choose a new password below before you continue.
            </asp:Panel>
            <div class="row mt-4">
                <div class="col-lg-6 mb-4">
                    <div class="card">
                        <div class="card-header">Profile details</div>
                        <div class="card-body">
                            <div class="mb-3">
                                <asp:Label runat="server" AssociatedControlID="txtEmail" CssClass="form-label" Text="Email address" />
                                <asp:TextBox runat="server" ID="txtEmail" CssClass="form-control" ReadOnly="true" />
                            </div>
                            <div class="mb-3">
                                <asp:Label runat="server" AssociatedControlID="txtFirstName" CssClass="form-label" Text="First name" />
                                <asp:TextBox runat="server" ID="txtFirstName" CssClass="form-control" MaxLength="100" />
                                <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="txtFirstName" runat="server" CssClass="text-danger" Display="Dynamic" ValidationGroup="profile" />
                            </div>
                            <div class="mb-3">
                                <asp:Label runat="server" AssociatedControlID="txtLastName" CssClass="form-label" Text="Last name" />
                                <asp:TextBox runat="server" ID="txtLastName" CssClass="form-control" MaxLength="100" />
                                <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="txtLastName" runat="server" CssClass="text-danger" Display="Dynamic" ValidationGroup="profile" />
                            </div>
                            <div class="mb-3">
                                <asp:Label runat="server" AssociatedControlID="txtMobile" CssClass="form-label" Text="Mobile number (optional)" />
                                <asp:TextBox runat="server" ID="txtMobile" CssClass="form-control" MaxLength="20" />
                                <asp:RegularExpressionValidator runat="server" ControlToValidate="txtMobile"
                                    ValidationExpression="^\+?[0-9 ()\-]{7,20}$" ErrorMessage="Enter a valid phone number."
                                    Display="Dynamic" CssClass="text-danger" ValidationGroup="profile" />
                            </div>
                            <asp:Button Text="Save profile" runat="server" ID="btnSaveProfile" CssClass="btn btn-primary" ValidationGroup="profile" />
                        </div>
                    </div>
                </div>
                <div class="col-lg-6 mb-4">
                    <div class="card">
                        <div class="card-header">Change password</div>
                        <div class="card-body">
                            <div class="mb-3">
                                <asp:Label runat="server" AssociatedControlID="txtCurrentPassword" CssClass="form-label" Text="Current password" />
                                <asp:TextBox runat="server" ID="txtCurrentPassword" TextMode="Password" CssClass="form-control" autocomplete="off" />
                                <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="txtCurrentPassword" runat="server" CssClass="text-danger" Display="Dynamic" ValidationGroup="password" />
                            </div>
                            <div class="mb-3">
                                <asp:Label runat="server" AssociatedControlID="txtNewPassword" CssClass="form-label" Text="New password" />
                                <asp:TextBox runat="server" ID="txtNewPassword" TextMode="Password" CssClass="form-control" autocomplete="off" />
                                <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="txtNewPassword" runat="server" CssClass="text-danger" Display="Dynamic" ValidationGroup="password" />
                            </div>
                            <div class="mb-3">
                                <asp:Label runat="server" AssociatedControlID="txtConfirmPassword" CssClass="form-label" Text="Confirm new password" />
                                <asp:TextBox runat="server" ID="txtConfirmPassword" TextMode="Password" CssClass="form-control" autocomplete="off" />
                                <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="txtConfirmPassword" runat="server" CssClass="text-danger" Display="Dynamic" ValidationGroup="password" />
                                <asp:CompareValidator runat="server" ControlToValidate="txtConfirmPassword" ControlToCompare="txtNewPassword"
                                    Operator="Equal" Type="String" ErrorMessage="Passwords do not match."
                                    Display="Dynamic" CssClass="text-danger" ValidationGroup="password" />
                            </div>
                            <asp:Button Text="Change password" runat="server" ID="btnChangePassword" CssClass="btn btn-primary" ValidationGroup="password" />
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </main>
</asp:Content>
