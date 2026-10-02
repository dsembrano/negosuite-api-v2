using System.Collections.Generic;
using System.Linq;
using negosuite_api.Contracts.Bills;
using negosuite_api.Contracts.Payments;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Items;
using negosuite_api.Models;

namespace negosuite_api.Contracts.Transactions;

// Explicit mappings retain the JSON contract without exposing tracked entities.
internal sealed partial class TransactionResponseMapping
{
    private readonly Dictionary<object, object> mapped = new(ReferenceEqualityComparer.Instance);

    public BillDetailDto Map(Bill value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (BillDetailDto)existing;
        var result = new BillDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.BillNo = value.BillNo;
        result.BillDate = value.BillDate;
        result.SupplierId = value.SupplierId;
        result.PaymentTermId = value.PaymentTermId;
        result.DueDate = value.DueDate;
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
        result.PaymentTerm = Map(value.PaymentTerm);
        result.BillDetails = value.BillDetails?.Select(Map).ToList();
        result.JournalEntries = value.JournalEntries?.Select(Map).ToList();
        result.InventoryLocation = Map(value.InventoryLocation);
        return result;
    }

    public BillLineDto Map(BillDetail value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (BillLineDto)existing;
        var result = new BillLineDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.BillId = value.BillId;
        result.ItemId = value.ItemId;
        result.Quantity = value.Quantity;
        result.Rate = value.Rate;
        result.DiscountAmount = value.DiscountAmount;
        result.DiscountPercent = value.DiscountPercent;
        result.Amount = value.Amount;
        result.TaxRateId = value.TaxRateId;
        result.TaxAmount = value.TaxAmount;
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
        result.InventoryLocationId = value.InventoryLocationId;
        result.InventoryLocation = Map(value.InventoryLocation);
        result.LandedCostJson = value.LandedCostJson;
        result.LandedCost = value.LandedCost;
        result.Deleted = value.Deleted;
        result.Touched = value.Touched;
        return result;
    }

    public PaymentDetailDto Map(Payment value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (PaymentDetailDto)existing;
        var result = new PaymentDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.ReferenceNo = value.ReferenceNo;
        result.ReferenceDate = value.ReferenceDate;
        result.SupplierId = value.SupplierId;
        result.CustomerId = value.CustomerId;
        result.IsBillPayment = value.IsBillPayment;
        result.Payee = value.Payee;
        result.PaymentModeId = value.PaymentModeId;
        result.CheckNo = value.CheckNo;
        result.PaidThroughAccountId = value.PaidThroughAccountId;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.Amount = value.Amount;
        result.Balance = value.Balance;
        result.PostedDate = value.PostedDate;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Supplier = Map(value.Supplier);
        result.Customer = Map(value.Customer);
        result.PaymentMode = Map(value.PaymentMode);
        result.PaidThroughAccount = Map(value.PaidThroughAccount);
        result.JournalEntries = value.JournalEntries?.Select(Map).ToList();
        result.ResponsibilityCenterEntry = value.ResponsibilityCenterEntry;
        return result;
    }

    public TransactionJournalDto Map(JournalEntry value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (TransactionJournalDto)existing;
        var result = new TransactionJournalDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.ReferenceNo = value.ReferenceNo;
        result.JournalDate = value.JournalDate;
        result.AccountId = value.AccountId;
        result.Amount = value.Amount;
        result.Balance = value.Balance;
        result.Nature = value.Nature;
        result.ResponsibilityCenterEntry = value.ResponsibilityCenterEntry;
        result.CustomerId = value.CustomerId;
        result.SupplierId = value.SupplierId;
        result.DebtorId = value.DebtorId;
        result.CreditorId = value.CreditorId;
        result.Notes = value.Notes;
        result.Source = value.Source;
        result.CurrencyXrate = value.CurrencyXrate;
        result.Status = value.Status;
        result.PostedDate = value.PostedDate;
        result.PostedByUserId = value.PostedByUserId;
        result.SalesInvoiceId = value.SalesInvoiceId;
        result.SalesInvoicePaymentId = value.SalesInvoicePaymentId;
        result.SalesReceiptId = value.SalesReceiptId;
        result.BillId = value.BillId;
        result.GeneralJournalId = value.GeneralJournalId;
        result.PaymentId = value.PaymentId;
        result.PaymentToJournalEntryId = value.PaymentToJournalEntryId;
        result.InventoryAdjustmentId = value.InventoryAdjustmentId;
        result.DueDate = value.DueDate;
        result.TaxRateId = value.TaxRateId;
        result.IsComputed = value.IsComputed;
        result.Particular = value.Particular;
        result.Payee = value.Payee;
        result.Payor = value.Payor;
        result.PaymentAdjustmentEntry = value.PaymentAdjustmentEntry;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Account = Map(value.Account);
        result.Customer = Map(value.Customer);
        result.Supplier = Map(value.Supplier);
        result.Creditor = Map(value.Creditor);
        result.Debtor = Map(value.Debtor);
        result.TaxRate = Map(value.TaxRate);
        result.PaymentToJournalEntry = Map(value.PaymentToJournalEntry);
        result.Deleted = value.Deleted;
        return result;
    }

