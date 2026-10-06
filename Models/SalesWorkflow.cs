using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace negosuite_api.Models;

public class SalesConfiguration
{
    public int UserConfigId { get; set; }
    public string SalesProcessingMode { get; set; } = "BOTH";
    public bool EnableSalesQuotation { get; set; } = true;
    public string CogsRecognitionPoint { get; set; } = "INVOICE";
    public bool EnableInventoryCommitment { get; set; } = true;
    public bool AllowNegativeInventory { get; set; }
    public long Version { get; set; } = 1;
    public DateTime CreatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? LastUpdatedByUserId { get; set; }
}

// A registry for the three NEW operational documents. Existing invoices/returns remain in their own tables.
// A single numeric identity makes generic source relationships unambiguous without nullable FK chains.
public class SalesWorkflowDocument
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Kind { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public int? InventoryLocationId { get; set; }
    public int? PaymentTermId { get; set; }
    public DateTime? RequestedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string Salesperson { get; set; }
    public string PriceList { get; set; }
    public string DeliveryAddress { get; set; }
    public string ResponsibilityCenterEntry { get; set; } = "[]";
    public string Notes { get; set; }
    public bool IsTaxExclusive { get; set; } = true;
    public bool HasItemLevelDiscount { get; set; } = true;
    public string DiscountMode { get; set; } = "percent";
    public decimal DiscountValue { get; set; }
    public decimal Amount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public short Status { get; set; }
    public string Disposition { get; set; }
    public string CogsRecognitionPoint { get; set; }
    public long Version { get; set; } = 1;
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
    public List<SalesWorkflowLine> Lines { get; set; } = new();
}
public class SalesWorkflowLine
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int ItemId { get; set; }
    public string Description { get; set; }
    public string Unit { get; set; }
    public int? InventoryLocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool DiscountIsAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Amount { get; set; }
    public int? TaxRateId { get; set; }
    public string TaxSnapshot { get; set; }
    public bool TrackInventory { get; set; }
    public decimal Cost { get; set; }
    public decimal CostAmount { get; set; }
    public int SalesAccountId { get; set; }
    public int? DiscountAccountId { get; set; }
    public int? InventoryAccountId { get; set; }
    public int? CostAccountId { get; set; }
}
public class DocumentRelationship
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string SourceDocumentType { get; set; }
    public int SourceDocumentId { get; set; }
    public string TargetDocumentType { get; set; }
    public int TargetDocumentId { get; set; }
    public string RelationshipType { get; set; } = "FULFILLMENT";
    public short Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public int CreatedByUserId { get; set; }
    public List<DocumentLineRelationship> Lines { get; set; } = new();
}
public class DocumentLineRelationship
{
    public int Id { get; set; }
    public int RelationshipId { get; set; }
    public int SourceLineId { get; set; }
    public int TargetLineId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime CreatedDate { get; set; }
    public int CreatedByUserId { get; set; }
}
public class SalesPostingRecord
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string DocumentType { get; set; }
    public int DocumentId { get; set; }
    public int? DocumentLineId { get; set; }
    public int JournalEntryId { get; set; }
    public string Effect { get; set; }
}
public class SalesWorkflowInvoice
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Kind { get; set; }
    public int InvoiceId { get; set; }
    public string RequestKey { get; set; }
    public string RequestHash { get; set; }
    public string ReturnSourceJson { get; set; }
    public short Status { get; set; } = 1;
    public long Version { get; set; } = 1;
    public DateTime CreatedDate { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? VoidedDate { get; set; }
    public int? VoidedByUserId { get; set; }
    public string VoidReason { get; set; }
}

