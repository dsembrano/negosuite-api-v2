using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class PaymentTerm
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public short? Days { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
    }
}
