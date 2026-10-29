<%@ Page Title="Forgot Password" Language="C#" MasterPageFile="~/Site1.Master" AutoEventWireup="true" CodeBehind="ForgotPassword.aspx.cs" Inherits="HostelManagement.ForgotPassword" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container">
            <div class="row justify-content-center">
                <div class="col-lg-5">
                    <div class="card shadow-lg border-0 rounded-lg mt-5">
                        <div class="card-header">
                            <h3 class="text-center font-weight-light my-4">Password Recovery</h3>
                        </div>
                        <div class="card-body">
                            <asp:Panel runat="server" ID="pnlRequest">
                                <div class="small mb-3 text-muted">Enter your email address and we will send you a link to choose a new password.</div>
                                <div class="form-floating mb-3">
                                    <asp:TextBox runat="server" ID="inputEmail" TextMode="Email" placeholder="name@example.com" CssClass="form-control" autocomplete="off" />
                                    <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputEmail" runat="server" CssClass="text-danger" Display="Dynamic" />
                                    <asp:RegularExpressionValidator runat="server" ControlToValidate="inputEmail"
                                        ValidationExpression="^[^\s@]+@[^\s@]+\.[^\s@]+$"
                                        ErrorMessage="Please enter a valid email address." Display="Dynamic" CssClass="text-danger" />
                                    <label for="<%= inputEmail.ClientID %>">Email address</label>
                                </div>
                                <div class="d-flex align-items-center justify-content-between mt-4 mb-0">
                                    <a class="small" href="Login.aspx">Return to login</a>
                                    <asp:Button Text="Send reset link" runat="server" ID="btnSend" CssClass="btn btn-primary" />
                                </div>
                            </asp:Panel>
                            <asp:Panel runat="server" ID="pnlSent" Visible="false">
                                <p>If an account exists for that email address, we have sent a link to it. The link works once and expires in one hour.</p>
                                <p class="small text-muted mb-0">Nothing arrived? Check your spam folder, or request another link.</p>
                                <a class="btn btn-primary mt-3" href="Login.aspx">Return to login</a>
                            </asp:Panel>
                            <asp:Panel runat="server" ID="pnlUnavailable" Visible="false">
                                <p>Resetting a password by email is not set up on this system. Please contact your hostel administrator.</p>
                                <a class="btn btn-primary mt-3" href="Login.aspx">Return to login</a>
                            </asp:Panel>
                        </div>
                        <div class="card-footer text-center py-3">
                            <div class="small"><a href="Register.aspx">Need an account? Sign up!</a></div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </main>
</asp:Content>
