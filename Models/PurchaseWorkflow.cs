using System;
using Microsoft.EntityFrameworkCore;
namespace negosuite_api.Models;
public class PurchaseConfiguration
{
 public int UserConfigId {get;set;}
 public string ProcessingMode {get;set;}="BOTH";
 public int? GrniAccountId {get;set;}
 public long Version {get;set;}
}
public class PurchaseWorkflowDocument
{
 public int Id {get;set;}
 public int UserConfigId {get;set;}
 public string Kind {get;set;}
 public string ReferenceNo {get;set;}
 public DateTime ReferenceDate {get;set;}
 public int SupplierId {get;set;}
 public string SupplierName {get;set;}
 public string ResponsibilityCenterEntry {get;set;}="[]";
 public string PayloadJson {get;set;}
 public decimal Amount {get;set;}
 public short Status {get;set;}
 public long Version {get;set;}=1;
 public int? LegacyId {get;set;}
 public string RequestKey {get;set;}
 public string RequestHash {get;set;}
 public DateTime CreatedDate {get;set;}
 public int CreatedByUserId {get;set;}
 public DateTime? PostedDate {get;set;}
 public string VoidReason {get;set;}
 public int? VoidedByUserId {get;set;}
}
public class PurchaseWorkflowLink
{
 public int Id {get;set;}
 public int UserConfigId {get;set;}
 public int SourceId {get;set;}
 public int SourceLine {get;set;}
 public int TargetId {get;set;}
 public int TargetLine {get;set;}
 public decimal Quantity {get;set;}
}
public partial class negosuiteContext
{
 public DbSet<PurchaseConfiguration> PurchaseConfigurations {get;set;}
 public DbSet<PurchaseWorkflowDocument> PurchaseWorkflowDocuments {get;set;}
 public DbSet<PurchaseWorkflowLink> PurchaseWorkflowLinks {get;set;}
 internal static void ConfigurePurchaseWorkflow(ModelBuilder b)
 {
  b.Entity<PurchaseConfiguration>(e=>{e.ToTable("purchaseconfiguration");e.HasKey(x=>x.UserConfigId);e.Property(x=>x.UserConfigId).ValueGeneratedNever();e.Property(x=>x.ProcessingMode).HasMaxLength(30);e.HasOne<Config>().WithMany().HasForeignKey(x=>x.UserConfigId).OnDelete(DeleteBehavior.Restrict);e.HasOne<Account>().WithMany().HasForeignKey(x=>x.GrniAccountId).OnDelete(DeleteBehavior.Restrict);});
  b.Entity<PurchaseWorkflowDocument>(e=>{e.ToTable("purchaseworkflowdocument");e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.UserConfigId,x.RequestKey}).IsUnique();e.HasIndex(x=>new{x.UserConfigId,x.Kind,x.ReferenceNo}).IsUnique();e.HasIndex(x=>new{x.UserConfigId,x.Kind,x.LegacyId}).IsUnique();e.HasOne<Config>().WithMany().HasForeignKey(x=>x.UserConfigId).OnDelete(DeleteBehavior.Restrict);e.HasOne<Supplier>().WithMany().HasForeignKey(x=>x.SupplierId).OnDelete(DeleteBehavior.Restrict);e.Property(x=>x.PayloadJson).HasColumnType("json");e.Property(x=>x.ResponsibilityCenterEntry).HasColumnType("json");e.Property(x=>x.Kind).HasMaxLength(8);e.Property(x=>x.RequestKey).HasMaxLength(36);e.Property(x=>x.RequestHash).HasMaxLength(64);e.Property(x=>x.ReferenceNo).HasMaxLength(50);e.Property(x=>x.SupplierName).HasMaxLength(250);e.Property(x=>x.VoidReason).HasMaxLength(250);e.Property(x=>x.Amount).HasPrecision(20,4);});
  b.Entity<PurchaseWorkflowLink>(e=>{e.ToTable("purchaseworkflowlink");e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.SourceId,x.SourceLine,x.TargetId,x.TargetLine}).IsUnique();e.HasOne<PurchaseWorkflowDocument>().WithMany().HasForeignKey(x=>x.SourceId).OnDelete(DeleteBehavior.Restrict);e.HasOne<PurchaseWorkflowDocument>().WithMany().HasForeignKey(x=>x.TargetId).OnDelete(DeleteBehavior.Restrict);e.Property(x=>x.Quantity).HasPrecision(20,4);});
 }
}
