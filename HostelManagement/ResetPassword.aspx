<%@ Page Title="Choose a New Password" Language="C#" MasterPageFile="~/Site1.Master" AutoEventWireup="true" CodeBehind="ResetPassword.aspx.cs" Inherits="HostelManagement.ResetPassword" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container">
            <div class="row justify-content-center">
                <div class="col-lg-5">
                    <div class="card shadow-lg border-0 rounded-lg mt-5">
                        <div class="card-header">
                            <h3 class="text-center font-weight-light my-4">Choose a New Password</h3>
                        </div>
                        <div class="card-body">
                            <asp:Panel runat="server" ID="pnlForm">
                                <div class="form-floating mb-3">
                                    <asp:TextBox runat="server" ID="inputPassword" TextMode="Password" placeholder="New password" CssClass="form-control" autocomplete="off" />
                                    <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputPassword" runat="server" CssClass="text-danger" Display="Dynamic" />
                                    <label for="<%= inputPassword.ClientID %>">New password</label>
                                </div>
                                <div class="form-floating mb-3">
                                    <asp:TextBox runat="server" ID="inputPasswordConfirm" TextMode="Password" placeholder="Confirm new password" CssClass="form-control" autocomplete="off" />
                                    <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputPasswordConfirm" runat="server" CssClass="text-danger" Display="Dynamic" />
                                    <asp:CompareValidator runat="server" ControlToValidate="inputPasswordConfirm" ControlToCompare="inputPassword"
                                        Operator="Equal" Type="String" ErrorMessage="Passwords do not match." Display="Dynamic" CssClass="text-danger" />
                                    <label for="<%= inputPasswordConfirm.ClientID %>">Confirm new password</label>
                                </div>
                                <div class="d-grid mt-4 mb-0">
                                    <asp:Button Text="Change password" runat="server" ID="btnReset" CssClass="btn btn-primary" />
                                </div>
                            </asp:Panel>
                            <asp:Panel runat="server" ID="pnlInvalid" Visible="false">
                                <p>This link is invalid or has expired. Links work once and last one hour.</p>
                                <a class="btn btn-primary" href="ForgotPassword.aspx">Request a new link</a>
                            </asp:Panel>
                        </div>
                        <div class="card-footer text-center py-3">
                            <div class="small"><a href="Login.aspx">Return to login</a></div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </main>
</asp:Content>
