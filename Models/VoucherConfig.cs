using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class VoucherConfig
    {
        public int Id { get; set; }
        public string VoucherType { get; set; }
        public int? ControlNoResetTypeId { get; set; }
        public int? PrintNumCopy { get; set; }
        public string SignatoryName1 { get; set; }
        public string SignatoryPosition1 { get; set; }
        public string SignatoryCaption1 { get; set; }
        public string SignatoryName2 { get; set; }
        public string SignatoryPosition2 { get; set; }
        public string SignatoryCaption2 { get; set; }
        public string SignatoryName3 { get; set; }
        public string SignatoryPosition3 { get; set; }
        public string SignatoryCaption3 { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
    }
}
