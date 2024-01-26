<%@ Page Title="" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="AdminList.aspx.cs" Inherits="HostelManagement.AdminList" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <main>
        <div class="container-fluid px-4">
            <h1 class="mt-4" runat="server" id="title">Admin List</h1>
            <div class="col-md-10 d-flex justify-content-end">
                <asp:HyperLink NavigateUrl="~/AddEditAdmin.aspx" runat="server" CssClass="btn btn-primary" ID="hlAddnew" />
            </div>
            <div class="row justify-content-center mt-3 ms-2 col-md-10">
                <asp:GridView ID="gvAdmin" DataKeyNames="Id" runat="server" AutoGenerateColumns="false"
                    CssClass="table table-bordered " ShowFooter="false" HeaderStyle-Font-Bold="true" OnRowDeleting="gvAdmin_RowDeleting">
                    <Columns>
                        <asp:TemplateField HeaderText="Name">
                            <ItemTemplate>
                                <asp:Label ID="lblFirstName" runat="server" Text='<%#Eval("UserName") %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Email">
                            <ItemTemplate>
                                <asp:Label ID="lblLastName" runat="server" Text='<%#Eval("Email") %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>
                                <asp:HyperLink NavigateUrl='<%# "AddEditAdmin.aspx?UserType="+UserType+"&Id=" + Eval("Id") %>' runat="server" Style="display: inline-block">
                                <span class="material-symbols-outlined">edit_square</span>
                                </asp:HyperLink>
                                <asp:LinkButton runat="server" CommandName="Delete" Text="Delete" CommandArgument='<%# Container.DataItemIndex %>' OnClientClick="return confirm('Are you sure you want to delete this');" ToolTip="Delete">
                                <span class="material-symbols-outlined">delete</span>
                                </asp:LinkButton>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>
    </main>
</asp:Content>
