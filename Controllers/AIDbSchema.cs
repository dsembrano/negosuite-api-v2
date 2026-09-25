using System;
using System.Text.Json;

namespace negosuite_api.Controllers
{
    public class AIDbSchema
    {
        static public string GetDbSchema()
        {
            try
            {
                var schema = new
                {
                    views = new[]
                    {
                        new
                        {
                            name = "salestransaction",
                            description = "Contains all sales transactions from Sales Invoice or Sales Receipt (Sales Receipt is equivalent to Cash Invoice), The currency is in Philippine Peso.",
                            columns = new[]
                            {
                                new { name = "UserConfigId", type = "int", description = "Company Id" },
                                new { name = "ReferenceNo", type = "varchar(50)", description = "Transaction reference" },
                                new { name = "ReferenceDate", type = "datetime", description = "Date of transaction" },
                                new { name = "CustomerId", type = "int", description = "Customer id" },
                                new { name = "CustomerName", type = "varchar(100)", description = "Customer name" },
                                new { name = "Cost", type = "decimal(18,2)", description = "Total cost of sales for this transaction" },
                                new { name = "Amount", type = "decimal(18,2)", description = "Total amount" },
                                new { name = "Balance", type = "decimal(18,2)", description = "Balance amount not paid by customer" },
                                new { name = "DiscountPercent", type = "decimal(5,2)", description = "Discount percentage" },
                                new { name = "DiscountAmount", type = "decimal(18,2)", description = "Discount amount" },
                                new { name = "TaxAmount", type = "decimal(18,2)", description = "Tax amount" },
                                new { name = "Source", type = "varchar(10)", description = "Source of this sales transaction. SI=Sales Invoice, SR=Cash Invoice" },
                                new { name = "SourceName", type = "varchar(50)", description = "Source name of this sales transaction" },
                                new { name = "Status", type = "int", description = "Status of transaction. 1=Active, -1=Deleted or not active" }
                            }
                        },
                        new
                        {
                            name = "salestransactiondetail",
                            description = "Contains sales transactions details which includes the product or service sold.",
                            columns = new[]
                            {
                                new { name = "UserConfigId", type = "int", description = "Company Id" },
                                new { name = "ReferenceNo", type = "varchar(50)", description = "Transaction reference" },
                                new { name = "ReferenceDate", type = "datetime", description = "Date of transaction" },
                                new { name = "CustomerId", type = "int", description = "Customer id" },
                                new { name = "CustomerName", type = "varchar(100)", description = "Customer name" },
                                new { name = "ItemId", type = "int", description = "Item Id" },
                                new { name = "ItemName", type = "varchar(100)", description = "Item name. This is either a product or service name." },
                                new { name = "Quantity", type = "decimal(18,2)", description = "Quantity sold" },
                                new { name = "Cost", type = "decimal(18,2)", description = "Cost per item" },
                                new { name = "Rate", type = "decimal(18,2)", description = "Selling rate or price of item." },
                                new { name = "Amount", type = "decimal(18,2)", description = "Total amount" },                 
                                new { name = "DiscountPercent", type = "decimal(5,2)", description = "Discount percentage" },
                                new { name = "DiscountAmount", type = "decimal(18,2)", description = "Discount amount" },
                                new { name = "TaxAmount", type = "decimal(18,2)", description = "Tax amount" },
                                new { name = "Source", type = "varchar(10)", description = "Source of this sales transaction. SI=Sales Invoice, SR=Cash Invoice" },
                                new { name = "SourceName", type = "varchar(50)", description = "Source name of this sales transaction" },
                                new { name = "Status", type = "int", description = "Status of transaction. 1=Active, -1=Deleted or not active" }
                            }
                        },
                        /*
                        new
                        {
                            name = "inventorysummary",
                            description = "Contains inventory or stock summary information. Data includes tha aggregated inventory balances from inventory transaction details, for all items from start until the current.",
                            columns = new[]
                            {
                                new { name = "UserConfigId", type = "int", description = "Company Id" },
                                new { name = "ItemId", type = "int", description = "Item Id" },
                                new { name = "ItemName", type = "varchar(100)", description = "Item name. This is the inventory item name." },
                                new { name = "LastInCost", type = "decimal(18,2)", description = "Cost of time when last purchased" },
                                new { name = "AverageCost", type = "decimal(18,2)", description = "Average cost of item" },
                                new { name = "ItemReorderPoint", type = "decimal(18,2)", description = "Reorder level of item." },
                                new { name = "InventoryBalance", type = "decimal(18,2)", description = "Current inventory quantity or balance of item" },
                                new { name = "LastPurchasedDate", type = "datetime", description = "Date last pruchase" }
                            }
                        },
                        */
                        new
                        {
                            name = "inventorytransaction",
                            description = "Contains consolidated inventory transaction details from different transaction sources.",
                            columns = new[]
                            {
                                new { name = "UserConfigId", type = "int", description = "Company Id" },
                                new { name = "ReferenceNo", type = "varchar(50)", description = "Transaction reference" },
                                new { name = "ReferenceDate", type = "datetime", description = "Date of transaction" },
                                new { name = "CustomerId", type = "int", description = "Customer id, null or blank if customer is not applicable" },
                                new { name = "CustomerName", type = "varchar(100)", description = "Customer name, null or blank if customer is not applicable" },
                                new { name = "SupplierId", type = "int", description = "Supplier name, null or blank if supplier is not applicable" },
                                new { name = "SupplierName", type = "varchar(100)", description = "Supplier name, null or blank if supplier is not applicable" },
                                new { name = "ItemId", type = "int", description = "Item id" },
                                new { name = "ItemName", type = "varchar(100)", description = "Item name. This is the inventory item name." },
                                new { name = "ItemCost", type = "decimal(18,2)", description = "Item cost during last pruchase" },
                                new { name = "AverageCost", type = "decimal(18,2)", description = "Average cost of item" },
                                new { name = "ItemReorderPoint", type = "decimal(18,2)", description = "Reorder level of item." },
                                new { name = "LastPurchasedDate", type = "datetime", description = "Last pruchased date" },
                                new { name = "Quantity", type = "decimal(18,2)", description = "Transaction quantity. Always positive regardless of IN or OUT." },
                                new { name = "QuantityIn", type = "decimal(18,2)", description = "Quantity IN or added to inventory" },
                                new { name = "QuantityOut", type = "decimal(18,2)", description = "Quantity OUT or subtracted from inventory" },
                                new { name = "Rate", type = "decimal(18,2)", description = "Rate applied for this transaction" },
                                new { name = "Amount", type = "decimal(18,2)", description = "Quantity times rate" },
                                new { name = "Source", type = "varchar(10)", description = "Source of this inventory transaction." },
                                new { name = "SourceName", type = "varchar(50)", description = "Source name of this inventory transaction" },
                                new { name = "TransactionType", type = "varchar(10)", description = "Transaction type either IN or OUT." },
                                new { name = "InventoryLocationId", type = "int", description = "InventoryLocation id" },
                                new { name = "InventoryLocationName", type = "varchar(100)", description = "InventoryLocation name" },
                                new { name = "Notes", type = "text", description = "Transaction notes" }
                            }
                        }
                    }
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                return JsonSerializer.Serialize(schema, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating DB schema: {ex.Message}");
                return "{}";
            }
        }
    }
}
