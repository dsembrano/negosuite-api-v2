using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace negosuite_api.Contracts.Transactions;
public class PurchaseWorkflowWrite
{
 public long Version {get;set;}
 [Required,StringLength(36)] public string RequestKey {get;set;}
 [StringLength(50)] public string ReferenceNo {get;set;}
 public DateTime ReferenceDate {get;set;}
 public DateTime? ExpectedDate {get;set;}
 public DateTime? DueDate {get;set;}
 public int SupplierId {get;set;}
 public int? InventoryLocationId {get;set;}
 public int? PaymentTermId {get;set;}
 [StringLength(100)] public string DeliveryReceiptNo {get;set;}
 [StringLength(4000)] public string Notes {get;set;}
 public string ResponsibilityCenterEntry {get;set;}="[]";
 public bool IsTaxExclusive {get;set;}=true;
 public bool HasItemLevelDiscount {get;set;}
 public string DiscountMode {get;set;}="percent";
 public decimal DiscountValue {get;set;}
 public List<PurchaseLine> Lines {get;set;}=new();
 // Populated by the server. Client-supplied snapshots are never trusted.
 public int? GrniAccountId {get;set;}
}
public class PurchaseLine
{
 public int ItemId {get;set;}
 public string Description {get;set;}
 public string Unit {get;set;}
 public decimal Quantity {get;set;}
 public decimal Rate {get;set;}
 public decimal? DiscountPercent {get;set;}=0;
 public decimal DiscountAmount {get;set;}
 public int? TaxRateId {get;set;}
 public int? SourceDocumentId {get;set;}
 public int? SourceLineId {get;set;}
 public List<PurchaseLandedCost> LandedCosts {get;set;}=new();
 // Explicit signed allocation for a later cost adjustment; never adds stock quantity.
 public decimal InventoryAdjustment {get;set;}
 public decimal CogsAdjustment {get;set;}
 public int? AdjustmentAccountId {get;set;}
 public decimal NetAmount {get;set;}
 public decimal TaxAmount {get;set;}
 public decimal Amount {get;set;}
 public decimal TaxPercent {get;set;}
 public int? TaxAccountId {get;set;}
 public int InventoryAccountId {get;set;}
 public int? CostAccountId {get;set;}
 public string StockFingerprint {get;set;}
 public decimal? PreviousAverageCost {get;set;} public decimal? PreviousCost {get;set;} public DateTime? PreviousPurchasedDate {get;set;}
 public decimal PostOnHand {get;set;}
 public decimal PostAverageCost {get;set;}
 public int? LegacyLineId {get;set;}
 public decimal RemainingQuantity {get;set;}
}
public class PurchaseLandedCost { public int AccountId {get;set;} public decimal Amount {get;set;} }
public class PurchaseAction {public long Version {get;set;} [StringLength(250)] public string Reason {get;set;}}
public class PurchaseListQuery
{
 public int? PageNumber {get;set;} public int? PageSize {get;set;} public short Status {get;set;}=1;
 public string Search {get;set;} public int? SupplierId {get;set;} public DateTime? PeriodStart {get;set;} public DateTime? PeriodEnd {get;set;}
 public string SortBy {get;set;}="referenceDate";public string SortDirection {get;set;}="desc";
}
