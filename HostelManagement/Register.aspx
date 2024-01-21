<%@ Page Title="Create Account" Language="C#" MasterPageFile="~/Site1.Master" AutoEventWireup="true" CodeBehind="Register.aspx.cs" Inherits="HostelManagement.Register" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container">
            <div class="row justify-content-center">
                <div class="col-lg-7">
                    <div class="card shadow-lg border-0 rounded-lg mt-5">
                        <div class="card-header">
                            <h3 class="text-center font-weight-light my-4">Create Account</h3>
                        </div>
                        <div class="card-body">
                            <div class="row mb-3">
                                <div class="col-md-6">
                                    <div class="form-floating mb-3 mb-md-0">
                                        <%--<input class="form-control" id="inputFirstName" type="text" placeholder="Enter your first name" />--%>
                                        <asp:TextBox runat="server" ID="inputFirstName" placeholder="Enter your first name" CssClass="form-control" />
                                        <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputFirstName" runat="server" CssClass="text-danger" />
                                        <label for="inputFirstName">First name</label>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-floating">
                                        <%--<input class="form-control" id="inputLastName" type="text" placeholder="Enter your last name" />--%>
                                        <asp:TextBox runat="server" ID="inputLastName" CssClass="form-control" placeholder="Enter your last name" />
                                        <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputLastName" runat="server" CssClass="text-danger" />
                                        <label for="inputLastName">Last name</label>
                                    </div>
                                </div>
                            </div>
                            <div class="form-floating mb-3">
                                <%--<input class="form-control" id="inputEmail" type="email" placeholder="name@example.com" />--%>
                                <asp:TextBox runat="server" ID="inputEmail" CssClass="form-control" TextMode="Email" placeholder="name@example.com" />
                                <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputEmail" runat="server" CssClass="text-danger" />
                                <asp:RegularExpressionValidator ID="regexEmail" runat="server"
                                    ControlToValidate="inputEmail"
                                    ValidationExpression="^[^\s@]+@[^\s@]+\.[^\s@]+$"
                                    ErrorMessage="Please enter a valid email address."
                                    Display="Dynamic"
                                    CssClass="text-danger" />
                                <label for="inputEmail">Email address</label>
                            </div>
                            <div class="row mb-3">
                                <div class="col-md-6">
                                    <div class="form-floating mb-3 mb-md-0">
                                        <%--<input class="form-control" id="inputPassword" type="password" placeholder="Create a password" />--%>
                                        <asp:TextBox runat="server" ID="inputPassword" TextMode="Password" CssClass="form-control" placeholder="Create a password" />
                                        <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputPassword" runat="server" CssClass="text-danger" />
                                        <label for="inputPassword">Password</label>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-floating mb-3 mb-md-0">
                                        <%--<input class="form-control" id="inputPasswordConfirm" type="password" placeholder="Confirm password" />--%>
                                        <asp:TextBox runat="server" ID="inputPasswordConfirm" TextMode="Password" CssClass="form-control" placeholder="Confirm password" />
                                        <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputPasswordConfirm" runat="server" CssClass="text-danger" />
                                        <asp:CompareValidator ID="cvConfirmPassword" runat="server"
                                            ControlToValidate="inputPasswordConfirm"
                                            ControlToCompare="inputPassword"
                                            Operator="Equal"
                                            Type="String"
                                            ErrorMessage="Passwords do not match."
                                            Display="Dynamic"
                                            CssClass="text-danger" />
                                        <label for="inputPasswordConfirm">Confirm Password</label>
                                    </div>
                                </div>
                            </div>
                            <div class="mt-4 mb-0">
                                <div class="d-grid">
                                    <%--<a class="btn btn-primary btn-block" href="login.html">Create Account</a>--%>
                                    <asp:Button Text="Create Account" runat="server" ID="btnCreateAccount" CssClass="btn btn-primary btn-block" />
                                </div>
                            </div>
                        </div>
                        <div class="card-footer text-center py-3">
                            <div class="small"><a href="Login.aspx">Have an account? Go to login</a></div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </main>
    <script>
        overrideOnOk(function () {
            document.location.href = "login.aspx";
            // Add your custom logic here
        });

    </script>
</asp:Content>
