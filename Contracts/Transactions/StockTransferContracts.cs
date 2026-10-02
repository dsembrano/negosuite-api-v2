using System;
using System.Collections.Generic;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Items;
namespace negosuite_api.Contracts.Transactions;

public class StockTransferWriteRequest
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
    public int FromInventoryLocationId { get; set; }
    public int ToInventoryLocationId { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public List<StockTransferLineRequest> StockTransferDetails { get; set; } = new();
}
public sealed class StockTransferCreateRequest : StockTransferWriteRequest { }
public sealed class StockTransferUpdateRequest : StockTransferWriteRequest { }
public sealed class StockTransferLineRequest
{
    public int Id { get; set; }
    public int StockTransferId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public bool? Deleted { get; set; }
}

public sealed class StockTransferDetailDto
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
    public int FromInventoryLocationId { get; set; }
    public int ToInventoryLocationId { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public ICollection<StockTransferLineDto> StockTransferDetails { get; set; }
    public TransactionInventoryLocationDto FromInventoryLocation { get; set; }
    public TransactionInventoryLocationDto ToInventoryLocation { get; set; }
}

public sealed class StockTransferLineDto
{
    public int Id { get; set; }
    public int StockTransferId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
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
