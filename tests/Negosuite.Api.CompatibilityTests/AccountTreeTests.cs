using negosuite_api.Contracts.Accounts;
using negosuite_api.Services;
using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using negosuite_api.Models;

namespace Negosuite.Api.CompatibilityTests;

public class AccountTreeTests
{
    [MySqlFact] public async Task Http_tree_is_company_scoped_and_preserves_search_paths_and_paging()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        f.Company.RequireAccountCode = 1;
        var child = new Account { UserConfigId=f.Company.Id, Name="Needle child", Code="100", CategoryId=f.Account.CategoryId, ParentAccountId=f.Account.Id, IsSubAccount=true };
        f.Db.Accounts.Add(child); await f.Db.SaveChangesAsync();
        var categories = await f.Host.Client.GetFromJsonAsync<JsonElement>("/api/accounts/tree/categories");
        Assert.Equal(1,categories.GetArrayLength());Assert.Equal(2,categories[0].GetProperty("accountCount").GetInt32());
        var category=f.Account.CategoryId;
        var branch=await f.Host.Client.GetFromJsonAsync<JsonElement>($"/api/accounts/tree/branch?categoryId={category}&limit=1&search=Needle");
        Assert.Equal(1,branch.GetProperty("totalCount").GetInt32());
        var root=branch.GetProperty("items")[0];Assert.Equal(f.Account.Id,root.GetProperty("account").GetProperty("id").GetInt32());Assert.False(root.GetProperty("isMatch").GetBoolean());
        var children=await f.Host.Client.GetFromJsonAsync<JsonElement>($"/api/accounts/tree/branch?categoryId={category}&parentAccountId={f.Account.Id}");
        Assert.Equal(child.Id,children.GetProperty("items")[0].GetProperty("account").GetProperty("id").GetInt32());
        var export=await f.Host.Client.GetFromJsonAsync<JsonElement>("/api/accounts/tree/export?search=Needle");
        Assert.Equal(new[]{f.Account.Id,child.Id},export.EnumerateArray().Select(a=>a.GetProperty("id").GetInt32()));
        Assert.Equal(HttpStatusCode.NotFound,(await f.Host.Client.GetAsync($"/api/accounts/tree/branch?categoryId={category}&parentAccountId={f.ForeignAccount.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Host.Client.GetAsync($"/api/accounts/tree/branch?categoryId={category}&limit=201")).StatusCode);
        Assert.Empty((await f.Host.Client.GetFromJsonAsync<JsonElement>($"/api/accounts/tree/export?categoryId={f.ForeignAccount.CategoryId}")).EnumerateArray());
    }
    private static AccountListDto A(int id, string code, int? parent = null, int category = 1, string name = null) => new() { Id = id, Code = code, Name = name ?? "Account " + id, ParentAccountId = parent, CategoryId = category, CategoryName = "Assets", Type = "Asset" };
    private static AccountCategoryDetailDto C(int id, int order, string name = "Assets") => new() { Id = id, Name = name, OrderNo = order };
    [Fact] public void Categories_follow_report_order_and_name_ties()
    {
        var tree = new AccountTree([], [C(1,2),C(2,1,"Z"),C(3,1,"A")], true, null);
        Assert.Equal(new[]{3,2,1}, tree.Categories.Select(c=>c.Id));
    }
    [Fact] public void Branches_page_siblings_and_export_is_parent_first()
    {
        var tree = new AccountTree([A(1,"200"),A(2,"100"),A(3,"001",1),A(4,"002",3)], [C(1,1)], true, null);
        Assert.Equal(2,tree.Branch(1,null,0,1).TotalCount);
        Assert.Equal(2,tree.Branch(1,null,0,1).Items.Single().Account.Id);
        Assert.Equal(1,tree.Branch(1,null,1,1).Items.Single().ChildCount);
        Assert.Equal(new[]{2,1,3,4},tree.Export(null).Select(a=>a.Id));
    }
    [Fact] public void Search_includes_ancestors_but_not_unmatched_siblings()
    {
        var tree = new AccountTree([A(1,"1"),A(2,"2",1),A(3,"3",2,name:"Match"),A(4,"4",1)], [C(1,1)], true, "match");
        Assert.Equal(1,tree.Categories.Single().MatchCount);
        Assert.False(tree.Branch(1,null,0,50).Items.Single().IsMatch);
        Assert.Equal(new[]{1,2,3},tree.Export(null).Select(a=>a.Id));
    }
    [Fact] public void Hidden_codes_use_names_and_category_filter_applies_to_export()
    {
        var tree = new AccountTree([A(1,"1",name:"Z"),A(2,"2",name:"A"),A(3,"3",category:2)], [C(1,1),C(2,2)], false, null);
        Assert.Equal(new[]{2,1},tree.Export(1).Select(a=>a.Id));
        Assert.False(tree.HasParent(1,3));Assert.False(tree.HasCategory(99));
    }
    [Fact] public void Orphans_cross_category_parents_and_cycles_are_reachable_once()
    {
        var tree = new AccountTree([A(1,"1",2),A(2,"2",1),A(3,"3",99),A(4,"4",5),A(5,"5",category:2)], [C(1,1),C(2,2)], true, null);
        Assert.Equal(5,tree.Export(null).Select(a=>a.Id).Distinct().Count());
        Assert.Equal(5,tree.Export(null).Count);
        Assert.Contains(tree.Branch(1,null,0,50).Items,a=>a.Account.Id==4);
    }
    [Fact] public void No_match_hides_categories_and_descendant_match_retains_entire_path()
    {
        var rows = Enumerable.Range(1,500).Select(id=>A(id,id.ToString(),id==1?null:id-1,name:id==500?"Needle":"Other"));
        var tree = new AccountTree(rows,[C(1,1)],true,"Needle");Assert.Equal(500,tree.Export(null).Count);
        Assert.Empty(new AccountTree(rows,[C(1,1)],true,"Absent").Categories);
    }
}
