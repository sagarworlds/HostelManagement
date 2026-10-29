<%@ Page Title="Password Recovery" Language="C#" MasterPageFile="~/Site1.Master" AutoEventWireup="true" CodeBehind="passwordreset.aspx.cs" Inherits="HostelManagement.passwordreset" %>

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
                            <div class="mb-3 text-muted">
                                Resetting a password from this page is not available yet.
                                If you cannot sign in, please contact your hostel administrator.
                            </div>
                            <div class="d-flex align-items-center justify-content-between mt-4 mb-0">
                                <a class="btn btn-primary" href="Login.aspx">Return to login</a>
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
