<%@ Page Title="Login" Language="C#" MasterPageFile="~/Site1.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="HostelManagement.Login" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container">
            <div class="row justify-content-center">
                <div class="col-lg-5">
                    <div class="card shadow-lg border-0 rounded-lg mt-5">
                        <div class="card-header">
                            <h3 class="text-center font-weight-light my-4">Login</h3>
                        </div>
                        <div class="card-body">
                            <div class="form-floating mb-3">
                                <asp:TextBox runat="server" ID="inputEmail" TextMode="Email" placeholder="name@example.com" CssClass="form-control" autocomplete="off" />
                                <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputEmail" runat="server" CssClass="text-danger" />
                                <asp:RegularExpressionValidator ID="regexEmail" runat="server"
                                    ControlToValidate="inputEmail"
                                    ValidationExpression="^[^\s@]+@[^\s@]+\.[^\s@]+$"
                                    ErrorMessage="Please enter a valid email address."
                                    Display="Dynamic"
                                    CssClass="text-danger" />
                                <label for="inputEmail">Email address</label>
                            </div>
                            <div class="form-floating mb-3">
                                <asp:TextBox runat="server" ID="inputPassword" TextMode="Password" placeholder="Password" CssClass="form-control" autocomplete="off" />
                                <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputPassword" runat="server" CssClass="text-danger" />
                                <label for="inputPassword">Password</label>
                            </div>
                           
                            <div class="d-flex align-items-center justify-content-between mt-4 mb-0">
                                <a class="small" href="passwordreset.aspx" style="visibility:hidden">Forgot Password?</a>
                                <asp:Button Text="Login" runat="server" ID="btnLogin" CssClass="btn btn-primary" />
                            </div>
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