    public TransactionInventoryLocationDto Map(InventoryLocation value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (TransactionInventoryLocationDto)existing;
        var result = new TransactionInventoryLocationDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.Code = value.Code;
        result.Name = value.Name;
        result.Status = value.Status;
        result.Notes = value.Notes;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        return result;
    }

    public TransactionPaymentModeDto Map(PaymentMode value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (TransactionPaymentModeDto)existing;
        var result = new TransactionPaymentModeDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.Name = value.Name;
        result.IsActive = value.IsActive;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        return result;
    }

    public TransactionCreditorDto Map(Creditor value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (TransactionCreditorDto)existing;
        var result = new TransactionCreditorDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.Code = value.Code;
        result.Name = value.Name;
        result.Address1 = value.Address1;
        result.Address2 = value.Address2;
        result.PhoneNo = value.PhoneNo;
        result.Email = value.Email;
        result.ContactName = value.ContactName;
        result.CreditLimit = value.CreditLimit;
        result.Tin = value.Tin;
        result.CreditorTypeId = value.CreditorTypeId;
        result.IsVat = value.IsVat;
        result.IsGenericName = value.IsGenericName;
        result.IsActive = value.IsActive;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.CreditorType = Map(value.CreditorType);
        return result;
    }

    public TransactionCreditorTypeDto Map(CreditorType value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (TransactionCreditorTypeDto)existing;
        var result = new TransactionCreditorTypeDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.Name = value.Name;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Creditors = value.Creditors?.Select(Map).ToList();
        return result;
    }

    public TransactionDebtorDto Map(Debtor value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (TransactionDebtorDto)existing;
        var result = new TransactionDebtorDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.Code = value.Code;
        result.Name = value.Name;
        result.Address1 = value.Address1;
        result.Address2 = value.Address2;
        result.PostalCode = value.PostalCode;
        result.PhoneNo = value.PhoneNo;
        result.Email = value.Email;
        result.ContactName = value.ContactName;
        result.CreditLimit = value.CreditLimit;
        result.Tin = value.Tin;
        result.DebtorTypeId = value.DebtorTypeId;
        result.IsGenericName = value.IsGenericName;
        result.IsActive = value.IsActive;
        result.SLType = value.SLType;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.DebtorType = Map(value.DebtorType);
        return result;
    }

    public TransactionDebtorTypeDto Map(DebtorType value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (TransactionDebtorTypeDto)existing;
        var result = new TransactionDebtorTypeDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.Name = value.Name;
        result.SLType = value.SLType;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Debtors = value.Debtors?.Select(Map).ToList();
        return result;
    }

    public TransactionCountryDto Map(Country value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (TransactionCountryDto)existing;
        var result = new TransactionCountryDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.Code = value.Code;
        result.Name = value.Name;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.StateProvinces = value.StateProvinces?.Select(Map).ToList();
        return result;
    }

    public SupplierDetailDto Map(Supplier value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (SupplierDetailDto)existing;
        var result = new SupplierDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.Code = value.Code;
        result.Name = value.Name;
        result.Tin = value.Tin;
        result.TaxRateId = value.TaxRateId;
        result.PaymentTermId = value.PaymentTermId;
        result.Notes = value.Notes;
        result.Status = value.Status;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.TaxRate = Map(value.TaxRate);
        result.PaymentTerm = Map(value.PaymentTerm);
        result.SupplierAddresses = value.SupplierAddresses?.Select(Map).ToList();
        result.SupplierContacts = value.SupplierContacts?.Select(Map).ToList();
        return result;
    }

    public SupplierAddressDto Map(SupplierAddress value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (SupplierAddressDto)existing;
        var result = new SupplierAddressDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.SupplierId = value.SupplierId;
        result.AddressLine1 = value.AddressLine1;
        result.AddressLine2 = value.AddressLine2;
        result.CityMunicipalityId = value.CityMunicipalityId;
        result.PostalCode = value.PostalCode;
        result.IsPrimaryAddress = value.IsPrimaryAddress;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.CityMunicipality = Map(value.CityMunicipality);
        result.Deleted = value.Deleted;
        return result;
    }

    public SupplierContactDto Map(SupplierContact value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (SupplierContactDto)existing;
        var result = new SupplierContactDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.SupplierId = value.SupplierId;
        result.Name = value.Name;
        result.Title = value.Title;
        result.Email = value.Email;
        result.PhoneNo = value.PhoneNo;
        result.AlternatePhoneNo = value.AlternatePhoneNo;
        result.AlternateEmail = value.AlternateEmail;
        result.IsPrimary = value.IsPrimary;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Deleted = value.Deleted;
        return result;
    }

