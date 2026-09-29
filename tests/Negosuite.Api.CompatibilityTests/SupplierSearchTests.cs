using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class SupplierSearchTests
{
    private static Supplier Row(int id, int company = 42, bool active = true) => new()
    {
        Id = id, UserConfigId = company, Status = active, Name = "Supplier " + id,
        SupplierContacts = new List<SupplierContact> {
            new() { Name = "Accounts clerk", PhoneNo = "0917-123", Email = "clerk@example.test" },
            new() { Name = "Other clerk", PhoneNo = "0917-123" }
        },
        SupplierAddresses = new List<SupplierAddress> {
            new() { AddressLine1 = "Main street", AddressLine2 = "Unit 4", PostalCode = "6001",
                CityMunicipality = new CityMunicipality { Name = "Cebu City", PostalCode = "6000", StateProvince = new StateProvince { Name = "Cebu province" } } }
        }
    };

    [Theory]
    [InlineData("Accounts clerk")]
    [InlineData("0917-123")]
    [InlineData("clerk@example.test")]
    [InlineData("Main street")]
    [InlineData("Unit 4")]
    [InlineData("Cebu City")]
    [InlineData("Cebu province")]
    [InlineData("6001")]
    [InlineData("6000")]
    public void Related_matches_preserve_tenant_status_unique_counts_and_paging(string term)
    {
        var source = new[] { Row(1), Row(2), Row(3, active: false), Row(4, company: 99), new Supplier { Id = 5, UserConfigId = 42, Status = true } }.AsQueryable();
        var scoped = source.Where(c => c.UserConfigId == 42 && c.Status);
        var results = SupplierSearch.Apply(scoped, "  " + term + "  ");
        Assert.Equal(2, results.Count());
        Assert.Equal(new[] { 1, 2 }, results.OrderBy(c => c.Id).Select(c => c.Id));
        Assert.Equal(2, results.OrderBy(c => c.Id).Skip(1).Take(1).Single().Id);
        Assert.Equal(3, SupplierSearch.Apply(source.Where(c => c.UserConfigId == 42), term).Count());
    }

    [Fact]
    public void Empty_and_unmatched_queries_keep_expected_behavior()
    {
        var source = new[] { Row(1), new Supplier { Id = 2 } }.AsQueryable();
        Assert.Equal(2, SupplierSearch.Apply(source, " ").Count());
        Assert.Empty(SupplierSearch.Apply(source, "Not present"));
        Assert.Single(SupplierSearch.Apply(source, "Supplier 1"));
    }

    [Fact]
    public void MySql_translates_related_search_to_exists_before_pagination()
    {
        using var db = new negosuiteContext(new DbContextOptionsBuilder<negosuiteContext>()
            .UseMySQL("Server=127.0.0.1;Database=search_translation_only;User ID=unused;Password=unused;").Options);
        var query = SupplierSearch.Apply(db.Suppliers.Where(c => c.UserConfigId == 42 && c.Status), "clerk");
        var sql = SupplierSorting.Apply(query).Skip(25).Take(25).Select(c => c.Id).ToQueryString();
        Assert.Contains("EXISTS", sql);
        Assert.Contains("SupplierContact", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SupplierAddress", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
    }
}
