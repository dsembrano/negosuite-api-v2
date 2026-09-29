using System.Net;
using System.Text.Json;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class SwaggerTests
{
    [Fact]
    public async Task Complete_api_definition_loads_with_distinct_item_and_category_schemas()
    {
        using var host = new ApiHost();
        var response = await host.Client.GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/items", out _));
        Assert.True(paths.TryGetProperty("/api/item-categories", out _));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var itemCategoryRef = schemas.GetProperty("ItemDetailDto").GetProperty("properties").GetProperty("itemCategory").GetProperty("$ref").GetString();
        var categoryRef = paths.GetProperty("/api/item-categories/{id}").GetProperty("get").GetProperty("responses")
            .GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
        Assert.NotEqual(itemCategoryRef, categoryRef);
        foreach (var reference in new[] { itemCategoryRef, categoryRef })
        {
            var properties = schemas.GetProperty(reference.Split('/').Last()).GetProperty("properties");
            Assert.Equal(8, properties.EnumerateObject().Count());
            Assert.True(properties.TryGetProperty("name", out _));
            Assert.True(properties.TryGetProperty("userConfigId", out _));
        }
    }
}
