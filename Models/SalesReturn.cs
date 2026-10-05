using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace negosuite_api.Models;

public class SalesReturn
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int CustomerId { get; set; }
    public int ReceivableAccountId { get; set; }
    public int? SalesInvoiceId { get; set; }
    public int? SalesReceiptId { get; set; }
    public int? InventoryLocationId { get; set; }
    public string Reason { get; set; }
    public string Notes { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public string SourceSnapshotJson { get; set; }
    public string ApplicationsJson { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string Taxes { get; set; }
    public short Status { get; set; }
    public long Version { get; set; }
    public string RequestKey { get; set; }
    public string RequestHash { get; set; }
    public DateTime CreatedDate { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public DateTime? PostedDate { get; set; }
    public int? PostedByUserId { get; set; }
    public DateTime? VoidedDate { get; set; }
    public int? VoidedByUserId { get; set; }
    public string VoidReason { get; set; }
    public Customer Customer { get; set; }
    public SalesInvoice SalesInvoice { get; set; }
    public SalesReceipt SalesReceipt { get; set; }
    public InventoryLocation InventoryLocation { get; set; }
    public ICollection<SalesReturnDetail> SalesReturnDetails { get; set; } = new List<SalesReturnDetail>();
    public ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
}
public class SalesReturnDetail
{
    public int Id { get; set; }
    public int SalesReturnId { get; set; }
    public int? SalesInvoiceDetailId { get; set; }
    public int? SalesReceiptDetailId { get; set; }
    public int? OriginalSourceDetailId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; }
    public string Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Cost { get; set; }
    public decimal Amount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public int? TaxRateId { get; set; }
    public string TaxName { get; set; }
    public bool IsInventoryTransaction { get; set; }
    public string Notes { get; set; }
    public string ComponentsJson { get; set; }
    public SalesReturn SalesReturn { get; set; }
}
public partial class negosuiteContext
{
    public DbSet<SalesReturn> SalesReturns { get; set; }
    public DbSet<SalesReturnDetail> SalesReturnDetails { get; set; }
    internal static void ConfigureSalesReturns(ModelBuilder builder)
    {
        builder.Entity<SalesReturn>(e => {
            e.ToTable("salesreturn");
            e.Property(r=>r.RequestHash).HasMaxLength(64).IsRequired(); e.HasKey(r=>r.Id);
            e.HasIndex(r=>new {r.UserConfigId,r.ReferenceNo}).IsUnique();
            e.HasIndex(r=>new {r.UserConfigId,r.RequestKey}).IsUnique();
            e.HasIndex(r=>new {r.UserConfigId,r.Status,r.ReferenceDate});
            e.Property(r=>r.ReferenceNo).HasMaxLength(50).IsRequired(); e.Property(r=>r.Reason).HasMaxLength(250).IsRequired();
            e.Property(r=>r.RequestKey).HasMaxLength(36).IsRequired();e.Property(r=>r.VoidReason).HasMaxLength(250);
            e.Property(r=>r.Version).IsConcurrencyToken();
            foreach(var name in new[]{"SourceSnapshotJson","ApplicationsJson","Taxes","ResponsibilityCenterEntry"})e.Property<string>(name).HasColumnType("json");
            foreach(var name in new[]{"Amount","Balance","DiscountAmount","TaxAmount"})e.Property<decimal>(name).HasPrecision(20,4);
            e.HasOne<Config>().WithMany().HasForeignKey(r=>r.UserConfigId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r=>r.Customer).WithMany().HasForeignKey(r=>r.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r=>r.SalesInvoice).WithMany().HasForeignKey(r=>r.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r=>r.SalesReceipt).WithMany().HasForeignKey(r=>r.SalesReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r=>r.InventoryLocation).WithMany().HasForeignKey(r=>r.InventoryLocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r=>r.SalesReturnDetails).WithOne(r=>r.SalesReturn).HasForeignKey(r=>r.SalesReturnId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r=>r.JournalEntries).WithOne().HasForeignKey(r=>r.SalesReturnId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<SalesReturnDetail>(e=>{
            e.ToTable("salesreturndetail");e.HasKey(d=>d.Id);
            e.Property(d=>d.ItemName).HasMaxLength(250).IsRequired();e.Property(d=>d.Unit).HasMaxLength(50);e.Property(d=>d.TaxName).HasMaxLength(100);
            e.Property(d=>d.ComponentsJson).HasColumnType("json").IsRequired();
            foreach(var name in new[]{"Quantity","Rate","Cost","Amount","DiscountAmount","TaxAmount"})e.Property<decimal>(name).HasPrecision(20,4);
            e.HasOne<Item>().WithMany().HasForeignKey(d=>d.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SalesInvoiceDetail>().WithMany().HasForeignKey(d=>d.SalesInvoiceDetailId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SalesReceiptDetail>().WithMany().HasForeignKey(d=>d.SalesReceiptDetailId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(d=>new{d.SalesReturnId,d.SalesInvoiceDetailId}).IsUnique();
            e.HasIndex(d=>new{d.SalesReturnId,d.SalesReceiptDetailId}).IsUnique();
        });
    }
}
