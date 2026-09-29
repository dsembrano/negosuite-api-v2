using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Controllers;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class CustomerSortingTests
{
    public static IEnumerable<object[]> SortCases() =>
        from field in new[] { "name", "tin", "taxRateName", "paymentTermName", "creditLimit", "status" }
        from direction in new[] { "asc", "desc" }
        select new object[] { field, direction };

    private static Customer Row(int id, bool high) => new()
    {
        Id = id, Name = high ? "Zulu" : "Alpha", Tin = high ? "Z" : "A",
        TaxRate = new TaxRate { Name = high ? "Zulu tax" : "Alpha tax" },
        PaymentTerm = new PaymentTerm { Name = high ? "Zulu term" : "Alpha term" },
        CreditLimit = high ? 10m : 2m, Status = high
    };

    [Theory]
    [MemberData(nameof(SortCases))]
    public void Sorts_entire_dataset_before_paging_with_id_ties(string field, string direction)
    {
        var rows = new[] { Row(4, false), Row(3, true), Row(2, false), Row(1, true) }.AsQueryable();
        var sorted = CustomerSorting.Apply(rows, field, direction);
        var expected = direction == "asc" ? new[] { 2, 4, 1, 3 } : new[] { 1, 3, 2, 4 };
        Assert.Equal(expected, sorted.Select(c => c.Id));
        Assert.Equal(expected.Take(2), sorted.Take(2).Select(c => c.Id));
        Assert.Equal(expected.Skip(2), sorted.Skip(2).Take(2).Select(c => c.Id));
    }

    [Theory]
    [InlineData("tin")]
    [InlineData("taxRateName")]
    [InlineData("paymentTermName")]
    [InlineData("creditLimit")]
    public void Nullable_fields_sort_without_errors(string field)
    {
        var rows = new[] { Row(3, true), new Customer { Id = 2 }, new Customer { Id = 1 } }.AsQueryable();
        Assert.Equal(new[] { 1, 2, 3 }, CustomerSorting.Apply(rows, field, "asc").Select(c => c.Id));
        Assert.Equal(new[] { 3, 1, 2 }, CustomerSorting.Apply(rows, field, "desc").Select(c => c.Id));
    }

    [Fact]
    public void Omitted_parameters_preserve_name_ascending()
    {
        Assert.True(CustomerSorting.IsValid(null, null));
        var rows = new[] { Row(3, true), Row(2, false), Row(1, false) }.AsQueryable();
        Assert.Equal(new[] { 1, 2, 3 }, CustomerSorting.Apply(rows).Select(c => c.Id));
    }

    [Theory]
    [InlineData("unknown", "asc")]
    [InlineData("Name DESC; DROP TABLE customers", "asc")]
    [InlineData("name", "up")]
    [InlineData("", "asc")]
    [InlineData("name", "")]
    public async Task Unsupported_sort_returns_bad_request_without_querying_database(string field, string direction)
    {
        using var db = Context();
        var controller = new CustomersController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.Items[ConfigUuidFilter.CompanyIdKey] = 42;
        Assert.False(CustomerSorting.IsValid(field, direction));
        Assert.IsType<BadRequestObjectResult>(await controller.GetCustomers("{\"userConfigId\":42}", 1, 25, sortBy: field, sortDirection: direction));
    }

    [Theory]
    [MemberData(nameof(SortCases))]
    public void MySql_provider_translates_sort_and_paging_without_client_evaluation(string field, string direction)
    {
        using var db = Context();
        var sql = CustomerSorting.Apply(db.Customers.Where(c => c.UserConfigId == 42), field, direction)
            .Skip(25).Take(25).Select(c => c.Id).ToQueryString();
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.True(sql.IndexOf("ORDER BY", StringComparison.Ordinal) < sql.IndexOf("LIMIT", StringComparison.Ordinal));
        if (direction == "desc") Assert.Contains("DESC", sql);
    }

    private static negosuiteContext Context() => new(new DbContextOptionsBuilder<negosuiteContext>()
        .UseMySQL("Server=127.0.0.1;Database=sort_translation_only;User ID=unused;Password=unused;").Options);
}
