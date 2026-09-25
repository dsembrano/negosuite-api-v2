using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class VoucherControlNo
    {
        public int Id { get; set; }
        public DateTime Period { get; set; }
        public string VoucherType { get; set; }
        public int LastControlNo { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
    }
}
