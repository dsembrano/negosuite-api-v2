using System;
using System.Collections.Generic;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Items;
namespace negosuite_api.Contracts.Transactions;

public class InventoryAdjustmentWriteRequest
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
    public int? AdjustmentAccountId { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public List<InventoryAdjustmentLineRequest> InventoryAdjustmentDetails { get; set; } = new();
    public List<InventoryAdjustmentJournalRequest> JournalEntries { get; set; } = new();
}
public sealed class InventoryAdjustmentCreateRequest : InventoryAdjustmentWriteRequest { }
public sealed class InventoryAdjustmentUpdateRequest : InventoryAdjustmentWriteRequest { }
public sealed class InventoryAdjustmentJournalRequest : TransactionJournalRequest
{
    public int? InventoryAdjustmentId { get; set; }
}
public sealed class InventoryAdjustmentLineRequest
{
    public int Id { get; set; }
    public int InventoryAdjustmentId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Rate { get; set; }
    public decimal? Amount { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public bool? Deleted { get; set; }
}

public sealed class InventoryAdjustmentDetailDto
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
    public int? AdjustmentAccountId { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public ICollection<InventoryAdjustmentLineDto> InventoryAdjustmentDetails { get; set; }
    public TransactionInventoryLocationDto InventoryLocation { get; set; }
    public ItemAccountDto AdjustmentAccount { get; set; }
    public CustomerDetailDto Customer { get; set; }
    public SupplierDetailDto Supplier { get; set; }
    public ICollection<TransactionJournalDto> JournalEntries { get; set; }
}

public sealed class InventoryAdjustmentLineDto
{
    public int Id { get; set; }
    public int InventoryAdjustmentId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Rate { get; set; }
    public decimal? Amount { get; set; }
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
