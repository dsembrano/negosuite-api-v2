using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;

#nullable disable

namespace negosuite_api.Models
{
    public partial class negosuiteContext : DbContext
    {
        public negosuiteContext()
        {
        }

        public negosuiteContext(DbContextOptions<negosuiteContext> options)
            : base(options)
        {
        }
        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<AccountCategory> AccountCategories { get; set; }
        public virtual DbSet<AccountTotal> AccountTotals { get; set; }
        public virtual DbSet<Address> Addresses { get; set; }
        public virtual DbSet<AgingPeriod> AgingPeriods { get; set; }
        public virtual DbSet<AppaymentDetail> AppaymentDetails { get; set; }
        public virtual DbSet<ArpaymentDetail> ArpaymentDetails { get; set; }
        public virtual DbSet<CityMunicipality> CityMunicipalities { get; set; }
        public virtual DbSet<Config> Configs { get; set; }
        public virtual DbSet<Contact> Contacts { get; set; }
        public virtual DbSet<ContactPhone> ContactPhones { get; set; }
        public virtual DbSet<Country> Countries { get; set; }
        public virtual DbSet<Creditor> Creditors { get; set; }
        public virtual DbSet<CreditorType> CreditorTypes { get; set; }
        public virtual DbSet<Currency> Currencies { get; set; }
        public virtual DbSet<Customer> Customers { get; set; }
        public virtual DbSet<CustomerAddress> CustomerAddresses { get; set; }
        public virtual DbSet<CustomerContact> CustomerContacts { get; set; }
        public virtual DbSet<Debtor> Debtors { get; set; }
        public virtual DbSet<DebtorType> DebtorTypes { get; set; }
        public virtual DbSet<GeneralJournal> GeneralJournals { get; set; }
        public virtual DbSet<SPGeneralJournal> SPGeneralJournals { get; set; }
        public virtual DbSet<GeneralLedger> GeneralLedgers { get; set; }
        public virtual DbSet<Industry> Industries { get; set; }
        public virtual DbSet<Item> Items { get; set; }
        public virtual DbSet<ItemCategory> ItemCategories { get; set; }
        public virtual DbSet<JournalEntry> JournalEntries { get; set; }
        public virtual DbSet<PaymentTerm> PaymentTerms { get; set; }
        public virtual DbSet<PaymentMode> PaymentModes { get; set; }
        public virtual DbSet<ResponsibilityCenter> ResponsibilityCenters { get; set; }
        public virtual DbSet<ResponsibilityCenterLedger> ResponsibilityCenterLedgers { get; set; }
        public virtual DbSet<ResponsibilityCenterType> ResponsibilityCenterTypes { get; set; }
        public virtual DbSet<Bill> Bills { get; set; }
        public virtual DbSet<SPBill> SPBills { get; set; }
        public virtual DbSet<BillDetail> BillDetails { get; set; }
        public virtual DbSet<BillPayment> BillPayments { get; set; }
        public virtual DbSet<ExpensePayment> ExpensePayments { get; set; }
        public virtual DbSet<Payment> Payments { get; set; }
        public virtual DbSet<SPPayment> SPPayments { get; set; }
        public virtual DbSet<SalesInvoice> SalesInvoices { get; set; }
        public virtual DbSet<SPSalesInvoice> SPSalesInvoices { get; set; }
        public virtual DbSet<SalesInvoicePayment> SalesInvoicePayments { get; set; }
        public virtual DbSet<SPSalesInvoicePayment> SPSalesInvoicePayments { get; set; }
        public virtual DbSet<SalesInvoiceDetail> SalesInvoiceDetails { get; set; }
        public virtual DbSet<SalesReceipt> SalesReceipts { get; set; }
        public virtual DbSet<SPSalesReceipt> SPSalesReceipts { get; set; }
        public virtual DbSet<SalesReceiptDetail> SalesReceiptDetails { get; set; }
        public virtual DbSet<StateProvince> StateProvinces { get; set; }
        public virtual DbSet<Supplier> Suppliers { get; set; }
        public virtual DbSet<SupplierAddress> SupplierAddresses { get; set; }
        public virtual DbSet<SupplierContact> SupplierContacts { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserRole> UserRoles { get; set; }
        public virtual DbSet<UserType> UserTypes { get; set; }
        public virtual DbSet<UserLog> UserLogs { get; set; }
        public virtual DbSet<EmailLog> EmailLogs { get; set; }
        public virtual DbSet<Vcitymunicipality> Vcitymunicipalities { get; set; }
        public virtual DbSet<VoucherConfig> VoucherConfigs { get; set; }
        public virtual DbSet<VoucherControlNo> VoucherControlNos { get; set; }
        public virtual DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public virtual DbSet<SPInventoryTransaction> SPInventoryTransactions { get; set; }
        public virtual DbSet<InventoryStockSummary> InventoryStockSummaries { get; set; }
        public virtual DbSet<InventoryLocation> InventoryLocations { get; set; }
        public virtual DbSet<SalesTransaction> SalesTransactions { get; set; }
        public virtual DbSet<SalesTransactionDetail> SalesTransactionDetails { get; set; }
        public virtual DbSet<StockTransfer> StockTransfers { get; set; }
        public virtual DbSet<SPStockTransfer> SPStockTransfers { get; set; }
        public virtual DbSet<StockTransferDetail> StockTransferDetails { get; set; }
        public virtual DbSet<StockIssuance> StockIssuances { get; set; }
        public virtual DbSet<SPStockIssuance> SPStockIssuances { get; set; }
        public virtual DbSet<StockIssuanceDetail> StockIssuanceDetails { get; set; }
        public virtual DbSet<InventoryAdjustment> InventoryAdjustments { get; set; }
        public virtual DbSet<SPInventoryAdjustment> SPInventoryAdjustments { get; set; }
        public virtual DbSet<InventoryAdjustmentDetail> InventoryAdjustmentDetails { get; set; }
        public virtual DbSet<ResponsibilityCenterJournalEntry> ResponsibilityCenterJournalEntries { get; set; }
        public virtual DbSet<JournalEntrySummary> JournalEntrySummaries { get; set; }
        public virtual DbSet<SalesTransactionFunction> SalesTransactionFunction { get; set; }
        public virtual DbSet<TaxRate> TaxRates { get; set; }
        public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public virtual DbSet<NavigationItem> NavigationItems { get; set; }
        public virtual DbSet<AccountRecap> AccountRecaps { get; set; }
        public virtual DbSet<GeneralLedgerRecap> GeneralLedgerRecaps { get; set; }
        public virtual DbSet<GeneralLedgerDetails> GeneralLedgerDetails { get; set; }
        public virtual DbSet<ItemSale> ItemSales { get; set; }
        public virtual DbSet<CustomerSale> CustomerSales { get; set; }
        public virtual DbSet<ReceivingReport> ReceivingReports { get; set; }
        public virtual DbSet<ReceivingReportDetail> ReceivingReportDetails { get; set; }
        public virtual DbSet<SPReceivingReport> SPReceivingReports { get; set; }
        public virtual DbSet<PivotedInventoryWithJsonArray> PivotedInventoryWithJsonArray { get; set; }
        public virtual DbSet<AppVersion> AppVersions { get; set; }
        public virtual DbSet<SalesMonthlyTrend> SalesMonthlyTrend { get; set; }
        public virtual DbSet<ChatSession> ChatSessions { get; set; }
        public virtual DbSet<ChatMessage> ChatMessages { get; set; }
        public virtual DbSet<DiscountType> DiscountTypes { get; set; }
        public virtual DbSet<TransactionSequence> TransactionSequences { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                //optionsBuilder.UseMySQL("server=localhost;port=3306;user=negosuite;password=negosuitepasswd;database=negosuite");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("account");

                entity.HasIndex(e => e.ParentAccountId, "FK_Account_Account_idx");

                entity.HasIndex(e => e.CategoryId, "FK_Account_Category");

                entity.Property(e => e.Code)
                    .HasMaxLength(20);

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(e => e.RequireCustomer).HasColumnName("RequireCustomer");

                entity.Property(e => e.RequireSupplier).HasColumnName("RequireSupplier");

                entity.HasOne(d => d.Category)
                    .WithMany()
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Account_Category");

                entity.HasOne(d => d.ParentAccount)
                    .WithMany()
                    .HasForeignKey(d => d.ParentAccountId)
                    .HasConstraintName("FK_Account_Account2");
            });