public partial class negosuiteContext
{
    public DbSet<SalesConfiguration> SalesConfigurations { get; set; }
    public DbSet<SalesWorkflowDocument> SalesWorkflowDocuments { get; set; }
    public DbSet<SalesWorkflowLine> SalesWorkflowLines { get; set; }
    public DbSet<DocumentRelationship> DocumentRelationships { get; set; }
    public DbSet<DocumentLineRelationship> DocumentLineRelationships { get; set; }
    public DbSet<SalesPostingRecord> SalesPostingRecords { get; set; }
    public DbSet<SalesWorkflowInvoice> SalesWorkflowInvoices { get; set; }
    internal static void ConfigureSalesWorkflow(ModelBuilder b)
    {
        b.Entity<SalesConfiguration>(e => { e.ToTable("salesconfiguration"); e.HasKey(x=>x.UserConfigId); e.Property(x=>x.UserConfigId).ValueGeneratedNever(); e.HasOne<Config>().WithMany().HasForeignKey(x=>x.UserConfigId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<SalesWorkflowDocument>(e => {
            e.ToTable("salesworkflowdocument"); e.HasKey(x=>x.Id);
            e.HasIndex(x=>new{x.UserConfigId,x.Kind,x.ReferenceNo}).IsUnique(); e.HasIndex(x=>new{x.UserConfigId,x.RequestKey}).IsUnique();
            e.HasIndex(x=>new{x.UserConfigId,x.Kind,x.Status,x.ReferenceDate}); e.HasIndex(x=>new{x.UserConfigId,x.CustomerId,x.ReferenceDate});
            e.HasOne<Config>().WithMany().HasForeignKey(x=>x.UserConfigId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x=>x.Customer).WithMany().HasForeignKey(x=>x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<InventoryLocation>().WithMany().HasForeignKey(x=>x.InventoryLocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PaymentTerm>().WithMany().HasForeignKey(x=>x.PaymentTermId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Supplier>().WithMany().HasForeignKey(x=>x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x=>x.Lines).WithOne().HasForeignKey(x=>x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SalesWorkflowLine>(e => { e.ToTable("salesworkflowline"); e.HasKey(x=>x.Id); e.HasIndex(x=>new{x.ItemId,x.InventoryLocationId}); e.HasOne<Item>().WithMany().HasForeignKey(x=>x.ItemId).OnDelete(DeleteBehavior.Restrict); e.HasOne<InventoryLocation>().WithMany().HasForeignKey(x=>x.InventoryLocationId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<DocumentRelationship>(e => { e.ToTable("documentrelationship"); e.HasKey(x=>x.Id); e.HasIndex(x=>new{x.UserConfigId,x.SourceDocumentType,x.SourceDocumentId,x.TargetDocumentType,x.TargetDocumentId}).IsUnique(); e.HasIndex(x=>new{x.UserConfigId,x.TargetDocumentType,x.TargetDocumentId}); e.HasOne<Config>().WithMany().HasForeignKey(x=>x.UserConfigId).OnDelete(DeleteBehavior.Restrict); e.HasMany(x=>x.Lines).WithOne().HasForeignKey(x=>x.RelationshipId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<DocumentLineRelationship>(e => { e.ToTable("documentlinerelationship"); e.HasKey(x=>x.Id); e.HasIndex(x=>new{x.RelationshipId,x.SourceLineId,x.TargetLineId}).IsUnique(); e.HasIndex(x=>x.SourceLineId); e.HasIndex(x=>x.TargetLineId); });
        b.Entity<SalesPostingRecord>(e => { e.ToTable("salespostingrecord"); e.HasKey(x=>x.Id); e.HasIndex(x=>new{x.UserConfigId,x.DocumentType,x.DocumentId}); e.HasIndex(x=>x.JournalEntryId).IsUnique(); e.HasOne<JournalEntry>().WithMany().HasForeignKey(x=>x.JournalEntryId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<SalesWorkflowInvoice>(e => { e.ToTable("salesworkflowinvoice"); e.HasKey(x=>x.Id); e.HasIndex(x=>new{x.UserConfigId,x.Kind,x.InvoiceId}).IsUnique(); e.HasIndex(x=>new{x.UserConfigId,x.RequestKey}).IsUnique(); e.HasOne<Config>().WithMany().HasForeignKey(x=>x.UserConfigId).OnDelete(DeleteBehavior.Restrict); });
        foreach(var type in new[]{typeof(SalesConfiguration),typeof(SalesWorkflowDocument),typeof(SalesWorkflowLine),typeof(DocumentRelationship),typeof(DocumentLineRelationship),typeof(SalesPostingRecord),typeof(SalesWorkflowInvoice)})
            foreach(var p in b.Model.FindEntityType(type).GetProperties())
            {
                if(p.ClrType==typeof(decimal)) p.SetColumnType("decimal(20,4)");
                if(p.ClrType==typeof(string))
                {
                    var name=p.Name;
                    if(name.EndsWith("Json")||name is "ResponsibilityCenterEntry" or "TaxSnapshot")p.SetColumnType("json");
                    else if(name is "Notes" or "DeliveryAddress")p.SetColumnType("longtext");
                    else p.SetMaxLength(name is "Kind" or "SourceDocumentType" or "TargetDocumentType" or "DocumentType"?8:name=="RequestHash"?64:name=="RequestKey"?36:name=="ReferenceNo"?50:250);
                }
            }
    }
}
