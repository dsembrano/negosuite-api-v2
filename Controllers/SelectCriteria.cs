using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using negosuite_api.Models;

namespace negosuite_api.Controllers
{
    public class SelectCriteria
    {
        public int? IndustryId { get; set; }
        public Boolean? Status { get; set; }
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public string ReferenceNo { get; set; }
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public int? CategoryId { get; set; }
        public Boolean? ShowDeleted { get; set; }
        public int? ItemId { get; set; }
        public string ItemType { get; set; }
        public Boolean? ToSell { get; set; }
        public Boolean? ToPurchase { get; set; }
        public String JournalSource { get; set; }
        public String[] JournalSources { get; set; }
        public Boolean? ShowInactive { get; set; }
        public int? InventoryLocationId { get; set; }
        public string TransactionSource { get; set; }
        public List<int> ResponsibilityCenterIds { get; set; }
        public string ArrayString { get; set; }
        public int? AccountId { get; set; }
        public int? AgingBaseDate { get; set; }
        public int? UserConfigId { get; set; }
        public int? ResponsibilityCenterTypeId { get; set; }
        public int? ItemCategoryId { get; set; }
        public bool? IsPOS { get; set; }

    }
}