            modelBuilder.Entity<AccountTotal>(entity =>
            {
                entity.ToTable("accounttotal");

                entity.Property(e => e.Debit)
                    .HasColumnType("decimal(20,4)")
                    .HasColumnName("Debit");

                entity.Property(e => e.Credit)
                    .HasColumnType("decimal(20,4)")
                    .HasColumnName("Credit");

                entity.HasOne(d => d.Account)
                    .WithMany()
                    .HasForeignKey(d => d.AccountId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_AccountMonthTotal_Account");
            });

            modelBuilder.Entity<Address>(entity =>
            {
                entity.ToTable("address");

                entity.HasIndex(e => e.CityMunicipalityId, "FK_Address_CityMunicipality_idx");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Line1)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(e => e.Line2).HasMaxLength(150);

                entity.Property(e => e.PostalCode).HasMaxLength(20);

            });

            modelBuilder.Entity<AgingPeriod>(entity =>
            {
                entity.ToTable("agingPeriod");

                entity.Property(e => e.Name).HasMaxLength(150);
            });

            modelBuilder.Entity<AppaymentDetail>(entity =>
            {
                entity.ToTable("appaymentdetail");

                entity.HasIndex(e => e.PayableJournalEntryId, "FK_APPaymentDetail_JournalEntry1");

                entity.HasIndex(e => e.PaymentJournalEntryId, "FK_APPaymentDetail_JournalEntry2");

                entity.Property(e => e.Amount).HasColumnType("decimal(20,4)");

            });

