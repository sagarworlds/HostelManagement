<%@ Page Title="" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="AddEditAdmin.aspx.cs" Inherits="HostelManagement.AddEditAdmin" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4" id="title" runat="server">Add/Edit Admin</h1>
            <div class="row justify-content-center">
                <div class="col-lg-7">
                    <div class="card border-0 rounded-lg mt-5">

                        <div class="card-body">
                            <div class="row mb-3">
                                <div class="col-md-6">
                                    <div class="form-floating mb-3 mb-md-0">
                                        <asp:TextBox runat="server" ID="inputFirstName" placeholder="Enter your first name" CssClass="form-control" />
                                        <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputFirstName" runat="server" CssClass="text-danger" />
                                        <label for="inputFirstName">First name</label>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-floating">
                                        <asp:TextBox runat="server" ID="inputLastName" CssClass="form-control" placeholder="Enter your last name" />
                                        <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputLastName" runat="server" CssClass="text-danger" />
                                        <label for="inputLastName">Last name</label>
                                    </div>
                                </div>
                            </div>
                            <div class="form-floating mb-3">
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
                            <asp:Panel runat="server" ID="pnlPassword" CssClass="row mb-3">
                                <div class="col-md-6">
                                    <div class="form-floating mb-3 mb-md-0">
                                        <asp:TextBox runat="server" ID="inputPassword" TextMode="Password" CssClass="form-control" placeholder="Create a password" />
                                        <asp:RequiredFieldValidator ErrorMessage="Required" ControlToValidate="inputPassword" runat="server" CssClass="text-danger" />
                                        <label for="inputPassword">Password</label>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-floating mb-3 mb-md-0">
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
                            </asp:Panel>
                            <div class="mt-4 mb-0">
                                <div class="d-grid gap-2 d-md-flex justify-content-md-end">
                                    <asp:Button Text="Add Admin" runat="server" ID="btnCreateAccount" CssClass="btn btn-primary" />
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </main>
</asp:Content>
