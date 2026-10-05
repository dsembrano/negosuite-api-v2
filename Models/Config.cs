using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace negosuite_api.Models
{
    public partial class Config
    {
        public Config()
        {
            TaxRates = new HashSet<TaxRate>();
        }

        public int Id { get; set; }
        public string Uuid { get; set; }
        public string CompanyName { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string PhoneNo { get; set; }
        public string FaxNo { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string Tin { get; set; }
        public int? IndustryId { get; set; }
        public string CompanyAbout { get; set; }       
        public int? CountryId { get; set; }
        public int? ARTradeAccountId { get; set; }        
        public short ARAgingBaseDate { get; set; }
        public bool ARAgingShowCurrent { get; set; }
        public short ARAgingPeriod1 { get; set; }
        public short ARAgingPeriod2 { get; set; }
        public short ARAgingPeriod3 { get; set; }
        public short ARAgingPeriod4 { get; set; }
        public int? APTradeAccountId { get; set; }
        public short APAgingBaseDate { get; set; }
        public bool APAgingShowCurrent { get; set; }
        public short APAgingPeriod1 { get; set; }
        public short APAgingPeriod2 { get; set; }
        public short APAgingPeriod3 { get; set; }
        public short APAgingPeriod4 { get; set; }
        public int? DiscountAccountId { get; set; }
        public int? PurchaseDiscountAccountId { get; set; }        
        public string DateFormat { get; set; }
        public string CompanyLogoURL { get; set; }
        public bool? InvoiceShowShippingAddress {  get; set; }
        public string InvoiceMargin { get; set; }
        public string InvoiceLogoURL { get; set; }
        public string InvoiceLogoPosition { get; set; }
        public string InvoiceLogoStyleClass { get; set; }
        public string InvoiceLogoWidth { get; set; }
        public string InvoiceLogoHeight { get; set; }
        public string InvoiceTemplate { get; set; }
        public string SalesReceiptTemplate { get; set; }
        public string PaymentAdjustmentTypes { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public string PaymentVerifier { get; set; }
        public string PaymentVerifierPosition { get; set; }
        public string PaymentApprover { get; set; }
        public string PaymentApproverPosition { get; set; }
        public string PaymentVoucherTemplate { get; set; }

        public string JournalVoucherVerifier { get; set; }
        public string JournalVoucherVerifierPosition { get; set; }
        public string JournalVoucherApprover { get; set; }
        public string JournalVoucherApproverPosition { get; set; }
        public string JournalVoucherTemplate { get; set; }

        public string IncomeStatementConfig { get; set; }
        public bool ShowAccountCodeInList { get; set; }
        public byte RequireAccountCode { get; set; }        
        public bool? IsTemplate { get; set; }

        public int? SubscriptionPlanId { get; set; }
        public DateTime? SubscriptionDate { get; set; }
        public bool? Trial { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public string BillingMode { get; set; }
        public byte? MaxUserCount { get; set; }

        public Industry Industry { get; set; }
        public Country Country { get; set; }
        public Account ARTradeAccount { get; set; }
        public Account APTradeAccount { get; set; }
        public Account DiscountAccount { get; set; }
        public Account PurchaseDiscountAccount { get; set; }
        public String LandedCostItemsJson { get; set; }
        public String PaymentModes { get; set; }
        public String AutoReferenceNoConfig { get; set; }

        public bool? MetabaseDashboard { get; set; }
        public bool? PointOfSales { get; set; }
        
        [NotMapped]
        public virtual ICollection<TaxRate> TaxRates { get; set; }

        [NotMapped]
        public string TaxRatesJson { get; set; }
    }


    public class AutoReferenceNoConfig
    {
        public bool AutoSIReferenceNo { get; set; }
        public String AutoSIReferenceNoFormat { get; set; }
        public String AutoSIReferenceNoPrefix { get; set; }
        public bool AutoSRReferenceNo { get; set; }
        public String AutoSRReferenceNoFormat { get; set; }
        public String AutoSRReferenceNoPrefix { get; set; }
        public bool AutoPOSReferenceNo { get; set; }
        public String AutoPOSReferenceNoFormat { get; set; }
        public String AutoPOSReferenceNoPrefix { get; set; }

        public static string getFormattedSequenceNo(int sequence, string format)
            => getFormattedSequenceNo(sequence, format, DateTime.Now);

        // Date is explicit for deterministic verification. Allocation and reset policy remain with the caller.
        public static string getFormattedSequenceNo(int sequence, string format, DateTime date)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var sixDigits = sequence.ToString("D6", culture);
            var eightDigits = sequence.ToString("D8", culture);
            return format switch
            {
                "######" => sixDigits,
                "YYYY-######" => date.ToString("yyyy", culture) + "-" + sixDigits,
                "YYYY-########" => date.ToString("yyyy", culture) + "-" + eightDigits,
                "YYYY-MM-######" => date.ToString("yyyy-MM", culture) + "-" + sixDigits,
                "YYYYMM-########" => date.ToString("yyyyMM", culture) + "-" + eightDigits,
                "YYMM-######" => date.ToString("yyMM", culture) + "-" + sixDigits,
                "YYYYMMDD-########" => date.ToString("yyyyMMdd", culture) + "-" + eightDigits,
                // Includes ########, missing/empty legacy settings, and unknown legacy formats.
                _ => eightDigits
            };
        }

    }
}