            modelBuilder.Entity<ArpaymentDetail>(entity =>
            {
                entity.ToTable("arpaymentdetail");

                entity.HasIndex(e => e.PaymentJournalEntryId, "FK_ARPaymentDetail_JournalEntry1");

                entity.HasIndex(e => e.ReceivableJournalEntryId, "FK_ARPaymentDetail_JournalEntry2");

                entity.Property(e => e.Amount).HasColumnType("decimal(20,4)");

            });

            modelBuilder.Entity<AccountCategory>(entity =>
            {
                entity.ToTable("accountcategory");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(e => e.Type)
                    .IsRequired()
                    .HasMaxLength(10)
                    .IsFixedLength(true);
            });

            modelBuilder.Entity<CityMunicipality>(entity =>
            {
                entity.ToTable("citymunicipality");

                entity.HasIndex(e => e.StateProvinceId, "FK_CityMunicipality_StateProvince");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.HasOne(d => d.StateProvince)
                    .WithMany()
                    .HasForeignKey(d => d.StateProvinceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_CityMunicipality_StateProvince");
            });

            modelBuilder.Entity<Config>(entity =>
            {
                entity.ToTable("config");

                entity.Property(e => e.Address1).HasMaxLength(150);

                entity.Property(e => e.Address2).HasMaxLength(150);

                entity.Property(e => e.CompanyName)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(e => e.Email).HasMaxLength(50);

                entity.Property(e => e.FaxNo).HasMaxLength(50);

                entity.Property(e => e.PhoneNo).HasMaxLength(50);

                entity.Property(e => e.Tin)
                    .HasMaxLength(50)
                    .HasColumnName("TIN");
            });

            modelBuilder.Entity<Contact>(entity =>
            {
                entity.ToTable("contact");

                entity.Property(e => e.AlternateEmail).HasMaxLength(100);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Email).HasMaxLength(100);

                entity.Property(e => e.FirstName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Lastname).HasMaxLength(100);

                entity.Property(e => e.Title).HasMaxLength(100);
            });

            modelBuilder.Entity<ContactPhone>(entity =>
            {
                entity.ToTable("contactphone");

                entity.HasIndex(e => e.ContactId, "FK_ContactPhone_Contact");

                entity.Property(e => e.ContactNumber)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.HasOne(d => d.Contact)
                    .WithMany(p => p.ContactPhones)
                    .HasForeignKey(d => d.ContactId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_ContactPhone_Contact");
            });

            modelBuilder.Entity<Country>(entity =>
            {
                entity.ToTable("country");

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(10);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);
            });

            modelBuilder.Entity<Creditor>(entity =>
            {
                entity.ToTable("creditor");

                entity.HasIndex(e => e.CreditorTypeId, "FK_Creditor_CreditorType");

                entity.Property(e => e.Address1).HasMaxLength(150);

                entity.Property(e => e.Address2).HasMaxLength(150);

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.ContactName).HasMaxLength(50);

                entity.Property(e => e.CreditLimit).HasColumnType("decimal(20,4)");

                entity.Property(e => e.Email).HasMaxLength(50);

                entity.Property(e => e.IsVat).HasColumnName("IsVAT");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.PhoneNo).HasMaxLength(50);

