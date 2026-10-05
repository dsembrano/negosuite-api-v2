using System.Net;
using System.Text.Json;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class SwaggerTests
{
    [Fact]
    public async Task Auth_and_email_routes_publish_separate_requests_without_stored_secret_models()
    {
        using var host = new ApiHost();
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = document.RootElement.GetProperty("paths"); var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var (path, request) in new[] { ("auth/sign-in", "SignInRequest"), ("auth/refresh-access-token", "RefreshAccessTokenRequest"),
            ("auth/change-password", "ChangePasswordRequest"), ("auth/reset-password", "ResetPasswordRequest"), ("email", "SendEmailRequest"),
            ("email/member-invite", "MemberInviteRequest"), ("email/email-confirmation", "EmailConfirmationRequest"), ("email/password-reset", "PasswordResetEmailRequest") })
        {
            var schema = paths.GetProperty("/api/" + path).GetProperty("post").GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
            Assert.Equal("#/components/schemas/" + request, schema.GetProperty("$ref").GetString());
        }
        Assert.False(schemas.GetProperty("AuthUserDto").GetProperty("properties").TryGetProperty("password", out _));
        Assert.False(schemas.TryGetProperty("EmailWorkflowData", out _));
        Assert.Contains(schemas.GetProperty("ResetPasswordRequest").GetProperty("required").EnumerateArray(), p => p.GetString() == "identifier");
        Assert.True(paths.TryGetProperty("/api/auth/app-version", out _)); Assert.True(paths.TryGetProperty("/api/auth/metabase-token", out _));
        Assert.True(paths.TryGetProperty("/api/email/log/{uuid}", out _));
    }

    [Theory]
    [InlineData("accounts", "Account")]
    [InlineData("payment-modes", "PaymentMode")]
    [InlineData("payment-terms", "PaymentTerm")]
    [InlineData("currencies", "Currency")]
    [InlineData("countries", "Country")]
    [InlineData("city-municipalities", "CityMunicipality")]
    [InlineData("industries", "Industry")]
    [InlineData("NavigationItems", "NavigationItem")]
    [InlineData("tax-rates", "TaxRate")]
    [InlineData("discount-types", "DiscountType")]
    [InlineData("responsibility-centers", "ResponsibilityCenter")]
    [InlineData("responsibility-center-types", "ResponsibilityCenterType")]
    [InlineData("inventory-locations", "InventoryLocation")]
    [InlineData("account-categories", "AccountCategory")]
    [InlineData("general-journals", "GeneralJournal")]
    [InlineData("receiving-reports", "ReceivingReport")]
    [InlineData("stock-issuances", "StockIssuance")]
    [InlineData("stock-transfers", "StockTransfer")]
    [InlineData("inventory-adjustments", "InventoryAdjustment")]
    [InlineData("sales-invoices", "SalesInvoice")]
    [InlineData("sales-receipts", "SalesReceipt")]
    [InlineData("sales-invoice-payments", "SalesInvoicePayment")]
    public async Task Transaction_endpoints_use_dto_schemas(string path, string model)
    {
        using var host = new ApiHost();
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = document.RootElement.GetProperty("paths");
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var (verb, suffix, request) in new[] { ("post", "", "CreateRequest"), ("put", "/{id}", "UpdateRequest") })
        {
            var reference = paths.GetProperty("/api/" + path + suffix).GetProperty(verb).GetProperty("requestBody").GetProperty("content")
                .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
            Assert.Equal("#/components/schemas/" + model + request, reference);
            var fields = schemas.GetProperty(model + request).GetProperty("properties");
            Assert.False(fields.TryGetProperty("supplier", out _)); Assert.False(fields.TryGetProperty("customer", out _));
        }
        var response = paths.GetProperty("/api/" + path + "/{id}").GetProperty("get").GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
        Assert.Equal("#/components/schemas/" + model + "DetailDto", response);
    }

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

    [Fact]
    public async Task Bills_and_payments_publish_separate_write_and_detail_contracts()
    {
        using var host = new ApiHost();
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/swagger/v1/swagger.json"));
        var root = document.RootElement;
        var paths = root.GetProperty("paths"); var schemas = root.GetProperty("components").GetProperty("schemas");
        foreach (var (path, name) in new[] { ("/api/bills", "Bill"), ("/api/payments", "Payment"), ("/api/payments/bill", "Payment") })
        {
            foreach (var (verb, suffix, request) in new[] { ("post", "", "CreateRequest"), ("put", "/{id}", "UpdateRequest") })
            {
                var reference = paths.GetProperty(path + suffix).GetProperty(verb).GetProperty("requestBody").GetProperty("content")
                    .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
                Assert.Equal("#/components/schemas/" + name + request, reference);
                var fields = schemas.GetProperty(name + request).GetProperty("properties");
                Assert.True(fields.TryGetProperty("journalEntries", out _));
                Assert.False(fields.TryGetProperty("supplier", out _));
            }
        }
        foreach (var (path, name) in new[] { ("/api/bills/{id}", "Bill"), ("/api/payments/{id}", "Payment") })
        {
            var reference = paths.GetProperty(path).GetProperty("get").GetProperty("responses").GetProperty("200")
                .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
            Assert.Equal("#/components/schemas/" + name + "DetailDto", reference);
        }
        Assert.False(schemas.GetProperty("BillLineRequest").GetProperty("properties").TryGetProperty("item", out _));
        foreach (var name in new[] { "BillJournalRequest", "PaymentJournalRequest" })
        {
            var fields = schemas.GetProperty(name).GetProperty("properties");
            Assert.False(fields.TryGetProperty("account", out _)); Assert.False(fields.TryGetProperty("salesInvoiceId", out _));
        }
    }
}
