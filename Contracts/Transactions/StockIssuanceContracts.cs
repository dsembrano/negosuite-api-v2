using System;
using System.Collections.Generic;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Items;
namespace negosuite_api.Contracts.Transactions;

public class StockIssuanceWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public int InventoryLocationId { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public List<StockIssuanceLineRequest> StockIssuanceDetails { get; set; } = new();
    public List<StockIssuanceJournalRequest> JournalEntries { get; set; } = new();
}
public sealed class StockIssuanceCreateRequest : StockIssuanceWriteRequest { }
public sealed class StockIssuanceUpdateRequest : StockIssuanceWriteRequest { }
public sealed class StockIssuanceJournalRequest : TransactionJournalRequest
{
}
public sealed class StockIssuanceLineRequest
{
    public int Id { get; set; }
    public int StockIssuanceId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Cost { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public bool? Deleted { get; set; }
}

public sealed class StockIssuanceDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public int InventoryLocationId { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public ICollection<StockIssuanceLineDto> StockIssuanceDetails { get; set; }
    public TransactionInventoryLocationDto InventoryLocation { get; set; }
    public CustomerDetailDto Customer { get; set; }
    public SupplierDetailDto Supplier { get; set; }
    public ICollection<TransactionJournalDto> JournalEntries { get; set; }
}

public sealed class StockIssuanceLineDto
{
    public int Id { get; set; }
    public int StockIssuanceId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Cost { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public ItemDetailDto Item { get; set; }
    public bool? Deleted { get; set; }
}
