using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class UserRole
    {
        public int Id { get; set; }
        public int? UserConfigId { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        public bool IsAdmin { get; set; }
        public string Permission { get; set; }
        public string AdvancePermission { get; set; }
        public string ColumnRestriction { get; set; }
        public string MobileAppPermission { get; set; }
        public bool? EnableAIChatBot { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
        
    }
}
