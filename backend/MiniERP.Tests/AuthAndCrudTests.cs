using Microsoft.AspNetCore.Mvc;
using MiniERP.Api.Controllers;

namespace MiniERP.Tests;

public class AuthAndCrudTests
{
    [Fact]
    public void Password_hash_verifies_and_is_salted()
    {
        var h1 = PasswordHasher.Hash("Secret#123"); var h2 = PasswordHasher.Hash("Secret#123");
        Assert.NotEqual(h1, h2);
        Assert.True(PasswordHasher.Verify("Secret#123", h1));
        Assert.False(PasswordHasher.Verify("wrong", h1));
    }

    [Fact]
    public async Task Login_returns_token_and_role_permissions_and_rejects_bad_password()
    {
        var w = await World.Create();
        var admin = w.Db.Roles.First(r => r.Name == "Admin");
        await w.Auth.SaveUser(null, new UserDto { Username = "tester", FullName = "T", RoleId = admin.Id, Password = "Passw0rd!" });
        var res = await w.Auth.Login(new LoginRequest { Username = "tester", Password = "Passw0rd!" });
        Assert.False(string.IsNullOrEmpty(res.Token));
        Assert.Contains("admin.manage", res.User.Permissions);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => w.Auth.Login(new LoginRequest { Username = "tester", Password = "nope" }));
        Assert.Equal(401, ex.Status);
    }

    [Fact]
    public async Task Viewer_role_has_no_manage_permissions()
    {
        var w = await World.Create();
        var roles = await w.Auth.Roles();
        var viewer = roles.First(r => r.Name == "Viewer");
        Assert.DoesNotContain(viewer.Permissions, p => p.EndsWith(".manage") || p == "procurement.approve");
        Assert.Contains("reports.view", viewer.Permissions);
        Assert.Contains("procurement.approve", roles.First(r => r.Name == "Purchase Manager").Permissions);
    }

    [Fact]
    public async Task Product_crud_with_validation_and_soft_delete()
    {
        var w = await World.Create();
        var c = new InventoryController(w.Db, w.Inv).WithPerms("inventory.view", "inventory.manage");
        var p = await c.CreateProduct(new ProductDto { Sku = "X-1", Name = "Widget", CategoryId = w.CatId, UnitId = w.UnitId, ProductType = ProductTypes.TradingProduct, SalePrice = 9 });
        Assert.True(p.Id > 0);
        await Assert.ThrowsAsync<BusinessException>(() => c.CreateProduct(new ProductDto { Sku = "X-1", Name = "Dup", CategoryId = w.CatId, UnitId = w.UnitId }));
        p.Name = "Widget 2"; p.SalePrice = 12;
        var upd = await c.UpdateProduct(p.Id, p);
        Assert.Equal("Widget 2", upd.Name); Assert.Equal(0, upd.AverageCost);
        var list = await c.Products(new PageQuery { Search = "Widget" });
        Assert.Single(list.Items);
        await c.DeleteProduct(p.Id);
        Assert.Empty((await c.Products(new PageQuery { Search = "Widget" })).Items);
    }

    [Fact]
    public async Task Cannot_delete_product_with_stock()
    {
        var w = await World.Create(); await w.Open(w.Pen, w.Main, 5, 10);
        var c = new InventoryController(w.Db, w.Inv).WithPerms("inventory.manage");
        await Assert.ThrowsAsync<BusinessException>(() => c.DeleteProduct(w.Pen));
    }

    [Fact]
    public async Task Supplier_and_customer_crud_enforce_permissions()
    {
        var w = await World.Create();
        var sup = new SuppliersController(w.Db).WithPerms("procurement.view", "procurement.manage");
        Assert.IsType<OkObjectResult>(await sup.Create(new SupplierDto { Code = "S9", Name = "New Supplier" }));
        await Assert.ThrowsAsync<BusinessException>(() => sup.Create(new SupplierDto { Code = "S9", Name = "Dup" }));
        var list = Assert.IsType<OkObjectResult>(await sup.List(new PageQuery { Search = "New" }));
        var id = ((PagedResult<SupplierDto>)list.Value!).Items.Single().Id;
        Assert.IsType<OkObjectResult>(await sup.Update(id, new SupplierDto { Code = "S9", Name = "Renamed" }));
        Assert.IsType<NoContentResult>(await sup.Delete(id));
        Assert.IsType<ForbidResult>(await new SuppliersController(w.Db).WithPerms("procurement.view").Create(new SupplierDto { Code = "S8", Name = "x" }));

        var cus = new CustomersController(w.Db).WithPerms("sales.view", "sales.manage");
        Assert.IsType<OkObjectResult>(await cus.Create(new CustomerDto { Code = "C9", Name = "Buyer", CreditLimit = 5000 }));
        Assert.IsType<ForbidResult>(await new CustomersController(w.Db).WithPerms().List(new PageQuery()));
    }
}