    public CustomerDetailDto Map(Customer value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (CustomerDetailDto)existing;
        var result = new CustomerDetailDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.Code = value.Code;
        result.Name = value.Name;
        result.CreditLimit = value.CreditLimit;
        result.Tin = value.Tin;
        result.TaxRateId = value.TaxRateId;
        result.PaymentTermId = value.PaymentTermId;
        result.Notes = value.Notes;
        result.BusinessStyle = value.BusinessStyle;
        result.Status = value.Status;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.TaxRate = Map(value.TaxRate);
        result.PaymentTerm = Map(value.PaymentTerm);
        result.CustomerAddresses = value.CustomerAddresses?.Select(Map).ToList();
        result.CustomerContacts = value.CustomerContacts?.Select(Map).ToList();
        return result;
    }

    public CustomerAddressDto Map(CustomerAddress value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (CustomerAddressDto)existing;
        var result = new CustomerAddressDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.CustomerId = value.CustomerId;
        result.AddressLine1 = value.AddressLine1;
        result.AddressLine2 = value.AddressLine2;
        result.CityMunicipalityId = value.CityMunicipalityId;
        result.PostalCode = value.PostalCode;
        result.IsDeliveryAddress = value.IsDeliveryAddress;
        result.IsBillingAddress = value.IsBillingAddress;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.CityMunicipality = Map(value.CityMunicipality);
        result.Deleted = value.Deleted;
        return result;
    }

    public CustomerContactDto Map(CustomerContact value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (CustomerContactDto)existing;
        var result = new CustomerContactDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.CustomerId = value.CustomerId;
        result.Name = value.Name;
        result.Title = value.Title;
        result.Email = value.Email;
        result.PhoneNo = value.PhoneNo;
        result.AlternatePhoneNo = value.AlternatePhoneNo;
        result.AlternateEmail = value.AlternateEmail;
        result.IsPrimary = value.IsPrimary;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Deleted = value.Deleted;
        return result;
    }

    public CityMunicipalityDto Map(CityMunicipality value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (CityMunicipalityDto)existing;
        var result = new CityMunicipalityDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.Name = value.Name;
        result.StateProvinceId = value.StateProvinceId;
        result.PostalCode = value.PostalCode;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.StateProvince = Map(value.StateProvince);
        return result;
    }

    public StateProvinceDto Map(StateProvince value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (StateProvinceDto)existing;
        var result = new StateProvinceDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.Name = value.Name;
        result.Capital = value.Capital;
        result.CountryId = value.CountryId;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Country = Map(value.Country);
        return result;
    }

    public TaxRateDto Map(TaxRate value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (TaxRateDto)existing;
        var result = new TaxRateDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.Name = value.Name;
        result.Rate = value.Rate;
        result.TaxAccountId = value.TaxAccountId;
        result.SalesAccountId = value.SalesAccountId;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.TaxAccount = Map(value.TaxAccount);
        result.SalesAccount = Map(value.SalesAccount);
        result.ApplyToSalesOrPurchase = value.ApplyToSalesOrPurchase;
        result.Deleted = value.Deleted;
        return result;
    }

    public PaymentTermDto Map(PaymentTerm value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (PaymentTermDto)existing;
        var result = new PaymentTermDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.Code = value.Code;
        result.Name = value.Name;
        result.Days = value.Days;
        result.IsActive = value.IsActive;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        return result;
    }

    public ItemAccountDto Map(Account value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (ItemAccountDto)existing;
        var result = new ItemAccountDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.Code = value.Code;
        result.Name = value.Name;
        result.CategoryId = value.CategoryId;
        result.IsSubAccount = value.IsSubAccount;
        result.ParentAccountId = value.ParentAccountId;
        result.RequireCustomer = value.RequireCustomer;
        result.RequireSupplier = value.RequireSupplier;
        result.Notes = value.Notes;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        result.Category = Map(value.Category);
        result.ParentAccount = Map(value.ParentAccount);
        return result;
    }

    public ItemAccountCategoryDto Map(AccountCategory value)
    {
        if (value == null) return null;
        if (mapped.TryGetValue(value, out var existing)) return (ItemAccountCategoryDto)existing;
        var result = new ItemAccountCategoryDto();
        mapped.Add(value, result);
        result.Id = value.Id;
        result.UserConfigId = value.UserConfigId;
        result.Name = value.Name;
        result.Type = value.Type;
        result.OrderNo = value.OrderNo;
        result.AccountCodePrefix = value.AccountCodePrefix;
        result.CreatedDate = value.CreatedDate;
        result.LastUpdatedDate = value.LastUpdatedDate;
        result.CreatedByUserId = value.CreatedByUserId;
        result.LastUpdatedByUserId = value.LastUpdatedByUserId;
        return result;
    }

    public ItemDetailDto Map(Item value) => ItemMapping.Map(value);
}
