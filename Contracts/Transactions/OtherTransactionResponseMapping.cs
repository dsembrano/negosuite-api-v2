using System.Linq;
using negosuite_api.Models;
namespace negosuite_api.Contracts.Transactions;

internal sealed partial class TransactionResponseMapping
{
    public GeneralJournalDetailDto Map(GeneralJournal value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (GeneralJournalDetailDto)existing;
        var result = new GeneralJournalDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.ReferenceNo = value.ReferenceNo;
        result.ReferenceDate = value.ReferenceDate;
        result.ResponsibilityCenterEntry = value.ResponsibilityCenterEntry;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.JournalEntries = value.JournalEntries?.Select(Map).ToList();
        return result;
    }
    public ReceivingReportDetailDto Map(ReceivingReport value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (ReceivingReportDetailDto)existing;
        var result = new ReceivingReportDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.ReferenceNo = value.ReferenceNo;
        result.ReferenceDate = value.ReferenceDate;
        result.SupplierId = value.SupplierId;
        result.LandedCostsJson = value.LandedCostsJson;
        result.CreditAccountId = value.CreditAccountId;
        result.PurchaseOrderNo = value.PurchaseOrderNo;
        result.DeliveryReceiptNo = value.DeliveryReceiptNo;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.DiscountAmount = value.DiscountAmount;
        result.DiscountPercent = value.DiscountPercent;
        result.Amount = value.Amount;
        result.Balance = value.Balance;
        result.Taxes = value.Taxes;
        result.IsTaxExclusive = value.IsTaxExclusive;
        result.DiscountIsBeforeTax = value.DiscountIsBeforeTax;
        result.HasItemLevelDiscount = value.HasItemLevelDiscount;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.ResponsibilityCenterEntry = value.ResponsibilityCenterEntry;
        result.InventoryLocationId = value.InventoryLocationId;
        result.Supplier = Map(value.Supplier);
        result.CreditAccount = Map(value.CreditAccount);
        result.ReceivingReportDetails = value.ReceivingReportDetails?.Select(Map).ToList();
        result.JournalEntries = value.JournalEntries?.Select(Map).ToList();
        result.InventoryLocation = Map(value.InventoryLocation);
        return result;
    }
    public ReceivingReportLineDto Map(ReceivingReportDetail value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (ReceivingReportLineDto)existing;
        var result = new ReceivingReportLineDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.ReceivingReportId = value.ReceivingReportId;
        result.ItemId = value.ItemId;
        result.Quantity = value.Quantity;
        result.Rate = value.Rate;
        result.DiscountAmount = value.DiscountAmount;
        result.DiscountPercent = value.DiscountPercent;
        result.Amount = value.Amount;
        result.TaxRateId = value.TaxRateId;
        result.TaxAmount = value.TaxAmount;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Item = Map(value.Item);
        result.TaxRate = Map(value.TaxRate);
        result.InventoryLocationId = value.InventoryLocationId;
        result.LandedCostJson = value.LandedCostJson;
        result.LandedCost = value.LandedCost;
        result.InventoryLocation = Map(value.InventoryLocation);
        result.Deleted = value.Deleted;
        return result;
    }
    public StockIssuanceDetailDto Map(StockIssuance value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (StockIssuanceDetailDto)existing;
        var result = new StockIssuanceDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.ReferenceNo = value.ReferenceNo;
        result.ReferenceDate = value.ReferenceDate;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.InventoryLocationId = value.InventoryLocationId;
        result.CustomerId = value.CustomerId;
        result.SupplierId = value.SupplierId;
        result.ResponsibilityCenterEntry = value.ResponsibilityCenterEntry;
        result.StockIssuanceDetails = value.StockIssuanceDetails?.Select(Map).ToList();
        result.InventoryLocation = Map(value.InventoryLocation);
        result.Customer = Map(value.Customer);
        result.Supplier = Map(value.Supplier);
        result.JournalEntries = value.JournalEntries?.Select(Map).ToList();
        return result;
    }
    public StockIssuanceLineDto Map(StockIssuanceDetail value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (StockIssuanceLineDto)existing;
        var result = new StockIssuanceLineDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.StockIssuanceId = value.StockIssuanceId;
        result.ItemId = value.ItemId;
        result.Quantity = value.Quantity;
        result.Cost = value.Cost;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Item = Map(value.Item);
        result.Deleted = value.Deleted;
        return result;
    }
    public StockTransferDetailDto Map(StockTransfer value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (StockTransferDetailDto)existing;
        var result = new StockTransferDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.ReferenceNo = value.ReferenceNo;
        result.ReferenceDate = value.ReferenceDate;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.FromInventoryLocationId = value.FromInventoryLocationId;
        result.ToInventoryLocationId = value.ToInventoryLocationId;
        result.ResponsibilityCenterEntry = value.ResponsibilityCenterEntry;
        result.StockTransferDetails = value.StockTransferDetails?.Select(Map).ToList();
        result.FromInventoryLocation = Map(value.FromInventoryLocation);
        result.ToInventoryLocation = Map(value.ToInventoryLocation);
        return result;
    }
    public StockTransferLineDto Map(StockTransferDetail value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (StockTransferLineDto)existing;
        var result = new StockTransferLineDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.StockTransferId = value.StockTransferId;
        result.ItemId = value.ItemId;
        result.Quantity = value.Quantity;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Item = Map(value.Item);
        result.Deleted = value.Deleted;
        return result;
    }
    public InventoryAdjustmentDetailDto Map(InventoryAdjustment value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (InventoryAdjustmentDetailDto)existing;
        var result = new InventoryAdjustmentDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.ReferenceNo = value.ReferenceNo;
        result.ReferenceDate = value.ReferenceDate;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.InventoryLocationId = value.InventoryLocationId;
        result.AdjustmentAccountId = value.AdjustmentAccountId;
        result.CustomerId = value.CustomerId;
        result.SupplierId = value.SupplierId;
        result.ResponsibilityCenterEntry = value.ResponsibilityCenterEntry;
        result.InventoryAdjustmentDetails = value.InventoryAdjustmentDetails?.Select(Map).ToList();
        result.InventoryLocation = Map(value.InventoryLocation);
        result.AdjustmentAccount = Map(value.AdjustmentAccount);
        result.Customer = Map(value.Customer);
        result.Supplier = Map(value.Supplier);
        result.JournalEntries = value.JournalEntries?.Select(Map).ToList();
        return result;
    }
    public InventoryAdjustmentLineDto Map(InventoryAdjustmentDetail value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (InventoryAdjustmentLineDto)existing;
        var result = new InventoryAdjustmentLineDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.InventoryAdjustmentId = value.InventoryAdjustmentId;
        result.ItemId = value.ItemId;
        result.Quantity = value.Quantity;
        result.Rate = value.Rate;
        result.Amount = value.Amount;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Item = Map(value.Item);
        result.Deleted = value.Deleted;
        return result;
    }
    public SalesInvoiceDetailDto Map(SalesInvoice value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (SalesInvoiceDetailDto)existing;
        var result = new SalesInvoiceDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.InvoiceNo = value.InvoiceNo;
        result.InvoiceDate = value.InvoiceDate;
        result.CustomerId = value.CustomerId;
        result.SupplierId = value.SupplierId;
        result.BillingAddress = value.BillingAddress;
        result.BillingContactName = value.BillingContactName;
        result.BillingContactEmail = value.BillingContactEmail;
        result.ShippingAddress = value.ShippingAddress;
        result.ShippingContactName = value.ShippingContactName;
        result.ShippingContactEmail = value.ShippingContactEmail;
        result.PaymentTermId = value.PaymentTermId;
        result.DueDate = value.DueDate;
        result.Notes = value.Notes;
        result.TermsConditions = value.TermsConditions;
        result.Status = value.Status;
        result.DiscountAmount = value.DiscountAmount;
        result.DiscountPercent = value.DiscountPercent;
        result.Amount = value.Amount;
        result.Balance = value.Balance;
        result.Taxes = value.Taxes;
        result.IsTaxExclusive = value.IsTaxExclusive;
        result.DiscountIsBeforeTax = value.DiscountIsBeforeTax;
        result.HasItemLevelDiscount = value.HasItemLevelDiscount;
        result.PurchaseOrderNo = value.PurchaseOrderNo;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Customer = Map(value.Customer);
        result.Supplier = Map(value.Supplier);
        result.PaymentTerm = Map(value.PaymentTerm);
        result.SalesInvoiceDetails = value.SalesInvoiceDetails?.Select(Map).ToList();
        result.JournalEntries = value.JournalEntries?.Select(Map).ToList();
        result.ResponsibilityCenterEntry = value.ResponsibilityCenterEntry;
        result.InventoryLocationId = value.InventoryLocationId;
        result.InventoryLocation = Map(value.InventoryLocation);
        result.AutoReferenceNo = value.AutoReferenceNo;
        return result;
    }
    public SalesInvoiceLineDto Map(SalesInvoiceDetail value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (SalesInvoiceLineDto)existing;
        var result = new SalesInvoiceLineDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.SalesInvoiceId = value.SalesInvoiceId;
        result.ItemId = value.ItemId;
        result.Quantity = value.Quantity;
        result.Cost = value.Cost;
        result.Rate = value.Rate;
        result.DiscountAmount = value.DiscountAmount;
        result.DiscountPercent = value.DiscountPercent;
        result.Amount = value.Amount;
        result.TaxRateId = value.TaxRateId;
        result.TaxAmount = value.TaxAmount;
        result.TaxExemptAmount = value.TaxExemptAmount;
        result.Notes = value.Notes;
        result.IsInventoryTransaction = value.IsInventoryTransaction;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Item = Map(value.Item);
        result.TaxRate = Map(value.TaxRate);
        result.Deleted = value.Deleted;
        result.InventoryLocationId = value.InventoryLocationId;
        result.InventoryLocation = Map(value.InventoryLocation);
        return result;
    }
    public SalesReceiptDetailDto Map(SalesReceipt value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (SalesReceiptDetailDto)existing;
        var result = new SalesReceiptDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.ReceiptNo = value.ReceiptNo;
        result.ReceiptDate = value.ReceiptDate;
        result.CustomerId = value.CustomerId;
        result.BillingAddress = value.BillingAddress;
        result.BillingContactName = value.BillingContactName;
        result.BillingContactEmail = value.BillingContactEmail;
        result.ShippingAddress = value.ShippingAddress;
        result.ShippingContactName = value.ShippingContactName;
        result.ShippingContactEmail = value.ShippingContactEmail;
        result.PaymentModeId = value.PaymentModeId;
        result.DepositToAccountId = value.DepositToAccountId;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.DiscountAmount = value.DiscountAmount;
        result.DiscountPercent = value.DiscountPercent;
        result.Amount = value.Amount;
        result.Balance = value.Balance;
        result.Taxes = value.Taxes;
        result.IsTaxExclusive = value.IsTaxExclusive;
        result.DiscountIsBeforeTax = value.DiscountIsBeforeTax;
        result.HasItemLevelDiscount = value.HasItemLevelDiscount;
        result.PurchaseOrderNo = value.PurchaseOrderNo;
        result.PostedDate = value.PostedDate;
        result.IsPOS = value.IsPOS;
        result.PaymentDetails = value.PaymentDetails;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Customer = Map(value.Customer);
        result.PaymentMode = Map(value.PaymentMode);
        result.DepositToAccount = Map(value.DepositToAccount);
        result.SalesReceiptDetails = value.SalesReceiptDetails?.Select(Map).ToList();
        result.JournalEntries = value.JournalEntries?.Select(Map).ToList();
        result.ResponsibilityCenterEntry = value.ResponsibilityCenterEntry;
        result.InventoryLocationId = value.InventoryLocationId;
        result.InventoryLocation = Map(value.InventoryLocation);
        result.AutoReferenceNo = value.AutoReferenceNo;
        return result;
    }
    public SalesReceiptLineDto Map(SalesReceiptDetail value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (SalesReceiptLineDto)existing;
        var result = new SalesReceiptLineDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.SalesReceiptId = value.SalesReceiptId;
        result.ItemId = value.ItemId;
        result.Quantity = value.Quantity;
        result.Cost = value.Cost;
        result.Rate = value.Rate;
        result.DiscountAmount = value.DiscountAmount;
        result.DiscountPercent = value.DiscountPercent;
        result.Amount = value.Amount;
        result.TaxRateId = value.TaxRateId;
        result.TaxAmount = value.TaxAmount;
        result.TaxExemptAmount = value.TaxExemptAmount;
        result.Notes = value.Notes;
        result.IsInventoryTransaction = value.IsInventoryTransaction;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Item = Map(value.Item);
        result.TaxRate = Map(value.TaxRate);
        result.Deleted = value.Deleted;
        result.InventoryLocationId = value.InventoryLocationId;
        result.InventoryLocation = Map(value.InventoryLocation);
        return result;
    }
    public SalesInvoicePaymentDetailDto Map(SalesInvoicePayment value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (SalesInvoicePaymentDetailDto)existing;
        var result = new SalesInvoicePaymentDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.ReferenceNo = value.ReferenceNo;
        result.ReferenceDate = value.ReferenceDate;
        result.CustomerId = value.CustomerId;
        result.PaymentModeId = value.PaymentModeId;
        result.DepositToAccountId = value.DepositToAccountId;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.Amount = value.Amount;
        result.Balance = value.Balance;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Customer = Map(value.Customer);
        result.PaymentMode = Map(value.PaymentMode);
        result.DepositToAccount = Map(value.DepositToAccount);
        result.JournalEntries = value.JournalEntries?.Select(Map).ToList();
        result.ResponsibilityCenterEntry = value.ResponsibilityCenterEntry;
        return result;
    }
}
