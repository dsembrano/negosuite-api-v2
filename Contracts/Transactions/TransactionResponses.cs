using System;
using System.Collections.Generic;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Items;

namespace negosuite_api.Contracts.Transactions;

public sealed class TransactionJournalDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime JournalDate { get; set; }
    public int AccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string Nature { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public int? DebtorId { get; set; }
    public int? CreditorId { get; set; }
    public string Notes { get; set; }
    public string Source { get; set; }
    public decimal? CurrencyXrate { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public int? PostedByUserId { get; set; }
    public int? SalesInvoiceId { get; set; }
    public int? SalesInvoicePaymentId { get; set; }
    public int? SalesReceiptId { get; set; }
    public int? BillId { get; set; }
    public int? GeneralJournalId { get; set; }
    public int? PaymentId { get; set; }
    public int? PaymentToJournalEntryId { get; set; }
    public int? InventoryAdjustmentId { get; set; }
    public DateTime? DueDate { get; set; }
    public int? TaxRateId { get; set; }
    public bool? IsComputed { get; set; }
    public string Particular { get; set; }
    public string Payee { get; set; }
    public string Payor { get; set; }
    public string PaymentAdjustmentEntry { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public ItemAccountDto Account { get; set; }
    public CustomerDetailDto Customer { get; set; }
    public SupplierDetailDto Supplier { get; set; }
    public TransactionCreditorDto Creditor { get; set; }
    public TransactionDebtorDto Debtor { get; set; }
    public TaxRateDto TaxRate { get; set; }
    public TransactionJournalDto PaymentToJournalEntry { get; set; }
    public bool? Deleted { get; set; }
}

public sealed class TransactionInventoryLocationDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public bool Status { get; set; }
    public string Notes { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
}

public sealed class TransactionPaymentModeDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
}

public sealed class TransactionCreditorDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Address1 { get; set; }
    public string Address2 { get; set; }
    public string PhoneNo { get; set; }
    public string Email { get; set; }
    public string ContactName { get; set; }
    public decimal? CreditLimit { get; set; }
    public string Tin { get; set; }
    public int CreditorTypeId { get; set; }
    public short IsVat { get; set; }
    public short IsGenericName { get; set; }
    public short IsActive { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public TransactionCreditorTypeDto CreditorType { get; set; }
}

public sealed class TransactionCreditorTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public ICollection<TransactionCreditorDto> Creditors { get; set; } = new List<TransactionCreditorDto>();
}

public sealed class TransactionDebtorDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Address1 { get; set; }
    public string Address2 { get; set; }
    public string PostalCode { get; set; }
    public string PhoneNo { get; set; }
    public string Email { get; set; }
    public string ContactName { get; set; }
    public decimal? CreditLimit { get; set; }
    public string Tin { get; set; }
    public int DebtorTypeId { get; set; }
    public short IsGenericName { get; set; }
    public short IsActive { get; set; }
    public string SLType { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public TransactionDebtorTypeDto DebtorType { get; set; }
}

public sealed class TransactionDebtorTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string SLType { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public ICollection<TransactionDebtorDto> Debtors { get; set; } = new List<TransactionDebtorDto>();
}

public sealed class TransactionCountryDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public ICollection<StateProvinceDto> StateProvinces { get; set; } = new List<StateProvinceDto>();
}