                entity.Property(e => e.Tin)
                    .HasMaxLength(50)
                    .HasColumnName("TIN");

                entity.HasOne(d => d.CreditorType)
                    .WithMany(p => p.Creditors)
                    .HasForeignKey(d => d.CreditorTypeId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Creditor_CreditorType");
            });

            modelBuilder.Entity<CreditorType>(entity =>
            {
                entity.ToTable("creditortype");

                entity.Property(e => e.Name).HasMaxLength(50);
            });

            modelBuilder.Entity<Currency>(entity =>
            {
                entity.ToTable("currency");

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(10)
                    .IsFixedLength(true);

                entity.Property(e => e.ExchangeRate).HasColumnType("decimal(10,4)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(40)
                    .IsFixedLength(true);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("customer");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(e => e.Notes).HasColumnType("longtext");

                entity.Property(e => e.Tin)
                    .HasMaxLength(50)
                    .HasColumnName("TIN");

                entity.Property(e => e.TaxRateId).HasColumnName("TaxRateId");

            });

            modelBuilder.Entity<CustomerAddress>(entity =>
            {
                entity.ToTable("customeraddress");

                entity.HasIndex(e => e.CustomerId, "FK_CustomerAddress_Customer");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.PostalCode).HasMaxLength(20);

            });

            modelBuilder.Entity<CustomerContact>(entity =>
            {
                entity.ToTable("customercontact");

                entity.HasIndex(e => e.CustomerId, "FK_CustomerContact_Customer");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

            });

            modelBuilder.Entity<Debtor>(entity =>
            {
                entity.ToTable("debtor");

                entity.HasIndex(e => e.DebtorTypeId, "FK_Debtor_DebtorType");

                entity.Property(e => e.Address1).HasMaxLength(150);

                entity.Property(e => e.Address2).HasMaxLength(150);

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.ContactName).HasMaxLength(50);

                entity.Property(e => e.Email).HasMaxLength(50);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.PhoneNo).HasMaxLength(50);

                entity.Property(e => e.PostalCode).HasMaxLength(20);

                entity.Property(e => e.Tin)
                    .HasMaxLength(50)
                    .HasColumnName("TIN");

