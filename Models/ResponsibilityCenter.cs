using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class ResponsibilityCenter
    {
        public ResponsibilityCenter()
        {
        }

        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Name { get; set; }
        public bool Status { get; set; }
        public int ResponsibilityCenterTypeId { get; set; }
        public string Notes { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

    }
}