                entity.HasOne(d => d.DebtorType)
                    .WithMany(p => p.Debtors)
                    .HasForeignKey(d => d.DebtorTypeId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Debtor_DebtorType");
            });

            modelBuilder.Entity<DebtorType>(entity =>
            {
                entity.ToTable("debtortype");

                entity.Property(e => e.Name).HasMaxLength(50);
            });

            modelBuilder.Entity<GeneralJournal>(entity =>
            {
                entity.ToTable("generaljournal");

                entity.HasIndex(e => e.ReferenceNo, "IX_GeneralJournal_ReferenceNo")
                    .IsUnique();

                entity.Property(e => e.ReferenceDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Notes).HasColumnType("longtext");

                entity.Property(e => e.ReferenceNo)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsFixedLength(true);
            });

            modelBuilder.Entity<GeneralLedger>(entity =>
            {
                entity.ToTable("generalledger");

                entity.Property(e => e.BalanceEnd).HasColumnType("decimal(20,4)");

                entity.Property(e => e.BeginBalance).HasColumnType("decimal(20,4)");

                entity.Property(e => e.CreatedBy).HasMaxLength(50);

                entity.Property(e => e.DateCreated).HasColumnType("datetime(6)");

                entity.Property(e => e.DateEdited).HasColumnType("datetime(6)");

                entity.Property(e => e.DateProcessed).HasColumnType("datetime(6)");

                entity.Property(e => e.EditedBy).HasMaxLength(50);

                entity.Property(e => e.Period).HasColumnType("datetime(6)");

                entity.Property(e => e.TotalCredit).HasColumnType("decimal(20,4)");

                entity.Property(e => e.TotalDebit).HasColumnType("decimal(20,4)");
            });

            modelBuilder.Entity<Industry>(entity =>
            {
                entity.ToTable("industry");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);
            });

            modelBuilder.Entity<Item>(entity =>
            {
                entity.ToTable("item");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.HasOne(e => e.PurchaseAccount)
                    .WithMany()
                    .HasForeignKey(d => d.PurchaseAccountId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Item_Purchase_Account");

                entity.HasOne(e => e.PurchaseTaxRate)
                    .WithMany()
                    .HasForeignKey(d => d.PurchaseTaxRateId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Item_Purchse_TaxRate");

                entity.HasOne(e => e.SalesAccount)
                    .WithMany()
                    .HasForeignKey(d => d.SalesAccountId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Item_Sales_Account");

                entity.HasOne(e => e.SalesTaxRate)
                    .WithMany()
                    .HasForeignKey(d => d.SalesTaxRateId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Item_Sales_TaxRate");

            });

            modelBuilder.Entity<JournalEntry>(entity =>
            {
                entity.ToTable("journalentry");

                entity.HasIndex(e => e.AccountId, "FK_JournalEntry_Account");

                entity.HasIndex(e => e.CreditorId, "FK_JournalEntry_Creditor");

                entity.HasIndex(e => e.DebtorId, "FK_JournalEntry_Debtor");

                entity.Property(e => e.Amount).HasColumnType("decimal(20,4)");

                entity.Property(e => e.CurrencyXrate)
                    .HasColumnType("decimal(10,4)")
                    .HasColumnName("CurrencyXRate");

                entity.Property(e => e.Nature)
                    .IsRequired()
                    .HasMaxLength(1)
                    .IsFixedLength(true);

                entity.Property(e => e.JournalDate).HasColumnType("datetime(6)");

                entity.Property(e => e.ReferenceNo)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Balance)
                    .HasColumnType("decimal(20,4)")
                    .HasColumnName("Balance");

                entity.Property(e => e.Source)
                    .IsRequired()
                    .HasMaxLength(4);

                entity.HasOne(d => d.Account)
                    .WithMany()
                    .HasForeignKey(d => d.AccountId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_JournalEntry_Account");

                entity.HasOne(d => d.Creditor)
                    .WithMany()
                    .HasForeignKey(d => d.CreditorId)
                    .HasConstraintName("FK_JournalEntry_Creditor");

                entity.HasOne(d => d.Debtor)
                    .WithMany()
                    .HasForeignKey(d => d.DebtorId)
                    .HasConstraintName("FK_JournalEntry_Debtor");

                entity.HasOne(d => d.PaymentToJournalEntry)
                    .WithMany()
                    .HasForeignKey(d => d.PaymentToJournalEntryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_JournalEntry_PaymentToJournalEntry");
            });


            modelBuilder.Entity<PaymentTerm>(entity =>
            {
                entity.ToTable("paymentterm");
            });


            modelBuilder.Entity<PaymentMode>(entity =>
            {
                entity.ToTable("paymentmode");
            });

            modelBuilder.Entity<ResponsibilityCenter>(entity =>
            {
                entity.ToTable("responsibilitycenter");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<ResponsibilityCenterType>(entity =>
            {
                entity.ToTable("responsibilitycentertype");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<ResponsibilityCenterLedger>(entity =>
            {
                entity.ToTable("responsibilitycenterledger");

                entity.Property(e => e.BalanceEnd).HasColumnType("decimal(20,4)");

                entity.Property(e => e.BeginBalance).HasColumnType("decimal(20,4)");

                entity.Property(e => e.DateProcessed).HasColumnType("datetime(6)");

                entity.Property(e => e.Period).HasColumnType("datetime(6)");

                entity.Property(e => e.TotalCredit).HasColumnType("decimal(20,4)");

                entity.Property(e => e.TotalDebit).HasColumnType("decimal(20,4)");
            });

            modelBuilder.Entity<SalesInvoice>(entity =>
            {
                entity.ToTable("salesinvoice");

                entity.HasIndex(e => e.CustomerId, "FK_SalesInvoice_Customer");

                entity.HasIndex(e => e.InvoiceNo, "IX_SalesInvoice")
                    .IsUnique();

                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(20,4)")
                    .HasDefaultValueSql("'0.0000'");

                entity.Property(e => e.DueDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Notes).HasColumnType("longtext");

                entity.Property(e => e.InvoiceDate).HasColumnType("datetime(6)");

                entity.Property(e => e.InvoiceNo)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasOne(d => d.Customer)
                    .WithMany()
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SalesInvoice_Customer");

                entity.HasOne(d => d.PaymentTerm)
                    .WithMany()
                    .HasForeignKey(d => d.PaymentTermId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SalesInvoice_PaymenTerm");

            });

            modelBuilder.Entity<SalesInvoiceDetail>(entity =>
            {
                entity.ToTable("salesinvoicedetail");

                entity.HasIndex(e => e.ItemId, "FK_SalesInvoiceDetail_Item_idx");

                entity.HasIndex(e => e.SalesInvoiceId, "FK_SalesInvoiceDetail_SalesInvoice_idx");

                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(20,4)");

            });

            modelBuilder.Entity<SalesReceipt>(entity =>
            {
                entity.ToTable("salesreceipt");               
            });

            modelBuilder.Entity<SalesReceiptDetail>(entity =>
            {
                entity.ToTable("salesreceiptdetail");

                entity.HasIndex(e => e.ItemId, "FK_SalesReceiptDetail_Item_idx");

                entity.HasIndex(e => e.SalesReceiptId, "FK_SalesReceiptDetail_SalesReceipt_idx");

                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(20,4)");

            });

            modelBuilder.Entity<SalesInvoicePayment>(entity =>
            {
                entity.ToTable("salesinvoicepayment");

                entity.HasIndex(e => e.CustomerId, "FK_SalesInvoice_Customer");

                entity.HasOne(d => d.Customer)
                    .WithMany()
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SalesInvoicePayment_Customer");

                entity.HasOne(d => d.PaymentMode)
                    .WithMany()
                    .HasForeignKey(d => d.PaymentModeId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SalesInvoicePayment_PaymenMode");

            });


            modelBuilder.Entity<StateProvince>(entity =>
            {
                entity.ToTable("stateprovince");

                entity.HasIndex(e => e.CountryId, "FK_StateProvince_Country");

                entity.Property(e => e.Capital).HasMaxLength(150);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.HasOne(d => d.Country)
                    .WithMany(p => p.StateProvinces)
                    .HasForeignKey(d => d.CountryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_StateProvince_Country");
            });

            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.ToTable("supplier");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Tin)
                    .HasMaxLength(50)
                    .HasColumnName("TIN");

            });

            modelBuilder.Entity<SupplierAddress>(entity =>
            {
                entity.ToTable("supplieraddress");

                entity.HasIndex(e => e.SupplierId, "FK_SuppplierAddress_Supplier");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.PostalCode).HasMaxLength(20);

            });

            modelBuilder.Entity<SupplierContact>(entity =>
            {
                entity.ToTable("suppliercontact");

                entity.HasIndex(e => e.SupplierId, "FK_SupplierContact_Supplier");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime(6)");

                entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime(6)");

            });

            modelBuilder.Entity<TaxRate>(entity =>
            {
                entity.ToTable("taxrate");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.HasOne(e => e.TaxAccount)
                    .WithMany()
                    .HasForeignKey(d => d.TaxAccountId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_TaxRate_Tax_Account");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("user");

                entity.HasIndex(e => e.Email, "Email_UNIQUE")
                    .IsUnique();

                entity.HasIndex(e => e.UserTypeId, "FK_User_UserType_idx");

                entity.HasIndex(e => e.MobileNo, "MobileNo_UNIQUE")
                    .IsUnique();

                entity.HasIndex(e => e.Username, "Username_UNIQUE")
                    .IsUnique();

                entity.Property(e => e.Avatar).HasMaxLength(150);

                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(e => e.MobileNo).HasMaxLength(20);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(e => e.Password)
                    .IsRequired()
                    .HasMaxLength(250);

                entity.Property(e => e.Username).HasMaxLength(50);

                entity.HasOne(d => d.UserType)
                    .WithMany(p => p.Users)
                    .HasForeignKey(d => d.UserTypeId)
                    .HasConstraintName("FK_User_UserType");

                entity.HasOne(d => d.Config)
                    .WithMany()
                    .HasForeignKey(d => d.ConfigId)
                    .HasConstraintName("FK_User_Config");
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("userrole");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<UserLog>(entity =>
            {
                entity.ToTable("userlog");
            });

            modelBuilder.Entity<UserType>(entity =>
            {
                entity.ToTable("usertype");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<EmailLog>(entity =>
            {
                entity.ToTable("emaillog");
            });

            modelBuilder.Entity<Vcitymunicipality>(entity =>
            {
                entity.HasNoKey();

                entity.ToView("vcitymunicipality");

                entity.Property(e => e.CountryCode)
                    .IsRequired()
                    .HasMaxLength(10);

                entity.Property(e => e.CountryName)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(e => e.StateProvinceName)
                    .IsRequired()
                    .HasMaxLength(150);
            });

            modelBuilder.Entity<VoucherConfig>(entity =>
            {
                entity.ToTable("voucherconfig");

                entity.HasIndex(e => e.VoucherType, "IX_vouchdefault")
                    .IsUnique();

                entity.Property(e => e.PrintNumCopy).HasDefaultValueSql("'0'");

                entity.Property(e => e.SignatoryCaption1).HasMaxLength(100);

                entity.Property(e => e.SignatoryCaption2).HasMaxLength(100);

                entity.Property(e => e.SignatoryCaption3).HasMaxLength(100);

                entity.Property(e => e.SignatoryName1).HasMaxLength(100);

                entity.Property(e => e.SignatoryName2).HasMaxLength(100);

                entity.Property(e => e.SignatoryName3).HasMaxLength(100);

                entity.Property(e => e.SignatoryPosition1).HasMaxLength(100);

                entity.Property(e => e.SignatoryPosition2).HasMaxLength(100);

                entity.Property(e => e.SignatoryPosition3).HasMaxLength(100);

                entity.Property(e => e.VoucherType)
                    .IsRequired()
                    .HasMaxLength(2)
                    .IsFixedLength(true);
            });

            modelBuilder.Entity<VoucherControlNo>(entity =>
            {
                entity.ToTable("vouchercontrolno");

                entity.Property(e => e.Period).HasColumnType("datetime(6)");

                entity.Property(e => e.VoucherType)
                    .IsRequired()
                    .HasMaxLength(2)
                    .IsFixedLength(true);
            });

            modelBuilder.Entity<PaymentTerm>(entity =>
            {
                entity.ToTable("paymentterm");

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<NavigationItem>(entity =>
            {
                entity.ToTable("navigationitem");

                
                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Subtitle)
                    .HasMaxLength(100);

                
                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId);
                

            });

            modelBuilder.Entity<Bill>(entity =>
            {
                entity.ToTable("bill");

                entity.HasIndex(e => e.SupplierId, "FK_Bill_Supplier");

                entity.HasIndex(e => e.BillNo, "IX_Bill")
                    .IsUnique();

                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(20,4)")
                    .HasDefaultValueSql("'0.0000'");

                entity.Property(e => e.Notes).HasColumnType("longtext");

                entity.Property(e => e.BillDate).HasColumnType("datetime(6)");

                entity.Property(e => e.BillNo)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasOne(d => d.Supplier)
                    .WithMany()
                    .HasForeignKey(d => d.SupplierId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Bill_Supplier");

            });

            modelBuilder.Entity<BillDetail>(entity =>
            {
                entity.ToTable("billdetail");

                entity.HasIndex(e => e.ItemId, "FK_SalesReceiptDetail_Item_idx");

                entity.HasIndex(e => e.BillId, "FK_BillDetail_Bill_idx");

                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(20,4)");

            });

            modelBuilder.Entity<BillPayment>(entity =>
            {
                entity.ToTable("billpayment");
            });

            modelBuilder.Entity<ExpensePayment>(entity =>
            {
                entity.ToTable("expensepayment");
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.ToTable("payment");
            });

            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.ToView("inventorytransaction");
                entity.HasNoKey();
            });

            modelBuilder.Entity<InventoryLocation>(entity =>
            {
                entity.ToTable("inventorylocation");
            });

            modelBuilder.Entity<StockTransfer>(entity =>
            {
                entity.ToTable("stocktransfer");
            });

            modelBuilder.Entity<StockTransferDetail>(entity =>
            {
                entity.ToTable("stocktransferdetail");
            });

            modelBuilder.Entity<StockIssuance>(entity =>
            {
                entity.ToTable("stockissuance");
            });

            modelBuilder.Entity<StockIssuanceDetail>(entity =>
            {
                entity.ToTable("stockissuancedetail");
            });

            modelBuilder.Entity<InventoryAdjustment>(entity =>
            {
                entity.ToTable("inventoryadjustment");
            });

            modelBuilder.Entity<InventoryAdjustmentDetail>(entity =>
            {
                entity.ToTable("inventoryadjustmentdetail");
            });

            modelBuilder.Entity<ReceivingReport>(entity =>
            {
                entity.ToTable("receivingreport");
            });

            modelBuilder.Entity<ReceivingReportDetail>(entity =>
            {
                entity.ToTable("receivingreportdetail");
            });

            modelBuilder.Entity<SalesTransaction>(entity =>
            {
                entity.ToView("salestransaction");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SubscriptionPlan>(entity =>
            {
                entity.ToTable("subscriptionplan");
            });
            
            modelBuilder.Entity<SalesTransactionDetail>(entity =>
            {
                //entity.ToView("salestransactiondetail");
                entity.ToFunction("GetSalesTransactionDetails");
                entity.HasNoKey();
            });

            modelBuilder.Entity<ResponsibilityCenterJournalEntry>(entity =>
            {
                entity.ToFunction("GetJournalEntryByResponsibilityCenter");
                entity.HasNoKey();
            });

            modelBuilder.Entity<JournalEntrySummary>(entity =>
            {
                entity.ToFunction("GetJournalEntryRecapIS");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SalesTransactionFunction>(entity =>
            {
                entity.ToFunction("GetSalesTransactions");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPSalesInvoice>(entity =>
            {
                entity.ToFunction("GetSalesInvoices");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPSalesReceipt>(entity =>
            {
                entity.ToFunction("GetSalesReceipts");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPPayment>(entity =>
            {
                entity.ToFunction("GetPayments");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPSalesInvoicePayment>(entity =>
            {
                entity.ToFunction("GetSalesInvoicePayments");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPBill>(entity =>
            {
                entity.ToFunction("GetBills");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPStockIssuance>(entity =>
            {
                entity.ToFunction("GetStockIssuances");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPInventoryAdjustment>(entity =>
            {
                entity.ToFunction("GetInventoryAdjustments");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPStockTransfer>(entity =>
            {
                entity.ToFunction("GetStockTransfers");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPReceivingReport>(entity =>
            {
                entity.ToFunction("GetReceivingReports");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPGeneralJournal>(entity =>
            {
                entity.ToFunction("GetGeneralJournals");
                entity.HasNoKey();
            });

            modelBuilder.Entity<InventoryStockSummary>(entity =>
            {
                entity.ToFunction("GetInventoryStockSummary");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AccountRecap>(entity =>
            {
                entity.ToFunction("GetAccontRecap");
                entity.HasNoKey();
            });

            modelBuilder.Entity<GeneralLedgerRecap>(entity =>
            {
                entity.ToFunction("GetGeneralLedgerRecap");
                entity.HasNoKey();
            });

            modelBuilder.Entity<GeneralLedgerDetails>(entity =>
            {
                entity.ToFunction("GetGeneralLedgerDetails");
                entity.HasNoKey();
            });

            modelBuilder.Entity<CustomerSale>(entity =>
            {
                entity.ToFunction("GetSalesByCustomer");
                entity.HasNoKey();
            });

            modelBuilder.Entity<ItemSale>(entity =>
            {
                entity.ToFunction("GetSalesByItem");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SPInventoryTransaction>(entity =>
            {
                entity.ToFunction("GetInventoryTransactions");
                entity.HasNoKey();
            });

            modelBuilder.Entity<PivotedInventoryWithJsonArray>(entity =>
            {
                entity.ToFunction("GetPivotedInventoryWithJsonArray");
                entity.HasNoKey();
            });

            modelBuilder.Entity<SalesMonthlyTrend>(entity =>
            {
                entity.ToFunction("GetSalesMonthlyTrend");
                entity.HasNoKey();
            });

            modelBuilder.Entity<AppVersion>(entity =>
            {
                entity.ToTable("appversion");

                entity.HasKey(e => e.Id);

            });


            modelBuilder.Entity<ChatSession>(entity =>
            {
                entity.ToTable("chatsession");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.HasMany(e => e.ChatMessages)
                      .WithOne()
                      .HasForeignKey(m => m.ChatSessionId)
                      .IsRequired();
            });

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.ToTable("chatmessage");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<ItemCategory>(entity =>
            {
                entity.ToTable("itemcategory");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<DiscountType>(entity =>
            {
                entity.ToTable("discounttype");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<TransactionSequence>(entity =>
            {
                entity.ToTable("transactionsequence");
                entity.HasKey(e => e.Id);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    }
}


